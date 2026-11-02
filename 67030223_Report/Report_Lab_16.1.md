# Labsheet 13.1: Embedded MQTT Broker & Client with MQTTnet inside Kestrel

#### 1. บันทึกผลการทดลอง / โค้ดที่พัฒนา

สร้างโปรเจกต์ .NET 8 Web และติดตั้งไลบรารี MQTTnet สำหรับทำหน้าที่เป็น Embedded MQTT Broker ภายในเว็บเซิร์ฟเวอร์ Kestrel:

```powershell
dotnet new web -n ConvergedEdgeGateway
cd ConvergedEdgeGateway
dotnet add package MQTTnet --version 4.3.7.1207
dotnet add package MQTTnet.AspNetCore --version 4.3.7.1207
```

โค้ดในไฟล์ `Program.cs`:
```csharp
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// กำหนดค่า Kestrel ให้เปิดพอร์ต HTTP (5000) และ MQTT TCP (1883)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // พอร์ต HTTP REST API และแดชบอร์ด
    options.ListenAnyIP(1883, listenOptions =>
    {
        listenOptions.UseMqtt(); // พอร์ต Embedded MQTT Broker
    });
});

// ลงทะเบียนบริการ MQTT Server
builder.Services.AddMqttServer(options =>
{
    options.WithDefaultEndpoint();
});

var app = builder.Build();

// ตั้งค่า Event Handlers ของ Broker
app.UseMqttServer(server =>
{
    // กำหนด ClientId อัตโนมัติถ้าไคลเอนต์ไม่ได้ส่งมา
    server.ValidatingConnectionAsync += args =>
    {
        if (string.IsNullOrEmpty(args.ClientId))
        {
            args.AssignedClientIdentifier = Guid.NewGuid().ToString("N");
        }
        return Task.CompletedTask;
    };

    // แสดง log เมื่อมี Client เชื่อมต่อ
    server.ClientConnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MQTT Server] Client เชื่อมต่อ: {args.ClientId} (จาก Endpoint: {args.Endpoint})");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    // แสดง log เมื่อ Client ตัดการเชื่อมต่อ
    server.ClientDisconnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[MQTT Server] Client ตัดการเชื่อมต่อ: {args.ClientId}");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    // ดักจับข้อความที่ถูก Publish ทุกอัน
    server.InterceptingPublishAsync += args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();
        var qos = args.ApplicationMessage.QualityOfServiceLevel;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[MQTT Message] หัวข้อ: {topic} | QoS: {qos} | ข้อความ: {payload}");
        Console.ResetColor();

        return Task.CompletedTask;
    };
});

app.MapGet("/", () => "Kestrel Converged Edge Gateway with Embedded MQTTnet Broker is RUNNING!");

app.Run();
```

#### 2. ผลลัพธ์จาก Serial Monitor / หน้าจอแสดงผล

ผลการรัน Kestrel Edge Gateway บน PowerShell หน้าต่างที่ 1:
```text
PS C:\IoT_Lab\Week-13\ConvergedEdgeGateway> dotnet run
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: mqtt://0.0.0.0:1883
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: C:\IoT_Lab\Week-13\ConvergedEdgeGateway
[MQTT Server] Client เชื่อมต่อ: sub_client (จาก Endpoint: 127.0.0.1:51234)
[MQTT Server] Client เชื่อมต่อ: pub_client (จาก Endpoint: 127.0.0.1:51240)
[MQTT Message] หัวข้อ: test/edge/topic | QoS: AtMostOnce | ข้อความ: Hello from CLI via Kestrel Embedded MQTT!
[MQTT Server] Client ตัดการเชื่อมต่อ: pub_client
```

ผลการทดสอบ Subscribe ผ่าน Mosquitto CLI บน PowerShell หน้าต่างที่ 2:
```text
PS C:\Users\student> mosquitto_sub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -i "sub_client" -v
test/edge/topic Hello from CLI via Kestrel Embedded MQTT!
```

ผลการทดสอบ Publish ผ่าน Mosquitto CLI บน PowerShell หน้าต่างที่ 3:
```text
PS C:\Users\student> mosquitto_pub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!" -i "pub_client"
```

#### 3. สรุปผลและการตอบคำถามท้ายใบงาน

- **สรุปผลการทดลอง:**  
 สามารถพัฒนา Converged Edge Gateway บนเฟรมเวิร์ก ASP.NET Core 8 ร่วมกับไลบรารี MQTTnet และ MQTTnet.AspNetCore ได้สำเร็จ โดยคอนฟิกให้เว็บเซิร์ฟเวอร์ Kestrel ทำงานเป็นโฮสต์ร่วม รับทราฟฟิกระดับ L7 HTTP (พอร์ต 5000) และ Embedded MQTT Broker บน TCP (พอร์ต 1883) ภายใต้โปรเซสเดียวกัน ระบบสามารถจัดการวงจรชีวิตของ Client ดักจับข้อความ Publish ผ่าน InterceptingPublishAsync และกระจายข้อมูลไปยัง Subscriber ได้อย่างถูกต้องและเสถียร

- **คำถามท้ายการทดลองที่ 1:**  
  การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?  
  **คำตอบ:**  
ด้าน Deployment: รวมระบบเว็บ, REST API และ MQTT Broker เป็นแอปพลิเคชันไบนารีเดียว (Single Unit) จัดการเป็น Docker Container หรือ Service เดียวได้ทันที ไม่ต้องติดตั้งและคอนฟิกเซอร์วิส Mosquitto ภายนอกเพิ่มเติม

ด้านประมวลผล In-Memory: ส่งต่อข้อมูลจาก MQTT เข้าสู่ Business Logic, Web API หรือบันทึกลงฐานข้อมูลผ่าน C# Memory/In-Process Pipeline ได้โดยตรง ไม่ต้องผ่าน Network Socket ย้อนกลับมาอีกรอบ ช่วยลด Latency และลดการใช้ทรัพยากรระบบ

- **คำถามท้ายการทดลองที่ 2:**  
  ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?  
  **คำตอบ:**  
สามารถเขียนโค้ดตรวจสอบเพย์โหลดก่อนส่งต่อได้ทันที โดยถอดรหัส JSON และตรวจสอบช่วงค่าข้อมูล (เช่น อุณหภูมิสูงผิดปกติ หรือฟิลด์ข้อมูลหาย) หากพบข้อมูลผิดเงื่อนไข สามารถสั่งระงับการกระจายข้อความด้วยการตั้งค่า args.ProcessPublish = false พร้อมทั้งแปลงข้อมูล บันทึก Log เตือนภัย หรือดักแปลงเพย์โหลดใหม่ได้ก่อนที่ข้อความจะไปถึง Subscriber เครื่องอื่น
 
---