using Microsoft.AspNetCore.SignalR;

// 1. คลาส SignalR Hub สำหรับการสื่อสารสองทาง
public class GatewayHub : Hub
{
    private readonly LedController _led;
    private readonly SerialBridgeWorker _serial;

    public GatewayHub(LedController led, SerialBridgeWorker serial)
    {
        _led = led;
        _serial = serial;
    }

    // หน้าเว็บที่เพิ่งเปิดจะได้สถานะปัจจุบันทันที ไม่ต้องรอ telemetry รอบถัดไป
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("LedStateUpdated", _led.IsOn);
        await Clients.Caller.SendAsync("DeviceStatus", _serial.IsConnected);
        await base.OnConnectedAsync();
    }

    // เมื่อหน้าเว็บกดปุ่มสลับสถานะสวิตช์ (LedController กระจายสถานะให้ทุกหน้าจอเอง)
    public async Task ToggleLed(bool state)
    {
        if (!await _led.SetAsync(state, "SignalR"))
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
