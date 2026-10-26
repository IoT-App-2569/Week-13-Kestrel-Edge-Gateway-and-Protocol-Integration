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

// 3. งานเบื้องหลังสำหรับเชื่อมต่อและดักฟัง Serial Port (พร้อมโหมดจำลองสำหรับผู้ไม่มี Potentiometer)
public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;
    private readonly string _preferredPortName = "COM3"; // ค้นหาพอร์ตอัตโนมัติหากไม่พบ COM3
    private readonly int _baudRate = 115200;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<SerialBridgeWorker> _logger;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;

    // ตัวแปรสำหรับจำลองค่า Potentiometer (กรณีไม่มีฮาร์ดแวร์ Potentiometer)
    private double _simAngle = 0.0;
    private int? _manualSimulatedAdc = null;

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

    public void SetSimulatedAdc(int? raw)
    {
        _manualSimulatedAdc = raw.HasValue ? Math.Clamp(raw.Value, 0, 4095) : null;
        if (_serial?.IsOpen == true)
        {
            try
            {
                _serial.WriteLine(_manualSimulatedAdc.HasValue ? $"ADC:{_manualSimulatedAdc.Value}" : "SIM:AUTO");
            }
            catch { }
        }
    }

    public async Task PublishDeviceOnlineStatusAsync()
    {
        try
        {
            var statusMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/status")
                .WithPayload("online")
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(true)
                .Build();
            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(statusMsg));
        }
        catch { }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔌 เริ่มต้นบริการ SerialBridgeWorker (รองรับทั้ง Hardware Serial และ Virtual Potentiometer)...");

        await Task.Delay(500, stoppingToken);
        await PublishDeviceOnlineStatusAsync();

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
                    string? line = await Task.Run(() =>
                    {
                        try
                        {
                            return _serial.ReadLine()?.Trim();
                        }
                        catch (TimeoutException)
                        {
                            return null;
                        }
                    }, stoppingToken);

                    if (!string.IsNullOrEmpty(line))
                    {
                        await HandleIncomingSerialLine(line);
                        await Task.Delay(50, stoppingToken);
                    }
                    else
                    {
                        await GenerateSimulatedTelemetryAsync();
                        await Task.Delay(500, stoppingToken);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                    _serial?.Close();
                    _serial = null;
                    await Task.Delay(1000, stoppingToken);
                }
            }
            else
            {
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
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            double v = root.GetProperty("voltage").GetDouble();
            int r = root.GetProperty("raw").GetInt32();

            // หากต่อบอร์ดจริงแต่ไม่ได้ต่อ Potentiometer (ค่า raw ใกล้ 0) และผู้ใช้ตั้งค่าจำลองไว้ ให้ใช้ค่าจำลอง
            if (_manualSimulatedAdc.HasValue)
            {
                r = _manualSimulatedAdc.Value;
                v = Math.Round((r / 4095.0) * 3.3, 2);
                line = JsonSerializer.Serialize(new { voltage = v, raw = r, uptime = Environment.TickCount64 / 1000 });
            }

            var telemetry = new TelemetryData(v, r, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _store.Set("latest_telemetry", telemetry);

            _metrics.SerialPackets++;

            // 1. ผลักเข้า MQTT
            _metrics.MqttMessages++;
            var mqttMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/telemetry")
                .WithPayload(line)
                .Build();
            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));

            // 2. ผลักเข้า SignalR หาหน้าเว็บทันที!
            _metrics.SignalRPushes++;
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
            {
                voltage = v,
                raw = r,
                metrics = _metrics
            });
        }
        catch { }
    }

    // ฟังก์ชันส่งคำสั่งย้อนกลับไปหาบอร์ดทาง Serial
    public bool SendSerialCommand(string cmd)
    {
        _store.Set("led_command", cmd);
        if (_serial?.IsOpen == true)
        {
            _serial.WriteLine(cmd);
            return true;
        }
        return true;
    }

    // ซิงโครไนซ์สถานะหลอดไฟข้ามทั้ง 4 โปรโตคอล (Serial + MQTT + SignalR + StateStore)
    public async Task<bool> SetLedStateAsync(bool isOn, string source)
    {
        string cmd = isOn ? "LED:ON" : "LED:OFF";
        bool sent = SendSerialCommand(cmd);
        _store.Set("led_state", isOn);

        // ส่ง ACK ไปยัง MQTT เพื่อให้แอปมือถือ Flutter อัปเดตสวิตช์ตามทันที
        var ackPayload = JsonSerializer.Serialize(new { led_state = isOn, source });
        var ackMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/led_ack")
            .WithPayload(ackPayload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();
        await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));

        // กระจายสถานะไปยังหน้าเว็บแดชบอร์ดผ่าน SignalR
        await _hubContext.Clients.All.SendAsync("LedStateUpdated", isOn);
        return sent;
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}
