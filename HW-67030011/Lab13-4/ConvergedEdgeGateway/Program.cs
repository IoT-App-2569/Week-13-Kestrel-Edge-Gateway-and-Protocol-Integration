using System.Text.Json;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// ตั้งค่า Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    // macOS: พอร์ต 5000 ถูก AirPlay Receiver (ControlCenter) จองไว้ จึงใช้ 5080 แทน
    options.ListenAnyIP(5080);
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
builder.Services.AddSingleton<LedController>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(); // ให้บริการไฟล์ใน wwwroot

app.MapHub<GatewayHub>("/hubs/gateway");

app.UseMqttServer(server =>
{
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

    // คำสั่งจากแอป Flutter: iot/esp32/control {"led":"ON"|"OFF"} → Serial + กระจายสถานะ
    server.InterceptingPublishAsync += args =>
    {
        if (args.ApplicationMessage.Topic != "iot/esp32/control") return Task.CompletedTask;

        var payload = args.ApplicationMessage.ConvertPayloadToString();
        var metrics = app.Services.GetRequiredService<ProtocolMetrics>();
        metrics.CountMqtt();
        try
        {
            using var doc = JsonDocument.Parse(payload);
            bool on = string.Equals(doc.RootElement.GetProperty("led").GetString(), "ON", StringComparison.OrdinalIgnoreCase);
            var led = app.Services.GetRequiredService<LedController>();
            var source = $"MQTT ({args.ClientId})";
            // ไม่ await ใน Callback ของ Broker: SetAsync จะ Inject ข้อความ ACK กลับเข้า Broker ตัวเดียวกัน
            _ = Task.Run(() => led.SetAsync(on, source));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT Control] ⛔ Payload ไม่ถูกต้อง: {payload} ({ex.Message})");
        }
        return Task.CompletedTask;
    };
});

// 1. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store, ProtocolMetrics metrics) =>
{
    metrics.CountRest();
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 2. API ควบคุมหลอดไฟผ่าน Serial Bridge (Third-Party REST Override)
app.MapPost("/api/led/{state}", async (string state, LedController led, ProtocolMetrics metrics) =>
{
    metrics.CountRest();
    bool on = state.ToLower() == "on";
    bool success = await led.SetAsync(on, "REST");

    return Results.Ok(new
    {
        dispatchedCommand = on ? "LED:ON" : "LED:OFF",
        sentToSerial = success,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

// 3. API อ่านสถานะ LED ปัจจุบัน
app.MapGet("/api/led", (LedController led, ProtocolMetrics metrics) =>
{
    metrics.CountRest();
    return Results.Ok(new { ledState = led.IsOn });
});

app.Run();
