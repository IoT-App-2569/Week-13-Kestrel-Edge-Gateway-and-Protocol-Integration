using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, listenOptions => listenOptions.UseMqtt());
});

builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddSingleton<SerialBridgeWorker>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<SerialBridgeWorker>());
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddSignalR();

var app = builder.Build();
var worker = app.Services.GetRequiredService<SerialBridgeWorker>();
var metrics = app.Services.GetRequiredService<ProtocolMetrics>();
var state = app.Services.GetRequiredService<GatewayStateStore>();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMqttServer(server =>
{
    server.InterceptingPublishAsync += async args =>
    {
        if (args.ApplicationMessage.Topic != "iot/esp32/led/set")
        {
            return;
        }

        var command = args.ApplicationMessage.ConvertPayloadToString().Trim().ToUpperInvariant();
        if (command is not ("ON" or "OFF"))
        {
            return;
        }

        var isOn = command == "ON";
        worker.SendSerialCommand(isOn ? "LED:ON" : "LED:OFF");
        state.Set("led_state", isOn);
        metrics.IncrementMqttCommands();
        await app.Services.GetRequiredService<IHubContext<GatewayHub>>()
            .Clients.All.SendAsync("LedStateUpdated", isOn);
        await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(
            new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/led/state")
                .WithPayload(isOn ? "ON" : "OFF")
                .Build()));
    };
});

app.MapHub<GatewayHub>("/hubs/gateway");

app.MapGet("/api/telemetry/latest", (GatewayStateStore store, ProtocolMetrics protocolMetrics) =>
{
    protocolMetrics.IncrementRestRequests();
    var telemetry = store.Get<TelemetryData>("latest_telemetry");
    return telemetry is null
        ? Results.NotFound(new { error = "No telemetry yet" })
        : Results.Ok(new { telemetry.Voltage, telemetry.Raw, telemetry.Timestamp });
});

app.MapPost("/api/led/{state}", async (
    string state,
    SerialBridgeWorker serialWorker,
    GatewayStateStore gatewayState,
    ProtocolMetrics protocolMetrics,
    IHubContext<GatewayHub> hubContext,
    MqttServer mqttServer) =>
{
    protocolMetrics.IncrementRestRequests();
    var command = state.ToLowerInvariant() switch
    {
        "on" => ("LED:ON", true),
        "off" => ("LED:OFF", false),
        _ => (string.Empty, false)
    };
    if (command.Item1.Length == 0)
    {
        return Results.BadRequest(new { error = "State must be 'on' or 'off'." });
    }

    var sent = serialWorker.SendSerialCommand(command.Item1);
    gatewayState.Set("led_state", command.Item2);
    await hubContext.Clients.All.SendAsync("LedStateUpdated", command.Item2);
    await mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(
        new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/led/state")
            .WithPayload(command.Item2 ? "ON" : "OFF")
            .Build()));

    return Results.Ok(new
    {
        dispatchedCommand = command.Item1,
        sentToSerial = sent,
        serverTime = DateTimeOffset.Now
    });
});

app.Run();
