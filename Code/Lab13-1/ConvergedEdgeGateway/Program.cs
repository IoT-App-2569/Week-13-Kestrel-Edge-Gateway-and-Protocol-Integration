using System.Security.Cryptography;
using System.Text;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Protocol;
using MQTTnet.Server;

Console.OutputEncoding = Encoding.UTF8;   // แสดงภาษาไทยและอีโมจิในคอนโซลได้ถูกต้อง

var builder = WebApplication.CreateBuilder(args);

var httpPort = builder.Configuration.GetValue("Gateway:HttpPort", 5000);
var mqttPort = builder.Configuration.GetValue("Gateway:MqttPort", 1883);

// อ่านรหัสผ่าน MQTT จาก user-secrets / environment variable (ห้ามเขียนลงไฟล์ที่ commit)
var requireAuth = builder.Configuration.GetValue("Mqtt:RequireAuthentication", true);
var mqttUser = builder.Configuration["Mqtt:Username"] ?? "";
var mqttPass = builder.Configuration["Mqtt:Password"] ?? "";
if (requireAuth && (string.IsNullOrWhiteSpace(mqttUser) || mqttPass.Length < 8))
{
    Console.Error.WriteLine("""
        [Security] ยังไม่ได้ตั้งค่า MQTT username/password (รหัสผ่านต้องยาว >= 8 ตัวอักษร)
        ตั้งค่าด้วย:
          dotnet user-secrets set "Mqtt:Username" "student"
          dotnet user-secrets set "Mqtt:Password" "<your-strong-password>"
        """);
    return 1;
}

// 1. กำหนดค่า Kestrel ให้เปิดทั้งพอร์ต HTTP และ MQTT TCP
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP เปิดเฉพาะเครื่องตัวเอง (Lab นี้ยังไม่มี API ให้เครื่องอื่นเรียก)
    options.ListenLocalhost(httpPort);

    // MQTT เปิดทุก IP เพื่อให้มือถือ/บอร์ดในวง LAN เชื่อมต่อได้ (ป้องกันด้วย username/password)
    options.ListenAnyIP(mqttPort, listenOptions => listenOptions.UseMqtt());
});

// 2. ลงทะเบียนบริการ MQTT Server
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddMqttConnectionHandler();
builder.Services.AddConnections();

var app = builder.Build();

// 3. ผูก Event Handlers สำหรับจัดการข้อความใน Broker
app.UseMqttServer(server =>
{
    server.ValidatingConnectionAsync += args =>
    {
        // Client ที่ไม่ระบุ ClientId (เช่น mosquitto CLI ไม่ใส่ -i) ให้ Broker สุ่มให้
        if (string.IsNullOrEmpty(args.ClientId))
        {
            args.AssignedClientIdentifier = Guid.NewGuid().ToString("N");
        }

        if (requireAuth && !(FixedEquals(args.UserName, mqttUser) && FixedEquals(args.Password, mqttPass)))
        {
            args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            WriteColor(ConsoleColor.Red, $"[MQTT Server] ⛔ ปฏิเสธการเชื่อมต่อ (รหัสผ่านผิด) จาก {args.Endpoint}");
        }
        return Task.CompletedTask;
    };

    // เมื่อมี Client เชื่อมต่อเข้ามา
    server.ClientConnectedAsync += args =>
    {
        WriteColor(ConsoleColor.Green, $"[MQTT Server] 🟢 Client เชื่อมต่อ: {args.ClientId} (จาก Endpoint: {args.Endpoint})");
        return Task.CompletedTask;
    };

    // เมื่อ Client ตัดการเชื่อมต่อ
    server.ClientDisconnectedAsync += args =>
    {
        WriteColor(ConsoleColor.DarkYellow, $"[MQTT Server] 🔴 Client ตัดการเชื่อมต่อ: {args.ClientId}");
        return Task.CompletedTask;
    };

    // ดักจับและอ่านข้อความที่ถูก Publish ทุกฉบับ
    server.InterceptingPublishAsync += args =>
    {
        var msg = args.ApplicationMessage;
        WriteColor(ConsoleColor.Cyan,
            $"[MQTT Message] 📩 หัวข้อ: {msg.Topic} | QoS: {msg.QualityOfServiceLevel} | ข้อความ: {msg.ConvertPayloadToString()}");
        return Task.CompletedTask;
    };
});

// 4. จุดทดสอบ REST Endpoint เบื้องต้น
app.MapGet("/", () => "🚀 Kestrel Converged Edge Gateway with Embedded MQTTnet Broker is RUNNING!");

app.Run();
return 0;

// พิมพ์ข้อความสีลงคอนโซล (lock กันข้อความจากหลาย Client พิมพ์สีปนกัน)
static void WriteColor(ConsoleColor color, string text)
{
    lock (Console.Out)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}

// เปรียบเทียบแบบใช้เวลาคงที่ ป้องกัน timing attack
static bool FixedEquals(string? a, string b) =>
    a is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
