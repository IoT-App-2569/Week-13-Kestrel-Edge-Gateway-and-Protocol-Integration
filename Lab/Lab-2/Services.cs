using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Server;

public sealed class GatewayStateStore
{
    private readonly ConcurrentDictionary<string, object> _state = new();

    public void Set(string key, object value) => _state[key] = value;

    public T? Get<T>(string key) =>
        _state.TryGetValue(key, out var value) && value is T typedValue
            ? typedValue
            : default;
}

public sealed record TelemetryData(double Voltage, int Raw, long Timestamp);

public sealed class SerialBridgeWorker : BackgroundService
{
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
        _logger.LogInformation("🔌 พยายามเปิด Serial Port {Port} ที่ {BaudRate} bps...", _portName, _baudRate);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_serial is null || !_serial.IsOpen)
            {
                try
                {
                    _serial?.Dispose();
                    _serial = new SerialPort(_portName, _baudRate)
                    {
                        NewLine = "\n",
                        ReadTimeout = 1000
                    };
                    _serial.Open();
                    _logger.LogInformation("✅ เชื่อมต่อฮาร์ดแวร์ Serial Port {Port} สำเร็จ!", _portName);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
                {
                    _logger.LogWarning("ยังเปิด Serial Port {Port} ไม่ได้: {Message}", _portName, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }
            }

            try
            {
                var line = _serial.ReadLine().Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    await HandleIncomingSerialLineAsync(line, stoppingToken);
                }
            }
            catch (TimeoutException)
            {
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                _serial.Close();
            }
            catch (IOException ex)
            {
                _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                _serial.Close();
            }
        }
    }

    private async Task HandleIncomingSerialLineAsync(string line, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var telemetry = new TelemetryData(
                root.GetProperty("voltage").GetDouble(),
                root.GetProperty("raw").GetInt32(),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _store.Set("latest_telemetry", telemetry);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/telemetry")
                .WithPayload(line)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _mqttServer.InjectApplicationMessage(
                new InjectedMqttApplicationMessage(message),
                cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("ข้ามข้อมูล Serial ที่ไม่ใช่ JSON: {Message}", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("ข้ามข้อมูล Telemetry ที่ฟิลด์ไม่ครบ: {Message}", ex.Message);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning("ข้ามข้อมูล Telemetry ที่รูปแบบไม่ถูกต้อง: {Message}", ex.Message);
        }
    }

    public bool SendSerialCommand(string command)
    {
        if (_serial?.IsOpen != true)
        {
            return false;
        }

        _serial.WriteLine(command);
        return true;
    }

    public override void Dispose()
    {
        _serial?.Dispose();
        base.Dispose();
    }
}
