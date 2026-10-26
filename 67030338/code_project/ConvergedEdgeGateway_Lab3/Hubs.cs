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

    // เมื่อหน้าเว็บกดปุ่มสลับสถานะสวิตช์
    public async Task ToggleLed(bool state)
    {
        string cmd = state ? "LED:ON" : "LED:OFF";
        _logger.LogInformation("🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: {Cmd}", cmd);

        // ส่งคำสั่งเข้าสู่พอร์ต Serial พร้อมซิงโครไนซ์ไปยัง MQTT (Flutter) และ SignalR (Web)
        await _serialWorker.SetLedStateAsync(state, "SignalR Web");
    }

    // สำหรับผู้ไม่มี Potentiometer: ปรับค่าจำลองจากสไลเดอร์บนหน้าเว็บได้ทันที
    public void SetVirtualPot(int? raw)
    {
        _serialWorker.SetSimulatedAdc(raw);
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
