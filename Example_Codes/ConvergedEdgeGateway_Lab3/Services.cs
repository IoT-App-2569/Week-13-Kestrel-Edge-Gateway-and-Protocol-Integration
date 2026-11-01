using System.IO.Ports;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Server;

public sealed class GatewayStateStore
{
    private TelemetryData? _latestTelemetry;

    public TelemetryData? LatestTelemetry => Volatile.Read(ref _latestTelemetry);

    public void SetLatestTelemetry(TelemetryData telemetry) =>
        Volatile.Write(ref _latestTelemetry, telemetry);
}

public sealed record TelemetryData(double Voltage, int Raw, long Timestamp);

public sealed class SerialBridgeWorker : BackgroundService
{
    private readonly object _serialLock = new();
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<SerialBridgeWorker> _logger;
    private SerialPort? _serial;

    public SerialBridgeWorker(
        IConfiguration configuration,
        GatewayStateStore store,
        MqttServer mqttServer,
        IHubContext<GatewayHub> hubContext,
        ProtocolMetrics metrics,
        IHostApplicationLifetime applicationLifetime,
        ILogger<SerialBridgeWorker> logger)
    {
        _portName = configuration["Serial:PortName"] ?? "COM3";
        _baudRate = configuration.GetValue("Serial:BaudRate", 115200);
        _store = store;
        _mqttServer = mqttServer;
        _hubContext = hubContext;
        _metrics = metrics;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("พยายามเปิด Serial Port {Port}...", _portName);

        try
        {
            var applicationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = _applicationLifetime.ApplicationStarted.Register(
                () => applicationStarted.TrySetResult());
            await applicationStarted.Task.WaitAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!EnsureSerialPortOpen(stoppingToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                string? line = null;
                try
                {
                    lock (_serialLock)
                    {
                        line = _serial!.ReadLine().Trim();
                    }
                }
                catch (TimeoutException)
                {
                    // A read timeout is expected while the device is idle.
                }
                catch (Exception ex) when (IsSerialException(ex))
                {
                    _logger.LogWarning(ex, "เกิดปัญหาระหว่างอ่าน Serial Port {Port}", _portName);
                    CloseSerialPort();
                    continue;
                }

                if (!string.IsNullOrEmpty(line))
                {
                    await HandleIncomingSerialLine(line, stoppingToken);
                }
            }
        }
        finally
        {
            CloseSerialPort();
        }
    }

    private bool EnsureSerialPortOpen(CancellationToken stoppingToken)
    {
        lock (_serialLock)
        {
            if (_serial?.IsOpen == true)
            {
                return true;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                return false;
            }

            var serial = new SerialPort(_portName, _baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 500,
                WriteTimeout = 1000
            };

            try
            {
                serial.Open();
                _serial = serial;
                _logger.LogInformation("เชื่อมต่อ Serial Port {Port} สำเร็จ", _portName);
                return true;
            }
            catch (Exception ex) when (IsSerialException(ex))
            {
                serial.Dispose();
                _logger.LogWarning(ex, "เปิด Serial Port {Port} ไม่สำเร็จ จะลองใหม่", _portName);
                return false;
            }
        }
    }

    private async Task HandleIncomingSerialLine(string line, CancellationToken stoppingToken)
    {
        double voltage;
        int raw;

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("voltage", out var voltageElement) ||
                !root.TryGetProperty("raw", out var rawElement) ||
                !voltageElement.TryGetDouble(out voltage) ||
                !rawElement.TryGetInt32(out raw))
            {
                _logger.LogDebug("ข้ามข้อมูล Serial ที่ไม่มีฟิลด์ voltage/raw: {Line}", line);
                return;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "ข้ามข้อมูล Serial ที่ไม่ใช่ JSON Telemetry");
            return;
        }

        if (!double.IsFinite(voltage) || voltage < 0 || voltage > 3.3 || raw is < 0 or > 4095)
        {
            _logger.LogWarning("ข้ามค่า Telemetry ที่อยู่นอกช่วง: voltage={Voltage}, raw={Raw}", voltage, raw);
            return;
        }

        _store.SetLatestTelemetry(new TelemetryData(
            voltage,
            raw,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        _metrics.RecordSerialPacket();

        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/telemetry")
            .WithPayload(line)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        try
        {
            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMessage), stoppingToken);
            _metrics.RecordMqttMessage();
        }
        catch (InvalidOperationException ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "ส่ง Telemetry เข้า MQTT Broker ไม่สำเร็จ");
        }

        _metrics.RecordSignalRPush();
        try
        {
            await _hubContext.Clients.All.SendAsync(
                "ReceiveTelemetry",
                new
                {
                    voltage,
                    raw,
                    metrics = _metrics.GetSnapshot()
                },
                stoppingToken);
        }
        catch (InvalidOperationException ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "ส่ง Telemetry ผ่าน SignalR ไม่สำเร็จ");
        }
    }

    public bool SendSerialCommand(string command)
    {
        lock (_serialLock)
        {
            if (_serial?.IsOpen != true)
            {
                return false;
            }

            try
            {
                _serial.WriteLine(command);
                return true;
            }
            catch (Exception ex) when (IsSerialException(ex))
            {
                _logger.LogWarning(ex, "ส่งคำสั่ง {Command} ไปยัง Serial Port {Port} ไม่สำเร็จ", command, _portName);
                CloseSerialPort();
                return false;
            }
        }
    }

    private void CloseSerialPort()
    {
        lock (_serialLock)
        {
            _serial?.Dispose();
            _serial = null;
        }
    }

    private static bool IsSerialException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException;

    public override void Dispose()
    {
        CloseSerialPort();
        base.Dispose();
    }
}

public sealed class ProtocolMetrics
{
    private long _serialPackets;
    private long _mqttMessages;
    private long _restRequests;
    private long _signalRPushes;

    public void RecordSerialPacket() => Interlocked.Increment(ref _serialPackets);
    public void RecordMqttMessage() => Interlocked.Increment(ref _mqttMessages);
    public void RecordRestRequest() => Interlocked.Increment(ref _restRequests);
    public void RecordSignalRPush() => Interlocked.Increment(ref _signalRPushes);

    public ProtocolMetricsSnapshot GetSnapshot() => new(
        Interlocked.Read(ref _serialPackets),
        Interlocked.Read(ref _mqttMessages),
        Interlocked.Read(ref _restRequests),
        Interlocked.Read(ref _signalRPushes));
}

public sealed record ProtocolMetricsSnapshot(
    long SerialPackets,
    long MqttMessages,
    long RestRequests,
    long SignalRPushes);
