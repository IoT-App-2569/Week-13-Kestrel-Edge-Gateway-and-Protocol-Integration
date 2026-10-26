using Microsoft.AspNetCore.SignalR;
using MQTTnet.Server;

public sealed class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _worker;
    private readonly GatewayStateStore _state;
    private readonly ProtocolMetrics _metrics;
    private readonly MqttServer _mqttServer;

    public GatewayHub(
        SerialBridgeWorker worker,
        GatewayStateStore state,
        ProtocolMetrics metrics,
        MqttServer mqttServer)
    {
        _worker = worker;
        _state = state;
        _metrics = metrics;
        _mqttServer = mqttServer;
    }

    public async Task ToggleLed(bool state)
    {
        _worker.SendSerialCommand(state ? "LED:ON" : "LED:OFF");
        _state.Set("led_state", state);
        _metrics.IncrementSignalRCommands();
        await Clients.All.SendAsync("LedStateUpdated", state);
        await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(
            new MQTTnet.MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/led/state")
                .WithPayload(state ? "ON" : "OFF")
                .Build()));
    }
}

public sealed class ProtocolMetrics
{
    private long _serialPackets;
    private long _mqttMessages;
    private long _restRequests;
    private long _signalRPushes;
    private long _mqttCommands;
    private long _signalRCommands;

    public void IncrementSerialPackets() => Interlocked.Increment(ref _serialPackets);
    public void IncrementMqttMessages() => Interlocked.Increment(ref _mqttMessages);
    public void IncrementRestRequests() => Interlocked.Increment(ref _restRequests);
    public void IncrementSignalRPushes() => Interlocked.Increment(ref _signalRPushes);
    public void IncrementMqttCommands() => Interlocked.Increment(ref _mqttCommands);
    public void IncrementSignalRCommands() => Interlocked.Increment(ref _signalRCommands);

    public object Snapshot() => new
    {
        serialPackets = Interlocked.Read(ref _serialPackets),
        mqttMessages = Interlocked.Read(ref _mqttMessages),
        restRequests = Interlocked.Read(ref _restRequests),
        signalRPushes = Interlocked.Read(ref _signalRPushes),
        mqttCommands = Interlocked.Read(ref _mqttCommands),
        signalRCommands = Interlocked.Read(ref _signalRCommands)
    };
}
