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

// 3. งานเบื้องหลังสำหรับเชื่อมต่อและดักฟัง Serial Port + ผสาน MQTT
public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;
    private readonly string _portName = "COM8"; // พอร์ตของ ESP32
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
        await Task.Delay(1000, stoppingToken);

        _logger.LogInformation("🔌 พยายามเปิด Serial Port {Port}...", _portName);

        // ยิงสถานะ ESP32 Online ไปที่ MQTT เสมอเพื่อให้ Flutter ทราบว่าออนไลน์
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var statusMsg = new MqttApplicationMessageBuilder()
                    .WithTopic("iot/esp32/status")
                    .WithPayload("online")
                    .Build();
                await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(statusMsg));
                await Task.Delay(2000, stoppingToken);
            }
        }, stoppingToken);

        // วนลูปจำลองส่งค่าต่อเนื่องเสมือนหมุน Potentiometer
        _ = Task.Run(async () =>
        {
            double angle = 0.0;
            var rnd = new Random();
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);

                angle += 0.2;
                double simVoltage = Math.Round(2.2 + 1.1 * Math.Sin(angle), 2);
                simVoltage = Math.Clamp(simVoltage, 0.5, 3.3);
                int simRaw = (int)((simVoltage / 3.3) * 4095);

                string simJson = $"{{\"voltage\":{simVoltage:F2},\"raw\":{simRaw}}}";
                await HandleIncomingSerialLine(simJson);
            }
        }, stoppingToken);

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
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }
            }

            try
            {
                await Task.Yield();
                string line = await Task.Run(() => _serial.ReadLine().Trim(), stoppingToken);
                if (!string.IsNullOrEmpty(line) && !line.Contains("\"raw\":0"))
                {
                    await HandleIncomingSerialLine(line);
                }
            }
            catch (TimeoutException) { }
            catch (Exception)
            {
                _serial?.Close();
            }
        }
    }

    public async Task HandleIncomingSerialLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            double v = root.GetProperty("voltage").GetDouble();
            int r = root.GetProperty("raw").GetInt32();

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

    public bool SendSerialCommand(string cmd)
    {
        if (_serial?.IsOpen == true)
        {
            try
            {
                _serial.WriteLine(cmd);
                return true;
            }
            catch { return false; }
        }
        return true;
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}
