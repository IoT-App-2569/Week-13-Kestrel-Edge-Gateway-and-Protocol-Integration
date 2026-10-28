<img width="1917" height="743" alt="image" src="https://github.com/user-attachments/assets/c31a182c-03ad-4f80-a83b-9c8647e815d9" /># ใบงานการทดลองที่ 13.3
### การสร้าง SignalR Realtime Hub และเว็บแดชบอร์ดศูนย์บัญชาการ (Converged Web Dashboard) สไตล์ Glassmorphism

> [!NOTE] **คำชี้แจง**
> ในใบงานนี้นักศึกษาจะได้ผสานเทคโนโลยี **ASP.NET Core SignalR (WebSockets)** เข้าสู่ Kestrel Edge Gateway เพื่อส่งผ่านข้อมูลเซนเซอร์ขึ้นสู่หน้าเว็บเบราว์เซอร์แบบสดๆ ในเสี้ยววินาที พร้อมทั้งสร้างหน้าเว็บ **ศูนย์บัญชาการระบบขอบ (Converged Edge Command Center)** สไตล์ Glassmorphism ที่สวยงามทันสมัย โฮสต์อยู่บน Kestrel โดยตรง เพื่อแสดงผลมาตรวัดเข็ม, สถิติทราฟฟิกของทั้ง 4 โปรโตคอล, และแผงควบคุมสวิตช์สั่งการแบบเรียลไทม์

---

## 1. วัตถุประสงค์การทดลอง
1. สามารถติดตั้งและกำหนดค่า SignalR Hub ใน ASP.NET Core (.NET 8) ได้
2. สามารถเขียนโค้ดผลักดันข้อมูลจากงานเบื้องหลัง (`SerialBridgeWorker`) ไปยังหน้าเว็บผ่าน `IHubContext` ได้
3. สามารถพัฒนาเว็บแดชบอร์ด HTML5/JavaScript ที่เชื่อมต่อกับ SignalR เพื่อแสดงผลมาตรวัดเข็มสด และสลับสวิตช์ไฟได้
4. สามารถติดตามและนับสถิติการรับส่งข้อมูลของทั้ง 4 โปรโตคอล (Protocol Traffic Counters) ได้

---

## 2. ผังการทำงานของ SignalR Realtime Push

<p align="center"><img src="Images/Fig_08-Labsheet-13-3_academic.svg" alt="08-Labsheet-13-3-Realtime-SignalR-Hub-and-Converged-Web-Dashboard" style=" height: auto;" ></p>
<p align="center"><b></b>ผังการทำงานของ SignalR Realtime Push</p>

---

## 3. ขั้นตอนการทดลอง (Step-by-Step Activities)

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

## ผลการทดลอง

### ไฟล์ที่สร้างและแก้ไข

| ไฟล์ | การเปลี่ยนแปลง |
|:---|:---|
| `Hubs.cs` | สร้างใหม่ ตามใบงาน (`GatewayHub` และ `ProtocolMetrics`) |
| `Services.cs` | เพิ่ม `IHubContext<GatewayHub>` และ `ProtocolMetrics` ใน `SerialBridgeWorker` ส่ง `ReceiveTelemetry` ทุกครั้งที่ได้ข้อมูล และ**คงบรรทัด** `_store.Set("latest_telemetry", ...)` ไว้ (โค้ดในใบงาน 13.3 ไม่มีบรรทัดนี้ ทำให้ `/api/telemetry/latest` ตอบ 404) |
| `wwwroot/index.html` | สร้างใหม่ ตามใบงาน |
| `Program.cs` | เพิ่ม `AddSignalR()`, `AddSingleton<ProtocolMetrics>()`, `UseDefaultFiles()`, `UseStaticFiles()`, `MapHub<GatewayHub>("/hubs/gateway")` และลบ `app.MapGet("/", ...)` จากใบงาน 13.1 ออก เพราะ Endpoint นี้ทำให้หน้าแรกตอบเป็นข้อความแทน `index.html` |

