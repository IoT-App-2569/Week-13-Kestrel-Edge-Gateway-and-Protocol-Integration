# Lab 13.3: SignalR Hub + Converged Web Dashboard

Adds real-time push to the browser on top of Lab 13.2.

```
ConvergedEdgeGateway/
├── Hubs.cs                          # GatewayHub.ToggleLed, ProtocolMetrics (thread-safe counters)
├── Services.cs                      # SerialBridgeWorker now pushes "ReceiveTelemetry" via IHubContext
├── Program.cs                       # + AddSignalR, static files, /api/metrics, REST counter, CSP headers
├── Security.cs                      # + UseSecurityHeaders (CSP, X-Frame-Options, nosniff)
└── wwwroot/
    ├── index.html                   # Glassmorphism dashboard (no inline script)
    ├── app.js                       # SignalR client logic
    └── lib/signalr/signalr.min.js   # @microsoft/signalr 10.0.11 served locally (works offline)
```

Firmware: same as [Lab13-2/esp32_serial_bridge](../Lab13-2/esp32_serial_bridge).

## Run

```powershell
cd Code\Lab13-3\ConvergedEdgeGateway
dotnet user-secrets set "Mqtt:Username" "student"
dotnet user-secrets set "Mqtt:Password" "<at-least-8-characters>"
dotnet user-secrets set "Gateway:ApiKey" ([guid]::NewGuid().ToString("N"))
dotnet run -- --Serial:PortName=COM3        # or: dotnet run -- --Serial:Mode=Simulated
```

Open **http://localhost:5000**.

| Check | Expected |
| :--- | :--- |
| Header | `● SignalR Online` (green) |
| Turn the potentiometer | voltage, bar and raw ADC update every 500 ms |
| Click the LED button | the board LED toggles and every open browser tab updates |
| Board unplugged and button clicked | "ส่งคำสั่งไม่สำเร็จ" for 2 s, then the button returns to its real state |
| Metrics card | Serial / MQTT / REST / SignalR counters increase (`curl.exe http://localhost:5000/api/metrics`) |
| Browser DevTools console | no CSP errors |

Verified on this machine in Simulated mode with the built-in browser: live telemetry, LED toggle through SignalR, all 4 counters, and no console errors.
