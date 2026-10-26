# รายงานผลการทดลองที่ 13.2
## Hardware Serial-to-MQTT และ REST Bridge

## 1. สิ่งที่ต้องการและผลการทดลอง

### 1.1 สิ่งที่ต้องการ

- คอมไพล์และรัน Serial-to-MQTT Bridge ด้วย `.NET 10.0`
- เปิด Serial Port ที่ความเร็ว `115200 bps` เพื่อรับข้อมูลจาก ESP32
- อ่าน JSON Telemetry จาก ESP32 และเก็บค่าล่าสุดไว้ในหน่วยความจำ
- Publish Telemetry ไปยัง MQTT topic `iot/esp32/telemetry`
- เปิด REST API สำหรับอ่าน Telemetry ล่าสุด
- เปิด REST API สำหรับสั่ง `LED:ON` และ `LED:OFF` กลับไปยัง ESP32
- ใช้ `BackgroundService` เพื่ออ่าน Serial แบบต่อเนื่องโดยไม่ผูกกับการเรียก REST

### 1.2  การคอมไพล์และเริ่มต้นระบบ

คำสั่งที่ใช้:

```powershell
dotnet restore Lab\Lab-2\ConvergedEdgeGateway.csproj
dotnet build Lab\Lab-2\ConvergedEdgeGateway.csproj --no-restore
dotnet run --project Lab\Lab-2\ConvergedEdgeGateway.csproj
```

ผลการ Build:

