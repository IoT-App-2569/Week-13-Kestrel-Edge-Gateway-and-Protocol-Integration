# รายงานผลการทดลอง Week-13: The Ultimate Converged Edge Gateway & System Integration (.NET 8 Kestrel)
**รหัสนักศึกษา:** 67030211  
**ชื่อ-นามสกุล:** วิชญาพร เนียมเที่ยง  
**วิชา:** 03376134 APPLICATIONS OF INTERNET OF THINGS  

---

## สรุปรายการปฏิบัติการ
- Labsheet 13.1: Embedded MQTT Broker & Client with MQTTnet inside Kestrel
- Labsheet 13.2: Hardware Serial-to-MQTT & REST Bridge Pipeline
- Labsheet 13.3: Real-Time SignalR WebSockets Hub & Converged Web Command Center
- Labsheet 13.4: The Grand Finale: All-in-One Multi-Protocol IoT System Integration

---

### 📍 Labsheet 13.1: Embedded MQTT Broker & Client with MQTTnet inside Kestrel

#### 1. บันทึกผลการทดลอง / โค้ดที่พัฒนา

สร้างโปรเจกต์ .NET 8 Web และติดตั้งไลบรารี MQTTnet สำหรับทำหน้าที่เป็น Embedded MQTT Broker ภายในเว็บเซิร์ฟเวอร์ Kestrel:

```powershell
dotnet new web -n ConvergedEdgeGateway
cd ConvergedEdgeGateway
dotnet add package MQTTnet --version 4.3.7.1207
dotnet add package MQTTnet.AspNetCore --version 4.3.7.1207
```

โค้ดในไฟล์ `Program.cs`:
```csharp
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// กำหนดค่า Kestrel ให้เปิดพอร์ต HTTP (5000) และ MQTT TCP (1883)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // พอร์ต HTTP REST API และแดชบอร์ด
    options.ListenAnyIP(1883, listenOptions =>
    {
        listenOptions.UseMqtt(); // พอร์ต Embedded MQTT Broker
    });
});

// ลงทะเบียนบริการ MQTT Server
builder.Services.AddMqttServer(options =>
{
    options.WithDefaultEndpoint();
});

var app = builder.Build();

// ตั้งค่า Event Handlers ของ Broker
app.UseMqttServer(server =>
{
    // กำหนด ClientId อัตโนมัติถ้าไคลเอนต์ไม่ได้ส่งมา
    server.ValidatingConnectionAsync += args =>
    {
        if (string.IsNullOrEmpty(args.ClientId))
        {
            args.AssignedClientIdentifier = Guid.NewGuid().ToString("N");
        }
        return Task.CompletedTask;
    };

    // แสดง log เมื่อมี Client เชื่อมต่อ
    server.ClientConnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MQTT Server] Client เชื่อมต่อ: {args.ClientId} (จาก Endpoint: {args.Endpoint})");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    // แสดง log เมื่อ Client ตัดการเชื่อมต่อ
    server.ClientDisconnectedAsync += args =>
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[MQTT Server] Client ตัดการเชื่อมต่อ: {args.ClientId}");
        Console.ResetColor();
        return Task.CompletedTask;
    };

    // ดักจับข้อความที่ถูก Publish ทุกอัน
    server.InterceptingPublishAsync += args =>
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString();
        var qos = args.ApplicationMessage.QualityOfServiceLevel;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[MQTT Message] หัวข้อ: {topic} | QoS: {qos} | ข้อความ: {payload}");
        Console.ResetColor();

        return Task.CompletedTask;
    };
});

app.MapGet("/", () => "Kestrel Converged Edge Gateway with Embedded MQTTnet Broker is RUNNING!");

app.Run();
```

#### 2. ผลลัพธ์จาก Serial Monitor / หน้าจอแสดงผล

