using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Server;

namespace ConvergedEdgeGateway;

// 1. คลาส SignalR Hub สำหรับการสื่อสารสองทาง
public class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _serialWorker;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(
        SerialBridgeWorker serialWorker,
        GatewayStateStore store,
        MqttServer mqttServer,
        ProtocolMetrics metrics,
        ILogger<GatewayHub> logger)
    {
        _serialWorker = serialWorker;
        _store = store;
        _mqttServer = mqttServer;
        _metrics = metrics;
        _logger = logger;
    }

    // เมื่อหน้าเว็บกดปุ่มสลับสถานะสวิตช์
    public async Task ToggleLed(bool state)
    {
        string cmd = state ? "LED:ON" : "LED:OFF";
        _logger.LogInformation("🌐 [SignalR Hub] สั่งการจาก Web Dashboard: {Cmd}", cmd);

        // 1. ส่งคำสั่งเข้าสู่พอร์ต Serial หาบอร์ด ESP32
        _serialWorker.SendSerialCommand(cmd);

        // 2. อัปเดตสถานะในหน่วยความจำ
        _store.Set("led_state", state);

        // 3. กระจายสถานะใหม่หาทุกหน้าจอ Web Browser ที่เปิดอยู่
        _metrics.SignalRPushes++;
        await Clients.All.SendAsync("LedStateUpdated", state);

        // 4. กระจายสถานะออกไปยัง MQTT Broker เพื่อให้อุปกรณ์ Mobile App (Flutter) ซิงค์สถานะด้วย
        _metrics.MqttMessages++;
        var mqttMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/led/status")
            .WithPayload(state ? "ON" : "OFF")
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();

        await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));
    }

    // ส่งสถานะปัจจุบันให้ Client ที่เพิ่งเชื่อมต่อเข้ามา
    public override async Task OnConnectedAsync()
    {
        bool currentLed = _store.Get<bool>("led_state");
        await Clients.Caller.SendAsync("LedStateUpdated", currentLed);
        await base.OnConnectedAsync();
    }
}
