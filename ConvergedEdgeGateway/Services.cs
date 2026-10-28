using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Server;

// 1. คลาสจัดเก็บสถานะระบบแบบ Thread-Safe
public class GatewayStateStore
{
    private readonly ConcurrentDictionary<string, object> _state = new();

    public void Set(string key, object val) => _state[key] = val;
    public T? Get<T>(string key) => _state.TryGetValue(key, out var val) && val is T t ? t : default;
    public object? Get(string key) => _state.TryGetValue(key, out var val) ? val : null;
}

// 2. โมเดลข้อมูล Telemetry
public record TelemetryData(double Voltage, int Raw, long Timestamp);

// 3. งานเบื้องหลังสำหรับเชื่อมต่อและดักฟัง Serial Port
public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;
    private readonly string _portName = "COM5"; // ปรับให้ตรงกับพอร์ตของ ESP32 บนเครื่องคุณ
    private readonly int _baudRate = 115200;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<SerialBridgeWorker> _logger;

    public SerialBridgeWorker(
        GatewayStateStore store,
        MqttServer mqttServer,
        IHubContext<GatewayHub> hubContext,
        ProtocolMetrics metrics,
        ILogger<SerialBridgeWorker> logger)
    {
        _store = store;
        _mqttServer = mqttServer;
        _hubContext = hubContext;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        _logger.LogInformation("🔌 พยายามเปิด Serial Port {Port}...", _portName);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_serial == null || !_serial.IsOpen)
            {
                try
                {
                    _serial = new SerialPort(_portName, _baudRate)
                    {
                        NewLine = "\n",
                        ReadTimeout = 1000
                    };
                    _serial.Open();
                    _logger.LogInformation("✅ เชื่อมต่อฮาร์ดแวร์ Serial Port {Port} สำเร็จ!", _portName);
                }
                catch
                {
                    // หากยังไม่เสียบบอร์ด ให้รอ 3 วินาทีแล้วลองใหม่
                    await Task.Delay(3000, stoppingToken);
                    continue;
                }
            }

            try
            {
                string line = _serial.ReadLine().Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    await HandleIncomingSerialLine(line);
                }
            }
            catch (TimeoutException) { /* ปกติเมื่อไม่มีข้อมูลส่งมาใน 1 วิ */ }
            catch (Exception ex)
            {
                _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                _serial?.Close();
            }
        }
    }

    private async Task HandleIncomingSerialLine(string line)
    {
        double voltage;
        int raw;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("voltage", out var voltageElement) ||
                !voltageElement.TryGetDouble(out voltage) ||
                !root.TryGetProperty("raw", out var rawElement) ||
                !rawElement.TryGetInt32(out raw))
            {
                _logger.LogWarning("Ignoring invalid telemetry payload from serial port: {Payload}", line);
                return;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Ignoring invalid JSON from serial port.");
            return;
        }

        _metrics.RecordSerialPacket();
        _store.Set("latest_telemetry", new TelemetryData(
            voltage,
            raw,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

        var mqttMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/telemetry")
            .WithPayload(line)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        try
        {
            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));
            _metrics.RecordMqttMessage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward telemetry to MQTT.");
        }

        _metrics.RecordSignalRPush();
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
            {
                voltage,
                raw,
                metrics = _metrics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push telemetry to SignalR clients.");
        }
    }

    // ฟังก์ชันส่งคำสั่งย้อนกลับไปหาบอร์ดทาง Serial
    public bool SendSerialCommand(string cmd)
    {
        if (_serial?.IsOpen == true)
        {
            _serial.WriteLine(cmd);
            return true;
        }
        return false;
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}