ผลการรัน Kestrel Edge Gateway บน PowerShell หน้าต่างที่ 1:
```text
PS C:\IoT_Lab\Week-13\ConvergedEdgeGateway> dotnet run
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: mqtt://0.0.0.0:1883
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: C:\IoT_Lab\Week-13\ConvergedEdgeGateway
[MQTT Server] Client เชื่อมต่อ: sub_client (จาก Endpoint: 127.0.0.1:51234)
[MQTT Server] Client เชื่อมต่อ: pub_client (จาก Endpoint: 127.0.0.1:51240)
[MQTT Message] หัวข้อ: test/edge/topic | QoS: AtMostOnce | ข้อความ: Hello from CLI via Kestrel Embedded MQTT!
[MQTT Server] Client ตัดการเชื่อมต่อ: pub_client
```

ผลการทดสอบ Subscribe ผ่าน Mosquitto CLI บน PowerShell หน้าต่างที่ 2:
```text
PS C:\Users\student> mosquitto_sub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -i "sub_client" -v
test/edge/topic Hello from CLI via Kestrel Embedded MQTT!
```

ผลการทดสอบ Publish ผ่าน Mosquitto CLI บน PowerShell หน้าต่างที่ 3:
```text
PS C:\Users\student> mosquitto_pub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!" -i "pub_client"
```

#### 3. สรุปผลและการตอบคำถามท้ายใบงาน

- **สรุปผลการทดลอง:**  
  โปรแกรม Kestrel บน .NET 8 สามารถเปิดพอร์ต 5000 สำหรับ HTTP และพอร์ต 1883 สำหรับ MQTT Broker พร้อมกันได้ โดยโปรแกรม Mosquitto CLI สามารถเชื่อมต่อเข้ามา Subscribe และ Publish ข้อความได้ตามปกติ และฟังก์ชัน `InterceptingPublishAsync` สามารถดักจับข้อความในระบบมาแสดงผลบนคอนโซลได้ถูกต้อง

- **คำถามท้ายการทดลองที่ 1:**  
  การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?  
  **คำตอบ:**  
  ไม่ต้องติดตั้งและคอนฟิกโปรแกรม Mosquitto เพิ่มเติมบนเครื่อง ทำให้การนำไปรันบนเครื่องอื่นหรือบนบอร์ดทำได้ง่ายเพราะรวมอยู่ในไฟล์โปรแกรมตัวเดียว และข้อมูลข้อความ MQTT ที่เข้ามาสามารถส่งต่อเข้าตัวแปรหรือคิวในหน่วยความจำของโค้ด C# ได้ทันที ไม่ต้องเสียเวลาส่งผ่านเครือข่าย Loopback หรือแปลงข้อมูลไปมาข้ามโปรเซส

- **คำถามท้ายการทดลองที่ 2:**  
  ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?  
  **คำตอบ:**  
  ฟังก์ชันนี้จะทำงานก่อนที่ข้อความจะถูกส่งต่อให้คนอื่น ทำให้เขียนโค้ดตรวจสอบข้อมูลข้างในได้ เช่น ตรวจสอบช่วงค่าแรงดัน ถ้าตัวเลขเกินช่วง 0 ถึง 3.3V หรือรูปแบบ JSON ผิด ก็สามารถสั่งยกเลิกการส่งต่อโดยตั้งค่า args.ProcessPublish = false เพื่อไม่ให้ข้อมูลผิดพลาดถูกส่งไปยัง Client ตัวอื่น

---

### 📍 Labsheet 13.2: Hardware Serial-to-MQTT & REST Bridge Pipeline

#### 1. บันทึกผลการทดลอง / โค้ดที่พัฒนา

ติดตั้งแพ็กเกจ `System.IO.Ports`:
```powershell
dotnet add package System.IO.Ports --version 8.0.0
```

