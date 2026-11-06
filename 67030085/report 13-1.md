# ใบงานการทดลองที่ 13.1
### การสร้าง Embedded MQTT Broker และ Client ภายใน Kestrel .NET 8 ด้วยไลบรารี MQTTnet

> [!NOTE] **คำชี้แจง**
> ในใบงานนี้นักศึกษาจะได้เปลี่ยนเว็บเซิร์ฟเวอร์ ASP.NET Core Kestrel ให้กลายเป็น **Embedded MQTT Broker** แบบเบ็ดเสร็จในตัวเอง โดยใช้ไลบรารี **`MQTTnet`** ทำให้แอปพลิเคชัน C# เพียงตัวเดียวสามารถรับการเชื่อมต่อจาก MQTT Client ภายนอกผ่านพอร์ต 1883 และสามารถดักจับ (Intercept) ข้อความเพื่อนำมาประมวลผลภายในได้อย่างมีประสิทธิภาพ โดยไม่ต้องติดตั้ง Mosquitto ภายนอก

---

## 1. วัตถุประสงค์การทดลอง
1. สามารถสร้างโปรเจกต์ ASP.NET Core Web Application (.NET 8) และติดตั้งแพ็กเกจ `MQTTnet.AspNetCore` ได้
2. สามารถกำหนดค่า Kestrel ให้เปิดพอร์ต TCP 1883 สำหรับให้บริการ Embedded MQTT Server ได้
3. สามารถเขียน Event Interceptor เพื่อดักจับข้อความ Inbound และบันทึก Log ลงในระบบได้
4. สามารถทดสอบการเชื่อมต่อ การ Publish และการ Subscribe ผ่านเครื่องมือ Mosquitto CLI หรือ MQTTX ได้

---

## 2. โครงสร้างสถาปัตยกรรม (Architecture Diagram)

<p align="center"><img src="Images/Fig_06-Labsheet-13-1_academic.svg" alt="06-Labsheet-13-1-Embedded-MQTT-Broker-and-Client-with-MQTTnet" width="400" style="  height: auto;" ></p>
<p align="center"><b></b>โครงสร้างสถาปัตยกรรม (Architecture Diagram)</p>

---

## 3. ขั้นตอนการทดลอง (Step-by-Step Activities)

### กิจกรรมที่ 13-1.1: การสร้างโปรเจกต์และติดตั้งแพ็กเกจ

#### 1. สร้างโปรเจกต์ .NET 8 Web ใหม่
เปิด PowerShell ในโฟลเดอร์สำหรับทำงาน:
```powershell
dotnet new web -n ConvergedEdgeGateway
cd ConvergedEdgeGateway
```

#### 2. ติดตั้งแพ็กเกจ `MQTTnet` และ `MQTTnet.AspNetCore`
```powershell
dotnet add package MQTTnet --version 4.3.7.1207
dotnet add package MQTTnet.AspNetCore --version 4.3.7.1207
```

---

### กิจกรรมที่ 13-1.2: การเขียนโค้ดตั้งค่าเซิร์ฟเวอร์ (`Program.cs`)

เปิดไฟล์ `Program.cs` และแทนที่ด้วยโค้ดต่อไปนี้:

