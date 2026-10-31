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

    // เมื่อหน้าเว็บเปิดขึ้นมา ส่งสถานะ LED ปัจจุบันให้หน้านั้นทันที
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("LedStateUpdated", _serialWorker.LedState);
        await base.OnConnectedAsync();
    }

    // เมื่อหน้าเว็บกดปุ่มสลับสถานะสวิตช์
    // คืนค่า true เมื่อส่งคำสั่งเข้า Serial สำเร็จ (false = บอร์ดไม่ได้เชื่อมต่อ)
    public async Task<bool> ToggleLed(bool state)
    {
        _logger.LogInformation("🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: {State}", state);

        // ส่งเข้า Serial, publish led_ack ให้ Flutter และกระจายสถานะหาทุกหน้าเว็บ
        return await _serialWorker.SetLedAsync(state, "SignalR");
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
