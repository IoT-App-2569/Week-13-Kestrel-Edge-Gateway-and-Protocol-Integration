using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, listenOptions => listenOptions.UseMqtt());
});

builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

var app = builder.Build();

app.UseMqttServer(_ => { });

app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var telemetry = store.Get<TelemetryData>("latest_telemetry");
    return telemetry is null
        ? Results.NotFound(new { error = "No telemetry yet" })
        : Results.Ok(telemetry);
});

app.MapPost("/api/led/{state}", (string state, SerialBridgeWorker worker) =>
{
    var command = state.ToLowerInvariant() switch
    {
        "on" => "LED:ON",
        "off" => "LED:OFF",
        _ => null
    };

    if (command is null)
    {
        return Results.BadRequest(new { error = "State must be 'on' or 'off'." });
    }

    return Results.Ok(new
    {
        dispatchedCommand = command,
        sentToSerial = worker.SendSerialCommand(command),
        serverTime = DateTimeOffset.Now
    });
});

app.Run();
