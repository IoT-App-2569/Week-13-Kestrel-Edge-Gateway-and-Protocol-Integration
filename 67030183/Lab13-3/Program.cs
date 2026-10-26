using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// ตั้งค่า Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001);
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
        Console.WriteLine($"[MQTT Server] 🟢 Client เชื่อมต่อ: {args.ClientId} (จาก Endpoint: {args.Endpoint})");
        Console.ResetColor();

        // ส่ง status "online" ให้ Flutter รู้ว่า ESP32 ออนไลน์ทันทีที่เชื่อมต่อ
        var statusMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/status")
            .WithPayload("online")
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(statusMsg));
    };

    server.InterceptingPublishAsync += async args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();

        // กรองคำสั่งจาก Flutter ผ่าน MQTT (iot/esp32/control)
        if (topic == "iot/esp32/control")
        {
            var metrics = app.Services.GetRequiredService<ProtocolMetrics>();
            metrics.MqttMessages++;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[MQTT Control] 📱 สั่งการจาก Flutter ผ่าน MQTT: {payload}");
            Console.ResetColor();

            bool isLedOn = payload.Contains("ON");
            string cmd = isLedOn ? "LED:ON" : "LED:OFF";

            var worker = app.Services.GetRequiredService<SerialBridgeWorker>();
            worker.SendSerialCommand(cmd);

            // ซิงก์สถานะหาหน้าเว็บผ่าน SignalR
            var hubContext = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<GatewayHub>>();
            metrics.SignalRPushes++;
            await hubContext.Clients.All.SendAsync("LedStateUpdated", isLedOn);

            // ส่ง ACK กลับหา Flutter
            var ackPayload = $"{{\"led_state\": {(isLedOn ? "true" : "false")}}}";
            var ackMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/led_ack")
                .WithPayload(ackPayload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));
        }
    };
});

// 1. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 2. API ควบคุมหลอดไฟผ่าน Serial Bridge และซิงก์ครบทุกโปรโตคอล
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker, Microsoft.AspNetCore.SignalR.IHubContext<GatewayHub> hub, MqttServer mqtt, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    bool isLedOn = state.ToLower() == "on";
    string cmd = isLedOn ? "LED:ON" : "LED:OFF";
    bool success = worker.SendSerialCommand(cmd);

    // 1. ซิงก์หาหน้าเว็บ SignalR
    metrics.SignalRPushes++;
    await hub.Clients.All.SendAsync("LedStateUpdated", isLedOn);

    // 2. ซิงก์หา Flutter ผ่าน MQTT
    metrics.MqttMessages++;
    var ackPayload = $"{{\"led_state\": {(isLedOn ? "true" : "false")}}}";
    var ackMsg = new MqttApplicationMessageBuilder()
        .WithTopic("iot/esp32/led_ack")
        .WithPayload(ackPayload)
        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();
    await mqtt.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));

    return Results.Ok(new
    {
        dispatchedCommand = cmd,
        sentToSerial = success,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

app.Run();