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

📁 **โฟลเดอร์วิดีโอรวมบน Google Drive:** [คลิกเปิดโฟลเดอร์ Google Drive](https://drive.google.com/drive/folders/19cNRJgQ5SsHgu9nlOEMk7COyEWwcyatz?usp=drive_link)

| ชื่อไฟล์คลิป | ใบงาน | กิจกรรมที่ทดสอบ | รายละเอียดสิ่งที่ทดสอบ | ลิงก์วิดีโอ (Google Drive) |
| :---: | :---: | :--- | :--- | :---: |
| **`IMG_4534.mov`** | **13.2** | กิจกรรมที่ 13-2.5: การทดสอบ Hardware Serial Bridge | ยิง cURL POST สั่งเปิด-ปิดไฟ LED บนบอร์ด ESP32 ผ่าน REST API | [🎬 ดูคลิป IMG_4534](https://drive.google.com/file/d/1CDkV_oaVfw33kteOsmS2F47qFXaGg_tw/view?usp=drive_link) |
| **`IMG_4541.mov`** | **13.3** | กิจกรรมที่ 13-3.4: การทดสอบเว็บแดชบอร์ด SignalR | หมุน Potentiometer ดูค่าแรงดันสด และกดเปิด-ปิดไฟ LED ผ่านหน้าเว็บ SignalR | [🎬 ดูคลิป IMG_4541](https://drive.google.com/file/d/1Hw4BE0leZBKNJQDue8AKADp77QhR8YBQ/view?usp=sharing) |
| **`IMG_4544.mov`** | **13.4** | กิจกรรมที่ 13-4.2: การซิงค์สถานะ LED ข้ามแพลตฟอร์ม | กดสวิตช์ไฟบนแอป Flutter แล้วไฟบนบอร์ดจริงและสถานะบนเว็บเปลี่ยนตามพร้อมกัน | [🎬 ดูคลิป IMG_4544](https://drive.google.com/file/d/15n7HglXIQh4iCPVfUjhTsG49-ftTMrUX/view?usp=sharing) |
| **`IMG_4545.mov`** | **13.4** | กิจกรรมที่ 13-4.2: การทดสอบรับส่งข้อมูลแบบ Real-time | หมุน Potentiometer บนบอร์ดจริง ค่าแรงดันบนแอป Flutter และหน้าเว็บอัปเดตตามทันที | [🎬 ดูคลิป IMG_4545](https://drive.google.com/file/d/1Kxnx1abyCg04YXceybViPZbsa_dIhiz5/view?usp=sharing) |

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