โค้ดไฟล์ `Services.cs`:
```csharp
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Server;

public class GatewayStateStore
{
    private readonly ConcurrentDictionary<string, object> _state = new();

    public void Set(string key, object val) => _state[key] = val;
    public T? Get<T>(string key) => _state.TryGetValue(key, out var val) && val is T t ? t : default;
    public object? Get(string key) => _state.TryGetValue(key, out var val) ? val : null;
}

public record TelemetryData(double Voltage, int Raw, long Timestamp);

public class SerialBridgeWorker : BackgroundService
{
    private SerialPort? _serial;
    private readonly string _portName = "COM24";
    private readonly int _baudRate = 115200;
    private readonly GatewayStateStore _store;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<SerialBridgeWorker> _logger;

    public SerialBridgeWorker(GatewayStateStore store, MqttServer mqttServer, ILogger<SerialBridgeWorker> logger)
    {
        _store = store;
        _mqttServer = mqttServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("พยายามเปิด Serial Port {Port}...", _portName);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_serial == null || !_serial.IsOpen)
            {
                try
                {
                    _serial = new SerialPort(_portName, _baudRate)
                    {
                        NewLine = "\n",
                        ReadTimeout = 1000
                    };
                    _serial.Open();
                    _logger.LogInformation("เชื่อมต่อฮาร์ดแวร์ Serial Port {Port} สำเร็จ", _portName);
                }
                catch
                {
                    await Task.Delay(3000, stoppingToken);
                    continue;
                }
            }

            try
            {
                string line = _serial.ReadLine().Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    await HandleIncomingSerialLine(line);
                }
            }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                _logger.LogWarning("Serial Read Warning: {Message}", ex.Message);
                _serial?.Close();
            }
        }
    }

    private async Task HandleIncomingSerialLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            double v = root.GetProperty("voltage").GetDouble();
            int r = root.GetProperty("raw").GetInt32();

            var telemetry = new TelemetryData(v, r, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _store.Set("latest_telemetry", telemetry);

            var mqttMsg = new MqttApplicationMessageBuilder()
                .WithTopic("iot/esp32/telemetry")
                .WithPayload(line)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));
        }
        catch { }
    }

    public bool SendSerialCommand(string cmd)
    {
        if (_serial?.IsOpen == true)
        {
            _serial.WriteLine(cmd);
            return true;
        }
        return false;
    }

    public override void Dispose()
    {
        _serial?.Close();
        base.Dispose();
    }
}
```

โค้ดไฟล์ `Program.cs` ที่เพิ่ม REST API:
```csharp
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();
builder.Services.AddSingleton(sp => 
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

var app = builder.Build();

app.UseMqttServer(server => { });

app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

app.MapPost("/api/led/{state}", (string state, SerialBridgeWorker worker) =>
{
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
```

โค้ดบน ESP32 (`ESP32_ADC_Stream.c`):
```c
#include <stdio.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/uart.h"
#include "esp_log.h"
#include "esp_adc/adc_oneshot.h"

#define LED_PIN             GPIO_NUM_2
#define POT_ADC_CHANNEL     ADC_CHANNEL_6

void app_main(void)
{
    gpio_reset_pin(LED_PIN);
    gpio_set_direction(LED_PIN, GPIO_MODE_OUTPUT);
    gpio_set_level(LED_PIN, 0);

    adc_oneshot_unit_handle_t adc1_handle;
    adc_oneshot_unit_init_cfg_t init_config = {
        .unit_id = ADC_UNIT_1,
        .ulp_mode = ADC_ULP_MODE_DISABLE,
    };
    ESP_ERROR_CHECK(adc_oneshot_new_unit(&init_config, &adc1_handle));

    adc_oneshot_chan_cfg_t chan_config = {
        .bitwidth = ADC_BITWIDTH_12,
        .atten = ADC_ATTEN_DB_12,
    };
    ESP_ERROR_CHECK(adc_oneshot_config_channel(adc1_handle, POT_ADC_CHANNEL, &chan_config));

    uart_driver_install(UART_NUM_0, 256, 0, 0, NULL, 0);
    uint8_t rx_buf[64];

    while (1) {
        int raw = 0;
        ESP_ERROR_CHECK(adc_oneshot_read(adc1_handle, POT_ADC_CHANNEL, &raw));
        float voltage = (raw / 4095.0f) * 3.3f;

        printf("{\"voltage\":%.2f,\"raw\":%d}\n", voltage, raw);

        int len = uart_read_bytes(UART_NUM_0, rx_buf, sizeof(rx_buf) - 1, pdMS_TO_TICKS(50));
        if (len > 0) {
            rx_buf[len] = '\0';
            if (strstr((char *)rx_buf, "LED:ON") != NULL) {
                gpio_set_level(LED_PIN, 1);
            } else if (strstr((char *)rx_buf, "LED:OFF") != NULL) {
                gpio_set_level(LED_PIN, 0);
            }
        }
        vTaskDelay(pdMS_TO_TICKS(450));
    }
}
```

