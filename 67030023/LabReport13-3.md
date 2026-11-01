### Lab13-3

### กิจกรรมที่ 13-3.1: การสร้างคลาส SignalR Hub และตัวนับสถิติ

สร้างไฟล์ `Hubs.cs` ในโปรเจกต์ `ConvergedEdgeGateway`:

```csharp
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
        
        // ส่งคำสั่งเข้าสู่พอร์ต Serial
        _serialWorker.SendSerialCommand(cmd);

        // กระจายสถานะใหม่หาทุกหน้าที่เปิดอยู่
        await Clients.All.SendAsync("LedStateUpdated", state);
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
```

---

### กิจกรรมที่ 13-3.2: ปรับปรุง Worker ให้ผลักข้อมูลเข้าสู่ Hub

ในไฟล์ `Services.cs` ให้เพิ่ม `using Microsoft.AspNetCore.SignalR;` ที่ด้านบนสุดของไฟล์ และปรับปรุงคลาส `SerialBridgeWorker` ให้รับ `IHubContext<GatewayHub>` ผ่าน Dependency Injection:

```csharp
using Microsoft.AspNetCore.SignalR; // เพิ่มด้านบนสุดของ Services.cs

// ... ภายในคลาส SerialBridgeWorker ...
private readonly IHubContext<GatewayHub> _hubContext;
private readonly ProtocolMetrics _metrics;


public SerialBridgeWorker(
    GatewayStateStore store, 
    MqttServer mqttServer, 
    IHubContext<GatewayHub> hubContext,
    ProtocolMetrics metrics,
    ILogger<SerialBridgeWorker> logger)
{
    _store = store;
    _mqttServer = mqttServer;
    _hubContext = hubContext;
    _metrics = metrics;
    _logger = logger;
}

// ในฟังก์ชัน HandleIncomingSerialLine:
private async Task HandleIncomingSerialLine(string line)
{
    try
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        double v = root.GetProperty("voltage").GetDouble();
        int r = root.GetProperty("raw").GetInt32();

        _metrics.SerialPackets++;

        // 1. ผลักเข้า MQTT
        _metrics.MqttMessages++;
        var mqttMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/telemetry")
            .WithPayload(line)
            .Build();
        await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));

        // 2. ผลักเข้า SignalR หาหน้าเว็บทันที!
        _metrics.SignalRPushes++;
        await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
        {
            voltage = v,
            raw = r,
            metrics = _metrics
        });
    }
    catch { }
}
```

---

### กิจกรรมที่ 13-3.3: การสร้างหน้าเว็บศูนย์บัญชาการ (`wwwroot/index.html`)

สร้างโฟลเดอร์ `wwwroot` และสร้างไฟล์ `index.html` สไตล์ Dark Glassmorphism ที่สมบูรณ์แบบ:

