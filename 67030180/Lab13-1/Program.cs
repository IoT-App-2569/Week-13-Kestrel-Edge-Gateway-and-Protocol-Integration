using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// 1. กำหนดค่า Kestrel ให้เปิดทั้งพอร์ต HTTP (5000) และ MQTT TCP (1883)
builder.WebHost.ConfigureKestrel(options =>
{
    // พอร์ตสำหรับ HTTP REST APIs และเว็บแดชบอร์ด (ใช้ 5001 เพื่อไม่ให้ชนกับ AirPlay Receiver บน Mac)
    options.ListenAnyIP(5001);

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
app.MapGet("/", () => "🚀 Kestrel Converged Edge Gateway (Student: 67030180) with Embedded MQTTnet Broker is RUNNING!");

app.Run();