#### 2. ผลลัพธ์จาก Serial Monitor / หน้าจอแสดงผล

ผลลัพธ์บนคอนโซลของ Kestrel:
```text
info: SerialBridgeWorker[0]
      พยายามเปิด Serial Port COM24...
info: SerialBridgeWorker[0]
      เชื่อมต่อฮาร์ดแวร์ Serial Port COM24 สำเร็จ
```

ผลการทดสอบเรียก REST API ผ่าน cURL เพื่อดูค่า Telemetry:
```bash
curl http://localhost:5000/api/telemetry/latest
```
```json
{"voltage":1.65,"raw":2048,"timestamp":1729987200}
```

ผลการดักฟัง MQTT Topic ผ่าน mosquitto_sub:
```text
PS C:\Users\student> mosquitto_sub -h 127.0.0.1 -p 1883 -t "iot/esp32/telemetry" -i "sub_client" -v
iot/esp32/telemetry {"voltage":1.65,"raw":2048}
iot/esp32/telemetry {"voltage":1.68,"raw":2085}
iot/esp32/telemetry {"voltage":1.70,"raw":2110}
```

ผลการส่งคำสั่งเปิดไฟ LED ผ่าน REST API:
```bash
curl -X POST http://localhost:5000/api/led/on
```
```json
{"dispatchedCommand":"LED:ON","sentToSerial":true,"serverTime":"14:30:15"}
```
*หลอดไฟ LED ที่ขา GPIO2 บนบอร์ด ESP32 ติดสว่างขึ้นทันที*

#### 3. สรุปผลและการตอบคำถามท้ายใบงาน

- **สรุปผลการทดลอง:**  
  `SerialBridgeWorker` สามารถเปิดพอร์ต COM24 และอ่านข้อมูล JSON ที่บอร์ด ESP32 ส่งมาได้แบบต่อเนื่อง จากนั้นนำค่าไปบันทึกลง `GatewayStateStore` และฉีดเข้า Embedded MQTT Broker ทำให้ดึงค่าผ่าน REST API `/api/telemetry/latest` หรือรับผ่าน MQTT ได้พร้อมกัน และเมื่อยิงคำสั่งผ่าน REST API ไปที่ `/api/led/on` ตัวเกตเวย์สามารถส่งสตริง `LED:ON` ผ่านสาย Serial ไปสั่งให้หลอดไฟบนบอร์ดติดได้จริง

- **คำถามท้ายการทดลองที่ 1:**  
  สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?  
  **คำตอบ:**  
  อุปกรณ์รุ่นเก่าสามารถส่งข้อมูลผ่านพอร์ต Serial เข้ามาที่เครื่องเกตเวย์ แล้วให้เกตเวย์ทำหน้าที่แปลงข้อความตัวอักษรนั้นให้เป็นข้อความ MQTT และ Publish ขึ้น Broker ให้อัตโนมัติ ทำให้อุปกรณ์หรือแอปพลิเคชันอื่นบนเครือข่ายสามารถรับข้อมูลและส่งคำสั่งกลับไปหาอุปกรณ์รุ่นเก่าได้เหมือนเป็นอุปกรณ์ IoT ตัวหนึ่ง

- **คำถามท้ายการทดลองที่ 2:**  
  เพราะเหตุใดเราจึงต้องใช้ `BackgroundService` ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?  
  **คำตอบ:**  
  พอร์ต Serial เปิดใช้งานได้ทีละโปรแกรม ถ้าเปิดปิดใน REST API จะทำให้ระบบทำงานช้าและเกิดข้อผิดพลาดแย่งกันเปิดพอร์ต และข้อมูลจาก Serial จะส่งมาตลอดเวลา การใช้ BackgroundService ทำให้มีเธรดทำงานค้างไว้คอยรอรับข้อมูลตลอดเวลาโดยไม่บล็อกการทำงานของคำขอ HTTP API ตัวอื่น

---

### 📍 Labsheet 13.3: Real-Time SignalR WebSockets Hub & Converged Web Command Center

#### 1. บันทึกผลการทดลอง / โค้ดที่พัฒนา

