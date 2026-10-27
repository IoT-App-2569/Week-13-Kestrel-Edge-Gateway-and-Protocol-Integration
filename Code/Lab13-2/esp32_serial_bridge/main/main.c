/*
 * Lab 13.2 - ESP32 Serial Bridge firmware (ESP-IDF v5.4.x)
 *
 *  - อ่านค่า Potentiometer ที่ GPIO34 (ADC1_CH6) แล้วส่ง JSON ออก UART0 (USB) ทุก 500 ms
 *      {"voltage":1.65,"raw":2048,"led":0}
 *  - รับคำสั่งจาก Kestrel Gateway ทาง UART0 ทีละบรรทัด: "LED:ON" / "LED:OFF" -> GPIO2
 *
 * บอร์ดนี้คุยกับเกตเวย์ผ่านสาย USB เท่านั้น ไม่ได้ใช้ Wi-Fi จึงไม่มี SSID/รหัสผ่านอยู่ในเฟิร์มแวร์
 *
 * CONFIG_LAB_SIMULATE_ADC=y (ตั้งใน sdkconfig.defaults หรือ idf.py menuconfig) = ไม่มี Potentiometer
 * ให้สุ่มค่า ADC แบบ random walk แทนการอ่าน GPIO34 ส่วน Serial / JSON / LED ยังทำงานจริงทั้งหมด
 */
#include <stdio.h>
#include <string.h>
#include <stdbool.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/uart.h"
#include "esp_log.h"
#include "esp_random.h"
#include "esp_adc/adc_oneshot.h"
#include "esp_adc/adc_cali.h"
#include "esp_adc/adc_cali_scheme.h"

#define LED_PIN             GPIO_NUM_2       // Onboard LED บน ESP32 DevKit
#define POT_ADC_CHANNEL     ADC_CHANNEL_6    // GPIO34 (ADC1 Channel 6)
#define POT_ADC_ATTEN       ADC_ATTEN_DB_12  // ช่วงวัด ~0 - 3.1 V
#define BRIDGE_UART         UART_NUM_0       // UART เดียวกับสาย USB / console
#define UART_RX_BUF_SIZE    1024
#define CMD_MAX_LEN         32
#define TELEMETRY_PERIOD_MS 500
#define ADC_SAMPLES         8                // เฉลี่ยหลายค่าเพื่อลด noise

static const char *TAG = "serial_bridge";
static volatile bool s_led_on = false;

static void set_led(bool on)
{
    s_led_on = on;
    gpio_set_level(LED_PIN, on ? 1 : 0);
}

// รับคำสั่งทีละบรรทัดจากเกตเวย์ ยอมรับเฉพาะคำสั่งที่รู้จัก (allowlist)
static void handle_command(const char *cmd)
{
    if (strcmp(cmd, "LED:ON") == 0) {
        set_led(true);
    } else if (strcmp(cmd, "LED:OFF") == 0) {
        set_led(false);
    }
    // คำสั่งอื่นไม่สนใจ
}

static void uart_rx_task(void *arg)
{
    uint8_t byte;
    char line[CMD_MAX_LEN + 1];
    size_t len = 0;
    bool overflow = false;

    while (1) {
        if (uart_read_bytes(BRIDGE_UART, &byte, 1, portMAX_DELAY) != 1) {
            continue;
        }
        if (byte == '\n' || byte == '\r') {
            if (len > 0 && !overflow) {
                line[len] = '\0';
                handle_command(line);
            }
            len = 0;
            overflow = false;
        } else if (len < CMD_MAX_LEN) {
            line[len++] = (char)byte;
        } else {
            overflow = true;   // บรรทัดยาวเกิน: ทิ้งทั้งบรรทัด
        }
    }
}

static bool adc_calibration_init(adc_cali_handle_t *out_handle)
{
#if ADC_CALI_SCHEME_LINE_FITTING_SUPPORTED
    adc_cali_line_fitting_config_t cali_config = {
        .unit_id = ADC_UNIT_1,
        .atten = POT_ADC_ATTEN,
        .bitwidth = ADC_BITWIDTH_12,
    };
    if (adc_cali_create_scheme_line_fitting(&cali_config, out_handle) == ESP_OK) {
        return true;
    }
#elif ADC_CALI_SCHEME_CURVE_FITTING_SUPPORTED
    adc_cali_curve_fitting_config_t cali_config = {
        .unit_id = ADC_UNIT_1,
        .chan = POT_ADC_CHANNEL,
        .atten = POT_ADC_ATTEN,
        .bitwidth = ADC_BITWIDTH_12,
    };
    if (adc_cali_create_scheme_curve_fitting(&cali_config, out_handle) == ESP_OK) {
        return true;
    }
#endif
    return false;
}

