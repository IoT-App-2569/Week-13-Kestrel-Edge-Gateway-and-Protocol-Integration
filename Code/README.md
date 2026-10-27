# Week 13 Lab Code: Kestrel Converged Edge Gateway

Working code for Labsheets 13.1–13.4. Each lab is a **standalone project** that builds on the one before it.

| Folder | Labsheet | What it adds |
| :--- | :--- | :--- |
| [Lab13-1](Lab13-1/README.md) | 13.1 | Embedded MQTT broker (MQTTnet) inside Kestrel, with username/password |
| [Lab13-2](Lab13-2/README.md) | 13.2 | ESP32 firmware + `SerialBridgeWorker`: Serial → MQTT + REST, REST → LED |
| [Lab13-3](Lab13-3/README.md) | 13.3 | SignalR hub + Glassmorphism dashboard + 4 protocol counters |
| [Lab13-4](Lab13-4/README.md) | 13.4 | Full integration: one LED controller for MQTT / SignalR / REST + Flutter app |

## Versions (pinned)

| Component | Version | Where it is pinned |
| :--- | :--- | :--- |
| .NET SDK | 10.0.400 (roll forward to newer 10.0.x feature bands only) | [global.json](global.json) |
| Target framework | `net10.0` (LTS) | [Directory.Build.props](Directory.Build.props) |
| MQTTnet / MQTTnet.AspNetCore | 4.3.7.1207 (4.x API used by the labsheets; 5.x is not compatible) | [Directory.Packages.props](Directory.Packages.props) |
| System.IO.Ports | 10.0.12 | [Directory.Packages.props](Directory.Packages.props) |
| NuGet transitive packages | exact versions | `packages.lock.json` in each project |
| SignalR JS client | @microsoft/signalr 10.0.11, served from `wwwroot/lib` | `wwwroot/lib/signalr/VERSION.txt` |
| ESP-IDF | `>=5.4.0,<6.2.0` (built and tested with v6.1 and v5.4.4) | `esp32_serial_bridge/main/idf_component.yml` |
| Flutter / Dart | Flutter 3.47.7 / Dart `^3.13.5`, `mqtt_client` 10.13.0 | `pubspec.yaml` + `pubspec.lock` |

NuGet Audit runs on every restore and **fails the build** if a package has a known vulnerability.
To check manually:

```powershell
dotnet list package --vulnerable --include-transitive
```

## Security rules used in every lab

1. **No secrets in the repo.** The MQTT password and REST API key are read from `dotnet user-secrets`
   (stored in `%APPDATA%\Microsoft\UserSecrets\`, outside this folder) or from environment variables
   (`Mqtt__Password`). The gateway **refuses to start** if they are missing or too short.
2. **MQTT broker requires username/password.** Wrong credentials get `BadUserNameOrPassword`.
   Passwords are compared in constant time.
3. **Only the gateway can publish its own topics** (`iot/esp32/telemetry`, `status`, `led_ack`).
   A client cannot fake sensor data.
4. **Hardware commands are allowlisted** (`LED:ON` / `LED:OFF` only). Serial input is validated
   (JSON, value ranges, line length) and re-serialised before it is forwarded.
5. **REST control needs `X-Api-Key`** and is rate limited (10 requests / 10 s per IP → `429`).
6. **The HTTP dashboard/REST listens on localhost only by default.** Set `Gateway:HttpLanAccess=true`
   only when another machine must reach it. MQTT (1883) listens on the LAN so the phone can connect.
7. **The dashboard works offline and sends a strict CSP.** No CDN scripts, no inline scripts,
   and `textContent` instead of `innerHTML`.

### Wi-Fi and Wi-Fi passwords

* The ESP32 talks to the gateway over **USB serial only**, so the firmware contains **no Wi-Fi SSID or password**.
* The phone (Flutter) joins Wi-Fi itself. The app only needs the gateway IP and the MQTT login, which go in
  `gateway.env.json`. That file is ignored by git (copy it from `gateway.env.example.json`).
* **Never reuse your Wi-Fi password as the MQTT password.** Values given with `--dart-define` are
  compiled into the app and can be extracted from it.
* MQTT on port 1883 is **not encrypted**. Run the lab only on a trusted private Wi-Fi (WPA2/WPA3),
  not on a public hotspot. In Windows, set that network's profile to **Private** and open the
  firewall for 1883 on the Private profile only. For production, use MQTT over TLS (port 8883, Week 11).

## One-time setup (per lab project)

```powershell
cd Code\Lab13-4\ConvergedEdgeGateway
dotnet user-secrets set "Mqtt:Username" "student"
dotnet user-secrets set "Mqtt:Password" "<at-least-8-characters>"
dotnet user-secrets set "Gateway:ApiKey" ([guid]::NewGuid().ToString("N"))
dotnet user-secrets list
```

Each lab has its own `UserSecretsId`, so repeat this in each lab folder (Lab13-1 needs only the MQTT pair).

## Tools for testing

* `mosquitto_pub` / `mosquitto_sub` (Mosquitto for Windows) or MQTTX. Always pass `-u` and `-P`.
* In Windows PowerShell 5.1, `curl` is an alias for `Invoke-WebRequest`. Use **`curl.exe`** as shown in the lab READMEs.

## Known issue on this PC: Smart App Control

Windows **Smart App Control** is on. It sometimes blocks freshly built, unsigned files
(`An Application Control policy has blocked this file`). If `dotnet run` fails with that message,
run a Release build directly:

```powershell
dotnet build -c Release
dotnet bin\Release\net10.0\ConvergedEdgeGateway.dll --environment Development
```

`--environment Development` is required. User-secrets are only loaded in the Development environment, and
`launchSettings.json` (which normally sets it) is not used when you run the `.dll` directly.

The Flutter Windows `.exe` and ESP-IDF's `openocd` were also blocked here. Running the Flutter app on an Android phone is not affected.
