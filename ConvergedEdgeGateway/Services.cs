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
    private readonly string _portName = "COM5"; // ปรับให้ตรงกับพอร์ตของ ESP32 บนเครื่องคุณ
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
        await Task.Yield();
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
                    await PublishDeviceStatusAsync("online");
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
                    if (line.StartsWith("{") && line.EndsWith("}"))
                    {
                        await HandleIncomingSerialLine(line);
                    }
                }
            }
            catch (TimeoutException) { /* ปกติเมื่อไม่มีข้อมูลส่งมาใน 1 วิ */ }
            catch (Exception ex)
            {
                _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                _serial?.Close();
                await PublishDeviceStatusAsync("offline");
            }
        }
    }

    public async Task PublishDeviceStatusAsync(string status)
    {
        try
        {
            var statusMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/status")
                .WithPayload(status)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(true)
                .Build();

            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(statusMsg)
            {
                SenderClientId = "KestrelGateway"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish device status to MQTT.");
        }
    }

    public async Task<bool> SetLedStateAsync(bool isOn, string source)
    {
        string cmd = isOn ? "LED:ON" : "LED:OFF";
        bool sentToSerial = SendSerialCommand(cmd);
        _store.Set("led_state", isOn);

        _logger.LogInformation("💡 [{Source}] สั่งงาน {Cmd} (Serial: {Sent})", source, cmd, sentToSerial);

        // 1. ส่ง ACK กลับไปยังแอป Flutter ผ่าน MQTT (iot/esp32/led_ack)
        try
        {
            string ackPayload = JsonSerializer.Serialize(new { led_state = isOn, ack = true });
            var ackMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/led_ack")
                .WithPayload(ackPayload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(true)
                .Build();

            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg)
            {
                SenderClientId = "KestrelGateway"
            });
            _metrics.RecordMqttMessage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish LED ACK to MQTT.");
        }

        // 2. อัปเดตปุ่มสวิตช์บนหน้าเว็บผ่าน SignalR
        try
        {
            await _hubContext.Clients.All.SendAsync("LedStateUpdated", isOn);
            _metrics.RecordSignalRPush();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast LedStateUpdated to SignalR.");
        }

        return sentToSerial;
    }

    private async Task HandleIncomingSerialLine(string line)
    {
        _logger.LogInformation("📥 ได้รับข้อมูล Serial: {Line}", line);
        double voltage;
        int raw;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("voltage", out var voltageElement) ||
                !voltageElement.TryGetDouble(out voltage) ||
                !root.TryGetProperty("raw", out var rawElement) ||
                !rawElement.TryGetInt32(out raw))
            {
                _logger.LogWarning("Ignoring invalid telemetry payload from serial port: {Payload}", line);
                return;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Ignoring invalid JSON from serial port.");
            return;
        }

        _metrics.RecordSerialPacket();
        _store.Set("latest_telemetry", new TelemetryData(
            voltage,
            raw,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

        // ส่งสถานะ online แบบ Retain เพื่อให้แอป Flutter ที่เพิ่งเปิดใหม่ทราบว่าบอร์ดออนไลน์อยู่เสมอ
        await PublishDeviceStatusAsync("online");

        var mqttMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/telemetry")
            .WithPayload(line)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();

        try
        {
            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg)
            {
                SenderClientId = "KestrelGateway"
            });
            _metrics.RecordMqttMessage();
            _logger.LogInformation("🚀 Injected MQTT telemetry successfully: {Line}", line);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward telemetry to MQTT.");
        }

        _metrics.RecordSignalRPush();
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
            {
                voltage,
                raw,
                metrics = _metrics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push telemetry to SignalR clients.");
        }
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

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}