```csharp
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// 1. กำหนดค่า Kestrel ให้เปิดทั้งพอร์ต HTTP (5000) และ MQTT TCP (1883)
builder.WebHost.ConfigureKestrel(options =>
{
    // พอร์ตสำหรับ HTTP REST APIs และเว็บแดชบอร์ด
    options.ListenAnyIP(5000);

    // พอร์ตสำหรับ Embedded MQTT Broker
    options.ListenAnyIP(1883, listenOptions =>
    {
        listenOptions.UseMqtt();
    });
});

// 2. ลงทะเบียนบริการ MQTT Server
builder.Services.AddMqttServer(options =>
{
    options.WithDefaultEndpoint();
});

var app = builder.Build();

// 3. ผูก Event Handlers สำหรับจัดการข้อความใน Broker
app.UseMqttServer(server =>
{
    // ตรวจสอบการเชื่อมต่อ: กรณี Client ไม่ได้ระบุ ClientId (เช่น mosquitto CLI ที่ไม่ได้ใส่ -i)
    // ให้ Broker สุ่ม ClientId ให้โดยอัตโนมัติ เพื่อป้องกัน Connection Refused: identifier rejected
    server.ValidatingConnectionAsync += args =>
    {
        if (string.IsNullOrEmpty(args.ClientId))
        {
            args.AssignedClientIdentifier = Guid.NewGuid().ToString("N");
        }
        return Task.CompletedTask;
    };

    // เมื่อมี Client เชื่อมต่อเข้ามา
    server.ClientConnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MQTT Server] 🟢 Client เชื่อมต่อ: {args.ClientId} (จาก Endpoint: {args.Endpoint})");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    // เมื่อ Client ตัดการเชื่อมต่อ
    server.ClientDisconnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[MQTT Server] 🔴 Client ตัดการเชื่อมต่อ: {args.ClientId}");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    // ดักจับและอ่านข้อความที่ถูก Publish ทุกฉบับ
    server.InterceptingPublishAsync += args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();
        var qos = args.ApplicationMessage.QualityOfServiceLevel;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[MQTT Message] 📩 หัวข้อ: {topic} | QoS: {qos} | ข้อความ: {payload}");
        Console.ResetColor();

        return Task.CompletedTask;
    };
});

// 4. จุดทดสอบ REST Endpoint เบื้องต้น
app.MapGet("/", () => "🚀 Kestrel Converged Edge Gateway with Embedded MQTTnet Broker is RUNNING!");

app.Run();
```

---

### กิจกรรมที่ 13-1.3: การสั่งรันและทดสอบด้วยเครื่องมือ CLI

#### 1. สั่งรัน Kestrel Edge Gateway
```powershell
dotnet run
```
*สังเกตข้อความบนคอนโซลว่าเซิร์ฟเวอร์กำลังฟังที่พอร์ต 5000 (HTTP) และ 1883 (MQTT)*

#### 2. เปิดหน้าต่าง PowerShell ที่สอง: ทดสอบ Subscribe หัวข้อ
ใช้คำสั่ง `mosquitto_sub` เพื่อดักฟังข้อความจาก Embedded Broker ของเรา (ระบุพารามิเตอร์ `-i` เพื่อกำหนด Client ID):
```powershell
mosquitto_sub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -i "sub_client" -v
```
> **ข้อสังเกต:** มาตรฐาน MQTT กำหนดให้ Client ต้องมี Client ID เสมอ สำหรับเครื่องมือ `mosquitto_sub` บน Windows หากไม่ระบุออปชัน `-i` มันจะส่ง Client ID ว่างเปล่ามา ซึ่งในโค้ดฝั่ง Broker เราได้ตั้งค่า `ValidatingConnectionAsync` ให้สุ่ม Client ID ให้โดยอัตโนมัติแล้ว

*สังเกตหน้าต่างคอนโซลของ Kestrel จะขึ้นข้อความสีเขียวว่ามี Client เชื่อมต่อเข้ามา!*

#### 3. เปิดหน้าต่าง PowerShell ที่สาม: ทดสอบ Publish ข้อความ
ส่งข้อความผ่าน `mosquitto_pub`:
```powershell
mosquitto_pub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!" -i "pub_client"
```

#### 4. สังเกตผลลัพธ์
* หน้าต่าง Kestrel จะพิมพ์ Log สีฟ้าว่าดักจับข้อความได้ถูกต้อง
* หน้าต่าง `mosquitto_sub` จะได้รับข้อความที่ถูกส่งต่อผ่าน Broker สำเร็จทันที

![alt text](image.png)

---

