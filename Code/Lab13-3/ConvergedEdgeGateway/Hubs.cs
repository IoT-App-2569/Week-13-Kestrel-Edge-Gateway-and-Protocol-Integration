using Microsoft.AspNetCore.SignalR;

// 1. คลาส SignalR Hub สำหรับการสื่อสารสองทาง
public class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _serialWorker;
    private readonly ProtocolMetrics _metrics;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(SerialBridgeWorker serialWorker, ProtocolMetrics metrics, ILogger<GatewayHub> logger)
    {
        _serialWorker = serialWorker;
        _metrics = metrics;
        _logger = logger;
    }

    // เมื่อหน้าเว็บกดปุ่มสลับสถานะสวิตช์
    public async Task ToggleLed(bool state)
    {
        string cmd = state ? "LED:ON" : "LED:OFF";
        _logger.LogInformation("🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: {Cmd}", cmd);

        // ส่งคำสั่งเข้าสู่พอร์ต Serial แล้วกระจายสถานะใหม่หาทุกหน้าที่เปิดอยู่
        if (_serialWorker.SendSerialCommand(cmd))
        {
            _metrics.CountSignalRPush();
            await Clients.All.SendAsync("LedStateUpdated", state);
        }
        else
        {
            // ส่งไม่สำเร็จ (บอร์ดไม่ได้เสียบ): บอกเฉพาะคนที่กด ให้ปุ่มกลับสถานะเดิม
            await Clients.Caller.SendAsync("LedCommandFailed", state);
        }
    }
}

// 2. คลาสนับสถิติทราฟฟิกของแต่ละโปรโตคอล (thread-safe ด้วย Interlocked)
public class ProtocolMetrics
{
    private long _serialPackets, _mqttMessages, _restRequests, _signalRPushes;

    public long SerialPackets => Interlocked.Read(ref _serialPackets);
    public long MqttMessages => Interlocked.Read(ref _mqttMessages);
    public long RestRequests => Interlocked.Read(ref _restRequests);
    public long SignalRPushes => Interlocked.Read(ref _signalRPushes);

    public void CountSerialPacket() => Interlocked.Increment(ref _serialPackets);
    public void CountMqttMessage() => Interlocked.Increment(ref _mqttMessages);
    public void CountRestRequest() => Interlocked.Increment(ref _restRequests);
    public void CountSignalRPush() => Interlocked.Increment(ref _signalRPushes);
}
