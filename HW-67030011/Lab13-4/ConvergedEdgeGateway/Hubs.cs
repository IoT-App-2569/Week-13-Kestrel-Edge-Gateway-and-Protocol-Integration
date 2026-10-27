using Microsoft.AspNetCore.SignalR;

// 1. คลาส SignalR Hub สำหรับการสื่อสารสองทาง
public class GatewayHub : Hub
{
    private readonly LedController _led;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(LedController led, ILogger<GatewayHub> logger)
    {
        _led = led;
        _logger = logger;
    }

    // หน้าเว็บที่เพิ่งเปิดต้องได้สถานะ LED ปัจจุบัน ไม่ใช่ค่าเริ่มต้น OFF ใน HTML
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("LedStateUpdated", _led.IsOn);
        await base.OnConnectedAsync();
    }

    // เมื่อหน้าเว็บกดปุ่มสลับสถานะสวิตช์
    public async Task ToggleLed(bool state)
    {
        _logger.LogInformation("🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: {Cmd}", state ? "LED:ON" : "LED:OFF");

        // LedController ส่งลง Serial และกระจายสถานะใหม่ให้ทั้งเว็บ (SignalR) และแอป (MQTT)
        await _led.SetAsync(state, "SignalR (Web)");
    }
}

// 2. คลาสนับสถิติทราฟฟิกของแต่ละโปรโตคอล
// ใบงาน 13.4 มีหลายเธรดเขียนพร้อมกัน (Serial worker, MQTT broker, HTTP request) จึงใช้ Interlocked แทน ++
public class ProtocolMetrics
{
    private long _serial, _mqtt, _rest, _signalR;

    public long SerialPackets => Interlocked.Read(ref _serial);
    public long MqttMessages => Interlocked.Read(ref _mqtt);
    public long RestRequests => Interlocked.Read(ref _rest);
    public long SignalRPushes => Interlocked.Read(ref _signalR);

    public void CountSerial() => Interlocked.Increment(ref _serial);
    public void CountMqtt() => Interlocked.Increment(ref _mqtt);
    public void CountRest() => Interlocked.Increment(ref _rest);
    public void CountSignalR() => Interlocked.Increment(ref _signalR);
}
