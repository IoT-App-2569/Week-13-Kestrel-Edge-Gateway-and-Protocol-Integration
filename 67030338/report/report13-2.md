# ใบงานการทดลองที่ 13.2
### การพัฒนา Hardware Serial Bridge ดักฟังข้อมูล USB-UART จาก ESP32 แปลงเข้าสู่ MQTT Topics และ REST API

---

## ผลการทดลอง

### 1. การรัน Kestrel Edge Gateway เชื่อมต่อ Serial Bridge (`SerialBridgeWorker`)
<!-- แคปรูปหน้าต่าง Terminal ขณะรัน dotnet run แสดงการทำงานของ SerialBridgeWorker -->
![ผลการรัน SerialBridgeWorker บน Kestrel](image-13-2-1.png)

### 2. การทดสอบเรียกดูค่าเซนเซอร์ล่าสุดผ่าน REST API (`GET /api/telemetry/latest`)
<!-- แคปรูปผลลัพธ์จากการรัน curl http://localhost:5000/api/telemetry/latest หรือเปิดผ่านเบราว์เซอร์ -->
![ผลการเรียกดูข้อมูลเซนเซอร์ผ่าน REST API](image-13-2-2.png)

### 3. การตรวจสอบข้อมูลที่ถูกแปลงจาก Serial เข้าสู่หัวข้อ MQTT (`iot/esp32/telemetry`)
<!-- แคปรูปหน้าต่าง mosquitto_sub -h 127.0.0.1 -p 1883 -t "iot/esp32/telemetry" -i "sub_client" -v -->
![ผลการรับข้อมูล Telemetry ผ่าน MQTT Subscriber](image-13-2-3.png)

### 4. การทดสอบส่งคำสั่งเปิด-ปิดไฟ LED ผ่าน REST API (`POST /api/led/on` และ `POST /api/led/off`)
<!-- แคปรูปผลลัพธ์คำสั่ง curl -X POST http://localhost:5000/api/led/on และ off -->
![ผลการสั่งเปิด-ปิดไฟ LED ผ่าน REST API](image-13-2-4.png)

---

## 4. คำถามท้ายการทดลอง (Review Questions)

### ข้อ 1: สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?
* **คำตอบ:**
  1. **ทำหน้าที่เป็นตัวแปลงโปรโตคอลและตัวแทนสื่อสารผ่านเครือข่าย (Protocol Translation & Network Proxy):**
     * อุปกรณ์รุ่นเก่าในโรงงานอุตสาหกรรมหรือห้องปฏิบัติการ (เช่น บอร์ดไมโครคอนโทรลเลอร์ยุคเก่า, PLC, เครื่องชั่งดิจิทัล, หรือเซนเซอร์ที่สื่อสารผ่านพอร์ต RS-232 / RS-485 / UART) ไม่มีชิป Wi-Fi, ไม่มี TCP/IP Stack และไม่สามารถประมวลผลโปรโตคอลสมัยใหม่อย่าง MQTT หรือ HTTP REST ได้ด้วยตัวเอง
     * เมื่อนำอุปกรณ์เหล่านี้มาต่อเข้ากับ **Edge Gateway** ผ่านสายสื่อสารอนุกรม (Serial/USB) คอมโพเนนต์ `SerialBridgeWorker` บนเกตเวย์จะทำหน้าที่ดักฟังไบต์ข้อมูลดิบ (Raw Byte Stream) ที่ไหลมาจากสายสัญญาณ แล้วนำมาจัดโครงสร้าง (Parse & Enrich เช่น แนบ Timestamp หรือแปลงเป็น JSON) ก่อนฉีดเข้าสู่ **Embedded MQTT Broker** และแคชไว้ใน **RESTful API State Store** โดยอัตโนมัติ
  2. **รองรับการควบคุมแบบสองทิศทาง (Bi-directional Actuation & Modernization):**
     * นอกจากส่งข้อมูลเซนเซอร์ขาขึ้น (Uplink Telemetry) แล้ว ระบบคลาวด์ แอปมือถือ หรือระบบ IT ภายนอกยังสามารถส่งคำสั่งมาตรฐานสมัยใหม่ (เช่น `HTTP POST /api/led/on` หรือ Publish เข้า MQTT Topic) มาที่เกตเวย์ เพื่อให้เกตเวย์แปลงกลับเป็นคำสั่งสตริงระดับฮาร์ดแวร์ (เช่น `LED:ON\n`) ส่งลงสาย Serial ไปควบคุมอุปกรณ์รุ่นเก่าได้ทันที
  3. **ความคุ้มค่าทางวิศวกรรม (Cost-Effective Brownfield Integration):**
     * ช่วยให้องค์กรสามารถยกระดับเครื่องจักรและเซนเซอร์เดิมที่มีความเสถียรสูงให้กลายเป็นส่วนหนึ่งของระบบ Smart Factory / Modern IoT Ecosystem ได้ทันทีโดยไม่ต้องรื้อถอนหรือซื้อฮาร์ดแวร์ใหม่ทั้งหมด (Retrofit)

