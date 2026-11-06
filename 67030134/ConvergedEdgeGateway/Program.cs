using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// ตั้งค่า Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5050);
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
    // เมื่อ Client เชื่อมต่อเข้ามา ส่งสถานะ online ของอุปกรณ์ให้แอป Flutter รับทราบทันที
    server.ClientConnectedAsync += async args =>
    {
        var statusMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/status")
            .WithPayload("online")
            .WithRetainFlag(true)
            .Build();
        await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(statusMsg));
    };

    server.InterceptingPublishAsync += async args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();

        // เมื่อแอป Flutter ส่งคำสั่งควบคุมหลอดไฟเข้ามาทาง MQTT
        if (topic == "iot/esp32/control")
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                string state = doc.RootElement.GetProperty("led").GetString()?.ToUpper() ?? "OFF";
                bool isOn = state == "ON";

                // 1. ส่งคำสั่งผ่าน Serial ไปยังบอร์ด ESP32
                var worker = app.Services.GetRequiredService<SerialBridgeWorker>();
                worker.SendSerialCommand(isOn ? "LED:ON" : "LED:OFF");

                // 2. ตอบกลับ ACK ไปที่ iot/esp32/led_ack เพื่อให้แอป Flutter ปลดล็อก Waiting ACK
                var ackPayload = System.Text.Json.JsonSerializer.Serialize(new { led_state = isOn });
                var ackMsg = new MqttApplicationMessageBuilder()
                    .WithTopic("iot/esp32/led_ack")
                    .WithPayload(ackPayload)
                    .Build();
                await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));

                // 3. อัปเดตสถานะบนหน้าเว็บแดชบอร์ดผ่าน SignalR
                var hubContext = app.Services.GetRequiredService<IHubContext<GatewayHub>>();
                await hubContext.Clients.All.SendAsync("LedStateUpdated", isOn);
            }
            catch { }
        }
    };
});

// 1. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 2. API ควบคุมหลอดไฟผ่าน Serial Bridge
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker, MqttServer mqttServer, IHubContext<GatewayHub> hubContext) =>
{
    bool isOn = state.ToLower() == "on";
    string cmd = isOn ? "LED:ON" : "LED:OFF";
    bool success = worker.SendSerialCommand(cmd);

    // กระจายสถานะไปยัง SignalR (Web Dashboard)
    await hubContext.Clients.All.SendAsync("LedStateUpdated", isOn);

    // กระจายสถานะไปยัง MQTT (Flutter App)
    var ackPayload = System.Text.Json.JsonSerializer.Serialize(new { led_state = isOn });
    var ackMsg = new MqttApplicationMessageBuilder()
        .WithTopic("iot/esp32/led_ack")
        .WithPayload(ackPayload)
        .Build();
    await mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));

    return Results.Ok(new
    {
        dispatchedCommand = cmd,
        sentToSerial = success,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

app.Run();
