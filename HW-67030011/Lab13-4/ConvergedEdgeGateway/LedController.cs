using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Server;

// จุดเดียวที่เปลี่ยนสถานะ LED ไม่ว่าคำสั่งจะมาจาก SignalR (เว็บ), MQTT (Flutter) หรือ REST (ระบบภายนอก)
// ส่งคำสั่งลง Serial แล้วกระจายสถานะใหม่กลับไปทุกโปรโตคอล ทุก Client จึงเห็นสถานะเดียวกันเสมอ
public class LedController
{
    public const string AckTopic = "iot/esp32/led_ack";

    private readonly SerialBridgeWorker _worker;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<LedController> _logger;

    public LedController(
        SerialBridgeWorker worker,
        GatewayStateStore store,
        MqttServer mqttServer,
        IHubContext<GatewayHub> hubContext,
        ProtocolMetrics metrics,
        ILogger<LedController> logger)
    {
        _worker = worker;
        _store = store;
        _mqttServer = mqttServer;
        _hubContext = hubContext;
        _metrics = metrics;
        _logger = logger;
    }

    public bool IsOn => _store.Get<bool>("led_state");

    // คืนค่า false เมื่อเขียนลง Serial ไม่ได้ (บอร์ดไม่ได้ต่อ) ในกรณีนั้นจะไม่เปลี่ยนสถานะและไม่ส่ง ACK
    public async Task<bool> SetAsync(bool on, string source)
    {
        string cmd = on ? "LED:ON" : "LED:OFF";
        bool sent = _worker.SendSerialCommand(cmd);
        _logger.LogInformation("💡 {Source} → {Cmd} (sentToSerial={Sent})", source, cmd, sent);
        if (!sent) return false;

        _store.Set("led_state", on);

        // 1. หน้าเว็บทุกหน้า (SignalR)
        _metrics.CountSignalR();
        await _hubContext.Clients.All.SendAsync("LedStateUpdated", on);

        // 2. แอป Flutter (MQTT) แบบ Retained เพื่อให้ Client ที่ต่อเข้ามาทีหลังได้สถานะปัจจุบันทันที
        _metrics.CountMqtt();
        var ack = new MqttApplicationMessageBuilder()
            .WithTopic(AckTopic)
            .WithPayload(JsonSerializer.Serialize(new { led_state = on, source }))
            .WithRetainFlag()
            .Build();
        await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(ack));

        return true;
    }
}
