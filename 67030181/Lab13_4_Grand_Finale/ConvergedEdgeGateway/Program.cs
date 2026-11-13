using ConvergedEdgeGateway;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// 1. ตั้งค่า Kestrel Web & Protocol Engine
builder.WebHost.ConfigureKestrel(options =>
{
    // พอร์ต 5000 สำหรับ HTTP RESTful APIs และ Web Dashboard
    options.ListenAnyIP(5000);

    // พอร์ต 1883 สำหรับ Embedded MQTT Broker
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

// 2. ลงทะเบียน Dependency Injection Services
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();
builder.Services.AddSingleton(sp =>
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());
builder.Services.AddSignalR();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(); // ให้บริการแดชบอร์ดใน wwwroot

// 3. ผูก Hub ของ SignalR สำหรับ Realtime WebSockets
app.MapHub<GatewayHub>("/hubs/gateway");

// 4. ผูกการทำงานของ Embedded MQTTnet Server
var mqttServer = app.Services.GetRequiredService<MqttServer>();
var stateStore = app.Services.GetRequiredService<GatewayStateStore>();
var metrics = app.Services.GetRequiredService<ProtocolMetrics>();
var hubContext = app.Services.GetRequiredService<IHubContext<GatewayHub>>();
var serialWorker = app.Services.GetRequiredService<SerialBridgeWorker>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.UseMqttServer(server =>
{
    // ตรวจสอบ Client ID เพื่อป้องกัน Connection Refused
    server.ValidatingConnectionAsync += args =>
    {
        if (string.IsNullOrEmpty(args.ClientId))
        {
            args.AssignedClientIdentifier = "Client_" + Guid.NewGuid().ToString("N")[..8];
        }
        return Task.CompletedTask;
    };

    server.ClientConnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MQTT Server] 🟢 Client เชื่อมต่อ: {args.ClientId} (จาก {args.Endpoint})");
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

    // ดักจับและประมวลผลข้อความข้ามโปรโตคอล (Multi-Protocol Message Interception)
    server.InterceptingPublishAsync += async args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();

        metrics.MqttMessages++;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[MQTT Message] 📩 หัวข้อ: {topic} | ข้อความ: {payload}");
        Console.ResetColor();

        // ตรวจจับคำสั่งสั่งการไฟ LED จาก MQTT Client (เช่น Flutter Mobile App หรือ Mosquitto CLI)
        if (topic.Equals("iot/esp32/led/cmd", StringComparison.OrdinalIgnoreCase) ||
            topic.Equals("iot/esp32/led/set", StringComparison.OrdinalIgnoreCase))
        {
            bool turnOn = payload.Equals("ON", StringComparison.OrdinalIgnoreCase) ||
                          payload.Equals("1") ||
                          payload.Equals("true", StringComparison.OrdinalIgnoreCase);

            string serialCmd = turnOn ? "LED:ON" : "LED:OFF";
            logger.LogInformation("🔄 [MQTT -> Serial & SignalR Bridge] แปลงคำสั่งจาก MQTT: {Cmd}", serialCmd);

            // ส่งไปหาบอร์ด ESP32 ทาง Serial
            serialWorker.SendSerialCommand(serialCmd);

            // อัปเดต State Store
            stateStore.Set("led_state", turnOn);

            // แจ้งเตือนไปยัง Web Dashboard ผ่าน SignalR ทันที
            metrics.SignalRPushes++;
            await hubContext.Clients.All.SendAsync("LedStateUpdated", turnOn);
        }
    };
});

// 5. RESTful Minimal API Endpoints

// 5.1 อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store, ProtocolMetrics met) =>
{
    met.RestRequests++;
    var data = store.Get("latest_telemetry");
    if (data != null) return Results.Ok(data);
    return Results.Ok(new
    {
        voltage = 0.0,
        raw = 0,
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        note = "รอข้อมูลรอบแรกจากเซนเซอร์"
    });
});

// 5.2 สั่งการ LED ข้ามโปรโตคอล (REST -> Serial, SignalR & MQTT)
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker, GatewayStateStore store, ProtocolMetrics met, IHubContext<GatewayHub> hub, MqttServer mqtt) =>
{
    met.RestRequests++;
    bool turnOn = state.ToLower() == "on" || state == "1" || state.ToLower() == "true";
    string cmd = turnOn ? "LED:ON" : "LED:OFF";

    bool success = worker.SendSerialCommand(cmd);
    store.Set("led_state", turnOn);

    // ผลักสถานะใหม่ไปหาหน้าเว็บผ่าน SignalR
    met.SignalRPushes++;
    await hub.Clients.All.SendAsync("LedStateUpdated", turnOn);

    // ผลักสถานะใหม่ไปยัง MQTT Topic เพื่อให้อุปกรณ์อื่นซิงค์
    met.MqttMessages++;
    var mqttMsg = new MqttApplicationMessageBuilder()
        .WithTopic("iot/esp32/led/status")
        .WithPayload(turnOn ? "ON" : "OFF")
        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
        .WithRetainFlag(true)
        .Build();
    await mqtt.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));

    return Results.Ok(new
    {
        dispatchedCommand = cmd,
        sentToSerial = success,
        ledState = turnOn ? "ON" : "OFF",
        serverTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        protocolBridged = new[] { "HTTP REST", "Hardware Serial", "SignalR WebSockets", "MQTT" }
    });
});

// 5.3 อ่านสถิติทราฟฟิกทั้ง 4 โปรโตคอล
app.MapGet("/api/metrics", (ProtocolMetrics met) =>
{
    met.RestRequests++;
    return Results.Ok(met);
});

// 5.4 จุดตรวจสอบสถานะระบบและความพร้อมระดับ Edge (Local Survivability Status)
app.MapGet("/api/status", (GatewayStateStore store, ProtocolMetrics met, SerialBridgeWorker worker) =>
{
    met.RestRequests++;
    return Results.Ok(new
    {
        status = "Healthy",
        gateway = "Converged Multi-Protocol Edge Gateway (.NET 8 Kestrel)",
        studentId = "67030281",
        localSurvivability = true,
        isHardwareConnected = worker.IsHardwareConnected,
        serialPort = worker.CurrentPortName,
        metrics = met,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
