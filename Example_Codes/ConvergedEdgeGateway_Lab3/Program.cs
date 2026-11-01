using MQTTnet;
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

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.RequestServices.GetRequiredService<ProtocolMetrics>().RecordRestRequest();
    }

    await next(context);
});

app.UseDefaultFiles();
app.UseStaticFiles();
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

    server.ClientConnectedAsync += args =>
    {
        app.Logger.LogInformation("MQTT Client เชื่อมต่อ: {ClientId}", args.ClientId);
        return Task.CompletedTask;
    };

    server.ClientDisconnectedAsync += args =>
    {
        app.Logger.LogInformation("MQTT Client ตัดการเชื่อมต่อ: {ClientId}", args.ClientId);
        return Task.CompletedTask;
    };

    server.InterceptingPublishAsync += args =>
    {
        app.Logger.LogInformation(
            "MQTT Message: {Topic} | QoS: {QoS} | ข้อความ: {Payload}",
            args.ApplicationMessage.Topic,
            args.ApplicationMessage.QualityOfServiceLevel,
            args.ApplicationMessage.ConvertPayloadToString());
        return Task.CompletedTask;
    };
});

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var telemetry = store.LatestTelemetry;
    return telemetry is null
        ? Results.NotFound(new { error = "No telemetry received from the serial device yet." })
        : Results.Ok(telemetry);
});

app.MapPost("/api/led/{state}", (string state, SerialBridgeWorker worker) =>
{
    string? command = state.ToLowerInvariant() switch
    {
        "on" => "LED:ON",
        "off" => "LED:OFF",
        _ => null
    };

    if (command is null)
    {
        return Results.BadRequest(new { error = "State must be 'on' or 'off'." });
    }

    if (!worker.SendSerialCommand(command))
    {
        return Results.Problem(
            title: "Serial device unavailable",
            detail: "The command was not sent. Check the configured COM port and serial connection.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        dispatchedCommand = command,
        sentToSerial = true,
        serverTime = DateTimeOffset.Now.ToString("HH:mm:ss")
    });
});

app.Run();