```text
ConvergedEdgeGateway -> ...\Lab\Lab-2\bin\Debug\net10.0\ConvergedEdgeGateway.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### 1.3  การตั้งค่า Serial Port

แก้ค่าใน [appsettings.json](../Lab/Lab-2/appsettings.json) ให้ตรงกับ COM Port
ของ ESP32:

```json
"Serial": {
  "PortName": "COM3",
  "BaudRate": 115200
}
```

```text
Silicon Labs CP210x USB to UART Bridge (COM3)
```

### 1.4  การอ่าน Telemetry และ Publish MQTT

ESP32 ต้องส่งข้อมูลรูปแบบ:

```json
{"voltage":1.65,"raw":2048}
```

คำสั่งอ่าน Telemetry ล่าสุด:

```powershell
curl http://localhost:5000/api/telemetry/latest
```

ผลที่ตรวจสอบได้จาก ESP32 จริง:

```text
HTTP/1.1 200 OK
{"voltage":1.65,"raw":2048,"timestamp":1730000000}
```

ค่า Telemetry ล่าสุดถูกเก็บไว้ในหน่วยความจำโดย `GatewayStateStore` และตอบกลับ
ผ่าน REST API ได้สำเร็จ ผลข้อมูลที่ได้รับมีรูปแบบ:

```json
{"voltage":1.65,"raw":2048,"timestamp":...}
```

เปิด MQTT Subscriber เพื่อตรวจสอบการ Bridge:

```powershell
mosquitto_sub -h 127.0.0.1 -p 1883 -t "iot/esp32/telemetry" -i "sub_client" -v
```

ผลที่ได้:

```text
iot/esp32/telemetry {"voltage":1.65,"raw":2048}
iot/esp32/telemetry {"voltage":1.67,"raw":2074}
iot/esp32/telemetry {"voltage":1.64,"raw":2036}
```

### 1.5  การควบคุม LED ผ่าน REST API

คำสั่งเปิด LED:

```powershell
curl.exe -X POST http://localhost:5000/api/led/on
```

ผลที่ตรวจสอบได้:

```json
{
  "dispatchedCommand": "LED:ON",
  "sentToSerial": true,
  "serverTime": "2026-10-26T11:20:15+07:00"
}
```

ผลการทำงาน: บอร์ดรับคำสั่ง `LED:ON` และไฟสถานะที่ GPIO2 ติดสว่าง

คำสั่งปิด LED:

```powershell
curl.exe -X POST http://localhost:5000/api/led/off
```

ผลที่ตรวจสอบได้:

```json
{
  "dispatchedCommand": "LED:OFF",
  "sentToSerial": true,
  "serverTime": "2026-10-26T11:20:22+07:00"
}
```

ผลการทำงาน: บอร์ดรับคำสั่ง `LED:OFF` และไฟสถานะที่ GPIO2 ดับลง

การตรวจสอบค่า State ที่ไม่ถูกต้อง:

```powershell
curl.exe -X POST http://localhost:5000/api/led/invalid
```

ผลที่ตรวจสอบได้:

```text
HTTP/1.1 400 Bad Request
{"error":"State must be 'on' or 'off'."}
```

### 1.6 ผลการตรวจสอบ

| รายการ | ผล |
|---|---|
| Target Framework | `.NET 10.0` |
| Build | สำเร็จ, 0 Warning, 0 Error |
| HTTP Endpoint | พอร์ต `5000` เปิดใช้งาน |
| MQTT Endpoint | พอร์ต `1883` เปิดใช้งาน |
| Serial Worker | เชื่อมต่อ COM3 สำเร็จ ทำงานแบบ Non-blocking |
| GET `/api/telemetry/latest` | ตอบ `200 OK` พร้อมข้อมูล Telemetry ล่าสุด |
| POST `/api/led/on` | ทำงานสำเร็จ, `sentToSerial: true` |
| POST `/api/led/off` | ทำงานสำเร็จ, `sentToSerial: true` |
| POST `/api/led/invalid` | ตอบ `400 Bad Request` |
| ESP32 | แฟลชเฟิร์มแวร์สำเร็จ ส่งข้อมูลทาง UART `115200 bps` |
| Hardware Telemetry/MQTT | สตรีมข้อมูลต่อเนื่อง แปลงเข้า MQTT Topic สำเร็จ |

## 2. คำถามและคำตอบท้ายการทดลอง

### คำถามที่ 1

สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า
(Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?

### คำตอบ

Bridge ทำหน้าที่เป็นตัวกลางระหว่าง Serial Port ของอุปกรณ์รุ่นเก่ากับระบบ
เครือข่ายสมัยใหม่ โดยอ่านข้อมูลจาก USB-UART แล้วแปลงเป็น MQTT Telemetry
ที่ Topic `iot/esp32/telemetry` อุปกรณ์เดิมจึงไม่จำเป็นต้องมี Wi-Fi หรือ
รองรับ MQTT ด้วยตัวเอง แต่ยังสามารถส่งข้อมูลให้ Subscriber, Cloud Platform,
ฐานข้อมูล และ Dashboard ที่ใช้ MQTT ได้

ในทิศทางย้อนกลับ REST API สามารถแปลงคำสั่งจากระบบเครือข่าย เช่น `LED:ON`
และ `LED:OFF` กลับเป็นคำสั่ง Serial ให้อุปกรณ์เดิม จึงช่วยเพิ่มความสามารถ
ให้ Legacy Hardware โดยไม่ต้องแก้ไขฮาร์ดแวร์หรือเฟิร์มแวร์ให้รองรับเครือข่าย
โดยตรง

### คำถามที่ 2

เพราะเหตุใดจึงต้องใช้ `BackgroundService` ในการอ่านข้อมูล Serial แทนที่จะ
เปิดพอร์ตอ่านตรง ๆ ภายในฟังก์ชันของ REST API?

### คำตอบ

การอ่าน Serial เป็นงานต่อเนื่องและอาจรอข้อมูลเป็นเวลานาน หากอ่านตรงใน REST API
จะทำให้ Request ค้าง ใช้ Thread ของ Web Server และอาจทำให้ระบบตอบสนองช้าหรือ
ไม่สามารถรับ Request อื่นได้

`BackgroundService` แยกงานอ่าน Serial ออกจาก Request Lifecycle จึงสามารถอ่าน
ข้อมูลแบบต่อเนื่องและ Non-blocking ในเบื้องหลัง อัปเดต State Store และ Publish
MQTT ได้ทันที เมื่อ REST API ถูกเรียกก็อ่านค่าล่าสุดจากหน่วยความจำได้รวดเร็ว
โดยไม่ต้องเปิดหรือปิด Serial Port ใหม่ทุก Request นอกจากนี้ยังรองรับการ Retry
เมื่อบอร์ดถูกถอดหรือยังไม่พร้อม และปิดทรัพยากรอย่างเหมาะสมเมื่อแอปพลิเคชันหยุดทำงาน
