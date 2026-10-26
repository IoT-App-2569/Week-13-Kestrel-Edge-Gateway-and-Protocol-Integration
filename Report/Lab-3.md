# รายงานผลการทดลองที่ 13.3
## Realtime SignalR Hub และ Converged Web Dashboard

## 1. สิ่งที่ต้องการและผลการทดลอง

### 1.1 สิ่งที่ต้องการ

- ติดตั้งและกำหนดค่า ASP.NET Core SignalR บน `.NET 10.0`
- ผลักข้อมูลจาก `SerialBridgeWorker` ไปยังหน้าเว็บด้วย `IHubContext`
- สร้าง Dashboard แบบ Dark Glassmorphism สำหรับแสดง Voltage และ Raw ADC
- ควบคุม LED ที่ GPIO2 จากหน้าเว็บผ่าน SignalR และ Serial
- นับสถิติ Serial, MQTT, REST และ SignalR แบบเรียลไทม์

### 1.2 การคอมไพล์และเริ่มต้นระบบ

คำสั่งที่ใช้:

```powershell
dotnet restore Lab\Lab-3\ConvergedEdgeGateway.csproj
dotnet build Lab\Lab-3\ConvergedEdgeGateway.csproj --no-restore
dotnet run --project Lab\Lab-3\ConvergedEdgeGateway.csproj
```

ผลการ Build:

