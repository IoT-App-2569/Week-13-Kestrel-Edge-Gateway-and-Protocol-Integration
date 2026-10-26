using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// 1. กำหนดค่า Kestrel ให้เปิดพอร์ต 5000 (HTTP/SignalR) และ 1883 (MQTT)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

// 2. ลงทะเบียนบริการต่างๆ (Dependency Injection)
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();

// ลงทะเบียน Worker เป็น Singleton เพื่อให้ REST API และส่วนควบคุมสามารถดึงไปใช้งานได้
builder.Services.AddSingleton(sp =>
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

builder.Services.AddSignalR();

var app = builder.Build();

// 3. กำหนดค่าให้บริการ Static Files สำหรับเว็บแดชบอร์ด
app.UseDefaultFiles();
app.UseStaticFiles();

// 4. แมป SignalR Hub
app.MapHub<GatewayHub>("/hubs/gateway");

// 5. ตั้งค่า MQTT Server Event Handlers สำหรับจัดการการเชื่อมต่อและการดักจับข้อความ
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
        app.Logger.LogInformation("🟢 [MQTT Client Connected]: {ClientId} (Endpoint: {Endpoint})", args.ClientId, args.Endpoint);
        
        // แจ้งเตือนแอป Flutter ทันทีว่า ESP32 อยู่ในสถานะ Online
        var statusMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/status")
            .WithPayload("online")
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
            .Build();
        await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(statusMsg));
    };

    server.ClientDisconnectedAsync += args =>
    {
        app.Logger.LogInformation("🔴 [MQTT Client Disconnected]: {ClientId}", args.ClientId);
        return Task.CompletedTask;
    };

    // ดักฟังข้อความจาก MQTT (เช่น แอป Flutter สั่งเปิด-ปิดไฟผ่าน MQTT)
    server.InterceptingPublishAsync += async args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();

        // ตรวจจับคำสั่งสั่งการควบคุม LED จากภายนอก (ไม่ดักฟังข้อความ ACK ของตัวเอง)
        if (topic == "iot/esp32/control" || topic == "iot/led/set" || topic == "iot/led/command")
        {
            var worker = app.Services.GetRequiredService<SerialBridgeWorker>();
            var hubContext = app.Services.GetRequiredService<IHubContext<GatewayHub>>();
            var metrics = app.Services.GetRequiredService<ProtocolMetrics>();

            metrics.MqttMessages++;

            bool turnOn = payload.Contains("ON", StringComparison.OrdinalIgnoreCase) || 
                          payload.Contains("true", StringComparison.OrdinalIgnoreCase) ||
                          payload.Trim() == "1";

            string cmd = turnOn ? "LED:ON" : "LED:OFF";
            worker.SendSerialCommand(cmd);

            // ซิงค์สถานะใหม่ขึ้น Web Dashboard ทันทีผ่าน SignalR
            metrics.SignalRPushes++;
            await hubContext.Clients.All.SendAsync("LedStateUpdated", turnOn);

            // ส่ง ACK กลับไปยังแอป Flutter ที่หัวข้อ iot/esp32/led_ack
            var ackMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/led_ack")
                .WithPayload(JsonSerializer.Serialize(new { led_state = turnOn }))
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();
            await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));

            app.Logger.LogInformation("📱 รับคำสั่งสั่งการจาก MQTT [{Topic}]: {Payload} -> ซิงค์ Serial, SignalR และตอบ ACK สำเร็จ", topic, payload);
        }
    };
});

// 6. REST API: ดึงข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry available yet" });
});

// 7. REST API: ควบคุมหลอดไฟผ่าน HTTP (Third-Party REST Override)
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker, IHubContext<GatewayHub> hubContext, ProtocolMetrics metrics, MqttServer mqttServer) =>
{
    metrics.RestRequests++;
    bool turnOn = state.ToLower() == "on" || state == "1" || state.ToLower() == "true";
    string cmd = turnOn ? "LED:ON" : "LED:OFF";

    // ส่งคำสั่งเข้า Serial ไปยัง ESP32
    bool success = worker.SendSerialCommand(cmd);

    // ซิงค์สถานะไปยัง Web Dashboard ผ่าน SignalR
    metrics.SignalRPushes++;
    await hubContext.Clients.All.SendAsync("LedStateUpdated", turnOn);

    // กระจายข้อความ ACK เข้าสู่ MQTT Broker เพื่อให้แอป Flutter ซิงค์ตามทันที
    metrics.MqttMessages++;
    var ackMsg = new MqttApplicationMessageBuilder()
        .WithTopic("iot/esp32/led_ack")
        .WithPayload(JsonSerializer.Serialize(new { led_state = turnOn }))
        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
        .Build();
    await mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));

    return Results.Ok(new
    {
        dispatchedCommand = cmd,
        sentToSerial = success,
        syncedToWeb = true,
        syncedToMqtt = true,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

app.Run();
