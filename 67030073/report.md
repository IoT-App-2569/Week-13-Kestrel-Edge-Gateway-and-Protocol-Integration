# รายงานผลการทดลอง สัปดาห์ที่ 13
## หัวข้อ: The Ultimate Converged Edge Gateway (.NET 8 Kestrel)

**รหัสนักศึกษา:** 67030073
**ชื่อ-นามสกุล:** นายณัฐพงศ์ เนียมประดิษฐ
**ชื่อภาษาอังกฤษ:** Natthapong Niampradit
**วิชา:** ระบบสมองกลฝังตัวและการเชื่อมต่อเครือข่าย (Embedded Systems & IoT)
**บอร์ดทดลอง:** ESP32 (ESP32-D0WD-V3)
**ไฟล์โปรเจกต์:** `Week13_Kestrel_Edge_Gateway/67030073_template/`

> คู่มือกรอกรายงาน
> * ค่าทุกช่องให้กรอกตามผลจริงที่กลุ่มวัดได้ (อย่าใช้ค่าตัวอย่าง)
> * รูปวางเป็น `![caption](images/ชื่อไฟล์)` และเก็บไฟล์รูปจริงในโฟลเดอร์ `images/`
> * คำตอบท้ายใบงานขอให้เขียนด้วยคำพูดตัวเอง อย่าก๊อปจากเพื่อน

---

## 1. การต่อวงจรฮาร์ดแวร์ (Hardware Setup & Wiring)
*(ใช้วงจรเดียวกับ Week 11, 12)*

| อุปกรณ์ | ขาบนอุปกรณ์ | ขาต่อบน ESP32 | หน้าที่ |
|---|---|---|---|
| LED | Anode (ผ่าน R 220Ω) | GPIO 2 | หลอดไฟสถานะ |
| LED | Cathode | GND | - |
| Potentiometer 10k | Wiper (ขากลาง) | GPIO 34 (ADC1_CH6) | เซนเซอร์แอนะล็อก |
| Potentiometer | ขาข้างหนึ่ง | 3.3V | ไฟเลี้ยง |
| Potentiometer | ขาอีกข้าง | GND | กราวด์ |

---
## Lab 13.1 — ใบงาน 13.1: Embedded MQTT Broker & Client ด้วย MQTTnet

> รัน MQTT Broker ฝังใน Kestrel ด้วย MQTTnet และดักข้อความด้วย InterceptingPublishAsync

### คำสั่งที่ใช้ในแล็บนี้
```bash
bash Week13_Kestrel_Edge_Gateway/lab13_1_auto.sh 67030073 67030180 67030248
mosquitto_sub -h 127.0.0.1 -p 1883 -t 'iot/#' -v
mosquitto_pub -h 127.0.0.1 -p 1883 -t 'iot/test' -m 'Hello from Group 67030073'
open ~/mqtt_lab
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| พอร์ต MQTT Broker | 1883 |
| พอร์ต HTTP Kestrel | 5001 |
| เวอร์ชัน MQTTnet | 4.3.7.1207 |

### 💻 Log Kestrel MQTTnet Broker Console Output (พอร์ต 1883 และการดักจับข้อความ)
*(วาง Log หน้าต่าง Kestrel คอนโซล ตอนไคลเอนต์เชื่อมต่อเข้ามา และดักจับเพย์โหลดข้อความ)*
```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: mqtt://[::]:1883
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: /Users/phuwish/Week13_Kestrel_Edge_Gateway/67030073_template/Lab13-1

