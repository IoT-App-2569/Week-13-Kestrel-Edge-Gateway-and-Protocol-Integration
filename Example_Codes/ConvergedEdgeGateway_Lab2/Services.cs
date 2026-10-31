
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Server;

// 1. จัดเก็บสถานะระบบแบบ Thread-Safe
public class GatewayStateStore
{
    private readonly ConcurrentDictionary<string, object> _state = new();

    public void Set(string key, object val) => _state[key] = val;

    public T? Get<T>(string key) =>
        _state.TryGetValue(key, out var val) && val is T t ? t : default;

    public object? Get(string key) =>
        _state.TryGetValue(key, out var val) ? val : null;
}

// 2. โมเดล Telemetry
public record TelemetryData(
    double Voltage,
    int Raw,
    long Timestamp
);

// 3. Background Service สำหรับ Serial Bridge
public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;

    private readonly string _portName = "/dev/cu.usbserial-0001";
    private readonly int _baudRate = 115200;

    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<SerialBridgeWorker> _logger;

    private readonly object _serialWriteLock = new();

    private const int MaxLineLength = 1024;

    public SerialBridgeWorker(
        GatewayStateStore store,
        MqttServer mqttServer,
        ILogger<SerialBridgeWorker> logger)
    {
        _store = store;
        _mqttServer = mqttServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "กำลังเชื่อมต่อ Serial Port {Port}...",
            _portName);

        var lineBuffer = new StringBuilder();
        var readBuffer = new byte[256];
        bool discardUntilNewline = false;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // เปิด Serial Port หรือเชื่อมต่อใหม่
                if (_serial == null || !_serial.IsOpen)
                {
                    try
                    {
                        CloseSerialPort();

                        _serial = new SerialPort(
                            _portName,
                            _baudRate)
                        {
                            ReadTimeout = 1000,
                            WriteTimeout = 1000,
                            NewLine = "\n",
                            DtrEnable = false,
                            RtsEnable = false
                        };

                        _serial.Open();

                        // ล้างข้อมูลที่ค้างจากการเชื่อมต่อก่อนหน้า
                        _serial.DiscardInBuffer();
                        lineBuffer.Clear();
                        discardUntilNewline = false;

                        _logger.LogInformation(
                            "เชื่อมต่อ Serial Port {Port} สำเร็จ",
                            _portName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            "เปิด Serial Port ไม่สำเร็จ: {Message}",
                            ex.Message);

                        CloseSerialPort();

                        await Task.Delay(
                            TimeSpan.FromSeconds(3),
                            stoppingToken);

                        continue;
                    }
                }

                try
                {
                    // อ่านข้อมูลเป็นชุดไบต์
                    int bytesRead = _serial.Read(
                        readBuffer,
                        0,
                        readBuffer.Length);

                    for (int i = 0; i < bytesRead; i++)
                    {
                        byte b = readBuffer[i];

                        // เมื่อพบ newline ถือว่าจบบรรทัด
                        if (b == (byte)'\n')
                        {
                            if (!discardUntilNewline)
                            {
                                string line = lineBuffer.ToString().Trim();

                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    await HandleIncomingSerialLine(
                                        line,
                                        stoppingToken);
                                }
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "ทิ้ง Serial line ที่ยาวเกิน {MaxLength} ตัวอักษร",
                                    MaxLineLength);
                            }

                            lineBuffer.Clear();
                            discardUntilNewline = false;
                            continue;
                        }

                        // หากบรรทัดยาวเกินกำหนด ให้ทิ้งจนกว่าจะพบ newline
                        if (discardUntilNewline)
                        {
                            continue;
                        }

                        // ข้าม CR ของ CRLF
                        if (b == (byte)'\r')
                        {
                            continue;
                        }

                        if (lineBuffer.Length >= MaxLineLength)
                        {
                            lineBuffer.Clear();
                            discardUntilNewline = true;
                            continue;
                        }

                        lineBuffer.Append((char)b);
                    }
                }
                catch (TimeoutException)
                {
                    // ไม่มีข้อมูลในช่วงเวลาที่กำหนด ให้อ่านต่อ
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "อ่าน Serial ไม่สำเร็จ: {Message}",
                        ex.Message);

                    CloseSerialPort();
                    lineBuffer.Clear();
                    discardUntilNewline = false;

                    await Task.Delay(
                        TimeSpan.FromSeconds(1),
                        stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "SerialBridgeWorker กำลังหยุดทำงาน");
        }
        finally
        {
            CloseSerialPort();

            _logger.LogInformation(
                "ปิด Serial Port เรียบร้อยแล้ว");
        }
    }

    // 4. ตรวจสอบและประมวลผล Telemetry JSON
    private async Task HandleIncomingSerialLine(
        string line,
        CancellationToken stoppingToken)
    {
        line = line.Trim();

        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        // ข้ามข้อความ Log ของ ESP-IDF
        if (!line.StartsWith("{"))
        {
            _logger.LogDebug(
                "ข้าม Serial Log ที่ไม่ใช่ JSON: {Line}",
                line);

            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("voltage", out var voltageElement) ||
                !root.TryGetProperty("raw", out var rawElement))
            {
                _logger.LogWarning(
                    "JSON ไม่มีฟิลด์ voltage หรือ raw: [{Line}]",
                    line);

                return;
            }

            if (!voltageElement.TryGetDouble(out double voltage) ||
                !rawElement.TryGetInt32(out int raw))
            {
                _logger.LogWarning(
                    "ชนิดข้อมูล voltage หรือ raw ไม่ถูกต้อง: [{Line}]",
                    line);

                return;
            }

            if (!double.IsFinite(voltage) ||
                voltage < 0 ||
                voltage > 3.6 ||
                raw < 0 ||
                raw > 4095)
            {
                _logger.LogWarning(
                    "ค่า Telemetry อยู่นอกช่วงที่คาดไว้: [{Line}]",
                    line);

                return;
            }

            var telemetry = new TelemetryData(
                voltage,
                raw,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // บันทึกข้อมูลล่าสุด
            _store.Set("latest_telemetry", telemetry);

            _logger.LogInformation(
                "Telemetry รับสำเร็จ: Voltage={Voltage:F2} V, Raw={Raw}",
                telemetry.Voltage,
                telemetry.Raw);

            // ส่งข้อมูลที่ผ่านการตรวจสอบแล้วเข้า MQTT
            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/telemetry")
                .WithPayload(line)
                .WithQualityOfServiceLevel(
                    MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _mqttServer.InjectApplicationMessage(
                new InjectedMqttApplicationMessage(mqttMessage),
                stoppingToken);

            _logger.LogInformation(
                "ส่ง Telemetry ไป MQTT Topic: iot/esp32/telemetry");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                "JSON parsing failed: {Message}; Received=[{Line}]",
                ex.Message,
                line);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ประมวลผล Telemetry ไม่สำเร็จ");
        }
    }

    // 5. ส่งคำสั่งควบคุม LED
    public bool SendSerialCommand(string cmd)
    {
        try
        {
            lock (_serialWriteLock)
            {
                if (_serial?.IsOpen != true)
                {
                    _logger.LogWarning(
                        "ส่งคำสั่งไม่ได้ เพราะ Serial Port ยังไม่เปิด");

                    return false;
                }

                _serial.WriteLine(cmd);
            }

            _logger.LogInformation(
                "ส่งคำสั่งไป ESP32: {Command}",
                cmd);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ส่งคำสั่ง Serial ไม่สำเร็จ");

            return false;
        }
    }

    // 6. ปิด Serial Port
    private void CloseSerialPort()
    {
        try
        {
            if (_serial != null)
            {
                if (_serial.IsOpen)
                {
                    _serial.Close();
                }

                _serial.Dispose();
                _serial = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "ปิด Serial Port ไม่สำเร็จ: {Message}",
                ex.Message);
        }
    }

    public override void Dispose()
    {
        CloseSerialPort();
        base.Dispose();
    }
}
