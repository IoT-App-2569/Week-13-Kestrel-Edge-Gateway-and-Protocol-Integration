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
    private readonly string _portName = "/dev/cu.usbserial-0001"; // ปรับให้ตรงกับพอร์ตของ ESP32 บนเครื่องคุณ
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
        // รอให้ Embedded MQTT Broker เริ่มทำงานก่อน ไม่งั้น publish status ตอนเปิด Serial จะหาย
        while (!_mqttServer.IsStarted && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(100, stoppingToken);
        }

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

                    await PublishStatusAsync(true);
                    await PublishLedAckAsync();
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
                await PublishStatusAsync(false);
            }
        }
    }

    private async Task HandleIncomingSerialLine(string line)
    {
        try
        {
            // ตัวอย่างข้อมูลที่ ESP32 ยิงมา: {"voltage": 1.65, "raw": 2048}
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            double v = root.GetProperty("voltage").GetDouble();
            int r = root.GetProperty("raw").GetInt32();

            var telemetry = new TelemetryData(v, r, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _metrics.SerialPackets++;

            // 1. อัปเดตลง State Store ในหน่วยความจำ
            _store.Set("latest_telemetry", telemetry);

            // 2. แปลงและผลักเข้าสู่ Embedded MQTT Broker
            _metrics.MqttMessages++;
            var mqttMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/telemetry")
                .WithPayload(line)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));

            // 3. ผลักเข้า SignalR หาหน้าเว็บทันที
            _metrics.SignalRPushes++;
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
            {
                voltage = v,
                raw = r,
                metrics = _metrics
            });
        }
        catch { /* หากไม่ใช่ JSON ให้ข้ามไป */ }
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

    // สถานะ LED ล่าสุดที่ Gateway สั่งไปยังบอร์ดสำเร็จ
    private volatile bool _ledOn;
    public bool LedState => _ledOn;

    // จุดสั่งไฟรวมศูนย์ ทุกช่องทาง (MQTT / SignalR / REST) เรียกผ่านที่นี่
    // เพื่อให้ Serial, MQTT (Flutter) และเว็บ (SignalR) ได้สถานะตรงกันเสมอ
    public async Task<bool> SetLedAsync(bool state, string source)
    {
        string cmd = state ? "LED:ON" : "LED:OFF";
        bool sent = SendSerialCommand(cmd);

        if (sent)
        {
            _ledOn = state;
            _store.Set("led_state", state);
        }
        _logger.LogInformation("💡 [{Source}] {Cmd} -> sentToSerial={Sent}", source, cmd, sent);

        // ส่งสถานะจริงกลับทุกครั้ง (ถ้าส่งเข้า Serial ไม่ได้ สถานะจะไม่เปลี่ยน UI จะถูกดึงกลับ)
        await PublishLedAckAsync();
        await _hubContext.Clients.All.SendAsync("LedStateUpdated", _ledOn);
        return sent;
    }

    private Task PublishStatusAsync(bool online) =>
        PublishMqttAsync("iot/esp32/status", online ? "online" : "offline", retain: true);

    private Task PublishLedAckAsync() =>
        PublishMqttAsync("iot/esp32/led_ack", JsonSerializer.Serialize(new { led_state = _ledOn }), retain: true);

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
        catch (Exception ex)
        {
            _logger.LogWarning("MQTT publish {Topic} failed: {Message}", topic, ex.Message);
        }
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}
