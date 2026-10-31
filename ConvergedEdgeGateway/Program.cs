using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// 1. ตั้งค่า Kestrel เปิดพอร์ต 5000 (HTTP) และ 1883 (MQTT)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

// 2. ลงทะเบียน Services
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddSignalR();
builder.Services.AddHostedService<SerialBridgeWorker>();

builder.Services.AddSingleton(sp =>
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

var app = builder.Build();

// 3. ตั้งค่า Static Files สำหรับให้บริการเว็บแดชบอร์ดใน wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// 4. ตั้งค่า Embedded MQTT Server
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
});

// 5. แมป SignalR Hub Endpoint
app.MapHub<GatewayHub>("/hubs/gateway");

// 6. REST API Endpoints
app.MapGet("/api/telemetry/latest", (GatewayStateStore store, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

app.MapPost("/api/led/{state}", (string state, SerialBridgeWorker worker, ProtocolMetrics metrics) =>
{
    metrics.RestRequests++;
    string cmd = state.ToLower() == "on" ? "LED:ON" : "LED:OFF";
    bool success = worker.SendSerialCommand(cmd);

    return Results.Ok(new
    {
        dispatchedCommand = cmd,
        sentToSerial = success,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

app.Run();