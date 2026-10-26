#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>
#include <math.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/event_groups.h"
#include "driver/gpio.h"
#include "driver/uart.h"
#include "esp_log.h"
#include "esp_adc/adc_oneshot.h"
#include "nvs_flash.h"
#include "esp_netif.h"
#include "esp_event.h"
#include "esp_wifi.h"

/* ========================================================
 * 1. ตั้งค่าเครือข่าย Wi-Fi (เลือกใช้ Wi-Fi 1234 จากไฟล์ก่อนหน้า)
 * ======================================================== */
#define WIFI_SSID           "1234_2.4G"
#define WIFI_PASS           "123456789"

#define LED_PIN             GPIO_NUM_2       // Onboard LED บน ESP32
#define POT_ADC_CHANNEL     ADC_CHANNEL_6    // GPIO 34 (ADC1 Channel 6)

static const char *TAG = "ESP32_ADC_STREAM";

/* ========================================================
 * 2. ตัวแปรจำลองค่า Potentiometer (สำหรับผู้ที่ไม่มีตัวต้านทานปรับค่าได้)
 * ======================================================== */
static bool  s_use_virtual_pot = true;       // เปิดโหมดจำลองค่าเริ่มต้นสำหรับผู้ไม่มี Potentiometer
static bool  s_auto_sweep      = true;       // ให้ค่าวิ่งขึ้น-ลงแบบ Sine Wave อัตโนมัติ (0 - 4095)
static int   s_manual_adc      = 2048;       // ค่าจำลองแบบกำหนดเองผ่านคำสั่ง Serial (ADC:xxxx)
static float s_sim_angle       = 0.0f;

static void wifi_event_handler(void* arg, esp_event_base_t event_base, int32_t event_id, void* event_data)
{
    if (event_base == WIFI_EVENT && event_id == WIFI_EVENT_STA_START) {
        esp_wifi_connect();
    } else if (event_base == WIFI_EVENT && event_id == WIFI_EVENT_STA_DISCONNECTED) {
        esp_wifi_connect();
    } else if (event_base == IP_EVENT && event_id == IP_EVENT_STA_GOT_IP) {
        ip_event_got_ip_t* event = (ip_event_got_ip_t*) event_data;
        ESP_LOGI(TAG, "Wi-Fi Connected (%s) - IP: " IPSTR, WIFI_SSID, IP2STR(&event->ip_info.ip));
    }
}

static void wifi_init_sta_nonblocking(void)
{
    esp_err_t ret = nvs_flash_init();
    if (ret == ESP_ERR_NVS_NO_FREE_PAGES || ret == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_ERROR_CHECK(nvs_flash_erase());
        ret = nvs_flash_init();
    }
    ESP_ERROR_CHECK(ret);

    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());
    esp_netif_create_default_wifi_sta();

    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&cfg));

    ESP_ERROR_CHECK(esp_event_handler_instance_register(WIFI_EVENT, ESP_EVENT_ANY_ID, &wifi_event_handler, NULL, NULL));
    ESP_ERROR_CHECK(esp_event_handler_instance_register(IP_EVENT, IP_EVENT_STA_GOT_IP, &wifi_event_handler, NULL, NULL));

    wifi_config_t wifi_config = {
        .sta = {
            .ssid = WIFI_SSID,
            .password = WIFI_PASS,
            .threshold.authmode = WIFI_AUTH_WPA2_PSK,
        },
    };

    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &wifi_config));
    ESP_ERROR_CHECK(esp_wifi_start());
}

void app_main(void)
{
    // 1. ตั้งค่า GPIO สำหรับไฟ LED Status (GPIO2)
    gpio_reset_pin(LED_PIN);
    gpio_set_direction(LED_PIN, GPIO_MODE_OUTPUT);
    gpio_set_level(LED_PIN, 0);

    // 2. เริ่มต้นการเชื่อมต่อ Wi-Fi ("1234_2.4G") แบบไม่บล็อกการทำงานของ Serial Bridge
    wifi_init_sta_nonblocking();

    // 3. ตั้งค่า ADC Unit 1
    adc_oneshot_unit_handle_t adc1_handle;
    adc_oneshot_unit_init_cfg_t init_config = {
        .unit_id = ADC_UNIT_1,
        .ulp_mode = ADC_ULP_MODE_DISABLE,
    };
    ESP_ERROR_CHECK(adc_oneshot_new_unit(&init_config, &adc1_handle));

    // 4. กำหนดความละเอียด 12-bit (0-4095) และ Attenuation 12dB (0 - 3.3V)
    adc_oneshot_chan_cfg_t chan_config = {
        .bitwidth = ADC_BITWIDTH_12,
        .atten = ADC_ATTEN_DB_12,
    };
    ESP_ERROR_CHECK(adc_oneshot_config_channel(adc1_handle, POT_ADC_CHANNEL, &chan_config));

    // 5. ติดตั้ง UART Driver บนพอร์ตเริ่มต้น เพื่อให้อ่านคำสั่งผ่าน Serial แบบ Non-blocking ได้
    uart_driver_install(UART_NUM_0, 256, 0, 0, NULL, 0);

    uint8_t rx_buf[64];

    while (1) {
        // --- ส่วนที่ 1: อ่านค่า ADC หรือจำลองค่าสำหรับผู้ไม่มี Potentiometer ---
        int raw = 0;
        ESP_ERROR_CHECK(adc_oneshot_read(adc1_handle, POT_ADC_CHANNEL, &raw));

        if (s_use_virtual_pot || raw < 100) {
            if (s_auto_sweep) {
                float sin_val = (sinf(s_sim_angle) + 1.0f) / 2.0f;
                raw = (int)(sin_val * 4095.0f);
                s_sim_angle += 0.15f;
                if (s_sim_angle >= 6.2831853f) {
                    s_sim_angle -= 6.2831853f;
                }
            } else {
                raw = s_manual_adc;
            }
        }

        float voltage = (raw / 4095.0f) * 3.3f;
        unsigned long uptime_sec = (unsigned long)(esp_log_timestamp() / 1000);

        // พิมพ์ JSON สตรีมมิ่งออก Serial
        printf("{\"voltage\":%.2f,\"raw\":%d,\"uptime\":%lu}\n", voltage, raw, uptime_sec);

        // --- ส่วนที่ 2: ตรวจจับคำสั่งจาก C# Kestrel (LED:ON / LED:OFF / ADC:xxxx / SIM:AUTO / SIM:HW) ---
        int len = uart_read_bytes(UART_NUM_0, rx_buf, sizeof(rx_buf) - 1, pdMS_TO_TICKS(50));
        if (len > 0) {
            rx_buf[len] = '\0';
            char *cmd_str = (char *)rx_buf;

            if (strstr(cmd_str, "LED:ON") != NULL) {
                gpio_set_level(LED_PIN, 1);
            } else if (strstr(cmd_str, "LED:OFF") != NULL) {
                gpio_set_level(LED_PIN, 0);
            }

            char *adc_ptr = strstr(cmd_str, "ADC:");
            if (adc_ptr != NULL) {
                int val = atoi(adc_ptr + 4);
                if (val < 0) val = 0;
                if (val > 4095) val = 4095;
                s_manual_adc = val;
                s_use_virtual_pot = true;
                s_auto_sweep = false;
            } else if (strstr(cmd_str, "SIM:AUTO") != NULL) {
                s_use_virtual_pot = true;
                s_auto_sweep = true;
            } else if (strstr(cmd_str, "SIM:HW") != NULL) {
                s_use_virtual_pot = false;
            }
        }

        vTaskDelay(pdMS_TO_TICKS(450));
    }
}
