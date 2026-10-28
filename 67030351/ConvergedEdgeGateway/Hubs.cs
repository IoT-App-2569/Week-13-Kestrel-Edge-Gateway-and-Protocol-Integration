using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Server;

// 1. คลาส SignalR Hub สำหรับการสื่อสารสองทาง
public class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _serialWorker;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(SerialBridgeWorker serialWorker, MqttServer mqttServer, ILogger<GatewayHub> logger)
    {
        _serialWorker = serialWorker;
        _mqttServer = mqttServer;
        _logger = logger;
    }

    // เมื่อหน้าเว็บกดปุ่มสลับสถานะสวิตช์
    public async Task ToggleLed(bool state)
    {
        string cmd = state ? "LED:ON" : "LED:OFF";
        _logger.LogInformation("🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: {Cmd}", cmd);
        
        // 1. ส่งคำสั่งเข้าสู่พอร์ต Serial
        _serialWorker.SendSerialCommand(cmd);

        // 2. กระจายสถานะใหม่หาทุกหน้าเว็บที่เปิดอยู่
        await Clients.All.SendAsync("LedStateUpdated", state);

        // 3. ซิงค์สถานะไปยังแอป Flutter ผ่าน MQTT
        var ackPayload = System.Text.Json.JsonSerializer.Serialize(new { led_state = state });
        var ackMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/led_ack")
            .WithPayload(ackPayload)
            .Build();
        await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));
    }
}

// 2. คลาสนับสถิติทราฟฟิกของแต่ละโปรโตคอล
public class ProtocolMetrics
{
    public long SerialPackets { get; set; } = 0;
    public long MqttMessages { get; set; } = 0;
    public long RestRequests { get; set; } = 0;
    public long SignalRPushes { get; set; } = 0;
}
