using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "ConvergedEdgeGateway_Lab3", "wwwroot"))
});

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
builder.Services.AddSingleton<LedController>();

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

    server.InterceptingPublishAsync += async args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();
        app.Logger.LogInformation(
            "MQTT Message: {Topic} | QoS: {QoS} | ข้อความ: {Payload}",
            topic,
            args.ApplicationMessage.QualityOfServiceLevel,
            payload);

        if (topic != "iot/esp32/led/set")
        {
            return;
        }

        var requestedState = payload.Trim().ToUpperInvariant() switch
        {
            "ON" or "LED:ON" => true,
            "OFF" or "LED:OFF" => false,
            _ => (bool?)null
        };

        if (requestedState is null)
        {
            app.Logger.LogWarning("ข้าม MQTT LED command ที่ไม่ถูกต้อง: {Payload}", payload);
            args.ProcessPublish = false;
            return;
        }

        var controller = app.Services.GetRequiredService<LedController>();
        if (!await controller.SetStateAsync(requestedState.Value))
        {
            app.Logger.LogWarning("ส่ง MQTT LED command ไป ESP32 ไม่สำเร็จ");
        }

        args.ProcessPublish = false;
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

app.MapPost("/api/led/{state}", async (string state, LedController controller, CancellationToken cancellationToken) =>
{
    bool? requestedState = state.ToLowerInvariant() switch
    {
        "on" => true,
        "off" => false,
        _ => null
    };

    if (requestedState is null)
    {
        return Results.BadRequest(new { error = "State must be 'on' or 'off'." });
    }

    if (!await controller.SetStateAsync(requestedState.Value, cancellationToken))
    {
        return Results.Problem(
            title: "Serial device unavailable",
            detail: "The command was not sent. Check the configured COM port and serial connection.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        dispatchedCommand = requestedState.Value ? "LED:ON" : "LED:OFF",
        sentToSerial = true,
        serverTime = DateTimeOffset.Now.ToString("HH:mm:ss")
    });
});

app.Run();
