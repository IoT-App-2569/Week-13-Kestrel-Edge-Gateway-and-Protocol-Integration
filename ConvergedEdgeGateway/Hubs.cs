
using Microsoft.AspNetCore.SignalR;
using System.Threading;

// 1. SignalR Hub สำหรับรับส่งข้อมูลระหว่างเว็บกับ ESP32
public class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _serialWorker;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(
        SerialBridgeWorker serialWorker,
        ILogger<GatewayHub> logger)
    {
        _serialWorker = serialWorker;
        _logger = logger;
    }

    // แจ้งเมื่อหน้าเว็บเชื่อมต่อเข้ามา
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "SignalR Client connected: {ConnectionId}",
            Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    // รับคำสั่งเปิดหรือปิด LED จากหน้าเว็บ
    public async Task ToggleLed(bool state)
    {
        _logger.LogInformation(
            "Web Dashboard ส่งคำสั่ง LED: {State}",
            state ? "ON" : "OFF");

        // ใช้ระบบควบคุม LED ส่วนกลางใน SerialBridgeWorker
        bool success = await _serialWorker.SetLedStateAsync(state);

        if (!success)
        {
            await Clients.Caller.SendAsync(
                "LedCommandFailed",
                "ไม่สามารถส่งคำสั่งไปยัง ESP32 ได้");

            return;
        }

        // SetLedStateAsync จะกระจายสถานะผ่าน SignalR และ MQTT แล้ว
    }
}

// 2. ตัวนับจำนวนข้อมูลของแต่ละโปรโตคอล
public class ProtocolMetrics
{
    private long _serialPackets;
    private long _mqttMessages;
    private long _restRequests;
    private long _signalRPushes;

    public long SerialPackets => Interlocked.Read(ref _serialPackets);
    public long MqttMessages => Interlocked.Read(ref _mqttMessages);
    public long RestRequests => Interlocked.Read(ref _restRequests);
    public long SignalRPushes => Interlocked.Read(ref _signalRPushes);

    public void IncrementSerialPackets() =>
        Interlocked.Increment(ref _serialPackets);

    public void IncrementMqttMessages() =>
        Interlocked.Increment(ref _mqttMessages);

    public void IncrementRestRequests() =>
        Interlocked.Increment(ref _restRequests);

    public void IncrementSignalRPushes() =>
        Interlocked.Increment(ref _signalRPushes);
}
