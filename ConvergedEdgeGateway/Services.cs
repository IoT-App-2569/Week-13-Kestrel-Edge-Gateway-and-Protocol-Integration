using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Server;
using Microsoft.AspNetCore.SignalR;

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

// 3. งานเบื้องหลัง: Serial Bridge + ศูนย์กลาง sync สถานะ LED (13.4)
public class SerialBridgeWorker : BackgroundService
{
    // หัวข้อ MQTT ให้ตรงกับแอป Flutter (Lab 12)
    public const string TopicTelemetry = "iot/esp32/telemetry";
    public const string TopicControl   = "iot/esp32/control";
    public const string TopicLedAck    = "iot/esp32/led_ack";
    public const string TopicStatus    = "iot/esp32/status";

    private SerialPort? _serial;
    private readonly object _serialLock = new();
    private readonly string _portName = "COM5"; // ปรับให้ตรงกับพอร์ตของ ESP32
    private readonly int _baudRate = 115200;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<SerialBridgeWorker> _logger;
    private readonly long _startTicks = Environment.TickCount64;

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
                    _serial = new SerialPort(_portName, _baudRate) { NewLine = "\n", ReadTimeout = 1000 };
                    _serial.Open();
                    _logger.LogInformation("✅ เชื่อมต่อฮาร์ดแวร์ Serial Port {Port} สำเร็จ!", _portName);
                    await PublishMqttAsync(TopicStatus, "online", retain: true);
                }
                catch
                {
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
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                _serial?.Close();
                // บอร์ดหลุด → แจ้งแอปว่า offline (เหมือน LWT)
                await PublishMqttAsync(TopicStatus, "offline", retain: true);
            }
        }
    }

    private async Task HandleIncomingSerialLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            double v = root.GetProperty("voltage").GetDouble();
            int r = root.GetProperty("raw").GetInt32();
            long uptime = (Environment.TickCount64 - _startTicks) / 1000;

            // 1. เก็บค่าล่าสุดให้ REST API
            _store.Set("latest_telemetry", new TelemetryData(v, r, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            _metrics.SerialPackets++;

            // 2. ส่งเข้า MQTT (เพิ่ม uptime ให้ตรงรูปแบบของแอป Flutter)
            var payload = JsonSerializer.Serialize(new { voltage = v, raw = r, uptime });
            await PublishMqttAsync(TopicTelemetry, payload);
            _metrics.MqttMessages++;

            // 3. ส่งเข้า SignalR หาหน้าเว็บ
            _metrics.SignalRPushes++;
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new { voltage = v, raw = r, metrics = _metrics });
        }
        catch { /* ไม่ใช่ JSON ให้ข้าม */ }
    }

    // ============================================================
    // ศูนย์กลางสั่ง LED: ทุกช่องทาง (MQTT / SignalR / REST) เรียกที่นี่
    // ส่งไป Serial แล้วแจ้งสถานะใหม่ให้ทั้งแอป (MQTT) และเว็บ (SignalR)
    // ============================================================
    public async Task<bool> SetLedAsync(bool on, string source)
    {
        string cmd = on ? "LED:ON" : "LED:OFF";
        bool sent = SendSerialCommand(cmd);
        _logger.LogInformation("💡 {Cmd} จาก {Source} (sentToSerial={Sent})", cmd, source, sent);

        if (sent)
        {
            _store.Set("led_state", on);
            await PublishMqttAsync(TopicLedAck, on ? "{\"led_state\": true, \"ack\": true}" : "{\"led_state\": false, \"ack\": true}");
            await _hubContext.Clients.All.SendAsync("LedStateUpdated", on);
        }
        return sent;
    }

    public bool SendSerialCommand(string cmd)
    {
        lock (_serialLock)
        {
            if (_serial?.IsOpen == true)
            {
                _serial.WriteLine(cmd);
                return true;
            }
            return false;
        }
    }

    private async Task PublishMqttAsync(string topic, string payload, bool retain = false)
    {
        try
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(retain)
                .Build();
            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(msg));
        }
        catch { /* Broker ยังไม่พร้อม */ }
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}