สร้างไฟล์ `Hubs.cs`:
```csharp
using Microsoft.AspNetCore.SignalR;

public class GatewayHub : Hub
{
    private readonly SerialBridgeWorker _serialWorker;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(SerialBridgeWorker serialWorker, ILogger<GatewayHub> logger)
    {
        _serialWorker = serialWorker;
        _logger = logger;
    }

    public async Task ToggleLed(bool state)
    {
        string cmd = state ? "LED:ON" : "LED:OFF";
        _logger.LogInformation("สั่งการจาก Web Dashboard ผ่าน SignalR: {Cmd}", cmd);
        _serialWorker.SendSerialCommand(cmd);
        await Clients.All.SendAsync("LedStateUpdated", state);
    }
}

public class ProtocolMetrics
{
    public long SerialPackets { get; set; } = 0;
    public long MqttMessages { get; set; } = 0;
    public long RestRequests { get; set; } = 0;
    public long SignalRPushes { get; set; } = 0;
}
```

ปรับปรุง `SerialBridgeWorker` ใน `Services.cs` ให้ฉีดข้อมูลเข้า SignalR และนับสถิติ Metrics:
```csharp
private async Task HandleIncomingSerialLine(string line)
{
    try
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        double v = root.GetProperty("voltage").GetDouble();
        int r = root.GetProperty("raw").GetInt32();

        _metrics.SerialPackets++;

        _metrics.MqttMessages++;
        var mqttMsg = new MqttApplicationMessageBuilder()
            .WithTopic("iot/esp32/telemetry")
            .WithPayload(line)
            .Build();
        await _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMsg));

        _metrics.SignalRPushes++;
        await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", new
        {
            voltage = v,
            raw = r,
            metrics = _metrics
        });
    }
    catch { }
}
```

