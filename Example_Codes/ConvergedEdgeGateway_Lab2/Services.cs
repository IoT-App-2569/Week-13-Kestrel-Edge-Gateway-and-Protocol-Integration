using System.IO.Ports;
using System.Text.Json;
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
    private readonly ILogger<SerialBridgeWorker> _logger;
    private SerialPort? _serial;

    public SerialBridgeWorker(
        IConfiguration configuration,
        GatewayStateStore store,
        MqttServer mqttServer,
        ILogger<SerialBridgeWorker> logger)
    {
        _portName = configuration["Serial:PortName"] ?? "COM3";
        _baudRate = configuration.GetValue("Serial:BaudRate", 115200);
        _store = store;
        _mqttServer = mqttServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("พยายามเปิด Serial Port {Port}...", _portName);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!EnsureSerialPortOpen(stoppingToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                try
                {
                    string line;
                    lock (_serialLock)
                    {
                        line = _serial!.ReadLine().Trim();
                    }

                    if (line.Length > 0)
                    {
                        await HandleIncomingSerialLine(line);
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

    private async Task HandleIncomingSerialLine(string line)
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

        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/telemetry")
            .WithPayload(line)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        try
        {
            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMessage));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "ส่ง Telemetry เข้า MQTT Broker ไม่สำเร็จ");
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
