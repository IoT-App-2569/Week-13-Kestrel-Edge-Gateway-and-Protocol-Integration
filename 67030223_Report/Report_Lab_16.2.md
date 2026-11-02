# Labsheet 13.2: Hardware Serial-to-MQTT & REST Bridge Pipeline

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
  สามารถพัฒนาไปป์ไลน์บริดจ์ข้อมูลแบบไฮบริด (Serial-to-MQTT & REST Pipeline) บน .NET 8 Kestrel ได้สำเร็จ โดยใช้ SerialBridgeWorker (BackgroundService) อ่านข้อมูล Telemetry แบบ JSON จากพอร์ต UART ของ ESP32 (COM24) อย่างต่อเนื่อง นำมาแคชไว้ใน In-Memory State Store เพื่อให้บริการผ่าน HTTP REST API (/api/telemetry/latest) ควบคู่กับการบริดจ์ข้อมูลเข้าสู่ระบบ Pub/Sub ของ Embedded MQTTnet ผ่านคำสั่ง InjectApplicationMessage และสามารถรับคำสั่งควบคุม LED จากภายนอกผ่าน HTTP POST ส่งย้อนกลับไปสั่งงานฮาร์ดแวร์ผ่าน Serial Port ได้แบบสองทางอย่างสมบูรณ์

- **คำถามท้ายการทดลองที่ 1:**  
  สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?  
  **คำตอบ:**  สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?

คำตอบ:

ทำหน้าที่เป็น Edge Protocol Gateway แปลงข้อมูลดิบผ่านพอร์ตมาตรฐานเดิม (UART / RS-232 / RS-485) ให้กลายเป็นแพ็กเก็ตโพรโทคอลสมัยใหม่ (MQTT / REST) โดยตรง ทำให้อุปกรณ์เดิมไม่ต้องเปลี่ยนบอร์ดหรือเพิ่มชิปเน็ตเวิร์ก แต่สามารถส่งข้อมูลขึ้นคลาวด์และทำงานร่วมกับแอปพลิเคชันยุคใหม่ได้ทันที
 

- **คำถามท้ายการทดลองที่ 2:**  
  เพราะเหตุใดเราจึงต้องใช้ `BackgroundService` ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?  
  **คำตอบ:**  1.พอร์ต Serial เปิดซ้ำซ้อนไม่ได้: การเปิดพอร์ตเป็นแบบ Exclusive Resource หากเปิดผ่าน REST API ที่มีหลาย Request เรียกพร้อมกันจะเกิดข้อผิดพลาด Port Conflict ทันที

2.หลีกเลี่ยง Thread Blocking: การรออ่าน Serial มีความหน่วง (I/O Blocking / Timeout) หากทำใน REST API จะทำให้เว็บเซิร์ฟเวอร์ค้างและกิน Thread Pool โดยเปล่าประโยชน์

3.ดักรับสตรีมข้อมูลต่อเนื่อง: ข้อมูลเซนเซอร์ไหลเข้ามาตลอดเวลา การรัน Background Loop ช่วยให้อ่านข้อมูลทันทีโดยไม่มีตกหล่น แล้วนำมาอัปเดตแคชให้ REST API ดึงค่าล่าสุดไปตอบได้ทันทีแบบ Sub-millisecond
  

---