using Microsoft.AspNetCore.SignalR;

// 1. คลาส SignalR Hub สำหรับการสื่อสารสองทาง
public class GatewayHub : Hub
{
    private readonly StateSyncCoordinator _coordinator;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(StateSyncCoordinator coordinator, ILogger<GatewayHub> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    // เมื่อหน้าเว็บกดปุ่มสลับสถานะสวิตช์
    public async Task ToggleLed(bool state)
    {
        _logger.LogInformation("🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: {State}", state ? "ON" : "OFF");
        await _coordinator.SetLedStateAsync(state, "SignalR Web Dashboard");
    }
}
