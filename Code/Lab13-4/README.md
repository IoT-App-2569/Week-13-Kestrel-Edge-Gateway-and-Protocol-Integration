# Lab 13.4 (Grand Finale): ESP32 + Kestrel Gateway + Flutter + Web

```
Lab13-4/
├── ConvergedEdgeGateway/          # the complete gateway
│   ├── Services.cs                # Topics, GatewayPublisher, LedController, SerialBridgeWorker
│   ├── Hubs.cs                    # GatewayHub (sends current state on connect), ProtocolMetrics
│   ├── Program.cs                 # MQTT auth + topic ACL + control handler, REST, SignalR
│   ├── Security.cs
│   └── wwwroot/                   # dashboard (shows ESP32 online/offline), local signalr.min.js
└── iot_unified_dashboard/         # Flutter app (Week 12 Lab 12-4 + gateway auth/config)
    ├── lib/gateway_config.dart    # host / port / MQTT login from --dart-define-from-file
    └── gateway.env.example.json   # copy to gateway.env.json (git-ignored)
```

Firmware: same as [Lab13-2/esp32_serial_bridge](../Lab13-2/esp32_serial_bridge).

## How the LED stays in sync everywhere

Every LED command goes through **one** `LedController.SetAsync()`:

```
Flutter  --MQTT iot/esp32/control {"led":"ON"}--+
Web      --SignalR ToggleLed(true)--------------+--> LedController --Serial "LED:ON"--> ESP32 GPIO2
3rd-party--REST POST /api/led/on (X-Api-Key)---+         |
                                                          +--> MQTT iot/esp32/led_ack {"led_state":true} (retained) --> Flutter
                                                          +--> SignalR "LedStateUpdated"                          --> Web
```

* `iot/esp32/status` (retained) = `online`/`offline` when the serial port opens or closes. Flutter shows this as its green/red dot.
* The board reports its real LED state in every telemetry line. If it differs (for example after a board reset),
  the gateway re-syncs all screens.

## 13-4.1 Prepare

1. **ESP32**: flash `Lab13-2/esp32_serial_bridge`, then close `idf monitor`.
2. **Gateway**:
   ```powershell
   cd Code\Lab13-4\ConvergedEdgeGateway
   dotnet user-secrets set "Mqtt:Username" "student"
   dotnet user-secrets set "Mqtt:Password" "<at-least-8-characters>"
   dotnet user-secrets set "Gateway:ApiKey" ([guid]::NewGuid().ToString("N"))
   dotnet run -- --Serial:PortName=COM24
   ```
3. **Firewall** (once, PowerShell as Administrator; your Wi-Fi profile must be *Private*):
   ```powershell
   New-NetFirewallRule -DisplayName "Kestrel MQTT 1883 (Lab13)" -Direction Inbound -Protocol TCP -LocalPort 1883 -Action Allow -Profile Private
   ```
4. **Browser**: http://localhost:5000 should show `SignalR Online · ESP32 Online`.
5. **Flutter**:
   ```powershell
   cd Code\Lab13-4\iot_unified_dashboard
   copy gateway.env.example.json gateway.env.json   # then edit: PC's LAN IP (ipconfig) + MQTT login
   flutter pub get
   flutter run --dart-define-from-file=gateway.env.json
   ```
   The phone and the PC must be on the **same Wi-Fi**.

## 13-4.2 Verification matrix

| Test | Action | Expected |
| :--- | :--- | :--- |
| 1. Universal Telemetry Sync | turn the potentiometer | web number and bar + Flutter gauge move together |
| 2. Mobile → Web | Flutter switch ON (MQTT) | LED on; web button turns green "ON" |
| 3. Web → Mobile | click the web button OFF (SignalR) | LED off; Flutter switch turns OFF |
| 4. REST override | `curl.exe -X POST -H "X-Api-Key: $key" http://localhost:5000/api/led/on` | LED on; web **and** Flutter show ON |

Useful while testing: `curl.exe http://localhost:5000/api/health`, `/api/led`, `/api/metrics`.

## 13-4.3 Local survivability

Disconnect the Internet (keep only the local Wi-Fi). Everything above keeps working. The broker runs inside the gateway, and the
dashboard's SignalR script is served from `wwwroot/lib`, not a CDN.

## What was verified on this machine (Simulated mode, no board)

| Item | Result |
| :--- | :--- |
| Gateway build (Release), NuGet vulnerability scan | ✅ no warnings, no vulnerable packages |
| Test 2: MQTT `{"led":"ON"}` | ✅ `led_ack {"led_state":true}`, `/api/led` = true |
| Test 3: web button (built-in browser) | ✅ MQTT listener got `led_ack` true then false |
| Test 4: REST `POST /api/led/off` | ✅ `led_ack {"led_state":false}` delivered to MQTT client |
| New client gets retained `status online` | ✅ |
| Fake `led_ack` / garbage on `control` | ✅ dropped, state unchanged |
| Flutter `analyze` / `test` / `build windows` | ✅ / ✅ / ✅ (the `.exe` itself was blocked by Smart App Control, so the app UI was not run here) |
| Real ESP32 + phone | ⏳ needs the hardware. Run the matrix above and record your results in `Answer/Lab13-4.md` |