ไฟล์หน้าเว็บศูนย์บัญชาการ `wwwroot/index.html`:
```html
<!DOCTYPE html>
<html lang="th">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Kestrel Converged Edge Command Center</title>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
    <style>
        :root {
            --bg: #0b0f19;
            --card-bg: rgba(22, 30, 49, 0.75);
            --primary: #00E5FF;
            --accent: #00E676;
            --danger: #FF5252;
            --border: rgba(255, 255, 255, 0.08);
        }
        body {
            margin: 0; padding: 24px;
            background: var(--bg);
            color: #f1f5f9;
            font-family: 'Segoe UI', system-ui, sans-serif;
        }
        .header {
            display: flex; justify-content: space-between; align-items: center;
            margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid var(--border);
        }
        .title { font-size: 24px; font-weight: 700; color: var(--primary); }
        .grid {
            display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 20px;
        }
        .card {
            background: var(--card-bg);
            backdrop-filter: blur(16px);
            border: 1px solid var(--border);
            border-radius: 20px; padding: 24px;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
        }
        .gauge-val {
            font-size: 52px; font-weight: 800; font-family: monospace; color: white;
            text-align: center; margin-top: 10px;
        }
        .metric-badge {
            display: flex; justify-content: space-between; padding: 12px;
            background: rgba(0,0,0,0.3); border-radius: 12px; margin-bottom: 8px;
        }
        .btn-toggle {
            width: 100%; padding: 16px; font-size: 16px; font-weight: bold;
            border: none; border-radius: 14px; cursor: pointer; transition: 0.2s;
            background: #334155; color: white; margin-top: 16px;
        }
        .btn-active {
            background: var(--accent); color: black; box-shadow: 0 0 20px rgba(0,230,118,0.4);
        }
    </style>
</head>
<body>
    <div class="header">
        <div>
            <div class="title">⚡ Kestrel Converged Edge Command Center</div>
            <div style="color: #94a3b8; font-size: 14px;">All-in-One Multi-Protocol Bridge (.NET 8)</div>
        </div>
        <div id="connStatus" style="color: var(--accent); font-weight: bold;">● SignalR Online</div>
    </div>

    <div class="grid">
        <div class="card" style="text-align: center;">
            <h3 style="margin-top:0; color:#94a3b8;">Real-time Potentiometer Voltage</h3>
            <div class="gauge-val" id="voltageText">0.00 <span style="font-size:24px; color:var(--primary);">V</span></div>
            <div style="color: #94a3b8;" id="rawText">Raw ADC: 0 / 4095</div>
        </div>

        <div class="card">
            <h3 style="margin-top:0; color:#94a3b8;">Hardware Actuator Control</h3>
            <div style="color: #cbd5e1; font-size: 14px;">ควบคุม LED Status ขา GPIO2 ข้ามทุกโปรโตคอล</div>
            <button id="ledBtn" class="btn-toggle" onclick="toggleLed()">💡 สั่งเปิดไฟ LED (OFF)</button>
        </div>

        <div class="card">
            <h3 style="margin-top:0; color:#94a3b8;">Protocol Convergence Metrics</h3>
            <div class="metric-badge"><span>🔌 Serial Packets (UART)</span><b id="mSerial" style="color:var(--primary);">0</b></div>
            <div class="metric-badge"><span>📬 MQTT Messages (Broker)</span><b id="mMqtt" style="color:var(--accent);">0</b></div>
            <div class="metric-badge"><span>🌐 WebSockets (SignalR)</span><b id="mSignalR" style="color:#e040fb;">0</b></div>
        </div>
    </div>

    <script>
        let isLedOn = false;
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/gateway")
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveTelemetry", function (data) {
            document.getElementById("voltageText").innerHTML = data.voltage.toFixed(2) + ' <span style="font-size:24px; color:var(--primary);">V</span>';
            document.getElementById("rawText").innerText = "Raw ADC: " + data.raw + " / 4095";
            if (data.metrics) {
                document.getElementById("mSerial").innerText = data.metrics.serialPackets;
                document.getElementById("mMqtt").innerText = data.metrics.mqttMessages;
                document.getElementById("mSignalR").innerText = data.metrics.signalRPushes;
            }
        });

        connection.on("LedStateUpdated", function (state) {
            isLedOn = state;
            updateLedButton();
        });

        function toggleLed() {
            isLedOn = !isLedOn;
            connection.invoke("ToggleLed", isLedOn);
            updateLedButton();
        }

        function updateLedButton() {
            const btn = document.getElementById("ledBtn");
            if (isLedOn) {
                btn.className = "btn-toggle btn-active";
                btn.innerText = "💡 หลอดไฟกำลังสว่าง (ON)";
            } else {
                btn.className = "btn-toggle";
                btn.innerText = "🌑 หลอดไฟปิดอยู่ (OFF)";
            }
        }

        connection.start().catch(err => {
            document.getElementById("connStatus").innerText = "○ Disconnected";
            document.getElementById("connStatus").style.color = "var(--danger)";
        });
    </script>
</body>
</html>
```

#### 2. ผลลัพธ์จาก Serial Monitor / หน้าจอแสดงผล

ผลการเปิดหน้าเว็บเบราว์เซอร์ `http://localhost:5000`:
```text
สถานะการเชื่อมต่อ: ● SignalR Online
Real-time Potentiometer Voltage: 2.15 V
Raw ADC: 2668 / 4095

Protocol Convergence Metrics:
- Serial Packets (UART): 142
- MQTT Messages (Broker): 142
- WebSockets (SignalR): 142
```
*เมื่อหมุนตัวต้านทานปรับค่าได้ ตัวเลขแรงดันบนหน้าจอขยับเปลี่ยนทันทีโดยไม่ต้องรีเฟรชหน้าเว็บ*

Log บนคอนโซลเมื่อคลิกปุ่มสลับไฟบนหน้าเว็บ:
```text
info: GatewayHub[0]
      สั่งการจาก Web Dashboard ผ่าน SignalR: LED:ON
info: GatewayHub[0]
      สั่งการจาก Web Dashboard ผ่าน SignalR: LED:OFF
```

#### 3. สรุปผลและการตอบคำถามท้ายใบงาน

- **สรุปผลการทดลอง:**  
  SignalR Hub ช่วยให้หน้าเว็บรับข้อมูลสตรีมมิ่งผ่าน WebSocket ได้ทันทีที่บอร์ด ESP32 ส่งข้อมูลมา และเมื่อคลิกปุ่มควบคุมไฟบนหน้าเว็บ คำสั่งถูกส่งผ่าน SignalR ไปยัง `SerialBridgeWorker` แล้วยิงออกทางสาย Serial สั่งให้ฮาร์ดแวร์ทำงานได้จริง พร้อมทั้งตัวนับ Metrics วิ่งขึ้นพร้อมกันทุกโปรโตคอล

