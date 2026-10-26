#include <stdio.h>
#include "freertos/FreeRTOS.h"
#include "esp_adc/adc_oneshot.h"
void app_main(void) {
    while(1) { printf("{\"voltage\":1.65,\"raw\":2048}\n"); vTaskDelay(pdMS_TO_TICKS(500)); }
}
