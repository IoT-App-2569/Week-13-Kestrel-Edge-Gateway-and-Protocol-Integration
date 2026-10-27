using System.Text;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

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
    server.ClientConnectedAsync += async args =>
    {
        var statusMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/status")
            .WithPayload("online")
            .Build();
        await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(statusMsg));
    };

    server.InterceptingPublishAsync += async args =>
    {
        if (args.ApplicationMessage.Topic == "iot/esp32/control")
        {
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            bool state = payload.Contains("ON");
            var worker = app.Services.GetRequiredService<SerialBridgeWorker>();
            worker.SendSerialCommand(state ? "LED:ON" : "LED:OFF");

            var hubContext = app.Services.GetRequiredService<IHubContext<GatewayHub>>();
            await hubContext.Clients.All.SendAsync("LedStateUpdated", state);

            var ackMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/led_ack")
                .WithPayload($"{{\"led_state\":{state.ToString().ToLower()}}}")
                .Build();
            await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(ackMsg));
        }
    };
});

app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

app.MapPost("/api/led/{state}", async (string state, SerialBridgeWorker worker, IHubContext<GatewayHub> hubContext, MqttServer mqttServer) =>
{
    bool isOn = state.ToLower() == "on";
    string cmd = isOn ? "LED:ON" : "LED:OFF";
    bool success = worker.SendSerialCommand(cmd);

    await hubContext.Clients.All.SendAsync("LedStateUpdated", isOn);

    var ackMsg = new MqttApplicationMessageBuilder()
        .WithTopic("iot/esp32/led_ack")
        .WithPayload($"{{\"led_state\":{isOn.ToString().ToLower()}}}")
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
