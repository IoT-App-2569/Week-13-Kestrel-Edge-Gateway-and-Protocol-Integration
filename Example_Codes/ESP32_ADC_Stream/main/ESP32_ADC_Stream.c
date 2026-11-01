#include <stdio.h>
#include <string.h>
#include <stdint.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#include "driver/gpio.h"
#include "driver/uart.h"

#include "esp_err.h"
#include "esp_adc/adc_oneshot.h"

// กำหนด GPIO
#define LED_PIN             GPIO_NUM_2
#define POT_ADC_CHANNEL     ADC_CHANNEL_6

// กำหนด UART
#define UART_PORT           UART_NUM_0
#define UART_BUFFER_SIZE    256

void app_main(void)
{
    // 1. ตั้งค่า GPIO สำหรับ LED
    ESP_ERROR_CHECK(gpio_reset_pin(LED_PIN));
    ESP_ERROR_CHECK(
        gpio_set_direction(LED_PIN, GPIO_MODE_OUTPUT)
    );
    ESP_ERROR_CHECK(gpio_set_level(LED_PIN, 0));

    // 2. ตั้งค่า ADC Unit 1
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

    // 4. ติดตั้ง UART สำหรับรับคำสั่ง
    ESP_ERROR_CHECK(
        uart_driver_install(
        UART_PORT,
        UART_BUFFER_SIZE,
        UART_BUFFER_SIZE,
        0,
        NULL,
        0
    )
);

    uint8_t rx_buf[UART_BUFFER_SIZE];

    printf("ESP32 ADC Serial Bridge Ready\n");

    while (1) {

        // ส่วนที่ 1: อ่านค่า ADC
        int raw = 0;

        ESP_ERROR_CHECK(
            adc_oneshot_read(
                adc1_handle,
                POT_ADC_CHANNEL,
                &raw
            )
        );

        // แปลงค่า ADC เป็น Voltage
        float voltage = (raw / 4095.0f) * 3.3f;

        // ส่งข้อมูล JSON ผ่าน Serial
        printf(
            "{\"voltage\":%.2f,\"raw\":%d}\n",
            voltage,
            raw
        );

        // ส่วนที่ 2: รับคำสั่ง LED จาก C#
        int len = uart_read_bytes(
            UART_PORT,
            rx_buf,
            sizeof(rx_buf) - 1,
            pdMS_TO_TICKS(50)
        );

        if (len > 0) {

            rx_buf[len] = '\0';

            if (strstr((char *)rx_buf, "LED:ON") != NULL) {

                gpio_set_level(LED_PIN, 1);

            }
            else if (strstr((char *)rx_buf, "LED:OFF") != NULL) {

                gpio_set_level(LED_PIN, 0);

            }
        }

        // ส่งข้อมูลประมาณทุก 500 ms
        vTaskDelay(pdMS_TO_TICKS(450));
    }
}
