# Lab 2: Hardware Serial-to-MQTT and REST Bridge

## ตั้งค่าพอร์ต Serial

แก้ค่า `Serial:PortName` ใน `appsettings.json` ให้ตรงกับ COM Port ของ ESP32
และปิด `idf monitor`, PuTTY หรือ Serial Monitor อื่นก่อนรัน Kestrel เพราะ
Windows อนุญาตให้เปิด Serial Port ได้ครั้งละหนึ่งโปรแกรม

## รันและทดสอบ

```powershell
dotnet run
curl http://localhost:5000/api/telemetry/latest
mosquitto_sub -h 127.0.0.1 -p 1883 -t "iot/esp32/telemetry" -i "sub_client" -v
curl.exe -X POST http://localhost:5000/api/led/on
curl.exe -X POST http://localhost:5000/api/led/off
```

Worker จะอ่าน JSON รูปแบบ `{"voltage":1.65,"raw":2048}` จาก ESP32 แล้วเก็บค่า
ล่าสุดไว้ใน REST API และ Publish ต่อไปยัง MQTT topic `iot/esp32/telemetry`