void app_main(void)
{
    // log ของระบบต้องไม่ปนกับ JSON มากเกินไป: แสดงเฉพาะ warning ขึ้นไป
    esp_log_level_set("*", ESP_LOG_WARN);

    // 1. ตั้งค่า GPIO สำหรับไฟ LED
    gpio_reset_pin(LED_PIN);
    gpio_set_direction(LED_PIN, GPIO_MODE_OUTPUT);
    set_led(false);

    // 2. ตั้งค่า ADC Unit 1 แบบ 12-bit (0-4095)
    adc_oneshot_unit_handle_t adc1_handle;
    adc_oneshot_unit_init_cfg_t init_config = {
        .unit_id = ADC_UNIT_1,
        .ulp_mode = ADC_ULP_MODE_DISABLE,
    };
    ESP_ERROR_CHECK(adc_oneshot_new_unit(&init_config, &adc1_handle));

    adc_oneshot_chan_cfg_t chan_config = {
        .bitwidth = ADC_BITWIDTH_12,
        .atten = POT_ADC_ATTEN,
    };
    ESP_ERROR_CHECK(adc_oneshot_config_channel(adc1_handle, POT_ADC_CHANNEL, &chan_config));

    adc_cali_handle_t cali_handle = NULL;
    bool calibrated = adc_calibration_init(&cali_handle);
    if (!calibrated) {
        ESP_LOGW(TAG, "ADC calibration not available, using linear 3.3 V scale");
    }

    // 3. ติดตั้ง UART Driver เพื่ออ่านคำสั่งจาก Gateway (TX ยังใช้ printf ตามปกติ)
    ESP_ERROR_CHECK(uart_driver_install(BRIDGE_UART, UART_RX_BUF_SIZE, 0, 0, NULL, 0));
    xTaskCreate(uart_rx_task, "uart_rx", 3072, NULL, 10, NULL);

#if CONFIG_LAB_SIMULATE_ADC
    int sim_raw = 2048;
    ESP_LOGW(TAG, "LAB_SIMULATE_ADC=y: sending random values, GPIO34 is not read");
#endif

    while (1) {
#if CONFIG_LAB_SIMULATE_ADC
        // 4. ไม่มี Potentiometer: สุ่มค่าแบบ random walk (เปลี่ยนทีละ -150..+150) ให้ดูเหมือนหมุนจริง
        sim_raw += (int)(esp_random() % 301) - 150;
        if (sim_raw < 0) sim_raw = 0;
        if (sim_raw > 4095) sim_raw = 4095;
        int raw = sim_raw;
        bool use_cali = false;
#else
        // 4. อ่านค่า ADC (เฉลี่ย ADC_SAMPLES ครั้ง)
        int sum = 0;
        for (int i = 0; i < ADC_SAMPLES; i++) {
            int sample = 0;
            ESP_ERROR_CHECK(adc_oneshot_read(adc1_handle, POT_ADC_CHANNEL, &sample));
            sum += sample;
        }
        int raw = sum / ADC_SAMPLES;
        bool use_cali = calibrated;
#endif

        float voltage;
        int mv = 0;
        if (use_cali && adc_cali_raw_to_voltage(cali_handle, raw, &mv) == ESP_OK) {
            voltage = mv / 1000.0f;
        } else {
            voltage = (raw / 4095.0f) * 3.3f;
        }

        // 5. ส่ง JSON หนึ่งบรรทัดต่อหนึ่งตัวอย่าง (Gateway อ่านด้วย ReadLine)
        printf("{\"voltage\":%.2f,\"raw\":%d,\"led\":%d}\n", voltage, raw, s_led_on ? 1 : 0);
        fflush(stdout);

        vTaskDelay(pdMS_TO_TICKS(TELEMETRY_PERIOD_MS));
    }
}
