# 13.3 การเชื่อมต่อ 4 โปรโตคอล (Multi-Protocol Bridging: MQTT, REST, WebSockets, Serial)

---

## 13.3.1 เมทริกซ์การแปลงโปรโตคอล 4 ทิศทาง (The 4-Way Protocol Translation Matrix)

หัวใจของ **Converged Edge Gateway** คือการทำหน้าที่เป็นตัวแปลงภาษา (Translator) โดยมีตารางการกระจายสัญญาณ (Routing Matrix) ดังนี้:

 ตารางการกระจายข้อมูลข้ามโปรโตคอล (Routing Matrix)

| แหล่งกำเนิดข้อมูล (Source)        | Embedded MQTT Broker             | Hardware Serial (UART)      | RESTful Minimal API       | SignalR WebSockets                  |
| :-------------------------------- | :------------------------------- | :-------------------------- | :------------------------ | :---------------------------------- |
| **Hardware Serial** (จาก ESP32)   | Publish ลง `iot/esp32/telemetry` | - (รับมาโดยตรง)             | อัปเดตแคชใน State Store   | เรียก `ReceiveTelemetry` ขึ้นจอเว็บ |
| **REST API** (จากระบบภายนอก)      | Publish ลง `iot/esp32/control`   | ยิงสตริง `LED:ON\n` เข้าสาย | - (ประมวลผลคำขอ)          | อัปเดตสถานะสวิตช์บนจอเว็บ           |
| **MQTT Client** (จาก Flutter)     | - (รับผ่าน Broker)               | ยิงคำสั่งเข้าขา ESP32       | อัปเดตสถานะใน State Store | อัปเดตสถานะสวิตช์บนจอเว็บ           |
| **SignalR Web** (จากเว็บแดชบอร์ด) | Publish ลง `iot/esp32/control`   | ยิงคำสั่งเข้าขา ESP32       | อัปเดตสถานะใน State Store | - (ส่งผลลัพธ์หา Client อื่น)        |

---

## 13.3.2 การฝัง MQTT Broker ภายในกระบวนการ Kestrel ด้วย `MQTTnet`

แทนที่จะต้องติดตั้งโปรแกรม Mosquitto แยกต่างหาก เราสามารถฝังตัวกลางกระจายข้อความ MQTT เข้ามาเป็นส่วนหนึ่งของ .NET Application ได้โดยตรงโดยใช้ไลบรารี **`MQTTnet`** (เวอร์ชัน 4.x):

```powershell
dotnet add package MQTTnet
dotnet add package MQTTnet.AspNetCore
```

### การตั้งค่า Embedded Broker ในไฟล์ `Program.cs`:
```csharp
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// 1. เพิ่มบริการ MQTT Server ใน Kestrel
builder.Services.AddMqttServer(options =>
{
    options.WithDefaultEndpoint();
    options.WithDefaultEndpointPort(1883); // เปิดพอร์ตมาตรฐาน 1883
});

var app = builder.Build();

// 2. ผูก MQTT Server เข้ากับ Kestrel Pipeline
app.UseMqttServer(server =>
{
    // ดักจับและประมวลผลข้อความทุกชิ้นที่ถูก Publish ผ่าน Broker
    server.InterceptingPublishAsync += async args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();
        Console.WriteLine($"[MQTT Inbound] Topic: {topic} | Payload: {payload}");

        // สามารถส่งต่อไปยัง SignalR Hub หรือ State Store ได้ทันที
        await Task.CompletedTask;
    };
});
```

---

## 13.3.3 สะพานเชื่อมต่อฮาร์ดแวร์อนุกรม (Hardware Serial Port Bridge)

ใช้คอมโพเนนต์ **`System.IO.Ports`** ในการดักฟังข้อมูลสาย USB จากบอร์ดไมโครคอนโทรลเลอร์:

```powershell
dotnet add package System.IO.Ports
```

