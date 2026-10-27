using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;

// 1. คลาสจัดเก็บสถานะระบบแบบ Thread-Safe
public class GatewayStateStore
{
    private readonly ConcurrentDictionary<string, object> _state = new();
    public void Set(string key, object val) => _state[key] = val;
    public T? Get<T>(string key) => _state.TryGetValue(key, out var val) && val is T t ? t : default;
    public object? Get(string key) => _state.TryGetValue(key, out var val) ? val : null;
}

// 2. โมเดลข้อมูล Telemetry (Timestamp = Unix milliseconds)
public record TelemetryData(double Voltage, int Raw, bool Led, long Timestamp);

// 3. ค่าตั้งของพอร์ต Serial (อ่านจากส่วน "Serial" ใน appsettings.json)
public class SerialOptions
{
    public string Mode { get; set; } = "Hardware";   // "Hardware" = บอร์ดจริง, "Simulated" = จำลองค่า (ไม่ต้องมีบอร์ด)
    public string PortName { get; set; } = "COM3";
    public int BaudRate { get; set; } = 115200;
    public bool IsSimulated => Mode.Equals("Simulated", StringComparison.OrdinalIgnoreCase);
}

// 4. งานเบื้องหลังสำหรับเชื่อมต่อและดักฟัง Serial Port
public class SerialBridgeWorker : BackgroundService
{
    public const string TelemetryTopic = "iot/esp32/telemetry";
    public const string GatewayClientId = "gateway";

    // คำสั่งที่อนุญาตให้ส่งลงสาย Serial เท่านั้น (Allowlist)
    private static readonly HashSet<string> AllowedCommands = ["LED:ON", "LED:OFF"];
    private const int MaxLineLength = 256;

    private readonly SerialOptions _options;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<SerialBridgeWorker> _logger;
    private readonly Lock _writeLock = new();
    private SerialPort? _serial;
    private volatile bool _simulatedLed;

    public SerialBridgeWorker(
        SerialOptions options,
        GatewayStateStore store,
        MqttServer mqttServer,
        IHubContext<GatewayHub> hubContext,
        ProtocolMetrics metrics,
        ILogger<SerialBridgeWorker> logger)
    {
        _options = options;
        _store = store;
        _mqttServer = mqttServer;
        _hubContext = hubContext;
        _metrics = metrics;
        _logger = logger;
    }

    public bool IsConnected => _options.IsSimulated || _serial?.IsOpen == true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // MQTT Server ถูกสตาร์ทโดย Hosted Service อีกตัว ต้องรอให้พร้อมก่อน ไม่งั้น InjectApplicationMessage จะ throw
        while (!_mqttServer.IsStarted)
        {
            await Task.Delay(100, stoppingToken);
        }