- **คำถามท้ายการทดลองที่ 1:**  
  เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript `setInterval()` เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?  
  **คำตอบ:**  
  SignalR ใช้การเชื่อมต่อ WebSocket ค้างไว้ เซิร์ฟเวอร์จะส่งข้อมูลมาที่หน้าเว็บทันทีที่มีข้อมูลใหม่โดยไม่ต้องร้องขอ ทำให้ข้อมูลบนหน้าจอแสดงผลได้เร็วกว่า ไม่กระตุก และไม่กินแบนด์วิดท์เครือข่ายจากการส่ง Header ของ HTTP ซ้ำๆ ทุกครึ่งวินาที

- **คำถามท้ายการทดลองที่ 2:**  
  การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?  
  **คำตอบ:**  
  ช่วยให้มองเห็นจำนวนแพ็กเก็ตของแต่ละโปรโตคอลพร้อมกัน ถ้ามีโปรโตคอลใดหยุดวิ่งหรือตัวเลขไม่ขยับตามส่วนอื่น จะรู้ได้ทันทีว่าเกิดปัญหาที่จุดไหน เช่น สาย Serial หลุด หรือ WebSockets หลุด ทำให้ตรวจหาจุดเสียได้เร็ว

---

### 📍 Labsheet 13.4: The Grand Finale: All-in-One Multi-Protocol IoT System Integration

#### 1. บันทึกผลการทดลอง / โค้ดที่พัฒนา

รวบรวมระบบทั้งหมดเข้าด้วยกัน:
1. ฮาร์ดแวร์จริง ESP32 ต่อ Potentiometer เข้าขา GPIO34 และ LED เข้าขา GPIO2 ส่ง JSON ผ่าน USB UART
2. Kestrel Edge Gateway รันทั้ง Embedded MQTT Broker (พอร์ต 1883), Serial Bridge Worker (COM24), REST API (พอร์ต 5000), และ SignalR Hub
3. เว็บแดชบอร์ด `http://localhost:5000` เชื่อมต่อ SignalR
4. แอปพลิเคชันมือถือ Flutter (จากสัปดาห์ที่ 12) เชื่อมต่อ MQTT มาที่พอร์ต 1883 ของเกตเวย์

#### 2. ผลลัพธ์จาก Serial Monitor / หน้าจอแสดงผล

ตารางบันทึกผลการทดสอบ **The Grand Verification Matrix**:

| มิติการทดสอบ | การกระทำเชิงปฏิบัติการ | ปรากฏการณ์ที่เกิดขึ้นจริงในระบบ | ผลการทดลอง |
| :--- | :--- | :--- | :---: |
| **1. Universal Telemetry Sync** | หมุนตัวต้านทานปรับค่าได้ (Potentiometer) บนบอร์ดจริง | 1. ค่าแรงดันวิ่งผ่านสาย Serial เข้า Kestrel<br/>2. ตัวเลขบนเว็บแดชบอร์ดวิ่งสดทันที<br/>3. เข็ม Radial Gauge บน Flutter หมุนตามทันทีพร้อมกัน | **ผ่าน** |
| **2. Mobile-to-Web Actuation** | แตะสวิตช์เปิดไฟบนแอป Flutter (ส่งผ่าน MQTT) | 1. หลอดไฟ LED จริงบนบอร์ด ESP32 ติดสว่างขึ้น<br/>2. ปุ่มสวิตช์บนหน้าเว็บเปลี่ยนเป็นสีเขียว "ON" อัตโนมัติ | **ผ่าน** |
| **3. Web-to-Mobile Actuation** | คลิกปุ่มปิดไฟบนหน้าเว็บ (ส่งผ่าน SignalR) | 1. หลอดไฟ LED จริงบนบอร์ด ESP32 ดับลงทันที<br/>2. สวิตช์บนแอปมือถือ Flutter สลับเป็น "OFF" ทันที | **ผ่าน** |
| **4. Third-Party REST Override** | ยิงคำสั่งผ่าน cURL หรือ Postman:<br/>`curl -X POST http://localhost:5000/api/led/on` | 1. หลอดไฟ LED สว่างขึ้น<br/>2. ทั้งหน้าเว็บและแอปมือถืออัปเดตสถานะเป็น "ON" พร้อมกัน | **ผ่าน** |

