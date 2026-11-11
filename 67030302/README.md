# การทดลองสัปดาห์ที่ 13: Kestrel Edge Gateway and Protocol Integration
**รหัสนักศึกษา:** 67030302  
**รายวิชา:** IoT Application Development (The Grand Finale: Converged Edge Gateway & System Integration)

---

## 📁 โครงสร้างโฟลเดอร์สำหรับส่งงานและทำการทดลอง

```text
67030302/
├── Lab13_1/                       # ใบงานที่ 13.1: Embedded MQTT Broker และ Client ด้วย MQTTnet
│   ├── ConvergedEdgeGateway/     # โปรเจกต์ ASP.NET Core Kestrel (.NET 8)
│   └── README.md                 # คู่มือการทดลอง บันทึกผล และตอบคำถามท้ายบท 13.1
├── Lab13_2/                       # ใบงานที่ 13.2: Hardware Serial to MQTT and REST Bridge
│   ├── ConvergedEdgeGateway/     # โปรเจกต์ Kestrel + SerialBridgeWorker + REST API
│   ├── ESP32_ADC_Stream/         # เฟิร์มแวร์ ESP-IDF สำหรับอ่าน ADC34 และควบคุม LED GPIO2
│   └── README.md                 # คู่มือการทดลอง บันทึกผล และตอบคำถามท้ายบท 13.2
├── Lab13_3/                       # ใบงานที่ 13.3: Realtime SignalR Hub and Converged Web Dashboard
│   ├── ConvergedEdgeGateway/     # โปรเจกต์ Kestrel + SignalR Hub + Glassmorphism Web Dashboard
│   ├── ESP32_ADC_Stream/         # เฟิร์มแวร์ ESP32
│   └── README.md                 # คู่มือการทดลอง บันทึกผล และตอบคำถามท้ายบท 13.3
├── Lab13_4/                       # ใบงานที่ 13.4: The Grand Finale (All-in-One IoT System Integration)
│   ├── ConvergedEdgeGateway/     # โปรเจกต์ Gateway สมบูรณ์แบบ (SignalR + MQTT + REST + Serial)
│   ├── ESP32_ADC_Stream/         # เฟิร์มแวร์ ESP-IDF สำหรับอ่าน ADC34 และควบคุม LED GPIO2
│   ├── run_lab4.sh               # สคริปต์รันเกตเวย์พร้อมทดสอบ 4 มิติอัตโนมัติ
│   ├── test_e2e_grand_finale.py  # ชุดทดสอบ End-to-End ข้ามโปรโตคอล
│   └── README.md                 # บันทึกผลการทดสอบ Verification Matrix 4 มิติ และคำถามท้ายภาค
└── README.md                     # สรุปภาพรวมและสารบัญการทดลอง (หน้านี้)
```

---

## 🛠️ ข้อกำหนดและข้อแนะนำในการรันบนเครื่อง

1. **.NET SDK Target Framework**:
   - หากเครื่องที่ใช้งานติดตั้ง **.NET 8.0 SDK** หรือ **.NET 10.0 SDK** ให้ตรวจสอบไฟล์ `.csproj` ว่ากำหนดเป็น `<TargetFramework>net8.0</TargetFramework>` หรือ `<TargetFramework>net10.0</TargetFramework>` ตามเวอร์ชันของ .NET บนเครื่อง
2. **การตั้งค่าพอร์ต Serial (ESP32)**:
   - ตรวจสอบชื่อพอร์ตในไฟล์ `Services.cs`:
     - **Windows**: เช่น `"COM3"`, `"COM24"`
     - **macOS / Linux**: เช่น `"/dev/cu.usbserial-0001"`, `"/dev/ttyUSB0"`
   - *หมายเหตุ: หากยังไม่ได้เชื่อมต่อบอร์ดจริง ระบบรองรับโหมดจำลองฮาร์ดแวร์เสมือน (Virtual Simulation) อัตโนมัติ*
3. **การล็อกพอร์ต Serial (Exclusive Access)**:
   - ปิดโปรแกรม Serial Monitor, PuTTY หรือ `idf.py monitor` ก่อนรันโปรเจกต์ Kestrel เสมอ เพื่อให้ BackgroundService เปิดพอร์ตสำเร็จ
4. **พอร์ตเครือข่ายที่ใช้งาน**:
   - **พอร์ต 5001**: HTTP REST API และ Web Dashboard (SignalR) *(ใช้พอร์ต 5001 เพื่อหลีกเลี่ยงการชนกับ AirPlay Receiver บน macOS)*
   - **พอร์ต 1883**: Embedded MQTT Broker (TCP)

---

## 📋 สรุปรายการใบงาน

| ใบงาน | หัวข้อการทดลอง | สถานะ |
| :---: | :--- | :---: |
| **Lab 13.1** | [Embedded MQTT Broker and Client with MQTTnet](Lab13_1/README.md) | สำเร็จเรียบร้อย |
| **Lab 13.2** | [Hardware Serial to MQTT and REST Bridge](Lab13_2/README.md) | สำเร็จเรียบร้อย |
| **Lab 13.3** | [Realtime SignalR Hub and Converged Web Dashboard](Lab13_3/README.md) | สำเร็จเรียบร้อย |
| **Lab 13.4** | [The Grand Finale: All-in-One IoT System Integration](Lab13_4/README.md) | สำเร็จเรียบร้อยสมบูรณ์ |

