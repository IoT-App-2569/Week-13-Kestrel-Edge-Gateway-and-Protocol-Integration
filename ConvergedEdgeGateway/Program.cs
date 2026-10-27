using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5050);   // 5050 แทน 5000 (ชน AirPlay บน macOS)
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();
builder.Services.AddSingleton(sp =>
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<GatewayHub>("/hubs/gateway");

app.UseMqttServer(server =>
{
    server.ValidatingConnectionAsync += args =>
    {
        if (string.IsNullOrEmpty(args.ClientId))
            args.AssignedClientIdentifier = Guid.NewGuid().ToString("N");
        return Task.CompletedTask;
    };

    server.ClientConnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MQTT Server] 🟢 Client เชื่อมต่อ: {args.ClientId}");
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

    server.InterceptingPublishAsync += args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString() ?? "";

        // ไม่พิมพ์ telemetry/status ที่ Gateway ส่งเองทุกครึ่งวินาที เพื่อให้ log อ่านง่าย
        if (topic != "iot/esp32/telemetry" && topic != "iot/esp32/status")
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[MQTT Message] 📩 {topic} | {payload}");
            Console.ResetColor();
        }

        // คำสั่งเปิด-ปิดไฟจากแอป Flutter: {"led":"ON"} / {"led":"OFF"}
        if (topic == "iot/esp32/control")
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                bool on = doc.RootElement.GetProperty("led").GetString()?.ToUpper() == "ON";
                var worker = app.Services.GetRequiredService<SerialBridgeWorker>();
                _ = worker.SetLedAsync(on);
            }
            catch { /* payload ไม่ใช่ JSON ที่ต้องการ */ }
        }

        return Task.CompletedTask;
    };
});

app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker) =>
{
    bool on = state.ToLower() == "on";
    bool sent = await worker.SetLedAsync(on);
    return Results.Ok(new
    {
        dispatchedCommand = on ? "LED:ON" : "LED:OFF",
        sentToSerial = sent,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

app.Run();