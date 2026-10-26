using System.Text.Json;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// ตั้งค่า Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

// ลงทะเบียน Services
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();
// เพื่อให้ API เรียกใช้งาน Worker ได้
builder.Services.AddSingleton(sp =>
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

builder.Services.AddSignalR();
builder.Services.AddSingleton<ProtocolMetrics>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(); // ให้บริการไฟล์ใน wwwroot (รวม lib/signalr.min.js สำหรับใช้งานแบบไม่มีอินเทอร์เน็ต)

app.MapHub<GatewayHub>("/hubs/gateway");

var metrics = app.Services.GetRequiredService<ProtocolMetrics>();

app.UseMqttServer(server =>
{
    // Client ที่ไม่ส่ง ClientId (ใช้ได้กับ MQTT v5 เท่านั้น)
    server.ValidatingConnectionAsync += args =>
    {
        if (string.IsNullOrEmpty(args.ClientId))
        {
            args.AssignedClientIdentifier = Guid.NewGuid().ToString("N");
        }
        return Task.CompletedTask;
    };

    server.ClientConnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MQTT Server] 🟢 Client เชื่อมต่อ: {args.ClientId} (จาก Endpoint: {args.Endpoint})");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    server.ClientDisconnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[MQTT Server] 🔴 Client ตัดการเชื่อมต่อ: {args.ClientId}");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    // ดักทุกข้อความ: นับสถิติ MQTT และรับคำสั่ง LED จากแอป Flutter
    server.InterceptingPublishAsync += args =>
    {
        metrics.MqttMessages++;
        var topic = args.ApplicationMessage.Topic;
        if (topic != SerialBridgeWorker.TopicControl)
        {
            return Task.CompletedTask;
        }

        var payload = args.ApplicationMessage.ConvertPayloadToString() ?? "";
        bool? on = null;
        try
        {
            // รูปแบบจากแอป Flutter สัปดาห์ที่ 12: {"led": "ON"} / {"led": "OFF"}
            using var doc = JsonDocument.Parse(payload);
            var led = doc.RootElement.GetProperty("led");
            on = led.ValueKind == JsonValueKind.String
                ? led.GetString()!.Equals("ON", StringComparison.OrdinalIgnoreCase)
                : led.GetBoolean();
        }
        catch
        {
            // รองรับข้อความธรรมดา "ON" / "OFF"
            if (payload.Trim().Equals("ON", StringComparison.OrdinalIgnoreCase)) on = true;
            else if (payload.Trim().Equals("OFF", StringComparison.OrdinalIgnoreCase)) on = false;
        }

        if (on.HasValue)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[MQTT Message] 📩 คำสั่งจาก {args.ClientId}: {payload}");
            Console.ResetColor();
            var worker = app.Services.GetRequiredService<SerialBridgeWorker>();
            _ = Task.Run(() => worker.SetLedAsync(on.Value, "MQTT"));
        }
        return Task.CompletedTask;
    };
});

// 1. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    metrics.RestRequests++;
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 2. API ควบคุมหลอดไฟผ่าน Serial Bridge
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker) =>
{
    metrics.RestRequests++;
    bool on = state.ToLower() == "on";
    var (sent, ledState) = await worker.SetLedAsync(on, "REST");

    return Results.Ok(new
    {
        dispatchedCommand = on ? "LED:ON" : "LED:OFF",
        sentToSerial = sent,
        ledState = ledState ? "ON" : "OFF",
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

app.Run();