---

### ข้อ 2: เพราะเหตุใดเราจึงต้องใช้ `BackgroundService` ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?
* **คำตอบ:**
  1. **ป้องกันปัญหาการแย่งชิงพอร์ตและการล็อกพอร์ต (Serial Port Exclusive Access Conflict):**
     * ในระบบปฏิบัติการ (เช่น Windows หรือ Linux) พอร์ตสื่อสารอนุกรม (`COMx` / `/dev/ttyUSB0`) อนุญาตให้เปิดใช้งานได้เพียง **1 เธรด/การเชื่อมต่อในเวลาเดียวกัน (Exclusive Lock)** หากเขียนโค้ดให้เปิดพอร์ต `SerialPort.Open()` ภายในฟังก์ชันของ REST API เมื่อมีผู้ใช้งานหรือระบบภายนอกยิง HTTP Request เข้ามาพร้อมกันแม้เพียง 2 คำขอ คำขอที่สองจะเกิดข้อผิดพลาด `UnauthorizedAccessException (Access to the port is denied)` และทำให้ API ล่มทันที
  2. **ขจัดความหน่วงในการเปิดพอร์ตและรอรับข้อมูล (Eliminating Port Handshake & Blocking I/O Latency):**
     * การเปิดพอร์ต Serial และการเรียก `ReadLine()` เพื่อรอจังหวะที่บอร์ดไมโครคอนโทรลเลอร์ส่งข้อความจบบรรทัด (`\n`) ใช้เวลาหลายสิบถึงหลายร้อยมิลลิวินาที (เช่น รอรอบส่งทุก 500 ms) หากทำใน REST API เธรดของเว็บเซิร์ฟเวอร์จะถูกบล็อกให้รอฮาร์ดแวร์ทุกครั้งที่มีคนเรียก API
     * การแยกให้ `BackgroundService` (`SerialBridgeWorker`) เปิดพอร์ตค้างไว้เพียงจุดเดียวในเธรดเบื้องหลังเพื่ออ่านสตรีมข้อมูลอย่างต่อเนื่อง แล้วนำค่าล่าสุดไปเก็บในหน่วยความจำ `GatewayStateStore` (`ConcurrentDictionary`) จะช่วยให้ REST API (`/api/telemetry/latest`) สามารถดึงข้อมูลจาก RAM ตอบกลับผู้เรียกได้ทันทีภายในเวลาไม่ถึง **1 มิลลิวินาที (Sub-millisecond Response)** และรองรับผู้เรียกพร้อมกันได้นับพันรายโดยไม่กระทบต่อฮาร์ดแวร์
  3. **รองรับการสตรีมข้อมูลแบบ Event-Driven อย่างต่อเนื่อง:**
     * เนื่องจาก `BackgroundService` ทำงานตลอดอายุของเซิร์ฟเวอร์โดยไม่ต้องรอให้มีคนเรียก HTTP API จึงสามารถนำข้อมูลทุกแพ็กเก็ตที่บอร์ดส่งมาไป Publish เข้าสู่ MQTT Broker และผลักขึ้นหน้าเว็บผ่าน SignalR ได้แบบเรียลไทม์อย่างสมบูรณ์
