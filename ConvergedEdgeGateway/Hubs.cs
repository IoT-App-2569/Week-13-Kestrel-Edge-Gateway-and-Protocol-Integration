using Microsoft.AspNetCore.SignalR;

// 1. SignalR Hub สำหรับหน้าเว็บ
public class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _serialWorker;
    private readonly GatewayStateStore _store;

    public GatewayHub(SerialBridgeWorker serialWorker, GatewayStateStore store)
    {
        _serialWorker = serialWorker;
        _store = store;
    }

    // หน้าเว็บกดปุ่ม → ส่งเข้าศูนย์กลาง (Serial + แจ้งแอปและเว็บทุกหน้า)
    public async Task ToggleLed(bool state)
    {
        await _serialWorker.SetLedAsync(state, "Web (SignalR)");
    }

    // เปิดหน้าเว็บใหม่ ให้รู้สถานะไฟปัจจุบันทันที
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("LedStateUpdated", _store.Get<bool>("led_state"));
        await base.OnConnectedAsync();
    }
}

// 2. ตัวนับสถิติทราฟฟิกของแต่ละโปรโตคอล
public class ProtocolMetrics
{
    public long SerialPackets { get; set; } = 0;
    public long MqttMessages { get; set; } = 0;
    public long RestRequests { get; set; } = 0;
    public long SignalRPushes { get; set; } = 0;
}