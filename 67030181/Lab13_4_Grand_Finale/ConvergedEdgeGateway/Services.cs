using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Server;

namespace ConvergedEdgeGateway;

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

// 3. คลาสนับสถิติทราฟฟิกของแต่ละโปรโตคอล
public class ProtocolMetrics
{
    public long SerialPackets { get; set; } = 0;
    public long MqttMessages { get; set; } = 0;
    public long RestRequests { get; set; } = 0;
    public long SignalRPushes { get; set; } = 0;
}

// 4. งานเบื้องหลังสำหรับเชื่อมต่อและดักฟัง Serial Port (พร้อมระบบจำลองข้อมูลอัตโนมัติหากยังไม่ได้ต่อบอร์ด)
public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;
    private readonly string _portName = "COM3"; // ปรับเป็นพอร์ตของ ESP32 ตามเครื่องของผู้ใช้ เช่น COM3, COM24
    private readonly int _baudRate = 115200;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<SerialBridgeWorker> _logger;
    private bool _simulatedFallback = false;

    public bool IsHardwareConnected => _serial?.IsOpen == true;
    public string CurrentPortName => _portName;

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
        _logger.LogInformation("🔌 กำลังเริ่มต้นระบบ Hardware Serial Bridge (กำลังค้นหาพอร์ต {Port})...", _portName);

        // ตรวจสอบพอร์ตที่มีอยู่ในระบบ
        var availablePorts = SerialPort.GetPortNames();
        _logger.LogInformation("📋 รายการพอร์ต Serial ที่พบในเครื่อง: [{Ports}]", string.Join(", ", availablePorts));

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
                    _simulatedFallback = false;
                    _logger.LogInformation("✅ เชื่อมต่อฮาร์ดแวร์จริง ESP32 บนพอร์ต {Port} สำเร็จ!", _portName);
                }
                catch (Exception ex)
                {
                    if (!_simulatedFallback)
                    {
                        _logger.LogWarning("⚠️ ไม่สามารถเปิดพอร์ต {Port} ได้ ({Msg}) -> เปิดใช้งานโหมดจำลองสถานะอัตโนมัติ (Simulation Mode) ชั่วคราว เพื่อให้ Web Dashboard และ MQTT ทำงานต่อเนื่อง", _portName, ex.Message);
                        _simulatedFallback = true;
                    }

                    // ในโหมดจำลอง (หากไม่มีบอร์ดจริงเสียบอยู่) ให้สร้างข้อมูล ADC จำลอง
                    double simVoltage = Math.Round(1.0 + Math.Sin(DateTime.UtcNow.Ticks / 10000000.0) * 1.5 + 0.8, 2);
                    if (simVoltage < 0) simVoltage = 0.05;
                    if (simVoltage > 3.3) simVoltage = 3.30;
                    int simRaw = (int)(simVoltage / 3.3 * 4095);

                    string simLine = $"{{\"voltage\":{simVoltage:F2},\"raw\":{simRaw}}}";
                    await ProcessIncomingData(simLine, isSimulated: true);

                    await Task.Delay(500, stoppingToken);
                    continue;
                }
            }

            try
            {
                string line = _serial.ReadLine().Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    await ProcessIncomingData(line, isSimulated: false);
                }
            }
            catch (TimeoutException)
            {
                // หมดเวลารอข้อมูลใน 1 วินาที เป็นเรื่องปกติเมื่อบอร์ดยังไม่ส่ง
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                _serial?.Close();
            }
        }
    }

    public async Task ProcessIncomingData(string line, bool isSimulated = false)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            double v = root.GetProperty("voltage").GetDouble();
            int r = root.GetProperty("raw").GetInt32();

            var telemetry = new TelemetryData(v, r, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // 1. อัปเดตลง State Store ในหน่วยความจำ
            _store.Set("latest_telemetry", telemetry);
            _store.Set("is_simulated", isSimulated);
            _metrics.SerialPackets++;

            // 2. แปลงและผลักเข้าสู่ Embedded MQTT Broker
            _metrics.MqttMessages++;
            var mqttMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/telemetry")
                .WithPayload(line)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));

            // 3. ผลักเข้า SignalR หาหน้าเว็บทันที!
            _metrics.SignalRPushes++;
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
            {
                voltage = v,
                raw = r,
                timestamp = telemetry.Timestamp,
                isSimulated = isSimulated,
                metrics = _metrics
            });
        }
        catch
        {
            // หากไม่ใช่ JSON ให้ข้ามไป
        }
    }

    // ฟังก์ชันส่งคำสั่งย้อนกลับไปหาบอร์ดทาง Serial
    public bool SendSerialCommand(string cmd)
    {
        if (_serial?.IsOpen == true)
        {
            try
            {
                _serial.WriteLine(cmd);
                _logger.LogInformation("📤 ส่งคำสั่งไปยังฮาร์ดแวร์ Serial สำเร็จ: {Cmd}", cmd);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("เกิดข้อผิดพลาดในการส่ง Serial: {Msg}", ex.Message);
                return false;
            }
        }
        else
        {
            _logger.LogInformation("ℹ️ โหมดจำลอง: จำลองการตอบรับคำสั่ง {Cmd} สำเร็จ", cmd);
            return true;
        }
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}