### กิจกรรม 13-3.4 เปิดแดชบอร์ด `http://localhost:5000`

| รายการ | ผล |
|:---|:---|
| สถานะการเชื่อมต่อ | ● SignalR Online |
| ค่าแรงดัน | แสดงค่าสดจาก ESP32 เช่น `1.94 V`, `Raw ADC: 2402 / 4095` เปลี่ยนตามการหมุน Potentiometer โดยไม่ต้องรีเฟรชหน้า |
| Protocol Convergence Metrics | Serial, MQTT และ SignalR เพิ่มขึ้นพร้อมกันและเท่ากันเสมอ (113/113/113, 300/300/300, 698/698/698) เพราะ 1 แพ็กเก็ตจาก Serial ถูกส่งต่อเข้า MQTT 1 ครั้งและขึ้นเว็บ 1 ครั้ง |

<img width="1917" height="765" alt="image" src="https://github.com/user-attachments/assets/41a05319-05ce-4783-8305-a780c9d96ab1" />


### การทดสอบปุ่มควบคุมหลอดไฟ

| การทดสอบ | ผลบนเว็บ | ผลบนบอร์ด | ผล |
|:---|:---|:---|:---:|
| กดปุ่มบนเว็บ | ปุ่มเปลี่ยนเป็น 💡 ON สีเขียว Log `🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: LED:ON` | LED ที่ GPIO 2 ติด | ✅ |
| เปิดเว็บ 2 แท็บ แล้วกดที่แท็บหนึ่ง | อีกแท็บเปลี่ยนเป็น ON เองทันที ผ่าน `Clients.All.SendAsync("LedStateUpdated")` | – | ✅ |
| สั่งเปิดไฟผ่าน REST `curl.exe -X POST http://localhost:5000/api/led/on` | ปุ่มยังเป็น 🌑 OFF | LED ติด | ⚠️ สถานะไม่ตรง |


<img width="1917" height="765" alt="image" src="https://github.com/user-attachments/assets/6c1fd568-d870-442f-bd28-fe35035e90ed" />
<img width="1917" height="743" alt="image" src="https://github.com/user-attachments/assets/26108cff-acdc-45af-a8bc-5d41a7182ccd" />


<img width="771" height="258" alt="image" src="https://github.com/user-attachments/assets/780a6ca1-4f0c-4ca6-b201-9edc9f8ede4f" />


<img width="960" height="1706" alt="image" src="https://github.com/user-attachments/assets/fbc85f6a-d3a1-49a2-827a-35bf050d6a1c" />


<img width="907" height="86" alt="image" src="https://github.com/user-attachments/assets/2eb8a661-39bc-4741-a852-f490276d8163" />
<img width="1917" height="656" alt="image" src="https://github.com/user-attachments/assets/7b4451ee-6580-494f-9be7-16aac76c3264" />
<img width="960" height="1706" alt="image" src="https://github.com/user-attachments/assets/82673b04-90ec-4495-a03b-3720d729717b" />


### ข้อสังเกต

- **สถานะปุ่มไม่ซิงก์กับช่องทางอื่น:** มีเฉพาะ `ToggleLed` ใน Hub ที่แจ้ง `LedStateUpdated` ให้หน้าเว็บ เมื่อสั่งไฟผ่าน REST API ไฟบนบอร์ดติดแต่หน้าเว็บไม่รู้ ปุ่มยังเป็น OFF
- **ปุ่มเปลี่ยนทันทีที่กด (Optimistic Update):** JavaScript เปลี่ยนปุ่มก่อนรู้ว่าคำสั่งถึงบอร์ดจริงไหม และ `ToggleLed` ไม่ได้ตรวจค่าที่ `SendSerialCommand` คืนกลับมา ถ้าสาย USB หลุดปุ่มก็ยังเปลี่ยนเป็น ON
- **ตัวนับ REST ไม่ถูกใช้:** `ProtocolMetrics` มีฟิลด์ `RestRequests` แต่ไม่มีโค้ดส่วนไหนเพิ่มค่าและหน้าเว็บไม่ได้แสดง
- **หน้าเว็บโหลด SignalR จาก CDN** หากเครื่องไม่มีอินเทอร์เน็ตหน้าเว็บจะเชื่อมต่อไม่ได้ แม้ Gateway อยู่ในเครื่องเดียวกัน

