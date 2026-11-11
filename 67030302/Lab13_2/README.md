# ใบงานการทดลองที่ 13.2
## การพัฒนา Hardware Serial Bridge ดักฟังข้อมูล USB-UART จาก ESP32 แปลงเข้าสู่ MQTT Topics และ REST API

**รหัสนักศึกษา:** 67030302  

---

## 1. วัตถุประสงค์การทดลอง
1. สามารถติดตั้งและใช้งานไลบรารี `System.IO.Ports` ในโปรเจกต์ ASP.NET Core ได้
2. สามารถพัฒนา `BackgroundService` เพื่อเปิดพอร์ต Serial อ่านข้อมูลแบบ Non-blocking ได้
3. สามารถแปลงข้อมูลที่อ่านได้จากสาย Serial ไป Publish ต่อใน Embedded MQTT Broker ได้
4. สามารถพัฒนา REST API เพื่ออ่านค่าเซนเซอร์ล่าสุด และส่งคำสั่งควบคุมหลอดไฟผ่านสาย Serial ได้

---

## 2. การเตรียมฮาร์ดแวร์และการเชื่อมต่อ

| อุปกรณ์ | ขาบน ESP32 | หน้าที่การทำงาน |
| :--- | :--- | :--- |
| **Potentiometer (10k)** | **GPIO34** (ADC1_CH6) | อ่านค่าแอนะล็อกของแรงดันไฟฟ้า (0 - 3.3V) |
| **LED Status** | **GPIO2** | หลอดไฟแสดงสถานะรับคำสั่งเปิด-ปิดทาง Serial |
| **สาย USB Data** | พอร์ต Type-C / Micro | สื่อสาร USB-UART ความเร็ว 115200 bps |

### แฟลชโค้ดเข้า ESP32
```bash
cd ESP32_ADC_Stream
idf.py build flash
```
*(หมายเหตุ: หากต้องการดู log ให้ทดสอบด้วย `idf.py monitor` แต่ **ต้องกด Ctrl+] เพื่อปิด monitor ก่อนรัน Kestrel เสมอ** เพื่อไม่ให้พอร์ตถูกล็อก)*

---

## 3. ขั้นตอนการรันและการทดสอบ

### 1) ตรวจสอบพอร์ต Serial ใน `Services.cs`
แก้ไขตัวแปร `_portName` ให้ตรงกับพอร์ตของ ESP32 บนเครื่องของคุณ เช่น:
- Windows: `"COM3"` หรือ `"COM24"`
- macOS: `"/dev/cu.usbserial-0001"` หรือ `"/dev/cu.wchusbserial..."`

### 2) สั่งรัน Kestrel Edge Gateway
```bash
cd ConvergedEdgeGateway
dotnet run
```

### 3) ทดสอบอ่านค่าเซนเซอร์ผ่าน REST API
```bash
curl http://localhost:5000/api/telemetry/latest
```
ตัวอย่างผลลัพธ์:
```json
{"voltage":1.65,"raw":2048,"timestamp":1731331200}
```

### 4) ทดสอบดักฟังค่าทาง MQTT Broker
```bash
mosquitto_sub -h 127.0.0.1 -p 1883 -t "iot/esp32/telemetry" -i "sub_client" -v
```

### 5) ทดสอบสั่งเปิด-ปิดไฟผ่าน REST API
- เปิดไฟ:
  ```bash
  curl -X POST http://localhost:5000/api/led/on
  ```
- ปิดไฟ:
  ```bash
  curl -X POST http://localhost:5000/api/led/off
  ```

---

## 4. บันทึกผลการทดลอง

*(ผลการรันจริงจากหน้าต่าง Terminal ร่วมกับบอร์ด ESP32 พอร์ต `/dev/cu.usbserial-0001`):*