ผลการทดสอบความทนทานในสภาวะตัดเน็ต (Local Survivability Challenge):
- เมื่อถอดสายแลนและตัดการเชื่อมต่ออินเทอร์เน็ตภายนอก โดยคงไว้เพียงเครือข่าย Wi-Fi ท้องถิ่นในวงแลน บอร์ด ESP32 ยังคงส่งข้อมูลผ่าน Serial เข้าเกตเวย์ได้ตามปกติ หน้าเว็บแดชบอร์ดและแอปมือถือ Flutter ยังคงอ่านค่าและสั่งเปิดปิดไฟ LED ได้ครบถ้วน ไม่เกิดอาการระบบหยุดทำงาน

#### 3. สรุปผลและการตอบคำถามท้ายใบงาน

- **สรุปผลการทดลอง:**  
  ระบบที่ออกแบบตามสถาปัตยกรรม Converged Edge Gateway สามารถเชื่อมโยงทั้ง 4 โปรโตคอล (Serial, MQTT, REST, SignalR) เข้าด้วยกันได้อย่างสมบูรณ์ การสั่งงานจากช่องทางใดก็ตามจะส่งต่อไปยังฮาร์ดแวร์จริงและซิงค์สถานะข้ามไปยังแดชบอร์ดและแอปมือถือพร้อมกันทั้งหมด และระบบมีความสามารถในการทำงานต่อได้ 100% แม้ไม่มีอินเทอร์เน็ตภายนอก

- **คำถามท้ายการทดลองที่ 1:**  
  ตลอดระยะเวลา 13 สัปดาห์ การเดินทางจากระดับบิตและรีจิสเตอร์ของเซนเซอร์ สู่เฟรมเวิร์ก FreeRTOS, ผ่านโปรโตคอลเครือข่ายท้องถิ่น, สู่ระบบคลาวด์ MQTT, หน้าจอมือถือ Flutter, และมาบรรจบที่ Kestrel Edge Gateway... สถาปัตยกรรมชิ้นใดที่ท่านคิดว่ามีความท้าทายเชิงวิศวกรรมมากที่สุด? เพราะเหตุใด?  
  **คำตอบ:**  
  สถาปัตยกรรมเกตเวย์รวมศูนย์ (Converged Edge Gateway) ในสัปดาห์ที่ 13 เพราะต้องจัดการการทำงานหลายโปรโตคอลพร้อมกัน ทั้ง Serial, MQTT, REST API และ WebSockets และต้องทำให้ข้อมูลระหว่างเธรดซิงค์กันแบบเรียลไทม์โดยไม่มีการชนกันของข้อมูลและไม่บล็อกการทำงานของระบบ

- **คำถามท้ายการทดลองที่ 2:**  
  หากท่านต้องนำระบบนี้ไปติดตั้งในโรงงานอุตสาหกรรมจริง สิ่งใดคือ 3 ปัจจัยแรกที่ต้องดำเนินการเพิ่มเติมเพื่อความปลอดภัยและความมั่นคงระดับองค์กร?  
  **คำตอบ:**  
  1. การเข้ารหัสข้อมูลและการตรวจสอบสิทธิ์ เช่น การเปิดใช้งาน TLS/SSL บนพอร์ต 5000 และ 8883 พร้อมใส่ระบบล็อกอินหรือ API Key ในการสั่งการ  
  2. การสำรองข้อมูลลงฐานข้อมูลในเครื่อง (Local Buffering) เพื่อเก็บข้อมูลยามเน็ตหลุด ไม่ให้ข้อมูลเซนเซอร์สูญหาย  
  3. การจัดการสิทธิ์และการแยกเครือข่าย (Network Segmentation) ไม่ให้อุปกรณ์ภายนอกเข้าถึงพอร์ตควบคุมได้โดยตรง  
