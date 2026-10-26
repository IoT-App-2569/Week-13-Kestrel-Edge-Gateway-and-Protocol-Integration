using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, listenOptions => listenOptions.UseMqtt());
});

builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddSignalR();
builder.Services.AddSingleton<SerialBridgeWorker>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<SerialBridgeWorker>());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMqttServer(_ => { });
app.MapHub<GatewayHub>("/hubs/gateway");

app.MapGet("/api/telemetry/latest", (GatewayStateStore store, ProtocolMetrics metrics) =>
{
    metrics.IncrementRestRequests();
    var telemetry = store.Get<TelemetryData>("latest_telemetry");
    return telemetry is null
        ? Results.NotFound(new { error = "No telemetry yet" })
        : Results.Ok(new { telemetry.Voltage, telemetry.Raw, telemetry.Timestamp, Metrics = metrics.Snapshot() });
});

app.MapPost("/api/led/{state}", (string state, SerialBridgeWorker worker, ProtocolMetrics metrics) =>
{
    metrics.IncrementRestRequests();
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
