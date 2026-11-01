### Lab13-2

### กิจกรรมที่ 13-2.3: ปรับแต่ง `Program.cs` ให้เปิดใช้ REST APIs

เปิด `Program.cs` และปรับปรุงส่วนการลงทะเบียนบริการและเส้นทาง API:

```csharp
using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);

// ตั้งค่า Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
    options.ListenAnyIP(1883, l => l.UseMqtt());
});

// ลงทะเบียน Services
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddMqttServer(options => options.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();
// เพื่อให้ API เรียกใช้งาน Worker ได้
builder.Services.AddSingleton(sp => 
    sp.GetServices<IHostedService>().OfType<SerialBridgeWorker>().First());

var app = builder.Build();

app.UseMqttServer(server => { /* callbacks */ });

// 1. API อ่านข้อมูลเซนเซอร์ล่าสุด
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) =>
{
    var data = store.Get("latest_telemetry");
    return data != null ? Results.Ok(data) : Results.NotFound(new { error = "No telemetry yet" });
});

// 2. API ควบคุมหลอดไฟผ่าน Serial Bridge
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

---

### กิจกรรมที่ 13-2.4: โค้ดฝั่ง ESP32 สำหรับสื่อสารสองทางผ่าน Serial (`main.c`)

สำหรับฝั่งไมโครคอนโทรลเลอร์ ให้เขียนโค้ดในโปรเจกต์ ESP-IDF เพื่ออ่านค่าแอนะล็อกและสตรีม JSON ออกทาง Serial รวมถึงอ่านคำสั่งควบคุมหลอดไฟจาก C# Gateway:

```c
#include <stdio.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/uart.h"
#include "esp_log.h"
#include "esp_adc/adc_oneshot.h"

#define LED_PIN             GPIO_NUM_2       // Onboard LED บน ESP32
#define POT_ADC_CHANNEL     ADC_CHANNEL_6    // GPIO 34 (ADC1 Channel 6)