### การทำงานของ Serial Background Listener:
```csharp
using System.IO.Ports;

public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serialPort;
    private readonly string _portName = "COM3"; // หรือ /dev/ttyUSB0 บน Linux
    private readonly int _baudRate = 115200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _serialPort = new SerialPort(_portName, _baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 500
            };
            _serialPort.Open();
            Console.WriteLine($"🔌 เชื่อมต่อฮาร์ดแวร์ Serial Port {_portName} สำเร็จ!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ ไม่พบ Serial Port {_portName}: {ex.Message}");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                string line = _serialPort.ReadLine().Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    // ส่งข้อมูลที่อ่านได้เข้าสู่ตัวกลางกระจายสัญญาณ
                    ProcessSerialTelemetry(line);
                }
            }
            catch (TimeoutException) { /* ไม่ได้รับข้อมูลในรอบเวลา หน่วงรอต่อไป */ }
            catch (Exception ex)
            {
                Console.WriteLine($"Serial Error: {ex.Message}");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    public void SendSerialCommand(string command)
    {
        if (_serialPort?.IsOpen == true)
        {
            _serialPort.WriteLine(command);
        }
    }

    private void ProcessSerialTelemetry(string rawLine)
    {
        // เช่น ได้รับ JSON {"voltage": 1.65, "raw": 2048}
        // แปลงและส่งต่อสู่ MQTT และ SignalR
    }
}
```

---

## 13.3.4 การเปิดบริการ HTTP RESTful Minimal APIs

Kestrel รองรับการสร้างจุดเชื่อมต่อ API ที่มีขนาดกะทัดรัดและประมวลผลได้เร็วเป็นพิเศษ:

```csharp
// 1. ตรวจสอบสถานะสุขภาพของเกตเวย์
app.MapGet("/api/gateway/health", (GatewayStateStore store) => Results.Ok(new
{
    gatewayId = "Kestrel-Converged-Edge-01",
    status = "Healthy",
    uptimeSeconds = Environment.TickCount64 / 1000,
    activeProtocols = new[] { "Serial", "MQTT", "REST", "WebSockets" }
}));

// 2. ดึงข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var latest = store.GetState<object>("telemetry");
    return latest != null ? Results.Ok(latest) : Results.NotFound("ยังไม่มีข้อมูลเซนเซอร์");
});

// 3. ควบคุมหลอดไฟ LED จากภายนอก
app.MapPost("/api/actuator/led", async (LedCommandRequest req, 
                                       SerialBridgeWorker serialWorker, 
                                       MqttServer mqttServer) =>
{
    bool turnOn = req.State.ToUpper() == "ON";
    string serialCmd = turnOn ? "LED:ON" : "LED:OFF";

    // กระจายคำสั่งออกสู่ทั้งพอร์ต Serial และหัวข้อ MQTT
    serialWorker.SendSerialCommand(serialCmd);

    var mqttMsg = new MqttApplicationMessageBuilder()
        .WithTopic("iot/esp32/control")
        .WithPayload(System.Text.Json.JsonSerializer.Serialize(new { led = req.State }))
        .Build();
    await mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));

    return Results.Ok(new { result = "Dispatched", state = req.State });
});
```

---

## 13.3.5 สตรีมมิ่งข้อมูลขึ้นเว็บด้วย ASP.NET Core SignalR

**SignalR** ช่วยให้เราสามารถผลักดันข้อมูล (Server Push) จาก C# Backend ไปยังเว็บเบราว์เซอร์ของผู้ใช้ได้ทันทีโดยไม่ต้อง Refresh หน้าจอ:

```csharp
using Microsoft.AspNetCore.SignalR;

// นิยามคลาส Hub
public class ConvergedGatewayHub : Hub
{
    // เมธอดที่ฝั่ง Web Client สามารถเรียกส่งคำสั่งเข้ามาได้
    public async Task SendActuatorCommand(string state)
    {
        // แจ้งเตือนทุกเครื่องที่กำลังเปิดหน้าจออยู่
        await Clients.Others.SendAsync("LedStateUpdated", state);
    }
}

// ในส่วนกระจายข้อมูลของ Gateway: ส่งข้อมูลออกหาทุกหน้าจอทันทีที่มีข้อมูลเซนเซอร์ใหม่
public async Task BroadcastTelemetryToWeb(IHubContext<ConvergedGatewayHub> hubContext, 
                                          object telemetryData)
{
    await hubContext.Clients.All.SendAsync("ReceiveTelemetry", telemetryData);
}
```
