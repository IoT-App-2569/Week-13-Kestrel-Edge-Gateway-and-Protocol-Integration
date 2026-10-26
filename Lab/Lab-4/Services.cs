using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Server;

public sealed class GatewayStateStore
{
    private readonly ConcurrentDictionary<string, object> _values = new();
    public void Set(string key, object value) => _values[key] = value;
    public T? Get<T>(string key) => _values.TryGetValue(key, out var value) && value is T typed ? typed : default;
}

public sealed record TelemetryData(double Voltage, int Raw, long Timestamp);

public sealed class SerialBridgeWorker : BackgroundService
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly GatewayStateStore _state;
    private readonly MqttServer _mqttServer;
    private readonly IHubContext<GatewayHub> _hub;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<SerialBridgeWorker> _logger;
    private readonly object _serialLock = new();
    private SerialPort? _serial;

    public SerialBridgeWorker(
        IConfiguration configuration,
        GatewayStateStore state,
        MqttServer mqttServer,
        IHubContext<GatewayHub> hub,
        ProtocolMetrics metrics,
        ILogger<SerialBridgeWorker> logger)
    {
        _portName = configuration["Serial:PortName"] ?? "COM3";
        _baudRate = configuration.GetValue("Serial:BaudRate", 115200);
        _state = state;
        _mqttServer = mqttServer;
        _hub = hub;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!TryOpen())
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                continue;
            }

            try
            {
                string line;
                lock (_serialLock) line = _serial!.ReadLine().Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    await HandleLine(line, stoppingToken);
                }
            }
            catch (TimeoutException) { }
            catch (InvalidOperationException ex) { _logger.LogWarning("Serial read: {Message}", ex.Message); Close(); }
            catch (IOException ex) { _logger.LogWarning("Serial read: {Message}", ex.Message); Close(); }
        }
    }

    private bool TryOpen()
    {
        lock (_serialLock)
        {
            if (_serial?.IsOpen == true) return true;
            try
            {
                _serial?.Dispose();
                _serial = new SerialPort(_portName, _baudRate) { NewLine = "\n", ReadTimeout = 1000 };
                _serial.Open();
                _logger.LogInformation("เชื่อมต่อ Serial Port {Port} สำเร็จ", _portName);
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
            {
                _logger.LogWarning("เปิด Serial Port {Port} ไม่ได้: {Message}", _portName, ex.Message);
                _serial?.Dispose();
                _serial = null;
                return false;
            }
        }
    }

    private async Task HandleLine(string line, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var telemetry = new TelemetryData(
                root.GetProperty("voltage").GetDouble(),
                root.GetProperty("raw").GetInt32(),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _state.Set("latest_telemetry", telemetry);
            _metrics.IncrementSerialPackets();

            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(
                new MqttApplicationMessageBuilder()
                    .WithTopic("iot/esp32/telemetry")
                    .WithPayload(line)
                    .Build()), cancellationToken);
            _metrics.IncrementMqttMessages();
            _metrics.IncrementSignalRPushes();
            await _hub.Clients.All.SendAsync("ReceiveTelemetry", new
            {
                voltage = telemetry.Voltage,
                raw = telemetry.Raw,
                timestamp = telemetry.Timestamp,
                metrics = _metrics.Snapshot()
            }, cancellationToken);
        }
        catch (JsonException ex) { _logger.LogWarning("Telemetry JSON ไม่ถูกต้อง: {Message}", ex.Message); }
        catch (KeyNotFoundException ex) { _logger.LogWarning("Telemetry ขาดฟิลด์: {Message}", ex.Message); }
    }

    public bool SendSerialCommand(string command)
    {
        lock (_serialLock)
        {
            if (_serial?.IsOpen != true) return false;
            _serial.WriteLine(command);
            return true;
        }
    }

    private void Close()
    {
        lock (_serialLock)
        {
            if (_serial?.IsOpen == true) _serial.Close();
        }
    }

    public override void Dispose()
    {
        lock (_serialLock) { _serial?.Dispose(); _serial = null; }
        base.Dispose();
    }
}
