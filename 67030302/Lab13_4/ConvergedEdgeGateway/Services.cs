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

// 2. คลาสนับสถิติทราฟฟิกของทั้ง 4 โปรโตคอล
public class ProtocolMetrics
{
    public long SerialPackets { get; set; } = 0;
    public long MqttMessages { get; set; } = 0;
    public long RestRequests { get; set; } = 0;
    public long SignalRPushes { get; set; } = 0;
}

// 3. โมเดลข้อมูล Telemetry
public record TelemetryData(double Voltage, int Raw, long Timestamp);

// 4. ผู้ประสานงานการซิงโครไนซ์สถานะข้าม 4 โปรโตคอล (Tri-directional State Synchronizer)
public class StateSyncCoordinator
{
    private readonly GatewayStateStore _store;
    private readonly ProtocolMetrics _metrics;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<StateSyncCoordinator> _logger;
    private SerialBridgeWorker? _serialWorker;

    public StateSyncCoordinator(
        GatewayStateStore store,
        ProtocolMetrics metrics,
        IHubContext<GatewayHub> hubContext,
        MqttServer mqttServer,
        ILogger<StateSyncCoordinator> logger)
    {
        _store = store;
        _metrics = metrics;
        _hubContext = hubContext;
        _mqttServer = mqttServer;
        _logger = logger;
    }

    public void RegisterSerialWorker(SerialBridgeWorker worker)
    {
        _serialWorker = worker;
    }

    public async Task SetLedStateAsync(bool state, string source)
    {
        _store.Set("led_state", state);
        _logger.LogInformation("🔄 [Universal State Sync] สั่งการจาก: {Source} -> สถานะ LED: {State}", source, state ? "ON" : "OFF");

        // 1. กระจายคำสั่งลงสู่สาย Serial ไปยังฮาร์ดแวร์ ESP32
        string cmd = state ? "LED:ON" : "LED:OFF";
        _serialWorker?.SendSerialCommand(cmd);

        // 2. กระจายสถานะใหม่ไปยังหน้าเว็บ Web Dashboard ทุกเครื่องผ่าน SignalR
        _metrics.SignalRPushes++;
        await _hubContext.Clients.All.SendAsync("LedStateUpdated", state);

        // 3. กระจายสัญญาณยืนยัน (ACK) และสถานะผ่าน Embedded MQTT Broker ไปยัง Flutter App
        try
        {
            _metrics.MqttMessages++;
            var ackPayload = JsonSerializer.Serialize(new { led_state = state, ack = true, source = source });
            var ackMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/led_ack")
                .WithPayload(ackPayload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("MQTT Inject Exception: {Msg}", ex.Message);
        }
    }
}

// 5. งานเบื้องหลังสำหรับเชื่อมต่อและดักฟัง Serial Port
public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;
    private readonly string _portName = "/dev/cu.usbserial-0001"; // พอร์ต ESP32 บน macOS
    private readonly int _baudRate = 115200;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<SerialBridgeWorker> _logger;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;

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
                    // หากยังไม่เสียบบอร์ด ให้ทำงานในโหมดจำลองฮาร์ดแวร์เสมือน (Virtual Simulation)
                    // จำลองการหมุนตัวต้านทานปรับค่าได้ (Potentiometer Sweep)
                    double simVoltage = Math.Round(1.65 + 1.45 * Math.Sin(DateTime.Now.Ticks / 15000000.0), 2);
                    if (simVoltage < 0.1) simVoltage = 0.1;
                    if (simVoltage > 3.3) simVoltage = 3.3;
                    int simRaw = (int)((simVoltage / 3.3) * 4095);

                    string simulatedLine = $"{{\"voltage\":{simVoltage:F2},\"raw\":{simRaw}}}";
                    await HandleIncomingSerialLine(simulatedLine);
                    await Task.Delay(500, stoppingToken);
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
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            double v = root.GetProperty("voltage").GetDouble();
            int r = root.GetProperty("raw").GetInt32();

            var telemetry = new TelemetryData(v, r, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // 1. อัปเดตลง State Store ในหน่วยความจำ (สำหรับ REST API)
            _store.Set("latest_telemetry", telemetry);

            _metrics.SerialPackets++;

            // 2. ผลักเข้าสู่ Embedded MQTT Broker (สำหรับ Flutter App)
            _metrics.MqttMessages++;
            var mqttMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/telemetry")
                .WithPayload(line)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();
            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));

            // 3. ผลักเข้า SignalR หาหน้าเว็บ Web Command Center ทันที!
            _metrics.SignalRPushes++;
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
            {
                voltage = v,
                raw = r,
                metrics = new
                {
                    serialPackets = _metrics.SerialPackets,
                    mqttMessages = _metrics.MqttMessages,
                    restRequests = _metrics.RestRequests,
                    signalRPushes = _metrics.SignalRPushes
                }
            });
        }
        catch { }
    }

    // ฟังก์ชันส่งคำสั่งย้อนกลับไปหาบอร์ดทาง Serial
    public bool SendSerialCommand(string cmd)
    {
        if (_serial?.IsOpen == true)
        {
            _serial.WriteLine(cmd);
            _logger.LogInformation("⚡ [ฮาร์ดแวร์ Serial] ส่งคำสั่ง '{Cmd}' ไปยัง ESP32 สำเร็จ", cmd);
            return true;
        }

        _logger.LogInformation("💡 [โหมดจำลอง] คำสั่ง Serial '{Cmd}' ถูกส่งไปยังบอร์ดเสมือนสำเร็จ!", cmd);
        return true;
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}
