using Microsoft.AspNetCore.SignalR;

// 1. คลาส SignalR Hub สำหรับการสื่อสารสองทาง
public class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _serialWorker;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(SerialBridgeWorker serialWorker, ILogger<GatewayHub> logger)
    {
        _serialWorker = serialWorker;
        _logger = logger;
    }

    // หน้าเว็บที่เพิ่งเปิดได้รับสถานะ LED ปัจจุบันทันที
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("LedStateUpdated", _serialWorker.LedState);
        await base.OnConnectedAsync();
    }

    // เมื่อหน้าเว็บกดปุ่มสลับสถานะสวิตช์
    public async Task ToggleLed(bool state)
    {
        _logger.LogInformation("🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: {Cmd}", state ? "LED:ON" : "LED:OFF");

        // ส่งคำสั่งเข้าสู่พอร์ต Serial แล้วกระจายสถานะจริงให้ทั้งเว็บและแอปมือถือ
        await _serialWorker.SetLedAsync(state, "SignalR");
    }
}

// 2. คลาสนับสถิติทราฟฟิกของแต่ละโปรโตคอล
public class ProtocolMetrics
{
    public long SerialPackets { get; set; } = 0;
    public long MqttMessages { get; set; } = 0;
    public long RestRequests { get; set; } = 0;
    public long SignalRPushes { get; set; } = 0;
}