```html
<!DOCTYPE html>
<html lang="th">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Kestrel Converged Edge Command Center</title>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
    <style>
        :root {
            --bg: #0b0f19;
            --card-bg: rgba(22, 30, 49, 0.75);
            --primary: #00E5FF;
            --accent: #00E676;
            --danger: #FF5252;
            --border: rgba(255, 255, 255, 0.08);
        }
        body {
            margin: 0; padding: 24px;
            background: var(--bg);
            color: #f1f5f9;
            font-family: 'Segoe UI', system-ui, sans-serif;
        }
        .header {
            display: flex; justify-content: space-between; align-items: center;
            margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid var(--border);
        }
        .title { font-size: 24px; font-weight: 700; color: var(--primary); }
        .grid {
            display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 20px;
        }
        .card {
            background: var(--card-bg);
            backdrop-filter: blur(16px);
            border: 1px solid var(--border);
            border-radius: 20px; padding: 24px;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
        }
        .gauge-val {
            font-size: 52px; font-weight: 800; font-family: monospace; color: white;
            text-align: center; margin-top: 10px;
        }
        .metric-badge {
            display: flex; justify-content: space-between; padding: 12px;
            background: rgba(0,0,0,0.3); border-radius: 12px; margin-bottom: 8px;
        }
        .btn-toggle {
            width: 100%; padding: 16px; font-size: 16px; font-weight: bold;
            border: none; border-radius: 14px; cursor: pointer; transition: 0.2s;
            background: #334155; color: white; margin-top: 16px;
        }
        .btn-active {
            background: var(--accent); color: black; box-shadow: 0 0 20px rgba(0,230,118,0.4);
        }
    </style>
</head>
<body>
    <div class="header">
        <div>
            <div class="title">⚡ Kestrel Converged Edge Command Center</div>
            <div style="color: #94a3b8; font-size: 14px;">All-in-One Multi-Protocol Bridge (.NET 8)</div>
        </div>
        <div id="connStatus" style="color: var(--accent); font-weight: bold;">● SignalR Online</div>
    </div>

    <div class="grid">
        <!-- การ์ด 1: มาตรวัดเซนเซอร์ -->
        <div class="card" style="text-align: center;">
            <h3 style="margin-top:0; color:#94a3b8;">Real-time Potentiometer Voltage</h3>
            <div class="gauge-val" id="voltageText">0.00 <span style="font-size:24px; color:var(--primary);">V</span></div>
            <div style="color: #94a3b8;" id="rawText">Raw ADC: 0 / 4095</div>
        </div>

        <!-- การ์ด 2: สวิตช์ควบคุมฮาร์ดแวร์ -->
        <div class="card">
            <h3 style="margin-top:0; color:#94a3b8;">Hardware Actuator Control</h3>
            <div style="color: #cbd5e1; font-size: 14px;">ควบคุม LED Status ขา GPIO2 ข้ามทุกโปรโตคอล</div>
            <button id="ledBtn" class="btn-toggle" onclick="toggleLed()">💡 สั่งเปิดไฟ LED (OFF)</button>
        </div>

        <!-- การ์ด 3: ตัวนับทราฟฟิก 4 โปรโตคอล -->
        <div class="card">
            <h3 style="margin-top:0; color:#94a3b8;">Protocol Convergence Metrics</h3>
            <div class="metric-badge"><span>🔌 Serial Packets (UART)</span><b id="mSerial" style="color:var(--primary);">0</b></div>
            <div class="metric-badge"><span>📬 MQTT Messages (Broker)</span><b id="mMqtt" style="color:var(--accent);">0</b></div>
            <div class="metric-badge"><span>🌐 WebSockets (SignalR)</span><b id="mSignalR" style="color:#e040fb;">0</b></div>
        </div>
    </div>

    <script>
        let isLedOn = false;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/gateway")
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveTelemetry", function (data) {
            document.getElementById("voltageText").innerHTML = data.voltage.toFixed(2) + ' <span style="font-size:24px; color:var(--primary);">V</span>';
            document.getElementById("rawText").innerText = "Raw ADC: " + data.raw + " / 4095";
            if (data.metrics) {
                document.getElementById("mSerial").innerText = data.metrics.serialPackets;
                document.getElementById("mMqtt").innerText = data.metrics.mqttMessages;
                document.getElementById("mSignalR").innerText = data.metrics.signalRPushes;
            }
        });

        connection.on("LedStateUpdated", function (state) {
            isLedOn = state;
            updateLedButton();
        });

        function toggleLed() {
            isLedOn = !isLedOn;
            connection.invoke("ToggleLed", isLedOn);
            updateLedButton();
        }

        function updateLedButton() {
            const btn = document.getElementById("ledBtn");
            if (isLedOn) {
                btn.className = "btn-toggle btn-active";
                btn.innerText = "💡 หลอดไฟกำลังสว่าง (ON)";
            } else {
                btn.className = "btn-toggle";
                btn.innerText = "🌑 หลอดไฟปิดอยู่ (OFF)";
            }
        }

        connection.start().catch(err => {
            document.getElementById("connStatus").innerText = "○ Disconnected";
            document.getElementById("connStatus").style.color = "var(--danger)";
        });
    </script>
</body>
</html>
```

---

### กิจกรรมที่ 13-3.4: ผูก Static Files และ SignalR ใน `Program.cs`

```csharp
builder.Services.AddSignalR();
builder.Services.AddSingleton<ProtocolMetrics>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(); // ให้บริการไฟล์ใน wwwroot

app.MapHub<GatewayHub>("/hubs/gateway");
```

