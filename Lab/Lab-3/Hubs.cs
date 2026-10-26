using Microsoft.AspNetCore.SignalR;

public sealed class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _serialWorker;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(
        SerialBridgeWorker serialWorker,
        ProtocolMetrics metrics,
        ILogger<GatewayHub> logger)
    {
        _serialWorker = serialWorker;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task ToggleLed(bool state)
    {
        var command = state ? "LED:ON" : "LED:OFF";
        var sent = _serialWorker.SendSerialCommand(command);
        _logger.LogInformation(
            "สั่งการจาก Web Dashboard ผ่าน SignalR: {Command}; ส่ง Serial สำเร็จ: {Sent}",
            command,
            sent);

        await Clients.All.SendAsync("LedStateUpdated", state);
    }
}

public sealed class ProtocolMetrics
{
    private long _serialPackets;
    private long _mqttMessages;
    private long _restRequests;
    private long _signalRPushes;

    public void IncrementSerialPackets() => Interlocked.Increment(ref _serialPackets);
    public void IncrementMqttMessages() => Interlocked.Increment(ref _mqttMessages);
    public void IncrementRestRequests() => Interlocked.Increment(ref _restRequests);
    public void IncrementSignalRPushes() => Interlocked.Increment(ref _signalRPushes);

    public ProtocolMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _serialPackets),
        Interlocked.Read(ref _mqttMessages),
        Interlocked.Read(ref _restRequests),
        Interlocked.Read(ref _signalRPushes));
}

public sealed record ProtocolMetricsSnapshot(
    long SerialPackets,
    long MqttMessages,
    long RestRequests,
    long SignalRPushes);
