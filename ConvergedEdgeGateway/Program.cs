using System.Text.Json;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// 5000 = HTTP/เว็บ/SignalR, 1883 = MQTT
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddSignalR();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();
builder.Services.AddSingleton(sp =>
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var worker = app.Services.GetRequiredService<SerialBridgeWorker>();

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

    server.InterceptingPublishAsync += args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();

        // ไม่พิมพ์ telemetry ทุกครึ่งวินาที กันจอรก
        if (topic != SerialBridgeWorker.TopicTelemetry)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[MQTT Message] 📩 หัวข้อ: {topic} | ข้อความ: {payload}");
            Console.ResetColor();
        }

        // 13.4: แอป Flutter สั่งไฟผ่าน MQTT → ส่งเข้าศูนย์กลาง
        if (topic == SerialBridgeWorker.TopicControl)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                bool on = doc.RootElement.GetProperty("led").GetString()?.ToUpper() == "ON";
                _ = Task.Run(() => worker.SetLedAsync(on, "Flutter (MQTT)"));
            }
            catch { /* payload ผิดรูปแบบ */ }
        }
        return Task.CompletedTask;
    };
});

app.MapHub<GatewayHub>("/hubs/gateway");

app.MapGet("/api/telemetry/latest", (GatewayStateStore store, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// REST สั่งไฟ → ส่งเข้าศูนย์กลาง (แอปและเว็บอัปเดตตาม)
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker w, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    bool on = state.ToLower() == "on";
    bool success = await w.SetLedAsync(on, "REST API");
    return Results.Ok(new
    {
        dispatchedCommand = on ? "LED:ON" : "LED:OFF",
        sentToSerial = success,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

app.Run();