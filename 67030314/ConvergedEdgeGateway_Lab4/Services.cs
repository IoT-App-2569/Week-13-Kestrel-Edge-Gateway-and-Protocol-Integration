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

// 3. งานเบื้องหลังสำหรับเชื่อมต่อและดักฟัง Serial Port + ศูนย์กลางสั่งการ LED ของทุกโปรโตคอล
public class SerialBridgeWorker : BackgroundService
{
    public const string TopicTelemetry = "iot/esp32/telemetry";
    public const string TopicControl = "iot/esp32/control";
    public const string TopicLedAck = "iot/esp32/led_ack";
    public const string TopicStatus = "iot/esp32/status";

    private SerialPort? _serial;
    private readonly string _portName = "COM8"; // ปรับให้ตรงกับพอร์ตของ ESP32 บนเครื่องคุณ
    private readonly int _baudRate = 115200;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<SerialBridgeWorker> _logger;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;
    private readonly object _serialLock = new();
    private bool? _lastPublishedOnline;

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

    public bool LedState => _store.Get<bool>("led_state");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔌 พยายามเปิด Serial Port {Port}...", _portName);
        await Task.Delay(500, stoppingToken);   // รอให้ Embedded MQTT Broker เริ่มทำงานก่อน
        await PublishDeviceStatusAsync(false);

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
                    await PublishDeviceStatusAsync(true);
                }
                catch
                {
                    // หากยังไม่เสียบบอร์ด ให้รอ 3 วินาทีแล้วลองใหม่
                    await PublishDeviceStatusAsync(false);
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
                await PublishDeviceStatusAsync(false);
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

            _metrics.SerialPackets++;

            // 0. อัปเดตลง State Store ให้ REST API อ่านได้
            _store.Set("latest_telemetry", new TelemetryData(v, r, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

            // 1. ผลักเข้า MQTT (นับใน InterceptingPublishAsync ของ Program.cs)
            await PublishAsync(TopicTelemetry, line, retain: false);

            // 2. ผลักเข้า SignalR หาหน้าเว็บทันที!
            _metrics.SignalRPushes++;
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
            {
                voltage = v,
                raw = r,
                metrics = _metrics
            });
        }
        catch { /* หากไม่ใช่ JSON (เช่น Boot Log ของ ESP32) ให้ข้ามไป */ }
    }

    // ฟังก์ชันส่งคำสั่งย้อนกลับไปหาบอร์ดทาง Serial
    public bool SendSerialCommand(string cmd)
    {
        lock (_serialLock)
        {
            if (_serial?.IsOpen == true)
            {
                try
                {
                    _serial.WriteLine(cmd);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Serial Write Warning: {Message}", ex.Message);
                }
            }
            return false;
        }
    }

    // จุดเดียวที่ทุกโปรโตคอล (MQTT / REST / SignalR) ใช้สั่ง LED
    // ส่งคำสั่งทาง Serial ก่อน แล้วกระจาย "สถานะจริง" ไปให้เว็บ (SignalR) และแอป (MQTT led_ack) พร้อมกัน
    public async Task<(bool Sent, bool State)> SetLedAsync(bool on, string source)
    {
        bool sent = SendSerialCommand(on ? "LED:ON" : "LED:OFF");
        if (sent)
        {
            _store.Set("led_state", on);
        }
        bool state = LedState;
        _logger.LogInformation("💡 LED {Cmd} จาก {Source} -> sentToSerial={Sent}, state={State}",
            on ? "ON" : "OFF", source, sent, state ? "ON" : "OFF");

        await _hubContext.Clients.All.SendAsync("LedStateUpdated", state);
        _metrics.SignalRPushes++;
        await PublishAsync(TopicLedAck, JsonSerializer.Serialize(new { led_state = state, ack = sent }), retain: false);
        return (sent, state);
    }

    // บอกแอป Flutter ว่าบอร์ดพร้อมรับคำสั่งหรือไม่ (Retained เหมือน LWT ในสัปดาห์ที่ 11-12)
    private async Task PublishDeviceStatusAsync(bool online)
    {
        if (_lastPublishedOnline == online)
        {
            return;
        }
        _lastPublishedOnline = online;
        await PublishAsync(TopicStatus, online ? "online" : "offline", retain: true);
    }

    private async Task PublishAsync(string topic, string payload, bool retain)
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
        catch (Exception ex)
        {
            _logger.LogWarning("MQTT Inject Warning ({Topic}): {Message}", topic, ex.Message);
        }
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}
