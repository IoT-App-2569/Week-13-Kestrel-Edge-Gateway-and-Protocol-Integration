# Lab 13.2: Hardware Serial → MQTT + REST Bridge

```
Lab13-2/
├── esp32_serial_bridge/            # ESP-IDF firmware (also used by Lab 13.3 and 13.4)
│   ├── main/main.c                 # ADC GPIO34 → JSON over USB; "LED:ON"/"LED:OFF" → GPIO2
│   ├── main/idf_component.yml      # ESP-IDF version pin: >=5.4.0,<6.2.0
│   ├── dependencies.lock           # resolved versions (commit it)
│   └── sdkconfig.defaults          # reproducible config (sdkconfig itself is generated)
└── ConvergedEdgeGateway/
    ├── Program.cs                  # Kestrel + MQTT auth + REST API (API key, rate limit)
    ├── Services.cs                 # GatewayStateStore, SerialBridgeWorker (Hardware / Simulated)
    ├── Security.cs                 # secret checks, constant-time compare, X-Api-Key filter
    └── appsettings.json            # Serial port settings, no secrets
```

## 1. Flash the ESP32

Wiring: potentiometer wiper → **GPIO34**, outer legs → 3.3 V / GND. LED → **GPIO2** (onboard LED works).
The firmware uses **USB serial only. No Wi-Fi, so there are no Wi-Fi credentials in the code.**

Open the **ESP-IDF 6.1 PowerShell** (or VS Code ESP-IDF terminal):

```powershell
cd Code\Lab13-2\esp32_serial_bridge
idf.py set-target esp32
idf.py build
idf.py -p COM3 flash monitor
```

No potentiometer? `sdkconfig.defaults` has `CONFIG_LAB_SIMULATE_ADC=y`, so the board sends random-walk ADC values (serial, JSON and LED stay real). Set it to `n` and delete `sdkconfig` when the potentiometer is wired.

In the monitor you should see one JSON line every 500 ms, such as `{"voltage":1.65,"raw":2048,"led":0}`.
Close the monitor (`Ctrl+]`) **before** starting the gateway. Windows allows only one program to open a COM port at a time.

## 2. Run the gateway

```powershell
cd Code\Lab13-2\ConvergedEdgeGateway
dotnet user-secrets set "Mqtt:Username" "student"
dotnet user-secrets set "Mqtt:Password" "<at-least-8-characters>"
dotnet user-secrets set "Gateway:ApiKey" ([guid]::NewGuid().ToString("N"))
dotnet run -- --Serial:PortName=COM3
```

No board? Run `dotnet run -- --Serial:Mode=Simulated` to generate the same JSON in software.

## 3. Test

```powershell
$key = (dotnet user-secrets list | Select-String "Gateway:ApiKey").ToString().Split("=")[1].Trim()
curl.exe http://localhost:5000/api/telemetry/latest
curl.exe -X POST -H "X-Api-Key: $key" http://localhost:5000/api/led/on
curl.exe -X POST -H "X-Api-Key: $key" http://localhost:5000/api/led/off
mosquitto_sub -h 127.0.0.1 -p 1883 -u student -P $env:MQTT_PASS -t "iot/esp32/telemetry" -v
```

| Check | Expected |
| :--- | :--- |
| `GET /api/telemetry/latest` | `{"voltage":..,"raw":..,"led":false,"timestamp":..}` (404 until the first line arrives) |
| `POST /api/led/on` with key | `sentToSerial: true` and the LED turns on |
| `POST /api/led/on` without key | `401` |
| `POST /api/led/blink` | `400` (only on/off allowed) |
| more than 10 POSTs in 10 s | `429 Too Many Requests` |
| `mosquitto_sub` on telemetry | validated JSON every 500 ms |
| `mosquitto_pub` to `iot/esp32/telemetry` | dropped. Only the gateway may publish telemetry |
| `sentToSerial: false` | the COM port is wrong or still open in `idf monitor` / PuTTY |

Verified on this machine:
* The firmware builds with ESP-IDF v6.1 and v5.4.4.
* The gateway was tested in Simulated mode. Every row in the table behaved as expected.
* Not yet tested here: flashing a real board. That needs the hardware.