void app_main(void)
{
    // 1. ตั้งค่า GPIO สำหรับไฟ LED
    gpio_reset_pin(LED_PIN);
    gpio_set_direction(LED_PIN, GPIO_MODE_OUTPUT);
    gpio_set_level(LED_PIN, 0);

    // 2. ตั้งค่า ADC Unit 1
    adc_oneshot_unit_handle_t adc1_handle;
    adc_oneshot_unit_init_cfg_t init_config = {
        .unit_id = ADC_UNIT_1,
        .ulp_mode = ADC_ULP_MODE_DISABLE,
    };
    ESP_ERROR_CHECK(adc_oneshot_new_unit(&init_config, &adc1_handle));

    // 3. กำหนดความละเอียด 12-bit (0-4095) และ Attenuation 12dB (0 - 3.3V)
    adc_oneshot_chan_cfg_t chan_config = {
        .bitwidth = ADC_BITWIDTH_12,
        .atten = ADC_ATTEN_DB_12,
    };
    ESP_ERROR_CHECK(adc_oneshot_config_channel(adc1_handle, POT_ADC_CHANNEL, &chan_config));

    // 4. ติดตั้ง UART Driver บนพอร์ตเริ่มต้น เพื่อให้อ่านคำสั่งผ่าน Serial แบบ Non-blocking ได้
    uart_driver_install(UART_NUM_0, 256, 0, 0, NULL, 0);

    uint8_t rx_buf[64];

    while (1) {
        // --- ส่วนที่ 1: อ่านค่า ADC และส่งออก JSON ทาง Serial ---
        int raw = 0;
        ESP_ERROR_CHECK(adc_oneshot_read(adc1_handle, POT_ADC_CHANNEL, &raw));
        float voltage = (raw / 4095.0f) * 3.3f;

        // พิมพ์ JSON สตรีมมิ่งออก Serial
        printf("{\"voltage\":%.2f,\"raw\":%d}\n", voltage, raw);

        // --- ส่วนที่ 2: ตรวจจับคำสั่งจาก C# Kestrel (LED:ON / LED:OFF) ---
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


---

### กิจกรรมที่ 13-2.5: การทดสอบสะพานเชื่อมต่อ

> [!WARNING] **ข้อพึงระวังเรื่องพอร์ตสื่อสารอนุกรม (Serial Port Exclusive Lock):**
> 1. ตรวจสอบหมายเลข COM Port ของบอร์ด ESP32 ผ่าน Device Manager แล้วแก้ไขตัวแปร `_portName` ในไฟล์ `Services.cs` ให้ตรงกับเครื่องของคุณ (เช่น `COM24`)
> 2. **ต้องปิด `idf monitor`, PuTTY หรือ Serial Monitor อื่นๆ ก่อนรัน Kestrel เสมอ** เพราะบน Windows พอร์ต COM จะถูกล็อกใช้งานแบบ Exclusive Access (เปิดได้เพียง 1 โปรแกรมในเวลาเดียวกัน) หากมีโปรแกรมอื่นเปิดพอร์ตค้างไว้ C# จะเปิดพอร์ตไม่สำเร็จและส่งคำสั่งไม่ได้ (`sentToSerial: false`)

1. เสียบบอร์ด ESP32 เข้ากับเครื่อง และสั่งรัน Kestrel:
```powershell
dotnet run
```
2. ทดสอบเรียกดูค่าเซนเซอร์ผ่านเบราว์เซอร์หรือ cURL:
```powershell
curl http://localhost:5000/api/telemetry/latest
```
*สังเกตว่าจะได้ข้อมูล JSON ของแรงดันไฟฟ้าทันที!*
3. เปิด `mosquitto_sub`:
```powershell
mosquitto_sub -h 127.0.0.1 -p 1883 -t "iot/esp32/telemetry" -i "sub_client" -v
```
*สังเกตว่าข้อมูลที่อ่านจากสาย USB Serial ถูกส่งต่อเข้าหัวข้อ MQTT อัตโนมัติ!*
4. ทดสอบสั่งเปิด-ปิดไฟผ่าน REST API:
```powershell
curl -X POST http://localhost:5000/api/led/on
```
*สังเกตว่าคำสั่ง `LED:ON` ถูกส่งผ่านสาย USB เข้าไปควบคุมบอร์ดทันที!*

---

## 4. คำถามท้ายการทดลอง
1. สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?
    สถาปัตยกรรมนี้เปลี่ยน Edge Gateway (Kestrel) ให้ทำหน้าที่เป็น Protocol Translator (ตัวแปลโปรโตคอล) เพื่อให้อุปกรณ์รุ่นเก่าเข้าร่วมระบบ IoT ได้ดังนี้:

    Hardware Abstraction & Modernization:
    อุปกรณ์อุตสาหกรรมรุ่นเก่า (Legacy Devices) หรือไมโครคอนโทรลเลอร์ที่ไม่มี Wi-Fi/Ethernet stack (เช่น RS-232, RS-485, UART) สามารถส่งข้อมูลดิบผ่านสายอนุกรม (Serial) เข้าหา Gateway จากนั้น Gateway จะทำหน้าที่แปลงโครงสร้างข้อมูลและห่อหุ้ม (Wrap) ให้อยู่ในรูป MQTT Payload

    Seamless Integration:
    เปิดโอกาสให้อุปกรณ์รุ่นเก่าสามารถ Publish/Subscribe ข้อมูลร่วมกับแอปพลิเคชันสมัยใหม่, Cloud Platforms, MQTT Brokers หรือ Dashboard บนระบบเครือข่ายได้ทันที โดย ไม่ต้องแก้ไขฮาร์ดแวร์เดิม หรือลงทุนเปลี่ยนอุปกรณ์ใหม่ยกชุด

    Security & Decoupling:
    อุปกรณ์ Legacy ไม่ต้องเผชิญความเสี่ยงจากการเชื่อมต่อเครือข่าย IP โดยตรง Edge Gateway จะทำหน้าที่เป็นปราการกรองข้อมูล และจัดการเรื่อง Encryption (TLS/SSL) รวมถึง Authentication ก่อนส่งออกสู่เครือข่ายภายนอก

2. เพราะเหตุใดเราจึงต้องใช้ `BackgroundService` ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?
    การป้องกัน Serial Port Lock Conflict (Single Ownership):
    พอร์ต COM บนระบบปฏิบัติการรองรับการเชื่อมต่อแบบ Exclusive Access (เปิดได้ทีละ 1 Process/Handle เท่านั้น) หากเปิดพอร์ตภายในฟังก์ชัน REST API ทุกครั้งที่มี HTTP Request เข้ามา จะเกิดความขัดแย้งในการเข้าถึงพอร์ต (Access Denied / Port Locked) การใช้ BackgroundService ช่วยให้เซิร์ฟเวอร์เปิดพอร์ตค้างไว้เพียงครั้งเดียวตลอด Lifecycle ของแอปพลิเคชัน

    Continuous Streaming & Non-Blocking Execution:
    ข้อมูลจาก Serial สตรีมเข้ามาแบบ Real-time และต่อเนื่อง การใช้ Loop อ่านข้อมูลใน BackgroundService ที่แยก Thread ออกจากหลัก จะไม่ไปบล็อก (Block) การทำงานของ Web Server (Kestrel Thread Pool) ทำให้ REST API ยังคงรับ HTTP Requests จากผู้ใช้ภายนอกได้อย่างรวดเร็ว

    State Management & Decoupling:BackgroundService จะทำหน้าที่รับข้อมูล Serial มาจัดเก็บไว้ใน Memory Store (GatewayStateStore) หรือ Publish เข้า MQTT Broker อย่างต่อเนื่อง เมื่อมีผู้ใช้เรียก GET /api/telemetry/latest ตัว REST API แค่ดึงค่าล่าสุดจาก Memory Store ไปตอบกลับทันที (Response time ระดับ milliseconds) โดยไม่ต้องรอให้วงรอบการอ่าน Serial สมบูรณ์

