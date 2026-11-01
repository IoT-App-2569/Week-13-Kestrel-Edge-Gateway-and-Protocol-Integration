using Microsoft.AspNetCore.SignalR;

public sealed class GatewayHub(
    SerialBridgeWorker serialWorker,
    ILogger<GatewayHub> logger) : Hub
{
    public async Task ToggleLed(bool state)
    {
        var command = state ? "LED:ON" : "LED:OFF";
        if (!serialWorker.SendSerialCommand(command))
        {
            throw new HubException("ส่งคำสั่งไม่ได้ โปรดตรวจสอบการเชื่อมต่อ Serial กับ ESP32");
        }

        logger.LogInformation("สั่งงานจาก Web Dashboard ผ่าน SignalR: {Command}", command);
        await Clients.All.SendAsync("LedStateUpdated", state);
    }
}
