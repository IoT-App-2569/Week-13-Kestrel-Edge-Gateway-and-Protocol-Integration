
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("==========================================");
Console.WriteLine("[STARTUP] Starting Kestrel Edge Gateway...");
Console.WriteLine("==========================================");

// 1. กำหนด Kestrel: HTTP 5000 และ MQTT 1883
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);

    options.ListenAnyIP(1883, listenOptions =>
    {
        listenOptions.UseMqtt();
    });
});

// 2. ลงทะเบียน MQTT Server
builder.Services
    .AddHostedMqttServer(options =>
    {
        options.WithoutDefaultEndpoint();
    })
    .AddMqttConnectionHandler()
    .AddConnections();

// 3. ลงทะเบียน SignalR
builder.Services.AddSignalR();

// 4. ลงทะเบียนตัวนับสถิติ
builder.Services.AddSingleton<ProtocolMetrics>();

// 5. ลงทะเบียน Serial Bridge Services
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddSingleton<MqttServerAccessor>();
builder.Services.AddSingleton<SerialBridgeWorker>();

builder.Services.AddHostedService<SerialBridgeWorker>(
    provider => provider.GetRequiredService<SerialBridgeWorker>());

var app = builder.Build();

// 6. กำหนด Event Handlers ของ MQTT Server
app.UseMqttServer(server =>
{
    app.Services
        .GetRequiredService<MqttServerAccessor>()
        .SetServer(server);

    Console.WriteLine("[DEBUG] MQTT Event Handlers registered.");

    server.ValidatingConnectionAsync += args =>
    {
        Console.WriteLine(
            $"[MQTT] Validating Client: {args.ClientId}");

        if (string.IsNullOrEmpty(args.ClientId))
        {
            args.AssignedClientIdentifier =
                Guid.NewGuid().ToString("N");
        }

        return Task.CompletedTask;
    };

    server.ClientConnectedAsync += args =>
    {
        Console.WriteLine(
            $"[MQTT Server] Client connected: {args.ClientId}");

        return Task.CompletedTask;
    };

    server.ClientDisconnectedAsync += args =>
    {
        Console.WriteLine(
            $"[MQTT Server] Client disconnected: {args.ClientId}");

        return Task.CompletedTask;
    };

    // รับคำสั่งควบคุม LED จาก Flutter ผ่าน MQTT
    server.InterceptingPublishAsync += async args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload =
            args.ApplicationMessage.ConvertPayloadToString();
        var qos =
            args.ApplicationMessage.QualityOfServiceLevel;

        Console.WriteLine(
            $"[MQTT Message] Topic: {topic} | " +
            $"QoS: {qos} | Payload: {payload}");

        if (topic == "iot/esp32/control")
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);

                if (!doc.RootElement.TryGetProperty(
                    "led", out var ledElement))
                {
                    Console.WriteLine(
                        "[MQTT] ไม่พบฟิลด์ led ในคำสั่ง");

                    return;
                }

                string? led = ledElement.GetString();

                bool state;

                if (string.Equals(
                    led, "ON", StringComparison.OrdinalIgnoreCase))
                {
                    state = true;
                }
                else if (string.Equals(
                    led, "OFF", StringComparison.OrdinalIgnoreCase))
                {
                    state = false;
                }
                else
                {
                    Console.WriteLine(
                        "[MQTT] คำสั่ง LED ไม่ถูกต้อง");

                    return;
                }

                var serialWorker =
                    app.Services.GetRequiredService<SerialBridgeWorker>();

                bool success =
                    await serialWorker.SetLedStateAsync(state);

                Console.WriteLine(
                    success
                        ? $"[MQTT] LED Command {led} สำเร็จ"
                        : "[MQTT] ไม่สามารถส่งคำสั่งไปยัง ESP32 ได้");
            }
            catch (JsonException ex)
            {
                Console.WriteLine(
                    $"[MQTT] JSON ไม่ถูกต้อง: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[MQTT] ประมวลผลคำสั่งผิดพลาด: {ex.Message}");
            }
        }
    };
});

// 7. เปิดใช้งาน Static Files จาก wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// 8. สร้าง SignalR Hub Endpoint
app.MapHub<GatewayHub>("/hubs/gateway");

// 9. REST API อ่านข้อมูล Telemetry ล่าสุด
app.MapGet("/api/telemetry/latest", (
    GatewayStateStore store,
    ProtocolMetrics metrics) =>
{
    metrics.IncrementRestRequests();

    var telemetry =
        store.Get<TelemetryData>("latest_telemetry");

    if (telemetry == null)
    {
        return Results.NotFound(new
        {
            message = "ยังไม่มีข้อมูลจาก ESP32"
        });
    }

    return Results.Ok(telemetry);
});

// 10. REST API ส่งคำสั่งควบคุม LED
app.MapPost("/api/led/{state}", async (
    string state,
    SerialBridgeWorker serialWorker,
    ProtocolMetrics metrics) =>
{
    metrics.IncrementRestRequests();

    bool ledState;

    if (state.Equals(
        "on", StringComparison.OrdinalIgnoreCase))
    {
        ledState = true;
    }
    else if (state.Equals(
        "off", StringComparison.OrdinalIgnoreCase))
    {
        ledState = false;
    }
    else
    {
        return Results.BadRequest(new
        {
            message = "กรุณาระบุ state เป็น on หรือ off"
        });
    }

    bool sent =
        await serialWorker.SetLedStateAsync(ledState);

    if (!sent)
    {
        return Results.Problem(
            "ไม่สามารถส่งคำสั่งไปยัง ESP32 ได้",
            statusCode: 503);
    }

    return Results.Ok(new
    {
        state = state.ToLowerInvariant(),
        command = ledState ? "LED:ON" : "LED:OFF",
        sentToSerial = true
    });
});

// 11. แสดงสถานะเมื่อ Application เริ่มทำงาน
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("[STARTUP] Application started successfully.");
    Console.WriteLine("[HTTP]    Listening on port 5000");
    Console.WriteLine("[MQTT]    Listening on port 1883");
    Console.WriteLine("[SIGNALR] Hub: /hubs/gateway");
    Console.WriteLine("[SERIAL]  Waiting for ESP32...");
});

app.Run();