```text
============================================================
 🚀 Lab 13.2: Hardware Serial to MQTT and REST Bridge
 รหัสนักศึกษา: 67030302
 เชื่อมต่อ ESP32 จริง: /dev/cu.usbserial-0001 (Baud 115200)
============================================================
info: SerialBridgeWorker[0]
      🔌 พยายามเปิด Serial Port /dev/cu.usbserial-0001...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:1883
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: SerialBridgeWorker[0]
      ✅ เชื่อมต่อฮาร์ดแวร์ Serial Port /dev/cu.usbserial-0001 สำเร็จ!

--- [1. ทดสอบอ่านค่า Telemetry จริงจากบอร์ด ESP32 ผ่าน REST API] ---
👉 curl http://localhost:5001/api/telemetry/latest
{"voltage":3.3,"raw":4095,"timestamp":1794408708}

--- [2. ทดสอบสั่งเปิดไฟ LED บนบอร์ดผ่าน REST API: POST /api/led/on] ---
👉 curl -X POST http://localhost:5001/api/led/on
{"dispatchedCommand":"LED:ON","sentToSerial":true,"serverTime":"21:50:26"}

--- [3. ทดสอบสั่งปิดไฟ LED บนบอร์ดผ่าน REST API: POST /api/led/off] ---
👉 curl -X POST http://localhost:5001/api/led/off
{"dispatchedCommand":"LED:OFF","sentToSerial":true,"serverTime":"21:50:29"}
```

---

## 5. คำถามท้ายการทดลอง (Review Questions)

### ข้อ 1: สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?

**คำตอบ:**  
สถาปัตยกรรม Serial-to-MQTT Bridge ทำหน้าที่เป็น **Universal Protocol Converter** ที่ระดับ Edge Gateway โดยอาศัยคอมพิวเตอร์เกตเวย์เชื่อมต่อทางกายภาพผ่านสาย Serial (เช่น RS-232, RS-485 หรือ USB-UART) เพื่อดักฟังไบต์สตรีมจากอุปกรณ์รุ่นเก่า แล้วนำมาแปลง (Parse) โครงสร้างข้อมูลให้อยู่ในรูปแบบสากล เช่น JSON จากนั้นจึงนำไป Publish เข้าสู่ระบบเครือข่ายไอโอทีผ่าน MQTT Broker และเปิดจุดเชื่อมต่อ HTTP RESTful API ส่งผลให้อุปกรณ์รุ่นเดิมสามารถบูรณาการเข้ากับระบบคลาวด์ แดชบอร์ด และแอปพลิเคชันมือถือสมัยใหม่ได้ทันที โดยไม่ต้องรื้อถอน ปรับเปลี่ยน หรือติดตั้งฮาร์ดแวร์ไมโครคอนโทรลเลอร์ใหม่

### ข้อ 2: เพราะเหตุใดเราจึงต้องใช้ `BackgroundService` ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?

**คำตอบ:**  
1. **การเข้าถึงพอร์ตแบบจำเพาะ (Exclusive Resource Lock):** พอร์ตสื่อสารอนุกรม (COM Port) ในระบบปฏิบัติการสามารถเปิดใช้งานได้เพียงโปรแกรมเดียวหรือเธรดเดียวในเวลาเดียวกัน หากเปิด-ปิดพอร์ตทุกครั้งที่มี HTTP Request เข้ามา จะทำให้เกิด Resource Contention และข้อผิดพลาดพอร์ตชนกัน
2. **การสตรีมข้อมูลต่อเนื่องและการลดความหน่วงเวลา (Latency & Non-blocking):** ข้อมูลจากเซนเซอร์ถูกสตรีมมาอย่างต่อเนื่อง การเปิดและปิดพอร์ตต้องใช้เวลาตั้งค่า Handshake หลายร้อยมิลลิวินาที ทำให้ข้อมูลที่ส่งมาระหว่างปิดพอร์ตสูญหาย (Data Loss) การใช้ `BackgroundService` ช่วยให้เกตเวย์เปิดพอร์ตค้างไว้ตลอดเวลาและดักอ่านข้อมูลแบบ Non-blocking แล้วนำค่าล่าสุดมาบันทึกพักไว้ในหน่วยความจำ (`GatewayStateStore`) เมื่อมี REST API ร้องขอเข้ามา ระบบจึงสามารถอ่านค่าจาก RAM และส่งคำตอบกลับได้ทันทีในระดับมิลลิวินาที

