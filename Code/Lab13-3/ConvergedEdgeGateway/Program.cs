using System.Threading.RateLimiting;
using MQTTnet.AspNetCore;
using MQTTnet.Protocol;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

if (!GatewaySecurity.ValidateSecrets(config, requireApiKey: true))
{
    return 1;
}

var httpPort = config.GetValue("Gateway:HttpPort", 5000);
var mqttPort = config.GetValue("Gateway:MqttPort", 1883);
var httpLanAccess = config.GetValue("Gateway:HttpLanAccess", false);
var requireMqttAuth = config.GetValue("Mqtt:RequireAuthentication", true);

// ตั้งค่า Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    // ค่าเริ่มต้น: เว็บ/REST เปิดเฉพาะเครื่องตัวเอง ตั้ง Gateway:HttpLanAccess=true ถ้าต้องให้เครื่องอื่นในวง LAN เปิด
    if (httpLanAccess) options.ListenAnyIP(httpPort);
    else options.ListenLocalhost(httpPort);

    // MQTT เปิดทุก IP ให้มือถือในวง LAN เชื่อมต่อได้ (ป้องกันด้วย username/password)
    options.ListenAnyIP(mqttPort, l => l.UseMqtt());
});

// ลงทะเบียน Services
builder.Services.AddSingleton(config.GetSection("Serial").Get<SerialOptions>() ?? new SerialOptions());
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddMqttConnectionHandler();
builder.Services.AddConnections();
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 4 * 1024);

// ลงทะเบียน Worker เป็น Singleton ตัวเดียว แล้วให้ Host เรียกใช้ตัวเดียวกันนั้น (API/Hub จะได้เรียกใช้ได้)
builder.Services.AddSingleton<SerialBridgeWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SerialBridgeWorker>());

// จำกัดอัตราคำสั่งควบคุม: 10 ครั้ง / 10 วินาที ต่อ IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("control", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromSeconds(10) }));
});

var app = builder.Build();

app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles(); // ให้บริการไฟล์ใน wwwroot (รวม signalr.min.js ที่เก็บไว้ในเครื่อง ใช้งานได้แม้ไม่มีอินเทอร์เน็ต)
app.UseRateLimiter();

// นับทุกคำขอที่เข้ามาทาง REST API
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch =>
    branch.Use(async (ctx, next) =>
    {
        ctx.RequestServices.GetRequiredService<ProtocolMetrics>().CountRestRequest();
        await next();
    }));

app.MapHub<GatewayHub>("/hubs/gateway");

app.UseMqttServer(server =>
{
    server.ValidatingConnectionAsync += args =>
    {
        if (string.IsNullOrEmpty(args.ClientId))
        {
            args.AssignedClientIdentifier = Guid.NewGuid().ToString("N");
        }
        if (requireMqttAuth &&
            !(GatewaySecurity.FixedEquals(args.UserName, config["Mqtt:Username"]) &&
              GatewaySecurity.FixedEquals(args.Password, config["Mqtt:Password"])))
        {
            args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            app.Logger.LogWarning("[MQTT Server] ⛔ ปฏิเสธการเชื่อมต่อ (รหัสผ่านผิด) จาก {Endpoint}", args.Endpoint);
        }
        return Task.CompletedTask;
    };

    server.InterceptingPublishAsync += args =>
    {
        if (args.ClientId == SerialBridgeWorker.GatewayClientId)
        {
            return Task.CompletedTask;   // ข้อความของเกตเวย์เอง นับไว้แล้วใน Worker
        }

        // ห้าม client ภายนอกปลอมข้อมูล telemetry (เฉพาะเกตเวย์เท่านั้นที่ publish หัวข้อนี้ได้)
        if (args.ApplicationMessage.Topic == SerialBridgeWorker.TelemetryTopic)
        {
            args.ProcessPublish = false;
            app.Logger.LogWarning("[MQTT Server] ⛔ {ClientId} พยายาม publish หัวข้อสงวน {Topic}", args.ClientId, args.ApplicationMessage.Topic);
            return Task.CompletedTask;
        }

        app.Services.GetRequiredService<ProtocolMetrics>().CountMqttMessage();
        return Task.CompletedTask;
    };
});

// 1. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get<TelemetryData>("latest_telemetry");
    return data is not null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 2. API อ่านสถิติทราฟฟิกของทั้ง 4 โปรโตคอล
app.MapGet("/api/metrics", (ProtocolMetrics metrics) => Results.Ok(metrics));

// 3. API ควบคุมหลอดไฟผ่าน Serial Bridge (ต้องมี X-Api-Key และถูกจำกัดอัตรา)
app.MapPost("/api/led/{state}", (string state, SerialBridgeWorker worker) =>
{
    string? cmd = state.ToLowerInvariant() switch
    {
        "on" => "LED:ON",
        "off" => "LED:OFF",
        _ => null
    };
    if (cmd is null)
    {
        return Results.BadRequest(new { error = "state must be 'on' or 'off'" });
    }

    bool success = worker.SendSerialCommand(cmd);
    return Results.Ok(new
    {
        dispatchedCommand = cmd,
        sentToSerial = success,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
})
.RequireApiKey()
.RequireRateLimiting("control");

app.Run();
return 0;