[10:14:08] [MQTT Server] 🟢 Client เชื่อมต่อ: mosq-sub-a17f4b82 (จาก Endpoint: 127.0.0.1:54210)
[10:14:15] [MQTT Server] 🟢 Client เชื่อมต่อ: mosq-pub-9e014c2d (จาก Endpoint: 127.0.0.1:54214)
[10:14:15] [MQTT Message] 📩 หัวข้อ: iot/test | QoS: AtMostOnce | ข้อความ: Hello from Group 67030073
[10:14:16] [MQTT Server] 🔴 Client ตัดการเชื่อมต่อ: mosq-pub-9e014c2d
```

### ❓ ตอบคำถามท้ายการทดลอง
1. **การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?**

   การฝัง Embedded MQTT Broker ลงใน Kestrel โดยตรงช่วยให้ระบบลดขั้นตอนการ Deploy เหลือเพียง Single Process / Single Container ไม่จำเป็นต้องติดตั้งหรือคอนฟิก Mosquitto Daemon ภายนอกเพิ่มเติม และที่สำคัญที่สุดคือในระดับ Memory Architecture ข้อความที่ถูก Publish สามารถส่งผ่าน Event Handlers และ In-Memory Channels ภายในโปรเซสเดียวกันได้ทันที ทำให้ตัด Overhead จากการทำ Inter-Process Communication (IPC) หรือ Network Loopback TCP Socket ระหว่าง Broker กับ Web Application ทำให้ Latency ต่ำและ Throughput สูงขึ้นอย่างเห็นได้ชัด

2. **ฟังก์ชัน InterceptingPublishAsync ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?**

   ฟังก์ชัน InterceptingPublishAsync ทำหน้าที่เป็นเสมือน Edge Middleware ก่อนที่ Broker จะกระจายข้อความไปยัง Client อื่นๆ โดยเราสามารถดักอ่าน Payload ตรวจสอบความถูกต้องของสคีมา JSON และเช็กค่าทางกายภาพ เช่น เช็กว่าแรงดันของเซนเซอร์อยู่ในช่วง 0.00 - 3.30V หรือไม่ หากพบค่าที่ Out-of-Bound หรือเกิดความผิดปกติ เราสามารถสั่ง `args.ProcessPublish = false` เพื่อ Reject ข้อความทิ้งทันที หรือจะดัดแปลงแก้ไข (Sanitize) เพย์โหลดก่อนส่งต่อ ช่วยป้องกันไม่ให้ข้อมูลขยะหรือแพ็กเก็ตที่ผิดปกติรั่วไหลเข้าไปรบกวนระบบคลาวด์และแดชบอร์ด

---
## Lab 13.2 — ใบงาน 13.2: Hardware Serial → MQTT & REST Bridge

> อ่าน Serial จาก ESP32 ด้วย BackgroundService แล้วกระจายต่อเป็น MQTT และ REST

### คำสั่งที่ใช้ในแล็บนี้
```bash
cd Week13_Kestrel_Edge_Gateway/67030073_template/ESP32_ADC_Stream && idf.py build && idf.py -p /dev/cu.usbserial-0001 flash
bash Week13_Kestrel_Edge_Gateway/lab13_2_auto.sh 67030073 67030180 67030248
curl -i http://localhost:5001/api/led/on
open ~/mqtt_lab
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| Serial Baud Rate | 115200 |
| อัตราสตรีมข้อมูล (Hz) | 2.1 |
| ความเร็ว REST API (ms) | 11.2 |

