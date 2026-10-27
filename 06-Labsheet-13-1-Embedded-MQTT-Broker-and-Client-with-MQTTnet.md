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

## ผลการทดลอง

| รายการ | ผล |
|:---|:---|
| .NET SDK | 10.0.400 (`net10.0`) |
| MQTTnet / MQTTnet.AspNetCore | 4.3.7.1207 |
| ผล `dotnet build` | Build succeeded |
| พอร์ตที่เปิด | HTTP 5000 และ MQTT 1883 |

### กิจกรรม 13-1.3 การรันและทดสอบ

**1. รัน Kestrel Edge Gateway**

<img width="763" height="230" alt="image" src="https://github.com/user-attachments/assets/5f715527-0d01-4c46-b1ba-377d1d5b4884" />


<img width="755" height="192" alt="image" src="https://github.com/user-attachments/assets/53b6e35a-89fa-4059-9bf8-f7ebb806d614" />


**2–4. ทดสอบ Subscribe และ Publish**

| ขั้น | คำสั่ง | ผลบน Kestrel Console | ผลบน mosquitto_sub |
|:---|:---|:---|:---|
| Subscribe | `mosquitto_sub -t "test/edge/topic" -i "sub_client"` | 🟢 `Client เชื่อมต่อ: sub_client` | รอรับข้อความ |
| Publish | `mosquitto_pub -t "test/edge/topic" -m "Hello from CLI..." -i "pub_client"` | 🟢 `pub_client` เชื่อมต่อ → 📩 ดักจับข้อความ `QoS: AtMostOnce` → 🔴 `pub_client` ตัดการเชื่อมต่อ | ได้รับ `test/edge/topic Hello from CLI via Kestrel Embedded MQTT!` |

<img width="1013" height="317" alt="image" src="https://github.com/user-attachments/assets/6ad629ad-63a3-4b88-87ea-dcf38101936c" />


<img width="1203" height="293" alt="image" src="https://github.com/user-attachments/assets/570e1820-ad1e-41bb-934e-74f4a9f97df6" />


### ปัญหาที่พบและการแก้ไข

| ปัญหา | สาเหตุ | วิธีแก้ |
|:---|:---|:---|
| `New-NetFirewallRule : Access is denied` | เปิด PowerShell แบบธรรมดา | เปิด PowerShell แบบ Run as administrator |
| เปิดลิงก์ `http://[::]:5000` แล้วขึ้น `ERR_ADDRESS_INVALID` | `[::]` คือที่อยู่ฝั่งเซิร์ฟเวอร์ที่แปลว่ารับทุก IP เบราว์เซอร์ใช้ไม่ได้ | เปิด `http://localhost:5000` แทน |
| `warn: Overriding address(es) 'http://localhost:5027'` | `launchSettings.json` กำหนดพอร์ต 5027 ไว้ แต่โค้ดกำหนดพอร์ตเองใน `ConfigureKestrel` | ไม่ต้องแก้ โปรแกรมใช้พอร์ต 5000 ตามโค้ด |
| ต้องตรวจว่าพอร์ต 1883 ว่าง | Mosquitto จากสัปดาห์ 11 อาจยังจองพอร์ตไว้ | ตรวจด้วย `Get-NetTCPConnection -LocalPort 1883` ก่อนรัน |
---

