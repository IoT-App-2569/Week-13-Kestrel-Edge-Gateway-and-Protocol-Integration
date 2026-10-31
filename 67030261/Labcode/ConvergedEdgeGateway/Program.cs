using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;
using System.Text.Json;

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

// 2. ลงทะเบียน Services
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddMqttServer(options =>
{
    options.WithDefaultEndpoint();
});
builder.Services.AddHostedService<SerialBridgeWorker>();
// เพื่อให้ API เรียกใช้งาน Worker ได้
builder.Services.AddSingleton(sp =>
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(); // ให้บริการไฟล์ใน wwwroot

app.MapHub<GatewayHub>("/hubs/gateway");

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

    // รับคำสั่งควบคุมไฟจากแอป Flutter: iot/esp32/control  payload {"led":"ON"|"OFF"}
    // แล้วส่งต่อเข้า Serial (ผ่าน SetLedAsync) พร้อม publish led_ack และผลัก SignalR
    var ledWorker = app.Services.GetRequiredService<SerialBridgeWorker>();
    server.InterceptingPublishAsync += args =>
    {
        if (args.ApplicationMessage.Topic != "iot/esp32/control")
            return Task.CompletedTask;

        try
        {
            using var doc = JsonDocument.Parse(args.ApplicationMessage.ConvertPayloadToString());
            string? led = doc.RootElement.GetProperty("led").GetString();
            bool on = string.Equals(led, "ON", StringComparison.OrdinalIgnoreCase);
            _ = Task.Run(() => ledWorker.SetLedAsync(on, "MQTT"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT Control] payload ไม่ถูกต้อง: {ex.Message}");
        }

        return Task.CompletedTask;
    };
});

// 4. จุดทดสอบ REST Endpoint เบื้องต้น (ย้ายจาก "/" เพื่อให้ "/" เปิดหน้า wwwroot/index.html)
app.MapGet("/health", () => "🚀 Kestrel Converged Edge Gateway with Embedded MQTTnet Broker is RUNNING!");

// 5. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 6. API ควบคุมหลอดไฟผ่าน Serial Bridge
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker) =>
{
    bool on = state.ToLower() == "on";
    string cmd = on ? "LED:ON" : "LED:OFF";
    bool success = await worker.SetLedAsync(on, "REST");

    return Results.Ok(new
    {
        dispatchedCommand = cmd,
        sentToSerial = success,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

app.Run();
