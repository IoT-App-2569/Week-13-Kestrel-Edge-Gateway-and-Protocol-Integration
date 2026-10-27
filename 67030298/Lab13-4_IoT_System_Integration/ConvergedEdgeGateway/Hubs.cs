using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Server;

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

    public async Task ToggleLed(bool state)
    {
        string cmd = state ? "LED:ON" : "LED:OFF";
        _logger.LogInformation("🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: {Cmd}", cmd);

        _serialWorker.SendSerialCommand(cmd);

        await Clients.All.SendAsync("LedStateUpdated", state);

        var ackMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/led_ack")
            .WithPayload($"{{\"led_state\":{state.ToString().ToLower()}}}")
            .Build();
        await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));
    }
}

public class ProtocolMetrics
{
    public long SerialPackets { get; set; } = 0;
    public long MqttMessages { get; set; } = 0;
    public long RestRequests { get; set; } = 0;
    public long SignalRPushes { get; set; } = 0;
}