ใบงาน 13.4 จะแก้ให้ทุกช่องทางอัปเดตสถานะจาก ACK ที่ ESP32 ส่งกลับมาหลังเปลี่ยนสถานะ GPIO จริง

### ปัญหาที่พบและการแก้ไข

| ปัญหา | สาเหตุ | วิธีแก้ |
|:---|:---|:---|
| `Serial Read Warning: The operation was canceled.` ตอนกด Ctrl+C | Worker กำลังรออ่าน COM3 อยู่แล้วถูกสั่งหยุด | ไม่ใช่ข้อผิดพลาด |
| เปิด `localhost:5000` แล้วเห็นข้อความ RUNNING แทนแดชบอร์ด (ถ้าไม่ลบ) | `app.MapGet("/")` จากใบงาน 13.1 รับ Request หน้าแรกก่อน Static Files | ลบ `MapGet("/")` ออก |
| `/api/telemetry/latest` ตอบ 404 ถ้าใช้โค้ดตามใบงาน | `HandleIncomingSerialLine` ในใบงาน 13.3 ไม่มี `_store.Set("latest_telemetry")` | เพิ่มบรรทัดนี้กลับเข้าไป |
---

## 4. คำถามท้ายการทดลอง
1. เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript `setInterval()` เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?
```
setInterval() + HTTP GET Polling ทุก 500 ms คือเบราว์เซอร์ถามเซิร์ฟเวอร์ซ้ำๆ ว่ามีข้อมูลใหม่ไหม ส่วน SignalR เปิดการเชื่อมต่อ WebSocket ค้างไว้ แล้วเซิร์ฟเวอร์ผลักข้อมูลมาเองทันทีที่มีข้อมูลใหม่ (Push)

1. ข้อมูลสดกว่า
Polling ได้ข้อมูลช้าที่สุดถึง 500 ms หลังเกิดเหตุการณ์ เพราะต้องรอรอบถัดไป ส่วน SignalR ส่งทันทีที่ SerialBridgeWorker อ่านบรรทัดจาก ESP32 ได้ ในการทดลองตัวเลขบนเว็บเปลี่ยนแทบจะพร้อมกับการหมุน Potentiometer

2. ไม่ได้ข้อมูลซ้ำหรือพลาดข้อมูล
ถ้า Poll เร็วกว่าที่ข้อมูลมา จะได้ค่าเดิมซ้ำ ถ้า Poll ช้ากว่า ค่าที่อยู่ระหว่างรอบจะหายไป SignalR ส่งทุกแพ็กเก็ตตามจังหวะจริงของข้อมูล เห็นได้จากตัวนับ SignalR ที่เท่ากับ Serial ทุกครั้ง

3. ภาระเครือข่ายและเซิร์ฟเวอร์น้อยกว่า
Polling ทุกครั้งเป็น HTTP Request ใหม่ มี Header หลายร้อยไบต์ทั้งไปและกลับ เซิร์ฟเวอร์ต้องแยกวิเคราะห์ทุกคำขอ แม้ข้อมูลไม่เปลี่ยน เปิด 10 หน้าเว็บก็เป็น 20 คำขอต่อวินาที ส่วน WebSocket หลัง Handshake ครั้งแรก แต่ละข้อความมี Header เพียงไม่กี่ไบต์ และส่งเฉพาะเมื่อมีข้อมูลจริง

4. สื่อสารได้สองทางบนช่องทางเดียว
ปุ่มเปิดไฟเรียก ToggleLed ผ่านการเชื่อมต่อเดิม และเซิร์ฟเวอร์ส่ง LedStateUpdated กลับไปทุกหน้าได้ทันที ในการทดลองเปิด 2 แท็บ กดที่แท็บหนึ่ง อีกแท็บเปลี่ยนเองโดยไม่ต้องรีเฟรช ถ้าใช้ Polling อีกแท็บจะรู้ก็ต่อเมื่อถึงรอบ Poll ของตัวเอง

5. ประหยัดทรัพยากรของเบราว์เซอร์
setInterval ทำงานตลอดแม้ไม่มีข้อมูลใหม่ เปลืองแบตเตอรี่บนมือถือและแท็บเล็ต

นอกจากนี้ SignalR ยังจัดการเรื่องที่ต้องเขียนเองถ้าใช้ WebSocket ตรงๆ ให้ เช่น withAutomaticReconnect() เชื่อมต่อใหม่เมื่อหลุด และถ้าเครือข่ายไม่รองรับ WebSocket จะถอยไปใช้ Server-Sent Events หรือ Long Polling ให้อัตโนมัติ
```
2. การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?
```
ตัวนับ Serial, MQTT และ SignalR บอกปริมาณข้อมูลที่ไหลผ่าน Gateway แต่ละช่องทาง ในโรงงานที่มีอุปกรณ์จำนวนมากและเครือข่ายหลายชั้น ข้อมูลนี้ช่วยได้ดังนี้

1. หาจุดที่ข้อมูลหาย
ในระบบปกติตัวนับต้องสัมพันธ์กัน ในการทดลอง 1 แพ็กเก็ต Serial ได้ 1 ข้อความ MQTT และ 1 SignalR เสมอ (113/113/113, 698/698/698) ถ้าวันหนึ่ง Serial ยังเพิ่มแต่ MQTT หยุด แปลว่าปัญหาอยู่ที่ Broker ถ้า Serial เองหยุดเพิ่ม แปลว่าปัญหาอยู่ที่อุปกรณ์หรือสาย ไม่ต้องไล่ตรวจทั้งระบบ

2. ตรวจว่าอุปกรณ์ยังทำงาน
ESP32 ส่งข้อมูลทุก 500 ms ตัวนับ Serial ควรเพิ่มประมาณ 2 ต่อวินาที ถ้าเพิ่มช้าลงหรือหยุด แสดงว่าอุปกรณ์ค้าง ไฟดับ หรือสายหลุด ตั้งเป็นเกณฑ์แจ้งเตือนอัตโนมัติได้

3. พบปัญหาที่ซ่อนอยู่
ในใบงาน 13.2 เคยได้ข้อมูลแค่ 1 ข้อความต่อวินาทีแทน 2 เพราะ mosquitto_sub สองตัวใช้ Client ID ซ้ำกันแล้วผลัดกันถูกตัด ถ้ามีตัวนับรายผู้รับ จะเห็นความผิดปกตินี้ได้ทันที ทั้งที่ระบบยังดูเหมือนทำงานปกติ

4. วางแผนความจุ
รู้ว่าแต่ละโปรโตคอลรับภาระเท่าไร ใช้ตัดสินใจว่าจะเพิ่ม Gateway แยกงาน หรือปรับความถี่การส่งข้อมูล ก่อนที่ระบบจะรับไม่ไหว

5. ตรวจจับความผิดปกติด้านความปลอดภัย
ถ้าตัวนับ MQTT หรือ REST พุ่งสูงผิดปกติโดยไม่มีอุปกรณ์เพิ่ม อาจมีคนพยายามส่งข้อมูลปลอมหรือโจมตีแบบ Flooding

ข้อจำกัดของโค้ดในใบงานคือนับด้วย ++ ธรรมดา ซึ่งไม่ปลอดภัยเมื่อหลายเธรดเพิ่มค่าพร้อมกัน ควรใช้ Interlocked.Increment และตัวนับเป็นค่าสะสมตั้งแต่เปิดโปรแกรม ในระบบจริงควรคิดเป็นอัตราต่อวินาที เก็บย้อนหลัง และส่งออกให้ระบบ Monitoring อย่าง Prometheus และ Grafana
```
