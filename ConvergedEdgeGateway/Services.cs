using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Server;

public class GatewayStateStore
{
    private readonly ConcurrentDictionary<string, object> _state = new();

    public void Set(string key, object val) => _state[key] = val;
    public T? Get<T>(string key) => _state.TryGetValue(key, out var val) && val is T t ? t : default;
    public object? Get(string key) => _state.TryGetValue(key, out var val) ? val : null;
}

public record TelemetryData(double Voltage, int Raw, long Timestamp);

public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;
    private readonly string _portName = "/dev/cu.usbserial-0001"; // แก้ให้ตรงกับ ls /dev/cu.*
    private readonly int _baudRate = 115200;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<SerialBridgeWorker> _logger;
    private DateTime _lastStatusPublish = DateTime.MinValue;

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
            // ประกาศสถานะ online ให้แอป Flutter ซ้ำทุก 5 วินาที (กันกรณี Broker ยังไม่พร้อมตอนเริ่ม)
            if (_serial?.IsOpen == true && DateTime.UtcNow - _lastStatusPublish > TimeSpan.FromSeconds(5))
            {
                _lastStatusPublish = DateTime.UtcNow;
                try { await PublishMqttAsync("iot/esp32/status", "online", retain: true); } catch { }
            }

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
                    try { await Task.Delay(3000, stoppingToken); }
                    catch (TaskCanceledException) { break; }
                    continue;
                }
            }

            try
            {
                string line = (await Task.Run(() => _serial.ReadLine(), stoppingToken)).Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    await HandleIncomingSerialLine(line);
                }
            }
            catch (TimeoutException) { }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                _serial?.Close();
                try { await PublishMqttAsync("iot/esp32/status", "offline", retain: true); } catch { }
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

            var telemetry = new TelemetryData(v, r, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _store.Set("latest_telemetry", telemetry);

            _metrics.MqttMessages++;
            var mqttMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/telemetry")
                .WithPayload(line)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();
            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));

            _metrics.SignalRPushes++;
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
            {
                voltage = v,
                raw = r,
                metrics = _metrics
            });
        }
        catch { /* ไม่ใช่ JSON (เช่น boot log) ให้ข้าม */ }
    }

    public bool SendSerialCommand(string cmd)
    {
        if (_serial?.IsOpen == true)
        {
            _serial.WriteLine(cmd);
            return true;
        }
        return false;
    }

    public async Task PublishMqttAsync(string topic, string payload, bool retain = false)
    {
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag(retain)
            .Build();
        await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(msg));
    }

    // ตัวกลาง: ทุกเส้นทาง (REST / MQTT / SignalR) เรียกอันนี้
    public async Task<bool> SetLedAsync(bool on)
    {
        string cmd = on ? "LED:ON" : "LED:OFF";
        bool sent = SendSerialCommand(cmd);                 // 1. สั่งบอร์ด
        _store.Set("led_state", on);                        // 2. จำสถานะ

        await _hubContext.Clients.All.SendAsync("LedStateUpdated", on);   // 3. แจ้งเว็บ

        // 4. แจ้งแอป Flutter (รูปแบบที่แอปอ่านได้: {"led_state":true/false})
        await PublishMqttAsync("iot/esp32/led_ack",
            $"{{\"led_state\":{(on ? "true" : "false")}}}", retain: true);

        return sent;
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}