รันเซิร์ฟเวอร์ด้วย `dotnet run` และเปิดเบราว์เซอร์ไปที่ `http://localhost:5000`:
* คุณจะพบหน้าจอศูนย์บัญชาการที่ตัวเลขแรงดันไฟฟ้าวิ่งสดๆ ตามการหมุนตัวต้านทานบนบอร์ด!
* ตัวเลขนับทราฟฟิกของทั้ง Serial, MQTT และ SignalR จะวิ่งขึ้นพร้อมๆ กันในเสี้ยววินาที!
* เมื่อคลิกปุ่มเปิดไฟบนหน้าเว็บ หลอดไฟ LED จริงบนบอร์ด ESP32 จะสว่างขึ้นทันที!

---

## 4. คำถามท้ายการทดลอง
1. เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript `setInterval()` เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?
    ลด Overhead ของโปรโตคอล (Low Latency & High Efficiency):
    HTTP Polling ต้องสร้าง TCP Connection และส่ง HTTP Headers (Request/Response) ใหม่ทุกๆ 500 ms ซึ่งก่อให้เกิด Network Overhead และยึดครอง Thread Pool ของ Web Server โดยไม่จำเป็น ในขณะที่ SignalR จะสถาปนาการเชื่อมต่อเพียงครั้งเดียว (Persistent Connection) แล้วส่งเฉพาะข้อมูล Data Payload แบบ Full-Duplex ทำให้การตอบสนองรวดเร็วในระดับ milliseconds (Real-time)

    การสื่อสารแบบ Push-based vs. Pull-based:
    HTTP Polling เป็นแบบ Pull (เบราว์เซอร์ต้องเอ่ยปากถามตลอดเวลา แม้ไม่มีข้อมูลใหม่) ส่วน SignalR เป็นแบบ Push (Server ผลักข้อมูลหา Client ทันทีที่มี Event เกิดขึ้น) ส่งผลให้ประหยัดพลังงาน CPU และ Bandwidth ทั้งฝั่ง Client และ Server

    State Synchronization ข้ามทุกหน้าจอ:
    การสั่งงานผ่าน SignalR Hub (Clients.All.SendAsync) ช่วยให้ผู้ใช้หลายคนที่เปิดหน้า Dashboard พร้อมกัน เห็นสถานะของหลอดไฟ (LED State) และค่าเซนเซอร์อัปเดตตรงกันทันทีแบบ Synchronized โดยไม่ต้องกด Refresh หน้าเว็บ

2. การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?
    การตรวจจับจุดคอขวดและข้อผิดพลาด (Pipeline Bottleneck & Packet Loss Detection):
    ช่วยให้วิศวกรเปรียบเทียบอัตราส่วนข้อมูลได้อย่างแม่นยำ หาก SerialPackets วิ่งขึ้นเรื่อยๆ แต่ MqttMessages หรือ SignalRPushes หยุดเดิน แสดงว่ามี Thread Lock, Memory Leak หรือ Exception เกิดขึ้นภายใน Pipeline การแปลงข้อมูลของ Gateway

    การเฝ้าระวังสุขภาพของฮาร์ดแวร์ฝั่ง Serial (Hardware Health Monitoring):
    หากค่า SerialPackets หยุดนิ่ง อาจหมายถึงสายสัญญาณหลุด (Disconnected), สัญญาณรบกวนในสาย (EMI Error) หรือเฟิร์มแวร์ฝั่ง ESP32/PLC เกิดอาการ Hang ทำให้ผู้ดูแลระบบสามารถเข้าแก้ไขฮาร์ดแวร์ได้อย่างทันท่วงที

    การประเมินภาระของระบบ (Capacity Planning & Rate Limiting):
    Metrics ช่วยให้เห็นปริมาณ Workload จริงที่ Gateway กำลังประมวลผลต่อวินาที (Throughput) เพื่อนำไปวิเคราะห์ในการขยายระบบ (Scaling) หรือปรับแต่ง Throttling/Buffering ไม่ให้ Message Queue ล้นเมื่อมีอุปกรณ์เซนเซอร์เชื่อมต่อเพิ่มขึ้นในโรงงาน

