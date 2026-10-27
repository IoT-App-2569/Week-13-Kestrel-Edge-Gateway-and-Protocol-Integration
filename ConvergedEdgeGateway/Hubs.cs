using Microsoft.AspNetCore.SignalR;

public class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _serialWorker;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(SerialBridgeWorker serialWorker, ILogger<GatewayHub> logger)
    {
        _serialWorker = serialWorker;
        _logger = logger;
    }

    public async Task ToggleLed(bool state)
    {
        _logger.LogInformation("🌐 สั่งการจาก Web Dashboard: {State}", state ? "ON" : "OFF");
        await _serialWorker.SetLedAsync(state);
    }
}

public class ProtocolMetrics
{
    public long SerialPackets { get; set; } = 0;
    public long MqttMessages { get; set; } = 0;
    public long RestRequests { get; set; } = 0;
    public long SignalRPushes { get; set; } = 0;
}