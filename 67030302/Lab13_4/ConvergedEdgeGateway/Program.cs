using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// 1. ตั้งค่า Kestrel รับการเชื่อมต่อทั้ง HTTP/SignalR (5001) และ MQTT (1883)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001);
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

// 2. ลงทะเบียน Services
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddSignalR();
builder.Services.AddSingleton<StateSyncCoordinator>();
builder.Services.AddSingleton<SerialBridgeWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SerialBridgeWorker>());

var app = builder.Build();

// เชื่อมโยง Serial Worker เข้ากับ StateSyncCoordinator
var coordinator = app.Services.GetRequiredService<StateSyncCoordinator>();
var serialWorker = app.Services.GetRequiredService<SerialBridgeWorker>();
coordinator.RegisterSerialWorker(serialWorker);

app.UseDefaultFiles();
app.UseStaticFiles(); // ให้บริการไฟล์แดชบอร์ดใน wwwroot

app.MapHub<GatewayHub>("/hubs/gateway");

// 3. ผูก Event Handlers บน Embedded MQTT Broker (รองรับ Flutter Mobile App)
app.UseMqttServer(server =>
{
    // Auto-assign ClientId หาก Client ไม่ได้ระบุมา เพื่อป้องกัน Connection Refused
    server.ValidatingConnectionAsync += args =>
    {
        if (string.IsNullOrEmpty(args.ClientId))
        {
            args.AssignedClientIdentifier = "kestrel-client-" + Guid.NewGuid().ToString("N")[..8];
        }
        return Task.CompletedTask;
    };

    server.ClientConnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MQTT Server] 🟢 Client เชื่อมต่อ: {args.ClientId}");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    // ดักฟังข้อความขาเข้าจากแอป Flutter (Mobile-to-Hardware & Mobile-to-Web)
    server.InterceptingPublishAsync += async args =>
    {
        var topic = args.ApplicationMessage.Topic;
        if (topic == "iot/esp32/control")
        {
            var payload = args.ApplicationMessage.ConvertPayloadToString();
            bool turnOn = payload.Contains("\"ON\"", StringComparison.OrdinalIgnoreCase) ||
                          payload.Contains(":true", StringComparison.OrdinalIgnoreCase) ||
                          payload.Contains(": true", StringComparison.OrdinalIgnoreCase);

            await coordinator.SetLedStateAsync(turnOn, "MQTT Broker (Mobile App)");
        }
    };
});

// 4. REST APIs
// 4.1 อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 4.2 ควบคุมหลอดไฟผ่าน REST API (Third-Party REST Override)
app.MapPost("/api/led/{state}", async (string state, StateSyncCoordinator coord, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    bool turnOn = state.Equals("on", StringComparison.OrdinalIgnoreCase);
    await coord.SetLedStateAsync(turnOn, "REST API (cURL/Postman)");

    return Results.Ok(new
    {
        dispatchedCommand = turnOn ? "LED:ON" : "LED:OFF",
        ledState = turnOn ? "ON" : "OFF",
        source = "REST API Override",
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

// 4.3 ตรวจสอบสถานะความสมบูรณ์และสถิติทราฟฟิกของเกตเวย์
app.MapGet("/api/gateway/health", (GatewayStateStore store, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    return Results.Ok(new
    {
        gateway = "Kestrel Converged Edge Gateway (.NET 8/10)",
        studentId = "67030302",
        status = "Healthy",
        localSurvivability = true,
        uptimeSeconds = Environment.TickCount64 / 1000,
        activeProtocols = new[] { "Serial (UART)", "MQTT (1883)", "REST (5001)", "SignalR WebSockets (5001)" },
        metrics = metrics,
        ledState = store.Get<bool?>("led_state") ?? false
    });
});

app.Run();
