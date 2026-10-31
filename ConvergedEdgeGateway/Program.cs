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
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.RequestServices.GetRequiredService<ProtocolMetrics>().RecordRestRequest();
    }

    await next();
});

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

    server.InterceptingPublishAsync += async args =>
    {
        // ข้ามข้อความที่ Gateway เป็นคน Inject เอง เพื่อป้องกัน Loop
        if (args.ClientId == "KestrelGateway") return;

        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();

        if (topic == "iot/esp32/control" && !string.IsNullOrEmpty(payload))
        {
            var worker = app.Services.GetRequiredService<SerialBridgeWorker>();
            var metrics = app.Services.GetRequiredService<ProtocolMetrics>();
            metrics.RecordMqttMessage();

            bool turnOn = false;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("led", out var ledProp))
                {
                    turnOn = string.Equals(ledProp.GetString(), "ON", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                turnOn = payload.Contains("ON", StringComparison.OrdinalIgnoreCase);
            }

            await worker.SetLedStateAsync(turnOn, $"MQTT ({args.ClientId})");
        }
    };
});

// 1. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 2. API ควบคุมหลอดไฟผ่าน Serial Bridge (ซิงค์ทั้ง Serial, Web SignalR และ Flutter MQTT)
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker) =>
{
    bool isOn = state.Equals("on", StringComparison.OrdinalIgnoreCase);
    string cmd = isOn ? "LED:ON" : "LED:OFF";
    bool success = await worker.SetLedStateAsync(isOn, "REST API");

    return Results.Ok(new
    {
        dispatchedCommand = cmd,
        sentToSerial = success,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

app.Run();