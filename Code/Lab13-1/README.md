# Lab 13.1: Embedded MQTT Broker in Kestrel (MQTTnet)

Kestrel runs **both** HTTP (port 5000, localhost only) and an **MQTT broker** (port 1883, LAN).
There is no separate Mosquitto server.

```
ConvergedEdgeGateway/
├── ConvergedEdgeGateway.csproj   # MQTTnet + MQTTnet.AspNetCore (versions in ../../Directory.Packages.props)
├── Program.cs                    # Kestrel ports, MQTT auth, connect/disconnect/publish interceptors
├── appsettings.json              # ports only, no secrets
└── Properties/launchSettings.json
```

## Run

```powershell
cd Code\Lab13-1\ConvergedEdgeGateway
dotnet user-secrets set "Mqtt:Username" "student"
dotnet user-secrets set "Mqtt:Password" "<at-least-8-characters>"
dotnet run
```

Expected: `Now listening on: http://localhost:5000` and `http://[::]:1883`.
If the secrets are missing, the program stops and prints how to set them.

## Test (two more PowerShell windows)

```powershell
mosquitto_sub -h 127.0.0.1 -p 1883 -u student -P $env:MQTT_PASS -i sub_client -t "test/edge/topic" -v
```

```powershell
mosquitto_pub -h 127.0.0.1 -p 1883 -u student -P $env:MQTT_PASS -i pub_client -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!"
```

| Check | Expected |
| :--- | :--- |
| `curl.exe http://localhost:5000/` | `🚀 Kestrel Converged Edge Gateway ... is RUNNING!` |
| sub/pub with correct password | green "Client เชื่อมต่อ" log, cyan "MQTT Message" log, `mosquitto_sub` prints the message |
| no `-u/-P` or wrong password | `Connection Refused: bad user name or password`, gateway logs `⛔` |
| no `-i` | still connects (the broker assigns a random ClientId) |

Verified on this machine: correct login connects and delivers messages. Anonymous and wrong-password clients are refused.
