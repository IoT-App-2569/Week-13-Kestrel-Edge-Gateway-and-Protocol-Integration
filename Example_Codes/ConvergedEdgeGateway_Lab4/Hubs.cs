using Microsoft.AspNetCore.SignalR;

public sealed class LedController(
    SerialBridgeWorker serialWorker,
    IHubContext<GatewayHub> hubContext,
    ILogger<LedController> logger)
{
    public async Task<bool> SetStateAsync(bool isOn, CancellationToken cancellationToken = default)
    {
        var command = isOn ? "LED:ON" : "LED:OFF";
        if (!serialWorker.SendSerialCommand(command))
        {
            return false;
        }

        logger.LogInformation("สั่งงาน LED: {Command}", command);
        await hubContext.Clients.All.SendAsync("LedStateUpdated", isOn, cancellationToken);
        return true;
    }
}

public sealed class GatewayHub(
    LedController ledController,
    ILogger<GatewayHub> logger) : Hub
{
    public async Task ToggleLed(bool state)
    {
        if (!await ledController.SetStateAsync(state, Context.ConnectionAborted))
        {
            throw new HubException("ส่งคำสั่งไม่ได้ โปรดตรวจสอบการเชื่อมต่อ Serial กับ ESP32");
        }

        logger.LogInformation("สั่งงานจาก Web Dashboard ผ่าน SignalR: {State}", state ? "ON" : "OFF");
    }
}