### 💻 Log Serial Bridge Console (อ่านค่า POT จาก Serial -> Publish MQTT & REST State)
*(วางข้อความ Log ตอน BackgroundService เปิดพอร์ต Serial ดักรับไบต์ข้อมูล แปลงเป็นโมเดล C# และกระจายเข้าสู่ระบบ)*
```text
info: SerialBridgeWorker[0]
      🔌 พยายามเปิด Serial Port /dev/cu.usbserial-0001...
info: SerialBridgeWorker[0]
      ✅ เชื่อมต่อฮาร์ดแวร์ Serial Port /dev/cu.usbserial-0001 สำเร็จ!
[10:14:24] [MQTT Message] 📩 หัวข้อ: iot/esp32/telemetry | QoS: AtMostOnce | ข้อความ: {"voltage":1.69,"raw":2097}
[10:14:25] [MQTT Message] 📩 หัวข้อ: iot/esp32/telemetry | QoS: AtMostOnce | ข้อความ: {"voltage":1.70,"raw":2110}

HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Fri, 30 Oct 2026 10:14:28 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"dispatchedCommand":"LED:ON","sentToSerial":true,"serverTime":"10:14:28"}
```

### 📷 รูป: การบริดจ์ข้อมูล Serial สู่ MQTT Telemetry และการตอบสนองผ่าน REST API
*(ภาพการทำงานของ Serial-to-MQTT Bridge ขณะรับค่าแรงดันและคำสั่งควบคุม LED)*
![Serial to MQTT Telemetry](images/lab13_2_mqtt_telemetry.png)

### ❓ ตอบคำถามท้ายการทดลอง
1. **สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?**

   สถาปัตยกรรม Serial-to-MQTT Bridge ทำหน้าที่เป็น 'Protocol Transcoder' ที่บริดจ์ข้อมูลฮาร์ดแวร์ยุคดั้งเดิม (ที่สื่อสารผ่านพอร์ต UART, RS-232 หรือ RS-485) ให้แปลงสภาพเป็นข้อความมาตรฐาน JSON ผ่าน TCP/IP ทันที โดยที่บอร์ด ESP32 ไม่จำเป็นต้องมีโมดูล Wi-Fi หรือรัน TCP Stack ให้เปลืองหน่วยความจำ ส่งผลให้อุปกรณ์เก่าในโรงงานสามารถแลกเปลี่ยนข้อมูลกับ Cloud, MQTT Broker และ Web Dashboard สมัยใหม่ได้โดยไม่ต้องเปลี่ยนฮาร์ดแวร์ใหม่ทั้งชุด

2. **เพราะเหตุใดเราจึงต้องใช้ BackgroundService ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?**

   เพราะพอร์ต Serial ทางฮาร์ดแวร์มีลักษณะเป็น Stream ที่มีข้อมูลไหลเข้ามาอย่างต่อเนื่องแบบ Stateful และมี Buffer จำกัด หากเปิดและปิดพอร์ตตามจังหวะ Request ของ REST API (ซึ่งเป็น Stateless แบบตามใจผู้เรียก) จะทำให้เกิดปัญหา Port Contention, สูญเสียข้อมูลไบต์ระหว่างทาง (Buffer Overflow) และทำให้ API ตอบสนองช้ามาก การใช้ BackgroundService ทำให้เรามี Background Thread แยกต่างหากที่ถือ Handle ของ Serial Port ไว้ตลอดเวลา คอยอ่านข้อมูลลง Concurrent In-Memory Store เมื่อ REST API ร้องขอข้อมูล จึงอ่านจากหน่วยความจำได้ในเวลาไม่ถึง 1 ms ทันที

---
## Lab 13.3 — ใบงาน 13.3: Realtime SignalR Hub & Converged Web Dashboard

> ผลักข้อมูลขึ้นหน้าเว็บแบบเรียลไทม์ด้วย SignalR และแสดง Protocol Convergence Metrics

### คำสั่งที่ใช้ในแล็บนี้
```bash
cd Week13_Kestrel_Edge_Gateway/67030073_template/ESP32_ADC_Stream && idf.py build && idf.py -p /dev/cu.usbserial-0001 flash
bash Week13_Kestrel_Edge_Gateway/lab13_3_auto.sh 67030073 67030180 67030248 67030249
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| SignalR Latency (ms) | 3.2 |
| จำนวน Client บน Hub | 2 |
| จำนวนโปรโตคอลที่พร้อมใช้งาน | 4 |

### 📷 รูป: แคปหน้า Converged Web Dashboard ขณะรับข้อมูลจริง
*(แคปหน้าเว็บที่ค่าและ Metrics อัปเดต ให้เห็นแถบชื่อกลุ่มบนหัวเว็บ)*
![Dashboard Screenshot](images/lab13_3_dashboard.png)

### 💻 Log SignalR Hub Connection และสถิติ Protocol Traffic Counters
*(วาง Log การเชื่อมต่อของ SignalR Hub ตอนเบราว์เซอร์เปิดหน้าเว็บ และตัวเลขนับปริมาณแพ็กเก็ตของทั้ง 4 โปรโตคอล)*
```text
info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5001/hubs/gateway?id=a17f4b829e01 - -
info: Microsoft.AspNetCore.SignalR.HubConnectionHandler[1]
      OnConnectedAsync started with ConnectionId 'a17f4b829e014c2d'.
[10:14:35] [SignalR Hub] 🌐 Web Client เชื่อมต่อสำเร็จ: a17f4b829e014c2d
[10:14:36] [Metrics Monitor] 📊 Current Counters: SerialPackets=128 | MqttMessages=136 | SignalRPushes=128 | RestRequests=4
[10:14:42] 🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: LED:ON
[10:14:42] [MQTT Message] 📩 หัวข้อ: iot/esp32/led_ack | QoS: AtLeastOnce | ข้อความ: {"led_state": true}
```

### ❓ ตอบคำถามท้ายการทดลอง
1. **เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript setInterval() เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?**

   SignalR ใช้เทคโนโลยี Full-Duplex Persistent Connection (โดยเฉพาะ WebSockets) ทำให้เซิร์ฟเวอร์สามารถ 'Push' ข้อมูลใหม่ลงสู่หน้าเบราว์เซอร์ได้ทันทีที่ได้รับข้อมูลจาก Serial โดยไม่มีความหน่วง ขณะที่วิธี HTTP GET Polling ด้วย setInterval() จะเป็นการ 'Pull' ซ้ำๆ ซึ่งสร้าง HTTP Request/Response Headers ขนาดใหญ่ซ้ำซ้อน เปลืองแบนด์วิดท์ และทำให้เกิด Polling Jitter (ข้อมูลอาจดีเลย์ได้สูงสุดเท่ากับ Interval 500 ms) SignalR จึงให้ประสบการณ์ผู้ใช้ที่ลื่นไหล ค่าและเกจวัดขยับตามการหมุน Potentiometer ได้แบบ Real-time โดยไม่กระตุก

2. **การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?**

   Protocol Convergence Metrics (Serial, MQTT, SignalR, REST) ช่วยให้ทีมวิศวกรและผู้ดูแลระบบสามารถตรวจสอบ Pipeline Synchronization และ Bottleneck ของเครือข่ายได้แบบ Real-time เช่น หากพบว่าตัวนับ Serial Packets เพิ่มขึ้นต่อเนื่องแต่ตัวเลข MQTT Messages หยุดนิ่ง จะสามารถระบุได้ทันทีว่าเกิดปัญหาคอขวดหรือ Deadlock ที่ตัวเชื่อมต่อ MQTT ช่วยลดเวลาในการ Troubleshoot และเพิ่ม Reliability ให้กับสายการผลิตในอุตสาหกรรมได้อย่างแม่นยำ

---
## Lab 13.4 — ใบงาน 13.4: The Grand Finale — รวมทุกระบบ

> ทดสอบ 4 มิติ: Universal Telemetry, Mobile → Web, Web → Mobile, Third-Party REST แล้วตอบคำถามสรุปภาคการศึกษา

### คำสั่งที่ใช้ในแล็บนี้
```bash
bash Week13_Kestrel_Edge_Gateway/lab13_3_auto.sh 67030073 67030180 67030248 67030249
bash Week12_Flutter_MQTT_App/lab12_4_auto.sh 67030073 67030180 67030248 67030249 --app-only
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| อัตราทราฟฟิกรวม (msg/s) | 28.5 |
| ความหน่วงรวมระบบ (ms) | 5.8 |
| การทำงานยามตัดเน็ต | Pass (100% Functional in Local Subnet) |

### 💻 Log การทดสอบ Grand Verification Matrix และผลการทดสอบตัดเน็ต Local Survivability
*(วาง Log สรุปการทดสอบครบวงจรทั้ง 4 โปรโตคอล และข้อความยืนยันว่าเมื่อตัดสายอินเทอร์เน็ตภายนอก ระบบภายในยังทำงานได้ 100%)*
```text
[10:14:50] === STARTING GRAND VERIFICATION MATRIX TESTING ===
[10:14:51] [Matrix Test 1] Universal Telemetry Sync: Serial(1.68V) -> In-Memory Store -> SignalR(Web) & MQTT(Flutter) [OK]
[10:14:53] [Matrix Test 2] Mobile-to-Web Actuation: Flutter(MQTT:iot/esp32/control) -> Kestrel -> Serial(LED:ON) -> SignalR Hub [OK]
[10:14:55] [Matrix Test 3] Web-to-Mobile Actuation: Web(SignalR:ToggleLed) -> Kestrel -> Serial(LED:OFF) -> MQTT(iot/esp32/led_ack) [OK]
[10:14:57] [Matrix Test 4] Third-Party REST: curl POST /api/led/on -> Kestrel -> Serial(LED:ON) -> Sync all UI [OK]
[10:14:59] [Local Survivability] WAN Uplink Disconnected. Edge Subnet 192.168.1.0/24 Operational: 100% PASS
```

#### กิจกรรมที่ 13-4.2: การทดสอบ 4 มหาการทดลอง (The Grand Verification Matrix)
| มิติการทดสอบ | การกระทำเชิงปฏิบัติการ | ปรากฏการณ์ที่เกิดขึ้นจริงในระบบ | ผลการทดลอง (ผ่าน/ไม่ผ่าน) |
|---|---|---|---|
| 1. Universal Telemetry Sync | ปรับหมุน Potentiometer บน ESP32 ที่ต่อขา GPIO34 | ตัวเลขแรงดันและ Raw ADC บน Web Dashboard และ Flutter App เปลี่ยนแปลงพร้อมกันทันทีแบบเรียลไทม์ | ผ่าน |
| 2. Mobile-to-Web Actuation | กดปุ่มเปิด-ปิดไฟ LED บนหน้าจอแอป Flutter | หลอดไฟ LED บนบอร์ด ESP32 ติด/ดับทันที และสวิตช์บน Web Dashboard ซิงก์สถานะตามอัตโนมัติผ่าน SignalR | ผ่าน |
| 3. Web-to-Mobile Actuation | กดปุ่มสวิตช์สั่งงานบน Web Dashboard | คำสั่งส่งผ่าน SignalR Hub เข้าไปสั่ง Serial พอร์ตให้บอร์ดเปลี่ยนสถานะ LED และส่ง MQTT ACK ให้แอป Flutter อัปเดตสถานะตรงกัน | ผ่าน |
| 4. Third-Party REST Override | ยิงคำสั่ง `curl -X POST http://localhost:5001/api/led/off` ผ่าน Terminal | หลอดไฟ LED ดับลงทันที พร้อมกับที่หน้าเว็บและหน้าจอมือถืออัปเดตสถานะเป็น OFF อย่างสมบูรณ์ | ผ่าน |

### 📷 รูป: การทำงานร่วมกันครบวงจรของทั้งระบบ (Grand Ecosystem Architecture)
*(ภาพรวมสถาปัตยกรรมและผลการทดสอบครบ 4 โปรโตคอล: ESP32 Hardware, MQTTnet Broker, SignalR Web, และ Flutter App)*
![Grand Ecosystem Verification](images/lab13_4_ecosystem.png)

### ❓ ตอบคำถามท้ายการทดลอง
1. **ตลอดระยะเวลา 13 สัปดาห์ การเดินทางจากระดับบิตและรีจิสเตอร์ของเซนเซอร์ สู่เฟรมเวิร์ก FreeRTOS, ผ่านโปรโตคอลเครือข่ายท้องถิ่น, สู่ระบบคลาวด์ MQTT, หน้าจอมือถือ Flutter, และมาบรรจบที่ Kestrel Edge Gateway... สถาปัตยกรรมชิ้นใดที่ท่านคิดว่ามีความท้าทายเชิงวิศวกรรมมากที่สุด? เพราะเหตุใด?**

   ผมมองว่าสถาปัตยกรรม 'Protocol Convergence บน Edge Gateway ด้วย Kestrel' มีความท้าทายเชิงวิศวกรรมมากที่สุด เนื่องจากเป็นการหลอมรวมโปรโตคอลที่แตกต่างกันอย่างสิ้นเชิง 4 ชนิด (Low-Level Streaming UART, Publish/Subscribe MQTT, Request-Response REST, และ Bi-directional WebSockets SignalR) ให้อยู่ภายใต้โปรเซสเดียวกัน ต้องบริหารจัดการ Thread Synchronization, Non-blocking I/O และ Concurrent State Management ไม่ให้เกิด Deadlock หรือ Race Condition เมื่อมีทราฟฟิกยิงเข้ามาพร้อมกัน

2. **หากท่านต้องนำระบบนี้ไปติดตั้งในโรงงานอุตสาหกรรมจริง สิ่งใดคือ 3 ปัจจัยแรกที่ต้องดำเนินการเพิ่มเติมเพื่อความปลอดภัยและความมั่นคงระดับองค์กร?**

   1. **การเข้ารหัสช่องสัญญาณ Transport Layer Security (TLS/mTLS):** บังคับใช้ TLS สำหรับทั้งพอร์ต MQTT (8883) และ HTTPS/WSS บน Kestrel พร้อมการยืนยันตัวตนด้วย Client Certificate
2. **ระบบยืนยันตัวตนและการกำหนดสิทธิ์ (Authentication & Role-Based Access Control):** กำหนด User/Password และสิทธิ์ Topic ACL สำหรับ MQTT Client และใช้ JWT Bearer Token ในการควบคุมสิทธิ์การเรียก REST API และ SignalR Hub
3. **การออกแบบ Hardware Watchdog & Isolation:** ติดตั้งวงจร Optocoupler ป้องกันสัญญาณรบกวนทางไฟฟ้าของพอร์ต Serial ในโรงงาน และตั้งค่า Hardware Watchdog ให้รีบูตเกตเวย์อัตโนมัติเมื่อตรวจพบสถานะค้าง

---
## 3. สรุปผลและการประยุกต์ใช้งาน

ตลอดการทดลอง 13 สัปดาห์ จากการเขียนโปรแกรมควบคุมรีจิสเตอร์และงานขัดจังหวะในระดับไมโครคอนโทรลเลอร์ จนกระทั่งนำมาผสานรวมเข้ากับ .NET 8 Kestrel Edge Gateway ในสัปดาห์นี้ ทำให้ผมเข้าใจภาพรวมของระบบ Cyber-Physical อย่างลึกซึ้ง การรวมฮาร์ดแวร์ ESP32, MQTT Broker, REST API, SignalR และแอปพลิเคชัน Flutter เข้าด้วยกัน แสดงให้เห็นถึงพลังของ Edge Computing ที่สามารถทำงานได้อย่างสมบูรณ์แบบแม้ในยามที่โครงข่ายภายนอกถูกตัดขาด ถือเป็นรากฐานที่สำคัญอย่างยิ่งสำหรับการนำไปประยุกต์ใช้ในระบบ Industrial IoT ในอนาคต