## 4. คำถามท้ายการทดลอง (Review Questions)
1. การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?
```
ข้อได้เปรียบด้านการติดตั้ง (Deployment)
- Gateway ทั้งระบบเป็นโปรแกรมเดียว ใช้คำสั่ง dotnet run หรือ dotnet publish แล้วนำไปรันได้เลย ไม่ต้องติดตั้ง Mosquitto, ตั้ง PATH, เขียนไฟล์ mosquitto.conf หรือจัดการ Windows Service แยก ในการทดลองนี้ต้องหยุด Mosquitto Service ก่อน เพราะสองโปรแกรมแย่งพอร์ต 1883 กัน ถ้า Broker อยู่ในโปรแกรมเดียวกันจะไม่มีปัญหานี้
- การตั้งค่าทั้งหมด เช่น พอร์ต การตรวจ ClientId อยู่ในโค้ด C# ใช้ Git ควบคุมเวอร์ชันได้ และเริ่ม/หยุดพร้อมกับ Gateway เสมอ ไม่มีกรณีที่ Gateway ทำงานแต่ Broker ดับ
- ใช้ได้ทุกระบบที่รัน .NET ได้ ทั้ง Windows, Linux และ Raspberry Pi

ข้อได้เปรียบด้านการประมวลผลในหน่วยความจำ
- ถ้าใช้ Mosquitto แยก Gateway ต้องเชื่อมต่อเป็น Client อีกตัวผ่าน TCP เพื่ออ่านข้อมูล ข้อความต้องเดินทาง Client → Mosquitto → TCP → Gateway และแปลงไปมาหลายรอบ
- แบบ Embedded ข้อความอยู่ในหน่วยความจำของโปรแกรมเดียวกัน Gateway อ่านได้ทันทีผ่าน InterceptingPublishAsync (ตามบรรทัดสีฟ้าในการทดลอง) และส่งข้อความเข้า Broker ได้โดยตรงด้วย InjectApplicationMessage โดยไม่ผ่านเครือข่าย ความหน่วงต่ำกว่าและไม่เปลือง Socket
- นำข้อมูลไปใช้ร่วมกับส่วนอื่นในโปรแกรมได้ทันที เช่น เก็บสถานะล่าสุดให้ REST API หรือส่งต่อไปหน้าเว็บผ่าน SignalR

ข้อแลกเปลี่ยน
Mosquitto เป็นซอฟต์แวร์ที่ผ่านการใช้งานจริงมานาน รองรับ Client จำนวนมาก มี Bridge, ACL และ Persistence ครบ ถ้า Gateway ล่ม Broker แบบ Embedded ก็ล่มไปด้วย จึงเหมาะกับ Edge Gateway ขนาดเล็กถึงกลาง ส่วนระบบขนาดใหญ่ยังนิยมใช้ Broker แยก
```
2. ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?
```
InterceptingPublishAsync ทำงานทุกครั้งที่มีข้อความ Publish เข้ามา ก่อนที่ Broker จะส่งต่อให้ Subscriber ในการทดลองเราใช้แค่พิมพ์ Log แต่ในตัวแปร args มีคุณสมบัติ ProcessPublish ถ้าตั้งเป็น false ข้อความจะถูกทิ้ง ไม่ถูกส่งต่อ จึงใช้ทำ Edge Validation ได้ เช่น

server.InterceptingPublishAsync += args =>
{
    if (args.ApplicationMessage.Topic == "iot/esp32/telemetry")
    {
        try
        {
            using var doc = JsonDocument.Parse(args.ApplicationMessage.ConvertPayloadToString());
            double v = doc.RootElement.GetProperty("voltage").GetDouble();
            if (v < 0 || v > 3.3)
                args.ProcessPublish = false;   // ค่านอกช่วงที่ ADC วัดได้ ทิ้ง
        }
        catch
        {
            args.ProcessPublish = false;       // ไม่ใช่ JSON หรือไม่มีฟิลด์ voltage ทิ้ง
        }
    }
    return Task.CompletedTask;
};

การประยุกต์ใช้
- ตรวจรูปแบบข้อมูล: ทิ้งข้อความที่ไม่ใช่ JSON หรือขาดฟิลด์ที่จำเป็น แอปปลายทางไม่ต้องรับข้อมูลเสียจนแครช (ต่างจากใบงาน 12.3 ที่ต้องป้องกันที่ฝั่ง Flutter เอง)
- ตรวจช่วงค่า: ค่าเซนเซอร์ที่เป็นไปไม่ได้ เช่น แรงดันเกิน 3.3 V หรือติดลบ แสดงว่าสายหลวมหรือเซนเซอร์เสีย
- ควบคุมสิทธิ์: ใช้ args.ClientId ตรวจว่าใครส่ง เช่น อนุญาตให้เฉพาะแอปที่กำหนดส่งคำสั่งไปหัวข้อ control
- กรองสัญญาณรบกวน: ทิ้งค่าที่เปลี่ยนน้อยกว่าเกณฑ์ (Deadband) ลดทราฟฟิก
- แปลงข้อมูล: แก้ไขข้อความก่อนส่งต่อ เช่น เติม Timestamp หรือแปลงหน่วย

ข้อดีคือกรองครั้งเดียวที่ Edge ทุก Subscriber ได้ข้อมูลที่สะอาดเหมือนกันหมด และลดข้อมูลเสียที่ส่งขึ้นคลาวด์ ข้อควรระวังคือฟังก์ชันนี้ทำงานกับทุกข้อความ ถ้าประมวลผลช้า Broker ทั้งตัวจะช้าตาม จึงควรตรวจแบบเบาๆ เท่านั้น
```
