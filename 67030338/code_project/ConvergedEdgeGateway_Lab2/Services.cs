using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
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

// 3. งานเบื้องหลังสำหรับเชื่อมต่อและดักฟัง Serial Port (พร้อมโหมดจำลองสำหรับผู้ไม่มี Potentiometer)
public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;
    private readonly string _preferredPortName = "COM3"; // ค้นหาอัตโนมัติหากไม่พบ COM3
    private readonly int _baudRate = 115200;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<SerialBridgeWorker> _logger;

    // ตัวแปรสำหรับจำลองค่า Potentiometer (กรณีไม่มีฮาร์ดแวร์ Potentiometer หรือไม่ได้เสียบบอร์ด)
    private double _simAngle = 0.0;
    private int? _manualSimulatedAdc = null;

    public SerialBridgeWorker(GatewayStateStore store, MqttServer mqttServer, ILogger<SerialBridgeWorker> logger)
    {
        _store = store;
        _mqttServer = mqttServer;
        _logger = logger;
    }

    public void SetSimulatedAdc(int? raw)
    {
        _manualSimulatedAdc = raw.HasValue ? Math.Clamp(raw.Value, 0, 4095) : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔌 เริ่มต้นบริการ SerialBridgeWorker (รองรับทั้ง Hardware Serial และ Virtual Potentiometer)...");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_serial == null || !_serial.IsOpen)
            {
                string[] ports = SerialPort.GetPortNames();
                string? targetPort = null;

                if (ports.Contains(_preferredPortName, StringComparer.OrdinalIgnoreCase))
                {
                    targetPort = _preferredPortName;
                }
                else
                {
                    targetPort = ports.FirstOrDefault(p => !p.Equals("COM1", StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(targetPort))
                {
                    try
                    {
                        _serial = new SerialPort(targetPort, _baudRate)
                        {
                            NewLine = "\n",
                            ReadTimeout = 1000
                        };
                        _serial.Open();
                        _logger.LogInformation("✅ เชื่อมต่อฮาร์ดแวร์ Serial Port {Port} สำเร็จ!", targetPort);
                    }
                    catch
                    {
                        _serial = null;
                    }
                }
            }

            if (_serial != null && _serial.IsOpen)
            {
                try
                {
                    string line = _serial.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        await HandleIncomingSerialLine(line);
                        continue;
                    }
                }
                catch (TimeoutException)
                {
                    // หากพอร์ตเปิดอยู่แต่ไม่มีข้อมูลเข้ามาภายใน 1 วินาที ให้จำลองค่า Virtual Potentiometer แทน
                    await GenerateSimulatedTelemetryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                    _serial?.Close();
                    _serial = null;
                }
            }
            else
            {
                // กรณีไม่มี Potentiometer หรือไม่ได้เชื่อมต่อบอร์ด ให้สร้างข้อมูลจำลอง (Virtual Potentiometer) ทุก 500ms
                await GenerateSimulatedTelemetryAsync();
                await Task.Delay(500, stoppingToken);
            }
        }
    }

    private async Task GenerateSimulatedTelemetryAsync()
    {
        int raw;
        if (_manualSimulatedAdc.HasValue)
        {
            raw = _manualSimulatedAdc.Value;
        }
        else
        {
            double sinVal = (Math.Sin(_simAngle) + 1.0) / 2.0;
            raw = (int)Math.Round(sinVal * 4095.0);
            _simAngle += 0.15;
            if (_simAngle >= Math.PI * 2.0) _simAngle -= Math.PI * 2.0;
        }

        double voltage = Math.Round((raw / 4095.0) * 3.3, 2);
        long uptime = Environment.TickCount64 / 1000;
        string jsonLine = JsonSerializer.Serialize(new { voltage, raw, uptime });

        await HandleIncomingSerialLine(jsonLine);
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

            // 1. อัปเดตลง State Store ในหน่วยความจำ
            _store.Set("latest_telemetry", telemetry);

            // 2. แปลงและผลักเข้าสู่ Embedded MQTT Broker
            var mqttMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/telemetry")
                .WithPayload(line)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));
        }
        catch { /* หากไม่ใช่ JSON ให้ข้ามไป */ }
    }

    // ฟังก์ชันส่งคำสั่งย้อนกลับไปหาบอร์ดทาง Serial (หรือจำลองการรับคำสั่งหากไม่ได้ต่อพอร์ต)
    public bool SendSerialCommand(string cmd)
    {
        _store.Set("led_command", cmd);
        if (_serial?.IsOpen == true)
        {
            _serial.WriteLine(cmd);
            return true;
        }
        _logger.LogInformation("💡 [Virtual Mode] จำลองการส่งคำสั่งควบคุมไปยังบอร์ด: {Cmd}", cmd);
        return true;
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}
