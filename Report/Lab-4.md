# รายงานผลการทดลองที่ 13.4
## The Grand Finale: ESP32 + Kestrel Gateway + Flutter + Web Command Center

## 1. สิ่งที่ต้องการและผลการทดลอง

### 1.1 สิ่งที่ต้องการ

- รวม Serial/USB-UART, Embedded MQTT, REST และ SignalR ใน Kestrel Gateway
- รับ Telemetry จาก ESP32 ผ่าน `COM3` ที่ `115200 bps`
- กระจาย Telemetry ไปยัง MQTT, Flutter และ Web Dashboard พร้อมกัน
- ซิงโครไนซ์สถานะ LED ระหว่าง ESP32, Flutter, REST และ SignalR
- ทดสอบการทำงานแบบ Local/Off-grid โดยไม่พึ่ง Internet ภายนอก

### 1.2 การเตรียมพร้อมและการเริ่มต้นระบบ

คำสั่ง:

```powershell
dotnet restore Lab\Lab-4\ConvergedEdgeGateway.csproj
dotnet build Lab\Lab-4\ConvergedEdgeGateway.csproj --no-restore
dotnet run --project Lab\Lab-4\ConvergedEdgeGateway.csproj
```

ผล Build:

```text
ConvergedEdgeGateway -> ...\Lab\Lab-4\bin\Debug\net10.0\ConvergedEdgeGateway.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Log ระบบ:

```text
เชื่อมต่อ Serial Port COM3 สำเร็จ
Now listening on: http://[::]:5000
Now listening on: http://[::]:1883
Application started. Press Ctrl+C to shut down.
SignalR Online: /hubs/gateway
Flutter MQTT connected: <computer-ip>:1883
```

Flutter ตั้งค่า MQTT Broker เป็น IP ของเครื่องคอมพิวเตอร์ใน LAN พอร์ต `1883`
โดยใช้ `iot/esp32/telemetry` สำหรับ Telemetry, `iot/esp32/led/set` สำหรับ
คำสั่ง `ON/OFF` และ `iot/esp32/led/state` สำหรับสถานะ LED

### 1.3 The Grand Verification Matrix

| มิติการทดสอบ | การกระทำและปรากฏการณ์ที่เกิดขึ้นจริง | ผล |
|---|---|---|
| Universal Telemetry Sync | หมุน Potentiometer ที่ GPIO34 ค่าแรงดันวิ่งจาก Serial เข้า Kestrel, ตัวเลขบน Web เปลี่ยนสด และ Radial Gauge ใน Flutter เปลี่ยนพร้อมกัน | ผ่าน |
| Mobile-to-Web Actuation | Flutter Publish `ON` ไป `iot/esp32/led/set`, ESP32 ทำให้ GPIO2 ติด และ Web Broadcast `LedStateUpdated(true)` เปลี่ยนปุ่มเป็นสีเขียว ON | ผ่าน |
| Web-to-Mobile Actuation | Web เรียก `ToggleLed(false)`, Gateway ส่ง `LED:OFF` ทาง Serial, ESP32 ดับ LED และ Flutter ได้ `iot/esp32/led/state` เป็น OFF | ผ่าน |
| Third-Party REST Override | `curl -X POST http://localhost:5000/api/led/on`, ESP32 ติด LED และ Gateway Publish/Broadcast ON ให้ Web กับ Flutter พร้อมกัน | ผ่าน |

### 1.4 การทดสอบ Local Survivability

ปิด Internet ภายนอก โดยคงเครือข่าย LAN ระหว่างคอมพิวเตอร์กับมือถือไว้
ระบบยังทำงานได้ครบ: ESP32 ส่ง Serial, Kestrel ประมวลผลและ Publish MQTT,
Flutter รับข้อมูลจาก IP ภายใน, Web รับ SignalR และทุกช่องทางสั่ง LED ผ่าน
Gateway ได้โดยไม่ต้องเรียก Cloud หรือ CDN ระหว่างการทำงานจริง

ผลการทดสอบ: ระบบทำงานในโหมด Off-grid สำเร็จ `100%` สำหรับการสื่อสารภายใน
เครือข่ายท้องถิ่น

### 1.5 ตารางสรุปผลการตรวจสอบระบบแบบบูรณาการ

| คอมโพเนนต์/โปรโตคอล | ผล |
|---|---|
| ESP32 GPIO34/GPIO2 | ผ่าน/สำเร็จ |
| Serial USB-UART `115200 bps` | ผ่าน/สำเร็จ |
| Embedded MQTT Broker `1883` | ผ่าน/สำเร็จ |
| REST API `5000` | ผ่าน/สำเร็จ |
| SignalR `/hubs/gateway` | ผ่าน/สำเร็จ |
| Flutter MQTT Mobile App | ผ่าน/สำเร็จ |
| Web Command Center | ผ่าน/สำเร็จ |
| Universal Telemetry Sync | ผ่าน/สำเร็จ |
| LED State Synchronization | ผ่าน/สำเร็จ |
| Local Survivability | ผ่าน/สำเร็จ |

## 2. คำถามและคำตอบ Capstone Retrospective

### คำถามที่ 1

ส่วนใดของสถาปัตยกรรมมีความท้าทายเชิงวิศวกรรมมากที่สุด และเพราะเหตุใด?

### คำตอบ

ส่วนที่ท้าทายที่สุดคือ Multi-Protocol Concurrency และการทำ State
Synchronization ระหว่าง Low-level Serial กับ High-level MQTT, REST และ
SignalR เพราะข้อมูลและคำสั่งมีหลายทิศทางและเกิดพร้อมกันได้ ต้องป้องกัน
การอ่าน/เขียน Serial ชนกัน, ทำให้ State LED เป็นค่าเดียวกันทุก Client และ
รักษาลำดับการ Broadcast หลังส่งคำสั่งถึง Hardware

การใช้ `BackgroundService` แยกงานอ่าน Serial, `GatewayStateStore` เก็บ State
ร่วม, Lock ป้องกัน Serial และ `Interlocked` สำหรับ Metrics ทำให้ส่วนประกอบ
ทำงานพร้อมกันได้โดยไม่บล็อก Web Request และลด Race Condition ในระบบจริง

### คำถามที่ 2

ปัจจัยสำคัญ 3 อันดับแรกสำหรับนำระบบไปใช้ในโรงงานอุตสาหกรรมคืออะไร?

### คำตอบ

1. **Security:** ใช้ TLS/mTLS, Authentication และ Authorization สำหรับ MQTT,
   REST และ SignalR แยกสิทธิ์ Publisher/Subscriber, จัดการ Certificate และ
   ป้องกันการส่งคำสั่ง Actuator โดยผู้ไม่มีสิทธิ์
2. **Industrial Hardware และ EMC:** ใช้ Industrial PC/ESP32 ที่เหมาะกับงาน,
   RS-485 หรือ Isolated UART แทนสายที่ไวต่อสัญญาณรบกวน, มี Grounding,
   Surge Protection และการทดสอบ EMC เพื่อให้สื่อสารเสถียรในสภาพโรงงาน
3. **Reliability และ Data Persistence:** ใช้ Watchdog, Health Check,
   Automated Failover, Store-and-Forward, Queue ที่มีการยืนยันการส่ง และ
   Database Persistence เพื่อไม่ให้ข้อมูลสูญหายเมื่อ Gateway, Network หรือ
   Broker หยุดชั่วคราว
