using System.Text.Json;
using System.Threading.RateLimiting;
using MQTTnet;
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

    // MQTT เปิดทุก IP ให้มือถือ (Flutter) ในวง LAN เชื่อมต่อได้ (ป้องกันด้วย username/password)
    options.ListenAnyIP(mqttPort, l => l.UseMqtt());
});

// ลงทะเบียน Services
builder.Services.AddSingleton(config.GetSection("Serial").Get<SerialOptions>() ?? new SerialOptions());
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<ProtocolMetrics>();
builder.Services.AddSingleton<GatewayPublisher>();
builder.Services.AddSingleton<LedController>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddMqttConnectionHandler();
builder.Services.AddConnections();
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 4 * 1024);

// ลงทะเบียน Worker เป็น Singleton ตัวเดียว แล้วให้ Host เรียกใช้ตัวเดียวกันนั้น
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

var metrics = app.Services.GetRequiredService<ProtocolMetrics>();
var led = app.Services.GetRequiredService<LedController>();   // สร้างตั้งแต่เริ่ม เพื่อให้ซิงก์สถานะ LED จาก telemetry ได้ทันที

app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles(); // ให้บริการไฟล์ใน wwwroot (รวม signalr.min.js ที่เก็บไว้ในเครื่อง ใช้งานได้แม้ไม่มีอินเทอร์เน็ต)
app.UseRateLimiter();

// นับทุกคำขอที่เข้ามาทาง REST API
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch =>
    branch.Use(async (ctx, next) =>
    {
        metrics.CountRestRequest();
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

    server.ClientConnectedAsync += args =>
    {
        app.Logger.LogInformation("[MQTT Server] 🟢 Client เชื่อมต่อ: {ClientId} (จาก {Endpoint})", args.ClientId, args.Endpoint);
        return Task.CompletedTask;
    };

    server.ClientDisconnectedAsync += args =>
    {
        app.Logger.LogInformation("[MQTT Server] 🔴 Client ตัดการเชื่อมต่อ: {ClientId}", args.ClientId);
        return Task.CompletedTask;
    };

    server.InterceptingPublishAsync += args =>
    {
        if (args.ClientId == GatewayPublisher.GatewayClientId)
        {
            return Task.CompletedTask;   // ข้อความของเกตเวย์เอง นับไว้แล้ว
        }

        var msg = args.ApplicationMessage;

        // ห้าม client ภายนอกปลอมข้อมูลในหัวข้อของเกตเวย์ (telemetry / status / led_ack)
        if (Topics.GatewayOwned.Contains(msg.Topic))
        {
            args.ProcessPublish = false;
            app.Logger.LogWarning("[MQTT Server] ⛔ {ClientId} พยายาม publish หัวข้อสงวน {Topic}", args.ClientId, msg.Topic);
            return Task.CompletedTask;
        }

        metrics.CountMqttMessage();

        // คำสั่งจากแอปมือถือ: {"led":"ON"} หรือ {"led":"OFF"}
        if (msg.Topic == Topics.Control)
        {
            if (TryParseLedCommand(msg.ConvertPayloadToString(), out var on))
            {
                // ทำงานนอก callback ของ broker เพื่อไม่ให้การ publish led_ack ไปบล็อกการรับข้อความ
                _ = Task.Run(() => led.SetAsync(on, $"MQTT ({args.ClientId})"));
            }
            else
            {
                args.ProcessPublish = false;
                app.Logger.LogWarning("[MQTT Server] ⛔ payload ของ {Topic} ไม่ถูกต้อง", msg.Topic);
            }
        }
        return Task.CompletedTask;
    };
});

// 1. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get<TelemetryData>("latest_telemetry");
    return data is not null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 2. API อ่านสถานะระบบและสถิติทราฟฟิกของทั้ง 4 โปรโตคอล
app.MapGet("/api/led", () => Results.Ok(new { ledState = led.IsOn }));
app.MapGet("/api/metrics", () => Results.Ok(metrics));
app.MapGet("/api/health", async (SerialBridgeWorker serial, MqttServer mqtt) => Results.Ok(new
{
    serialConnected = serial.IsConnected,
    mqttClients = (await mqtt.GetClientsAsync()).Count,
    serverTime = DateTimeOffset.Now
}));

// 3. API ควบคุมหลอดไฟ (ต้องมี X-Api-Key และถูกจำกัดอัตรา) -> ทั้งเว็บและมือถืออัปเดตพร้อมกัน
app.MapPost("/api/led/{state}", async (string state) =>
{
    bool? on = state.ToLowerInvariant() switch
    {
        "on" => true,
        "off" => false,
        _ => null
    };
    if (on is null)
    {
        return Results.BadRequest(new { error = "state must be 'on' or 'off'" });
    }

    bool success = await led.SetAsync(on.Value, "REST");
    return Results.Ok(new
    {
        dispatchedCommand = on.Value ? "LED:ON" : "LED:OFF",
        sentToSerial = success,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
})
.RequireApiKey()
.RequireRateLimiting("control");

app.Run();
return 0;

// ตรวจ payload คำสั่งจากแอปมือถือ ยอมรับเฉพาะ {"led":"ON"} / {"led":"OFF"} ขนาดไม่เกิน 128 ไบต์
static bool TryParseLedCommand(string payload, out bool on)
{
    on = false;
    if (payload.Length > 128) return false;
    try
    {
        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.ValueKind != JsonValueKind.Object ||
            !doc.RootElement.TryGetProperty("led", out var el) || el.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        switch (el.GetString()?.ToUpperInvariant())
        {
            case "ON": on = true; return true;
            case "OFF": on = false; return true;
            default: return false;
        }
    }
    catch (JsonException)
    {
        return false;
    }
}
