
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Server;
using Microsoft.AspNetCore.SignalR;

// 1. คลาสจัดเก็บสถานะระบบในหน่วยความจำ
public class GatewayStateStore
{
    private readonly ConcurrentDictionary<string, object> _state = new();

    public void Set(string key, object val)
    {
        _state[key] = val;
    }

    public T? Get<T>(string key)
    {
        return _state.TryGetValue(key, out var val) && val is T t
            ? t
            : default;
    }

    public object? Get(string key)
    {
        return _state.TryGetValue(key, out var val) ? val : null;
    }
}

// 2. คลาสเก็บ Instance ของ Embedded MQTT Server
public class MqttServerAccessor
{
    public MqttServer? Server { get; private set; }

    public void SetServer(MqttServer server)
    {
        Server = server;
    }
}

// 3. โมเดลข้อมูล Telemetry
public record TelemetryData(
    double Voltage,
    int Raw,
    long Timestamp
);

// 4. BackgroundService สำหรับอ่านและส่งข้อมูล Serial
public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;

    private readonly string _portName = "/dev/cu.usbserial-0001";
    private readonly int _baudRate = 115200;

    private readonly GatewayStateStore _store;
    private readonly MqttServerAccessor _mqttServerAccessor;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<SerialBridgeWorker> _logger;

    private readonly object _serialLock = new();

    public SerialBridgeWorker(
        GatewayStateStore store,
        MqttServerAccessor mqttServerAccessor,
        IHubContext<GatewayHub> hubContext,
        ProtocolMetrics metrics,
        ILogger<SerialBridgeWorker> logger)
    {
        _store = store;
        _mqttServerAccessor = mqttServerAccessor;
        _hubContext = hubContext;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // คืนการควบคุมให้ Host เริ่ม Hosted Services อื่นก่อน
        await Task.Yield();

        _logger.LogInformation(
            "กำลังเชื่อมต่อ Serial Port {Port}...",
            _portName);

        while (!stoppingToken.IsCancellationRequested)
        {
            // เปิด Serial Port หากยังไม่ได้เชื่อมต่อ
            if (_serial == null || !_serial.IsOpen)
            {
                try
                {
                    _serial?.Dispose();

                    _serial = new SerialPort(_portName, _baudRate)
                    {
                        NewLine = "\n",
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };

                    _serial.Open();

                    _logger.LogInformation(
                        "เชื่อมต่อ ESP32 ผ่าน Serial Port {Port} สำเร็จ!",
                        _portName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "ไม่สามารถเปิด Serial Port ได้: {Message}",
                        ex.Message);

                    await Task.Delay(3000, stoppingToken);
                    continue;
                }
            }

            try
            {
                string line;

                lock (_serialLock)
                {
                    line = _serial.ReadLine().Trim();
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    await HandleIncomingSerialLine(line);
                }
            }
            catch (TimeoutException)
            {
                // ปกติเมื่อไม่มีข้อมูลเข้ามาภายใน 1 วินาที
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Serial Read Warning: {Message}",
                    ex.Message);

                lock (_serialLock)
                {
                    _serial?.Close();
                }
            }
        }
    }

    // 5. ประมวลผล JSON ที่ได้รับจาก ESP32
    private async Task HandleIncomingSerialLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);

            var root = doc.RootElement;

            double voltage = root.GetProperty("voltage").GetDouble();
            int raw = root.GetProperty("raw").GetInt32();

            var telemetry = new TelemetryData(
                voltage,
                raw,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            );

            // เก็บข้อมูลล่าสุดในหน่วยความจำ
            _store.Set("latest_telemetry", telemetry);

            // นับจำนวนข้อมูล Serial ที่อ่านและประมวลผลสำเร็จ
            _metrics.IncrementSerialPackets();

            // 6. ส่งข้อมูลเข้า MQTT Broker
            var mqttServer = _mqttServerAccessor.Server;

            if (mqttServer != null)
            {
                try
                {
                    var mqttMsg = new MqttApplicationMessageBuilder()
                        .WithTopic("iot/esp32/telemetry")
                        .WithPayload(line)
                        .WithQualityOfServiceLevel(
                            MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                        .Build();

                    await mqttServer.InjectApplicationMessage(
                        new InjectedMqttApplicationMessage(mqttMsg));

                    _metrics.IncrementMqttMessages();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "ส่งข้อมูล MQTT ไม่สำเร็จ: {Message}",
                        ex.Message);
                }
            }
            else
            {
                _logger.LogWarning(
                    "MQTT Server ยังไม่พร้อมใช้งาน");
            }

            // 7. ส่งข้อมูลไปยังหน้าเว็บผ่าน SignalR
            try
            {
                await _hubContext.Clients.All.SendAsync(
                    "ReceiveTelemetry",
                    new
                    {
                        voltage = voltage,
                        raw = raw,
                        metrics = new
                        {
                            serialPackets = _metrics.SerialPackets,
                            mqttMessages = _metrics.MqttMessages,
                            restRequests = _metrics.RestRequests,
                            signalRPushes = _metrics.SignalRPushes
                        }
                    });

                _metrics.IncrementSignalRPushes();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "ส่งข้อมูล SignalR ไม่สำเร็จ: {Message}",
                    ex.Message);
            }

            _logger.LogInformation(
                "ได้รับข้อมูล ESP32: Voltage={Voltage} V, Raw={Raw}",
                voltage,
                raw);
        }
        catch (JsonException)
        {
            _logger.LogWarning(
                "ข้ามข้อมูลที่ไม่ใช่ JSON: {Line}",
                line);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "ไม่สามารถประมวลผลข้อมูล Serial ได้: {Message}",
                ex.Message);
        }
    }

    // 8. ส่งคำสั่งควบคุม LED กลับไปยัง ESP32
    public bool SendSerialCommand(string cmd)
    {
        try
        {
            lock (_serialLock)
            {
                if (_serial?.IsOpen == true)
                {
                    _serial.WriteLine(cmd);

                    _logger.LogInformation(
                        "ส่งคำสั่งไปยัง ESP32: {Command}",
                        cmd);

                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "ส่งคำสั่ง Serial ไม่สำเร็จ: {Message}",
                ex.Message);
        }

        return false;
    }

    // 9. ควบคุม LED และกระจายสถานะไปยังทุกช่องทาง
    public async Task<bool> SetLedStateAsync(bool state)
    {
        string command = state ? "LED:ON" : "LED:OFF";

        // ส่งคำสั่งไปยัง ESP32 ผ่าน Serial
        bool sent = SendSerialCommand(command);

        if (!sent)
        {
            _logger.LogWarning(
                "ไม่สามารถส่งคำสั่ง LED ไปยัง ESP32 ได้");

            return false;
        }

        // เก็บสถานะ LED ล่าสุดใน Gateway
        _store.Set("led_state", state);

        // ส่งสถานะ LED กลับไปยัง MQTT
        var mqttServer = _mqttServerAccessor.Server;

        if (mqttServer != null)
        {
            try
            {
                string payload = JsonSerializer.Serialize(
                    new { led_state = state });

                var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic("iot/esp32/led_ack")
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(
                        MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await mqttServer.InjectApplicationMessage(
                    new InjectedMqttApplicationMessage(mqttMessage));

                _logger.LogInformation(
                    "ส่งสถานะ LED ผ่าน MQTT: {Payload}",
                    payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "ส่งสถานะ LED ผ่าน MQTT ไม่สำเร็จ: {Message}",
                    ex.Message);
            }
        }

        // แจ้งสถานะ LED ไปยัง Web Dashboard ทุกเครื่อง
        try
        {
            await _hubContext.Clients.All.SendAsync(
                "LedStateUpdated",
                state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "ส่งสถานะ LED ผ่าน SignalR ไม่สำเร็จ: {Message}",
                ex.Message);
        }

        _logger.LogInformation(
            "Gateway อัปเดตสถานะ LED: {State}",
            state ? "ON" : "OFF");

        return true;
    }

    // 10. ปิด Serial Port เมื่อหยุดโปรแกรม
    public override void Dispose()
    {
        lock (_serialLock)
        {
            if (_serial?.IsOpen == true)
            {
                _serial.Close();
            }

            _serial?.Dispose();
            _serial = null;
        }

        base.Dispose();
    }
}
