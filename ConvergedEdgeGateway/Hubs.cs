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
        await _serialWorker.SetLedStateAsync(state, "SignalR Web");
    }
}

// 2. คลาสนับสถิติทราฟฟิกของแต่ละโปรโตคอล
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

    public void RecordSerialPacket() => Interlocked.Increment(ref _serialPackets);
    public void RecordMqttMessage() => Interlocked.Increment(ref _mqttMessages);
    public void RecordRestRequest() => Interlocked.Increment(ref _restRequests);
    public void RecordSignalRPush() => Interlocked.Increment(ref _signalRPushes);
}
