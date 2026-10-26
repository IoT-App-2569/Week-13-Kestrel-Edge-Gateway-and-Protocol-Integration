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
app.UseStaticFiles(); // ให้บริการไฟล์ใน wwwroot

app.MapHub<GatewayHub>("/hubs/gateway");

var workerInstance = app.Services.GetRequiredService<SerialBridgeWorker>();
var metricsInstance = app.Services.GetRequiredService<ProtocolMetrics>();

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

    server.ClientConnectedAsync += async args =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MQTT Server] 🟢 Client เชื่อมต่อ: {args.ClientId}");
        Console.ResetColor();
        await workerInstance.PublishDeviceOnlineStatusAsync();
    };

    server.ClientDisconnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[MQTT Server] 🔴 Client ตัดการเชื่อมต่อ: {args.ClientId}");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    server.InterceptingPublishAsync += async args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();

        // ดักจับคำสั่งควบคุมจากแอปมือถือ Flutter (iot/esp32/control) หรือ MQTT CLI
        if (topic == "iot/esp32/control" && !string.IsNullOrEmpty(payload))
        {
            metricsInstance.MqttMessages++;
            bool turnOn = false;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("led", out var ledProp))
                {
                    turnOn = ledProp.GetString()?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true;
                }
            }
            catch
            {
                turnOn = payload.Contains("ON", StringComparison.OrdinalIgnoreCase);
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[MQTT Control] 📱 รับคำสั่งจาก MQTT ({topic}): LED -> {(turnOn ? "ON" : "OFF")}");
            Console.ResetColor();

            await workerInstance.SetLedStateAsync(turnOn, "MQTT Client");
        }
    };
});

// 1. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 2. API ควบคุมหลอดไฟผ่าน Serial Bridge (ซิงโครไนซ์ทั้ง Serial, Web Dashboard และแอป Flutter)
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
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

// 3. API ปรับค่า Virtual Potentiometer สำหรับผู้ที่ไม่มีตัวต้านทานปรับค่าได้
app.MapPost("/api/sim/adc/{raw:int}", (int raw, SerialBridgeWorker worker, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    worker.SetSimulatedAdc(raw < 0 ? null : raw);
    return Results.Ok(new
    {
        mode = raw < 0 ? "Auto Sine Wave" : "Manual Fixed Value",
        simulatedRaw = raw < 0 ? (int?)null : Math.Clamp(raw, 0, 4095)
    });
});

app.Run();