```text
ConvergedEdgeGateway -> ...\Lab\Lab-3\bin\Debug\net10.0\ConvergedEdgeGateway.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Log การเริ่มต้นระบบ:

```text
เชื่อมต่อฮาร์ดแวร์ Serial Port COM3 สำเร็จ!
Now listening on: http://[::]:5000
Now listening on: http://[::]:1883
Application started. Press Ctrl+C to shut down.
```

สรุป: Kestrel เปิด HTTP/SignalR ที่พอร์ต `5000` และ MQTT ที่พอร์ต `1883`
สำเร็จ พร้อมเชื่อมต่อ ESP32 ผ่าน Serial `115200 bps`

### 1.3 การผลักดันข้อมูลเซนเซอร์แบบเรียลไทม์

ข้อมูลที่ ESP32 ส่งทาง Serial:

```json
{"voltage":1.65,"raw":2048}
```

ลำดับการทำงานที่ตรวจสอบได้:

```text
Serial JSON รับเข้า SerialBridgeWorker
Serial Packets = 1
Publish MQTT: iot/esp32/telemetry
MQTT Messages = 1
SignalR ReceiveTelemetry ส่งไปยัง Dashboard
SignalR Pushes = 1
```

ตัวอย่างข้อมูลที่หน้าเว็บได้รับ:

```json
{
  "voltage": 1.65,
  "raw": 2048,
  "timestamp": 1730000000,
  "metrics": {
    "serialPackets": 1,
    "mqttMessages": 1,
    "restRequests": 0,
    "signalRPushes": 1
  }
}
```

Dashboard แสดง Voltage, Raw ADC และตัวนับทราฟฟิกที่เพิ่มขึ้นทุกครั้งที่
ได้รับข้อมูลจาก ESP32

### 1.4 การทดสอบควบคุม Actuator ผ่าน Web Dashboard

เมื่อกดปุ่ม LED บนหน้าเว็บ จะเกิดลำดับการทำงาน:

```text
Web Dashboard -> SignalR ToggleLed(true)
สั่งคำสั่ง LED:ON ผ่าน Serial
ESP32 รับคำสั่งและทำให้ GPIO2 ติดสว่าง
Gateway Broadcast LedStateUpdated(true)
```

Log ที่ตรวจสอบได้:

```text
สั่งการจาก Web Dashboard ผ่าน SignalR: LED:ON; ส่ง Serial สำเร็จ: True
ส่งคำสั่ง LED:ON ไปยัง Serial Port COM3
```

เมื่อกดปุ่มอีกครั้ง:

```text
Web Dashboard -> SignalR ToggleLed(false)
สั่งคำสั่ง LED:OFF ผ่าน Serial
ESP32 รับคำสั่งและทำให้ GPIO2 ดับลง
Gateway Broadcast LedStateUpdated(false)
```

Log:

```text
สั่งการจาก Web Dashboard ผ่าน SignalR: LED:OFF; ส่ง Serial สำเร็จ: True
ส่งคำสั่ง LED:OFF ไปยัง Serial Port COM3
```

ผลการทดสอบ: คำสั่งควบคุมจาก Dashboard ถึง ESP32 และ LED ตอบสนองถูกต้อง

### 1.5 สถิติทราฟฟิก Protocol Convergence Metrics

ตัวอย่างค่าที่อ่านได้จาก Dashboard หลังรับข้อมูลต่อเนื่อง:

| โปรโตคอล | จำนวนแพ็กเก็ต/ข้อความ | ผล |
|---|---:|---|
| Serial Packets | 120 | รับจาก ESP32 สำเร็จ |
| MQTT Messages | 120 | Publish เข้า `iot/esp32/telemetry` สำเร็จ |
| REST Requests | 2 | คำขออ่านสถานะ/ควบคุมทำงาน |
| SignalR Pushes | 120 | ผลักข้อมูลไป Dashboard สำเร็จ |

ค่า Serial, MQTT และ SignalR เพิ่มขึ้นสอดคล้องกัน แสดงว่า Data Bridge
ทำงานครบตั้งแต่ UART ผ่าน MQTT จนถึง WebSocket Dashboard

### 1.6 ตารางสรุปผลการตรวจสอบระบบ

| รายการ | ผล |
|---|---|
| Target Framework | `.NET 10.0` |
| Build | สำเร็จ, 0 Warning, 0 Error |
| Kestrel HTTP/SignalR | พอร์ต `5000` เปิดใช้งานสำเร็จ |
| Embedded MQTT Broker | พอร์ต `1883` เปิดใช้งานสำเร็จ |
| SignalR Hub | `/hubs/gateway` เชื่อมต่อสำเร็จ |
| SerialBridgeWorker | เชื่อมต่อ COM3 และอ่านข้อมูลแบบ Background Service |
| Telemetry Push | Serial -> MQTT -> SignalR สำเร็จ |
| LED Control | Dashboard -> SignalR -> Serial -> ESP32 สำเร็จ |
| Glassmorphism Dashboard | โหลดและแสดงผลเรียลไทม์สำเร็จ |
| Protocol Metrics | นับ Serial/MQTT/REST/SignalR สำเร็จ |

![alt text](/Report/img/image.png)

## 2. คำถามและคำตอบท้ายการทดลอง

### คำถามที่ 1

เปรียบเทียบ SignalR ซึ่งใช้ Persistent Full-Duplex WebSocket กับ HTTP GET
Polling แบบ `setInterval` ในด้าน Network Overhead, Latency, Server Load
และ User Experience

### คำตอบ

SignalR เปิดการเชื่อมต่อแบบ Persistent Full-Duplex ไว้ระหว่าง Browser กับ
Server เมื่อมี Telemetry ใหม่ Server สามารถ Push ได้ทันทีโดยไม่ต้องรอ Request
ใหม่ จึงมี Latency ต่ำและใช้ Header/Handshake ซ้ำน้อยกว่า Polling

HTTP GET Polling ต้องส่ง Request ตามช่วงเวลา แม้ไม่มีข้อมูลใหม่ ทำให้เกิด
Network Overhead จาก Header และการเปิด Request ซ้ำ รวมถึงอาจพลาดข้อมูลระหว่าง
รอบ Polling หากตั้งช่วงเวลานาน หากตั้งช่วงเวลาสั้นจะเพิ่มภาระ Server และ
Network มากขึ้น

SignalR ใช้ Connection และทรัพยากรค้างต่อ Client แต่ในงาน Realtime จำนวนมาก
เหมาะสมกว่าเพราะ Server ส่งเฉพาะเมื่อมี Event ทำให้การตอบสนองต่อผู้ใช้ดีกว่า
และ Dashboard แสดงข้อมูลสดได้ต่อเนื่อง ส่วน Polling เหมาะกับข้อมูลที่ไม่ถี่
หรือระบบที่ไม่สามารถเปิด Persistent Connection ได้

### คำถามที่ 2

Protocol Convergence Metrics มีประโยชน์อย่างไรในการตรวจจับ Anomaly,
Bottleneck และเฝ้าระวังสุขภาพของระบบ IoT ระดับอุตสาหกรรม?

### คำตอบ

Metrics ทำให้เห็นจำนวนข้อมูลในแต่ละช่วงของ Pipeline ได้แก่ Serial, MQTT,
REST และ SignalR หาก `SerialPackets` เพิ่มขึ้นแต่ `MqttMessages` ไม่เพิ่ม
อาจแสดงว่า MQTT Broker หรือการ Publish มีปัญหา หาก MQTT เพิ่มแต่
`SignalRPushes` หยุดนิ่ง อาจเกิดคอขวดที่ Hub, Network หรือ Browser

การเปรียบเทียบอัตราและสัดส่วนของ Counter ช่วยตรวจจับ Anomaly เช่น ข้อมูล
Serial หยุดกะทันหัน, Message หายระหว่าง Protocol, REST Request พุ่งสูงผิดปกติ
หรือ SignalR Push ต่ำกว่าจำนวนข้อมูลต้นทาง นอกจากนี้ยังใช้กำหนด Alert,
วิเคราะห์ Throughput, วางแผน Capacity และตรวจสอบสุขภาพของระบบแบบต่อเนื่อง

ในระบบอุตสาหกรรมควรเก็บ Metrics เหล่านี้ร่วมกับ Timestamp, Error Count,
Latency, Queue Depth และสถานะการเชื่อมต่อ เพื่อทำ Dashboard Monitoring,
ระบบแจ้งเตือน และประวัติสำหรับวิเคราะห์เหตุขัดข้องได้อย่างมีหลักฐาน
