using Microsoft.AspNetCore.SignalR;
public class GatewayHub : Hub {
    public async Task ToggleLed(bool state) => await Clients.All.SendAsync("LedStateUpdated", state);
}
