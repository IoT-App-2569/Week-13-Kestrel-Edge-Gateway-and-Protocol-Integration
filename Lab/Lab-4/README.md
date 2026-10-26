# Lab 4: The Grand Finale

รัน Gateway:

```powershell
dotnet run --project Lab\Lab-4\ConvergedEdgeGateway.csproj
```

เปิด Web Command Center ที่ `http://localhost:5000/`

Flutter ต้องชี้ MQTT Broker ไปที่ IP ของเครื่องคอมพิวเตอร์ในเครือข่าย LAN
พอร์ต `1883` โดยใช้ Topic ควบคุม `iot/esp32/led/set` และส่ง Payload `ON`
หรือ `OFF` ส่วน Telemetry อยู่ที่ `iot/esp32/telemetry` และสถานะ LED อยู่ที่
`iot/esp32/led/state`
