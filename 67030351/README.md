# Week 13: Kestrel Edge Gateway and Protocol Integration
### ดัชนีสารบัญผลงานและรายงานผลการทดลอง (Student Portfolio Index)
**รหัสนักศึกษา:** `67030351`  
**สาขาวิชา:** วิศวกรรมคอมพิวเตอร์ / เทคโนโลยีสารสนเทศ  
**รายวิชา:** การพัฒนาแอปพลิเคชันไอโอที (IoT Application Development)

---

## 📌 สารบัญดัชนีนำทาง (Index to Lab Reports)

เอกสารรายงานผลการทดลอง บันทึกผล และตอบคำถามท้ายการทดลองทั้งหมดถูกรวบรวมไว้ที่:
👉 **[📄 เปิดอ่านรายงานฉบับสมบูรณ์ (report.md)](report.md)**

| ลำดับใบงาน | หัวข้อการทดลอง | ลิงก์ตรงสู่รายงาน | สถานะ |
| :---: | :--- | :--- | :---: |
| **13.1** | **การสร้าง Embedded MQTT Broker และ Client ภายใน Kestrel .NET 8 ด้วยไลบรารี MQTTnet** | [อ่านผลการทดลอง 13.1](report.md#131-การสร้าง-embedded-mqtt-broker-และ-client-ภายใน-kestrel-net-8-ด้วยไลบรารี-mqttnet) | ✅ ผ่าน |
| **13.2** | **การพัฒนา Hardware Serial Bridge ดักฟังข้อมูล USB-UART จาก ESP32 แปลงเข้าสู่ MQTT Topics และ REST API** | [อ่านผลการทดลอง 13.2](report.md#132-การพัฒนา-hardware-serial-bridge-ดักฟังข้อมูล-usb-uart-จาก-esp32-แปลงเข้าสู่-mqtt-topics-และ-rest-api) | ✅ ผ่าน |
| **13.3** | **การสร้าง SignalR Realtime Hub และเว็บแดชบอร์ดศูนย์บัญชาการ สไตล์ Glassmorphism** | [อ่านผลการทดลอง 13.3](report.md#133-การสร้าง-signalr-realtime-hub-และเว็บแดชบอร์ดศูนย์บัญชาการ-converged-web-dashboard-สไตล์-glassmorphism) | ✅ ผ่าน |
| **13.4** | **(The Grand Finale) การทดสอบผสานรวมระบบไอโอทีแบบครบวงจร (ESP32 + Gateway + Flutter + Web)** | [อ่านผลการทดลอง 13.4](report.md#134-the-grand-finale-การทดสอบผสานรวมระบบไอโอทีแบบครบวงจร-esp32--kestrel-gateway--flutter-app--web-command-center) | ✅ ผ่าน |

---

## 📹 วิดีโอบันทึกผลการทดลอง (Video Demonstrations)

📁 **ลิงก์ Google Drive รวมคลิปวิดีโอทั้งหมด:**  
👉 **[Google Drive Folder: คลิปวิดีโอประกอบการทดลอง Week 13](https://drive.google.com/drive/folders/19cNRJgQ5SsHgu9nlOEMk7COyEWwcyatz?usp=drive_link)**

| ชื่อไฟล์คลิป | ใบงานที่เกี่ยวข้อง | กิจกรรมที่ทดสอบ | คำอธิบายรายละเอียดสิ่งที่เกิดขึ้นในคลิป |
| :---: | :---: | :--- | :--- |
| **`IMG_4534`** | **ใบงานที่ 13.2** | กิจกรรมที่ 13-2.5: การทดสอบสะพานเชื่อมต่อ Hardware Serial Bridge | **การทดสอบสั่งเปิด-ปิดไฟ LED ผ่าน REST API:**<br/>แสดงการส่งคำสั่ง HTTP POST (`/api/led/on`) จาก Kestrel Gateway ผ่านสาย USB-UART สั่งงานให้หลอดไฟ LED บนบอร์ด ESP32 จริงติดสว่างขึ้นทันที |
| **`IMG_4541`** | **ใบงานที่ 13.3** | กิจกรรมที่ 13-3.4: การทดสอบเว็บแดชบอร์ดและการสื่อสารสองทางผ่าน SignalR | **การทดสอบหมุน Potentiometer และควบคุม LED ผ่านหน้าเว็บ Real-time:**<br/>แสดงหน้าเว็บ Glassmorphism Command Center รับข้อมูลแรงดันสดผ่าน WebSocket (SignalR) พร้อมคลิกสลับปุ่มสวิตช์ไฟบนหน้าเว็บเพื่อสั่งเปิด-ปิดหลอดไฟ LED จริงบนบอร์ด ESP32 แบบสองทาง (Bidirectional Control) |
| **`IMG_4544`** | **ใบงานที่ 13.4** | กิจกรรมที่ 13-4.2: การทดสอบ Mobile & Web Actuator Synchronization | **การทดสอบสั่งงานสวิตช์ไฟ LED สอดประสาน (Flutter + Web + ESP32):**<br/>แสดงการแตะสวิตช์ไฟบนแอปพลิเคชันมือถือ Flutter (ส่งคำสั่งผ่าน MQTT Broker 1883) และหน้าเว็บแดชบอร์ด (ผ่าน SignalR) สอดประสานร่วมกับหลอดไฟ LED จริงบนบอร์ดฮาร์ดแวร์อย่างไร้รอยต่อ |
| **`IMG_4545`** | **ใบงานที่ 13.4** | กิจกรรมที่ 13-4.2: การทดสอบ Universal Telemetry Sync และหมุน Potentiometer สด | **การทดสอบหมุน Potentiometer แบบเรียลไทม์ครบวงจร (The Grand Finale):**<br/>แสดงการใช้มือหมุนตัวต้านทานปรับค่าได้ (Potentiometer) บนบอร์ดจริง แล้วเข็มหน้าปัด Radial Gauge บนแอปมือถือ Flutter และตัวเลขบน Web Command Center วิ่งตามการหมุนสดๆ พร้อมสาธิตการสั่งงานสวิตช์ไฟ LED ควบคู่กัน |

---

## 📂 โครงสร้างไดเรกทอรีและซอร์สโค้ด (Project Directory Index)

```text
67030351/
├── README.md                     # ดัชนีนำทางสรุปผลงานภาพรวม (หน้านี้)
├── report.md                     # รายงานผลการทดลองและการตอบคำถามฉบับสมบูรณ์
├── ConvergedEdgeGateway/         # โปรเจกต์ C# .NET 8 Kestrel Edge Gateway
│   ├── Program.cs                # จุดตั้งค่า Kestrel, Embedded MQTTnet Broker, Minimal APIs และ SignalR
│   ├── Services.cs               # BackgroundService อ่าน Serial จาก ESP32 และ In-Memory State Store
│   ├── Hubs.cs                   # SignalR Realtime Hub สำหรับสื่อสารกับ Web Command Center
│   └── wwwroot/index.html        # เว็บแดชบอร์ดศูนย์บัญชาการสไตล์ Glassmorphism
├── ESP32_ADC_Stream/             # โปรเจกต์ ESP-IDF C สำหรับไมโครคอนโทรลเลอร์ ESP32
│   └── main/ESP32_ADC_Stream.c   # สตรีมมิ่งค่าแรงดัน ADC (GPIO34) และควบคุม LED (GPIO2) ผ่าน Serial
├── iot_unified_dashboard/        # โปรเจกต์แอปพลิเคชันโมบายล์ Flutter
│   ├── lib/main.dart             # จุดเริ่มต้นแอปและสถาปัตยกรรมแท็บเมนู
│   ├── lib/telemetry_dashboard_screen.dart # หน้าจอแสดงผลเข็มไมล์และกราฟ Real-time จาก MQTT
│   ├── lib/actuator_control_screen.dart    # หน้าจอสวิตช์สั่งงานหลอดไฟ LED พร้อมระบบ ACK
│   └── lib/radial_gauge_widget.dart        # วิดเจ็ต Custom Radial Gauge วัดแรงดันไฟฟ้า
└── image/                        # คลังภาพถ่ายหน้าจอและภาพเคลื่อนไหว GIF แสดงผลการทดสอบ
    ├── image.png                 # ผลการทดลอง 13.1 (Console พอร์ต 5000/1883)
    ├── image copy.png            # ผลการทดลอง 13.1 (MQTT Client เชื่อมต่อสำเร็จ)
    ├── image copy 2.png          # ผลการทดลอง 13.2 (เรียกดู Telemetry ผ่าน REST API)
    ├── image copy 3.png          # ผลการทดลอง 13.2 (ดักฟัง MQTT Topic ผ่าน CLI)
    ├── image copy 4.png          # ผลการทดลอง 13.2 (สั่งเปิดไฟผ่าน HTTP POST)
    ├── IMG_4534.gif              # ผลการทดลอง 13.2 (ไฟ LED ติดบนบอร์ดจริง)
    ├── image copy 5.png          # ผลการทดลอง 13.3 (Web Dashboard SignalR Online & Metrics)
    ├── IMG_4541.gif              # ผลการทดลอง 13.3 (หมุน Potentiometer & สลับไฟผ่าน Web สด)
    ├── image copy 6.png          # ผลการทดลอง 13.4 (Universal Telemetry Sync 1.58V)
    ├── image copy 7.png          # ผลการทดลอง 13.4 (Mobile & Web Switch Synchronization ON)
    ├── IMG_4544.gif              # ผลการทดลอง 13.4 (ทดสอบสวิตช์ไฟ LED สอดประสานฮาร์ดแวร์จริง)
    └── IMG_4545.gif              # ผลการทดลอง 13.4 (ทดสอบหมุน Potentiometer สดพร้อมไฟ LED)
```

---

## 🏆 ผลการทดสอบ Grand Verification Matrix (ใบงานที่ 13.4)

| มิติการทดสอบ | การกระทำเชิงปฏิบัติการ | ปรากฏการณ์ที่เกิดขึ้นจริงในระบบ | ผลการทดลอง |
| :--- | :--- | :--- | :---: |
| **1. Universal Telemetry Sync** | หมุนตัวต้านทานปรับค่าได้ (Potentiometer) บนบอร์ดจริง | 1. ค่าแรงดันวิ่งผ่านสาย Serial เข้า Kestrel<br/>2. ตัวเลขบนเว็บแดชบอร์ดวิ่งสดทันที<br/>3. เข็ม Radial Gauge บน Flutter หมุนตามทันทีพร้อมกัน | **ผ่าน** |
| **2. Mobile-to-Web Actuation** | แตะสวิตช์เปิดไฟบนแอป Flutter (ส่งผ่าน MQTT) | 1. หลอดไฟ LED จริงบนบอร์ด ESP32 ติดสว่างขึ้น<br/>2. ปุ่มสวิตช์บนหน้าเว็บเปลี่ยนเป็นสีเขียว "ON" อัตโนมัติ | **ผ่าน** |
| **3. Web-to-Mobile Actuation** | คลิกปุ่มปิดไฟบนหน้าเว็บ (ส่งผ่าน SignalR) | 1. หลอดไฟ LED จริงบนบอร์ด ESP32 ดับลงทันที<br/>2. สวิตช์บนแอปมือถือ Flutter สลับเป็น "OFF" ทันที | **ผ่าน** |
| **4. Third-Party REST Override** | ยิงคำสั่งผ่าน cURL หรือ Postman:<br/>`curl -X POST http://localhost:5050/api/led/on` | 1. หลอดไฟ LED สว่างขึ้น<br/>2. ทั้งหน้าเว็บและแอปมือถืออัปเดตสถานะเป็น "ON" พร้อมกัน | **ผ่าน** |

---

## 🚀 วิธีการติดตั้งและเริ่มรันระบบ (Quick Start)

### 1. ฝั่งฮาร์ดแวร์ ESP32
- เสียบสาย USB ระหว่างคอมพิวเตอร์กับบอร์ด ESP32 (ตรวจสอบ Serial Port เช่น `/dev/cu.usbserial-0001` หรือ `COM3`)
- ตรวจสอบว่าเฟิร์มแวร์ใน [`ESP32_ADC_Stream`](ESP32_ADC_Stream) ถูกแฟลชลงบนบอร์ดเรียบร้อย

### 2. ฝั่ง Kestrel Converged Edge Gateway
```bash
cd ConvergedEdgeGateway
dotnet run
```
*ระบบจะเปิดให้บริการพอร์ต HTTP 5050 (REST API / SignalR / Web UI) และพอร์ต TCP 1883 (Embedded MQTT Broker)*

### 3. ฝั่งเว็บแดชบอร์ดศูนย์บัญชาการ
- เปิดเว็บเบราว์เซอร์ไปที่: `http://localhost:5050`

### 4. ฝั่งแอปพลิเคชันโมบายล์ Flutter
```bash
cd iot_unified_dashboard
flutter run
```
*แอปจะเชื่อมต่อไปยัง Embedded MQTT Broker ที่พอร์ต 1883 และซิงโครไนซ์ข้อมูลร่วมกับทั้งระบบทันที*
