using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// ตั้งค่า Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 5000);
    options.Listen(IPAddress.Any, 1883, l => l.UseMqtt());
});

// ลงทะเบียน Services
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();
builder.Services.AddSingleton(sp =>
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

builder.Services.AddSignalR();
builder.Services.AddSingleton<ProtocolMetrics>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<GatewayHub>("/hubs/gateway");

app.UseMqttServer(server =>
{
    // เมื่อ Flutter สั่งเปิดปิดไฟผ่าน MQTT (iot/esp32/control)
    server.InterceptingPublishAsync += async args =>
    {
        string topic = args.ApplicationMessage.Topic;
        if (topic == "iot/esp32/control")
        {
            string payload = System.Text.Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            try
            {
                using var doc = JsonDocument.Parse(payload);
                string ledState = doc.RootElement.GetProperty("led").GetString()?.ToUpper() ?? "OFF";
                bool isOn = ledState == "ON";

                // 1. ส่งคำสั่ง Serial
                var worker = app.Services.GetRequiredService<SerialBridgeWorker>();
                worker.SendSerialCommand(isOn ? "LED:ON" : "LED:OFF");

                // 2. ซิงค์สถานะกลับไปหน้าเว็บ SignalR
                var hubContext = app.Services.GetRequiredService<IHubContext<GatewayHub>>();
                await hubContext.Clients.All.SendAsync("LedStateUpdated", isOn);

                // 3. ตอบกลับ ACK ไปที่ MQTT เพื่อให้ Flutter อัปเดตสวิตช์
                var ackMsg = new MqttApplicationMessageBuilder()
                    .WithTopic("iot/esp32/led_ack")
                    .WithPayload($"{{\"led_state\":{(isOn ? "true" : "false")}}}")
                    .Build();
                await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));
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
app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker, IHubContext<GatewayHub> hubContext, MqttServer mqttServer) =>
{
    bool isOn = state.ToLower() == "on";
    string cmd = isOn ? "LED:ON" : "LED:OFF";
    bool success = worker.SendSerialCommand(cmd);

    await hubContext.Clients.All.SendAsync("LedStateUpdated", isOn);

    var ackMsg = new MqttApplicationMessageBuilder()
        .WithTopic("iot/esp32/led_ack")
        .WithPayload($"{{\"led_state\":{(isOn ? "true" : "false")}}}")
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
