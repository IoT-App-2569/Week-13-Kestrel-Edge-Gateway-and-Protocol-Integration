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

<p align="center"><img src="../Images/Fig_06-Labsheet-13-1_academic.svg" alt="06-Labsheet-13-1-Embedded-MQTT-Broker-and-Client-with-MQTTnet" width="400" style="  height: auto;" ></p>
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

> **กรณี Windows แจ้งว่าไม่รู้จัก `mosquitto_sub`:** หมายความว่ายังไม่ได้ติดตั้ง Mosquitto CLI หรือยังไม่ได้เพิ่มโฟลเดอร์ติดตั้งลงใน `PATH` ให้ติดตั้ง Mosquitto สำหรับ Windows จาก [หน้าดาวน์โหลดทางการ](https://mosquitto.org/download/) แล้วเปิด PowerShell ใหม่ หรือเรียกไฟล์ `.exe` ด้วย path เต็มตามตัวอย่างด้านล่าง (ตัวอย่างนี้ติดตั้งไว้ที่ `C:\Program Files\Mosquitto`)

#### 2. เปิดหน้าต่าง PowerShell ที่สอง: ทดสอบ Subscribe หัวข้อ
ใช้คำสั่ง `mosquitto_sub` เพื่อดักฟังข้อความจาก Embedded Broker ของเรา (ระบุพารามิเตอร์ `-i` เพื่อกำหนด Client ID):
```powershell
& 'C:\Program Files\Mosquitto\mosquitto_sub.exe' -h '::1' -p 1883 -t "test/edge/topic" -i "sub_client" -v
```
> ใช้ `::1` เพื่อเชื่อมต่อ Kestrel โดยตรงเมื่อมี Mosquitto Service อีกตัวฟังพอร์ต `1883` ที่ `127.0.0.1` อยู่ หากเครื่องไม่มีพอร์ตชนกันและเพิ่ม Mosquitto CLI ใน `PATH` แล้ว สามารถใช้ `mosquitto_sub -h 127.0.0.1 ...` ได้ตามปกติ
> **ข้อสังเกต:** มาตรฐาน MQTT กำหนดให้ Client ต้องมี Client ID เสมอ สำหรับเครื่องมือ `mosquitto_sub` บน Windows หากไม่ระบุออปชัน `-i` มันจะส่ง Client ID ว่างเปล่ามา ซึ่งในโค้ดฝั่ง Broker เราได้ตั้งค่า `ValidatingConnectionAsync` ให้สุ่ม Client ID ให้โดยอัตโนมัติแล้ว

*สังเกตหน้าต่างคอนโซลของ Kestrel จะขึ้นข้อความสีเขียวว่ามี Client เชื่อมต่อเข้ามา!*

#### 3. เปิดหน้าต่าง PowerShell ที่สาม: ทดสอบ Publish ข้อความ
ส่งข้อความผ่าน `mosquitto_pub`:
```powershell
& 'C:\Program Files\Mosquitto\mosquitto_pub.exe' -h '::1' -p 1883 -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!" -i "pub_client"
```

#### 4. สังเกตผลลัพธ์
* หน้าต่าง Kestrel จะพิมพ์ Log สีฟ้าว่าดักจับข้อความได้ถูกต้อง
* หน้าต่าง `mosquitto_sub` จะได้รับข้อความที่ถูกส่งต่อผ่าน Broker สำเร็จทันที

![alt text](img/image.png) ![alt text](img/image-1.png)
![alt text](img/image-2.png) ![alt text](img/image-3.png)

## 4. คำถามท้ายการทดลอง (Review Questions)
1. การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?
2. ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?


1. **ด้าน Deployment:** แอปพลิเคชันและ Broker อยู่ในโปรเซส/ชุดติดตั้งเดียวกัน จึงลดขั้นตอนติดตั้ง ตั้งค่า และดูแลบริการ Broker แยกต่างหาก อีกทั้งโค้ดในแอปสามารถนำข้อความไปประมวลผลต่อได้โดยตรง โดยไม่ต้องส่งผ่านบริการหรือ IPC เพิ่ม อย่างไรก็ตาม วิธีนี้ทำให้วงจรชีวิตและความพร้อมใช้งานของ Broker ผูกกับแอป และไม่ได้หมายความว่าจะเร็วกว่า Mosquitto ในทุกกรณี เพราะยังมีการรับส่งผ่านเครือข่าย MQTT ตามปกติ

2. `InterceptingPublishAsync` ทำงานเมื่อ Broker ได้รับข้อความ Publish จึงใช้ตรวจหัวข้อและ Payload ก่อน Broker ประมวลผล/ส่งต่อ เช่น ตรวจรูปแบบ JSON ชนิดข้อมูล และช่วงค่าที่อนุญาต หากไม่ผ่านเงื่อนไขให้กำหนด `args.ProcessPublish = false` เพื่อไม่ให้ Broker ประมวลผลข้อความนั้น พร้อมบันทึกเหตุผลหรือเพิ่มตัวนับสำหรับติดตาม ส่วนข้อความที่ผ่านการตรวจสอบปล่อยให้ Broker ทำงานตามปกติ การตรวจสอบควรทำให้รวดเร็วและไม่ทำ I/O ที่ใช้เวลานานใน Event Handler เพื่อไม่ให้การรับข้อความอื่นล่าช้า