## 4. คำถามท้ายการทดลอง (Review Questions)
1. การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?
   * **ด้านการกระจายงาน (Deployment):**
     * **Single Executable / All-in-One Process:** รวม Web Server, REST API, SignalR และ MQTT Broker ไว้ในโปรเซสเดียวกัน ทำให้ Deploy ง่ายมากผ่านคำสั่ง `dotnet run` หรือแพ็กเกจเป็นไฟล์เดี่ยว (Single-file binary)
     * **ลด Dependency ภายนอก:** ไม่ต้องติดตั้ง ตั้งค่าคอนฟิก หรือจัดการ Lifecycle ของโปรแกรม Mosquitto แยกเป็น Daemon/Service หรือสร้าง Container แยก ทำให้มีความเป็น Portable สูง สามารถนำไปรันบน Edge Device (เช่น Raspberry Pi หรือ Industrial PC) ได้ทันที
     * **รวมศูนย์การจัดการ (Unified Logging & Config):** สามารถใช้ระบบ Logging, `appsettings.json`, การจัดการสิทธิ์ (Security) และ Health Check ของ ASP.NET Core ร่วมกันได้ทั้งหมดในจุดเดียว
   * **ด้านการประมวลผลข้อมูลในหน่วยความจำ (In-Memory Processing):**
     * **Direct In-Memory Data Access:** เมื่อมีข้อความ MQTT เข้ามา เกตเวย์สามารถดักจับและส่งผ่านข้อมูลไปยังส่วนอื่นๆ (เช่น SignalR Hub, Local Cache, SQLite Buffer, หรือ Serial Port) ผ่านตัวแปรในหน่วยความจำ (In-Memory Pipeline / Channels) ได้ทันที
     * **ลด Network & IPC Overhead:** หากใช้ Mosquitto แยกต่างหาก แอป Kestrel จะต้องทำตัวเป็น MQTT Client เชื่อมต่อผ่าน Local TCP Loopback (localhost:1883) หรือ IPC ซึ่งทำให้เกิดความหน่วงเวลา (Latency) และภาระของ CPU จากการ Serialize/Deserialize ข้อมูลหลายรอบ การใช้ Embedded Broker จึงช่วยลด Latency เหลือต่ำกว่า 1-2 ms และประหยัดทรัพยากรได้มากกว่าอย่างเห็นได้ชัด

2. ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?
   * **หลักการทำงาน:** `InterceptingPublishAsync` เป็น Event Interceptor ที่ทำงานคั่นกลางระหว่างที่ Broker ได้รับข้อความ Publish จาก Client และก่อนที่จะกระจายข้อความต่อไปยัง Subscriber รายอื่น โดยสามารถเข้าถึง Topic, Payload, QoS และสามารถสั่ง **ยกเลิกการส่งต่อ (Drop/Reject)** หรือ **แก้ไขเนื้อหาข้อความ (Mutate)** ได้โดยตรง
   * **การนำไปประยุกต์ใช้ในการทำ Edge Validation:**
     * **1. การตรวจสอบช่วงค่าของข้อมูล (Range & Boundary Check):** สามารถแปลง Payload เป็นตัวเลขเพื่อตรวจสอบค่าเซนเซอร์ เช่น ค่าแรงดันต้องอยู่ในช่วง 0.0 - 3.3V หรือค่า ADC อยู่ในช่วง 0 - 4095 หากพบค่าผิดปกติ (เช่น เซนเซอร์เสียอ่านได้ค่าลบ, NaN, หรือเกินค่าสูงสุด) สามารถตั้งค่า `args.ProcessPublish = false;` เพื่อระงับไม่ให้ข้อความนั้นถูกส่งต่อ ช่วยป้องกันไม่ให้แดชบอร์ดหรือแอปพลิเคชันปลายทางประมวลผลผิดพลาด
     * **2. การตรวจสอบโครงสร้างข้อมูล (Schema & JSON Validation):** ตรวจสอบว่าข้อความที่ส่งมามีรูปแบบ JSON ที่ถูกต้องและมีฟิลด์ข้อมูลครบถ้วนหรือไม่ (เช่น ต้องมี `voltage`, `raw`, `timestamp`) หากข้อมูลไม่สมบูรณ์ (Malformed Data) ให้ดรอปทิ้งและบันทึก Log แจ้งเตือน
     * **3. การรักษาความปลอดภัยและการป้องกันสแปม (Rate Limiting & Topic Authorization):** ตรวจสอบว่า Client มีสิทธิ์ในการ Publish ลงหัวข้อนั้นๆ หรือไม่ หรือตรวจสอบความถี่ในการส่งข้อมูล หากส่งข้อมูลถี่เกินปกติผิดวิสัย (DDoS/Spam) สามารถปฏิเสธข้อความ หรือสั่งตัดการเชื่อมต่อได้ทันทีด้วย `args.CloseConnection = true;`
     * **4. การปรับแก้ข้อมูลก่อนส่ง (Data Cleansing & Calibration):** สามารถทำการชดเชยค่าผิดพลาด (Offset/Calibration) หรือแปลงหน่วยของข้อมูลก่อนส่งต่อให้ Subscriber อื่นๆ ได้แบบเรียลไทม์
