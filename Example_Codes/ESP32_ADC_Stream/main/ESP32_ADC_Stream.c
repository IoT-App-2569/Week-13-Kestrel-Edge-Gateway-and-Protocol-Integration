
#include <stdio.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#include "driver/gpio.h"
#include "esp_log.h"
#include "esp_adc/adc_oneshot.h"

#define LED_PIN             GPIO_NUM_2
#define POT_ADC_CHANNEL     ADC_CHANNEL_6   // GPIO34 บน ESP32 รุ่นดั้งเดิม

static const char *TAG = "ESP32_ADC_STREAM";

void app_main(void)
{
    // 1. ตั้งค่า GPIO สำหรับ LED
    ESP_ERROR_CHECK(gpio_reset_pin(LED_PIN));
    ESP_ERROR_CHECK(gpio_set_direction(LED_PIN, GPIO_MODE_OUTPUT));
    ESP_ERROR_CHECK(gpio_set_level(LED_PIN, 0));

    // 2. สร้าง ADC Unit 1
    adc_oneshot_unit_handle_t adc1_handle;

    adc_oneshot_unit_init_cfg_t init_config = {
        .unit_id = ADC_UNIT_1,
        .ulp_mode = ADC_ULP_MODE_DISABLE,
    };

    ESP_ERROR_CHECK(
        adc_oneshot_new_unit(&init_config, &adc1_handle)
    );

    // 3. ตั้งค่า ADC Channel 6 (GPIO34)
    adc_oneshot_chan_cfg_t chan_config = {
        .bitwidth = ADC_BITWIDTH_12,
        .atten = ADC_ATTEN_DB_12,
    };

    ESP_ERROR_CHECK(
        adc_oneshot_config_channel(
            adc1_handle,
            POT_ADC_CHANNEL,
            &chan_config
        )
    );

    ESP_LOGI(TAG, "ADC initialized. Starting telemetry stream.");

    // 4. อ่าน ADC และส่ง JSON ผ่าน Serial
    while (1)
    {
        int raw = 0;

        esp_err_t err = adc_oneshot_read(
            adc1_handle,
            POT_ADC_CHANNEL,
            &raw
        );

        if (err == ESP_OK)
        {
            // คำนวณแรงดันโดยประมาณ
            float voltage = (raw / 4095.0f) * 3.3f;

            // ส่ง JSON หนึ่งบรรทัดต่อหนึ่งชุดข้อมูล
            printf(
                "{\"voltage\":%.2f,\"raw\":%d}\n",
                (double)voltage,
                raw
            );

            // บังคับให้ข้อมูลถูกส่งออกทันที
            fflush(stdout);
        }
        else
        {
            ESP_LOGE(TAG, "ADC read failed: %s", esp_err_to_name(err));
        }

        vTaskDelay(pdMS_TO_TICKS(500));
    }
}