        await (_options.IsSimulated
            ? RunSimulatedAsync(stoppingToken)
            : Task.Run(() => RunHardwareAsync(stoppingToken), stoppingToken));
    }

    private async Task RunHardwareAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔌 พยายามเปิด Serial Port {Port} @ {Baud} bps...", _options.PortName, _options.BaudRate);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_serial is not { IsOpen: true })
            {
                try
                {
                    _serial?.Dispose();
                    _serial = new SerialPort(_options.PortName, _options.BaudRate)
                    {
                        NewLine = "\n",
                        ReadTimeout = 1000,
                        WriteTimeout = 1000,
                        // ESP32 DevKit: DTR/RTS ต่อกับขา EN/IO0 ถ้าเปิดไว้บอร์ดอาจ reset ค้าง
                        DtrEnable = false,
                        RtsEnable = false
                    };
                    _serial.Open();
                    _logger.LogInformation("✅ เชื่อมต่อฮาร์ดแวร์ Serial Port {Port} สำเร็จ!", _options.PortName);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                {
                    // ยังไม่เสียบบอร์ด หรือโปรแกรมอื่น (idf monitor / PuTTY) ล็อกพอร์ตไว้: รอ 3 วินาทีแล้วลองใหม่
                    _logger.LogDebug("Serial open failed: {Message}", ex.Message);
                    await Task.Delay(3000, stoppingToken);
                    continue;
                }
            }

            try
            {
                string line = _serial.ReadLine().Trim();
                if (line.Length is > 0 and <= MaxLineLength)
                {
                    await HandleIncomingSerialLine(line);
                }
            }
            catch (TimeoutException) { /* ปกติเมื่อไม่มีข้อมูลส่งมาใน 1 วินาที */ }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                _logger.LogWarning("⚠️ Serial หลุดการเชื่อมต่อ: {Message}", ex.Message);
                _serial?.Close();
            }
        }
    }

    // โหมดจำลอง: สร้างข้อมูลรูปแบบเดียวกับเฟิร์มแวร์ ESP32 ทุก 500 ms
    private async Task RunSimulatedAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("🧪 Serial Mode = Simulated (ไม่ได้อ่านจากบอร์ดจริง)");
        var t = 0.0;
        while (!stoppingToken.IsCancellationRequested)
        {
            int raw = (int)(2048 + 1800 * Math.Sin(t));
            t += 0.15;
            var voltage = (raw / 4095.0 * 3.3).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            await HandleIncomingSerialLine($"{{\"voltage\":{voltage},\"raw\":{raw},\"led\":{(_simulatedLed ? 1 : 0)}}}");
            await Task.Delay(500, stoppingToken);
        }
    }

    private async Task HandleIncomingSerialLine(string line)
    {
        // ตัวอย่างข้อมูลที่ ESP32 ส่งมา: {"voltage":1.65,"raw":2048,"led":0}
        // บรรทัดอื่น (เช่น boot log ของ ESP-IDF) ไม่ใช่ JSON ให้ข้ามไป
        if (!line.StartsWith('{') || !TryParseTelemetry(line, out var telemetry))
        {
            return;
        }

        _metrics.CountSerialPacket();

        // 1. อัปเดตลง State Store ในหน่วยความจำ
        _store.Set("latest_telemetry", telemetry);

        // 2. แปลงและผลักเข้าสู่ Embedded MQTT Broker
        //    ส่ง JSON ที่ผ่านการตรวจสอบแล้วเท่านั้น (ไม่ส่งต่อข้อความดิบจากสาย Serial)
        await PublishAsync(TelemetryTopic, JsonSerializer.Serialize(new
        {
            voltage = telemetry.Voltage,
            raw = telemetry.Raw,
            led = telemetry.Led,
            ts = telemetry.Timestamp
        }));
        _metrics.CountMqttMessage();

        // 3. ผลักเข้า SignalR หาหน้าเว็บทันที!
        _metrics.CountSignalRPush();
        await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
        {
            voltage = telemetry.Voltage,
            raw = telemetry.Raw,
            led = telemetry.Led,
            metrics = _metrics
        });
    }

    private Task PublishAsync(string topic, string payload, bool retain = false)
    {
        if (!_mqttServer.IsStarted) return Task.CompletedTask;   // กำลังปิดระบบ

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .WithRetainFlag(retain)
            .Build();
        return _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(msg) { SenderClientId = GatewayClientId });
    }

    private static bool TryParseTelemetry(string line, out TelemetryData telemetry)
    {
        telemetry = default!;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("voltage", out var vEl) || vEl.ValueKind != JsonValueKind.Number || !vEl.TryGetDouble(out var v) ||
                !root.TryGetProperty("raw", out var rEl) || rEl.ValueKind != JsonValueKind.Number || !rEl.TryGetInt32(out var r))
            {
                return false;
            }
            // ตรวจช่วงค่าให้สมเหตุสมผล (ADC 12-bit, 0–3.3 V)
            if (r is < 0 or > 4095 || v is < 0 or > 3.6)
            {
                return false;
            }
            bool led = root.TryGetProperty("led", out var lEl) && lEl.ValueKind == JsonValueKind.Number && lEl.TryGetInt32(out var l) && l == 1;
            telemetry = new TelemetryData(Math.Round(v, 3), r, led, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // ฟังก์ชันส่งคำสั่งย้อนกลับไปหาบอร์ดทาง Serial (thread-safe, allowlist เท่านั้น)
    public bool SendSerialCommand(string cmd)
    {
        if (!AllowedCommands.Contains(cmd))
        {
            _logger.LogWarning("⛔ ปฏิเสธคำสั่ง Serial ที่ไม่อยู่ใน allowlist");
            return false;
        }

        if (_options.IsSimulated)
        {
            _simulatedLed = cmd == "LED:ON";
            return true;
        }

        lock (_writeLock)
        {
            if (_serial?.IsOpen != true) return false;
            try
            {
                _serial.WriteLine(cmd);
                return true;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
            {
                _logger.LogWarning("Serial write failed: {Message}", ex.Message);
                return false;
            }
        }
    }

    public override void Dispose()
    {
        _serial?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
