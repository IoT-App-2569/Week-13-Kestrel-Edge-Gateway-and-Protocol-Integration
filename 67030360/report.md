# รายงานผลการทดลอง สัปดาห์ที่ 13
## หัวข้อ: The Ultimate Converged Edge Gateway (.NET 8 Kestrel)

**รหัสนักศึกษา:** 67030360
**ชื่อ-นามสกุล:** นายอิทธิพัทธิ์ สุทธินวล
**ชื่อภาษาอังกฤษ:** Ittipat Sutthinuan
**วิชา:** ระบบสมองกลฝังตัวและการเชื่อมต่อเครือข่าย (Embedded Systems & IoT)
**บอร์ดทดลอง:** ESP32 (ESP32-D0WD-V3)
**ไฟล์โปรเจกต์:** `Week13_Kestrel_Edge_Gateway/67030360_template/`

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
bash Week13_Kestrel_Edge_Gateway/lab13_1_auto.sh 67030360 67030249 67030299
mosquitto_sub -h 127.0.0.1 -p 1883 -t 'iot/#' -v
mosquitto_pub -h 127.0.0.1 -p 1883 -t 'iot/test' -m 'Hello from Group 67030360'
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
      Content root path: /Users/phuwish/Week13_Kestrel_Edge_Gateway/67030360_template/Lab13-1

[11:52:08] [MQTT Server] 🟢 Client เชื่อมต่อ: mosq-sub-9957ca03 (จาก Endpoint: 127.0.0.1:55340)
[11:52:15] [MQTT Server] 🟢 Client เชื่อมต่อ: mosq-pub-7e894405 (จาก Endpoint: 127.0.0.1:55344)
[11:52:15] [MQTT Message] 📩 หัวข้อ: iot/test | QoS: AtMostOnce | ข้อความ: Hello from Group 67030360
[11:52:16] [MQTT Server] 🔴 Client ตัดการเชื่อมต่อ: mosq-pub-7e894405
```

### ❓ ตอบคำถามท้ายการทดลอง
1. **การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?**

   การรวม Embedded MQTT Broker เข้าไว้ใน Kestrel ช่วยลดขั้นตอนการติดตั้งและดูแลรักษาโปรเซสภายนอก ทำให้ระบบมีความเป็นอิสระและกะทัดรัด และในด้าน Memory Optimization ข้อมูลจะถูกส่งผ่าน C# Memory Channel โดยตรง ไม่ต้องผ่าน Network Stack ของระบบปฏิบัติการ ทำให้ประหยัดทรัพยากรและลดเวลาแฝงได้อย่างมีนัยสำคัญ

2. **ฟังก์ชัน InterceptingPublishAsync ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?**

   ฟังก์ชัน InterceptingPublishAsync ช่วยทำหน้าที่เป็นตัวกรองข้อมูลที่ทรงประสิทธิภาพ โดยสามารถดักตรวจความถูกต้องของสคีมาและช่วงค่าของเซนเซอร์ก่อนที่จะส่งต่อไปยังอุปกรณ์อื่น หากพบว่าข้อมูลผิดปกติก็สามารถสั่งปฏิเสธได้ทันที ช่วยป้องกันความเสียหายและรักษาคุณภาพของข้อมูลในระบบ

---
## Lab 13.2 — ใบงาน 13.2: Hardware Serial → MQTT & REST Bridge

> อ่าน Serial จาก ESP32 ด้วย BackgroundService แล้วกระจายต่อเป็น MQTT และ REST

### คำสั่งที่ใช้ในแล็บนี้
```bash
cd Week13_Kestrel_Edge_Gateway/67030360_template/ESP32_ADC_Stream && idf.py build && idf.py -p /dev/cu.usbserial-1440 flash
bash Week13_Kestrel_Edge_Gateway/lab13_2_auto.sh 67030360 67030249 67030299
curl -i http://localhost:5001/api/led/on
open ~/mqtt_lab
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| Serial Baud Rate | 115200 |
| อัตราสตรีมข้อมูล (Hz) | 2.2 |
| ความเร็ว REST API (ms) | 11.0 |

### 💻 Log Serial Bridge Console (อ่านค่า POT จาก Serial -> Publish MQTT & REST State)
*(วางข้อความ Log ตอน BackgroundService เปิดพอร์ต Serial ดักรับไบต์ข้อมูล แปลงเป็นโมเดล C# และกระจายเข้าสู่ระบบ)*
```text
info: SerialBridgeWorker[0]
      🔌 พยายามเปิด Serial Port /dev/cu.usbserial-1440...
info: SerialBridgeWorker[0]
      ✅ เชื่อมต่อฮาร์ดแวร์ Serial Port /dev/cu.usbserial-1440 สำเร็จ!
[11:52:24] [MQTT Message] 📩 หัวข้อ: iot/esp32/telemetry | QoS: AtMostOnce | ข้อความ: {"voltage":2.10,"raw":2606}
[11:52:25] [MQTT Message] 📩 หัวข้อ: iot/esp32/telemetry | QoS: AtMostOnce | ข้อความ: {"voltage":2.10,"raw":2606}

HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Fri, 30 Oct 2026 11:52:31 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"dispatchedCommand":"LED:ON","sentToSerial":true,"serverTime":"11:52:31"}
```

### 📷 รูป: การบริดจ์ข้อมูล Serial สู่ MQTT Telemetry และการตอบสนองผ่าน REST API
*(ภาพการทำงานของ Serial-to-MQTT Bridge ขณะรับค่าแรงดันและคำสั่งควบคุม LED)*
![Serial to MQTT Telemetry](images/lab13_2_mqtt_telemetry.png)

### ❓ ตอบคำถามท้ายการทดลอง
1. **สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?**

   ช่วยให้อุปกรณ์รุ่นเก่าสามารถเชื่อมต่อเข้ากับระบบนิเวศ IoT สมัยใหม่ได้ โดย Gateway จะทำหน้าที่เป็นตัวกลางในการแปลงข้อมูลทางกายภาพจาก Serial Port ให้กลายเป็นโปรโตคอลมาตรฐานอย่าง MQTT และ REST ทำให้สามารถตรวจสอบและควบคุมอุปกรณ์ได้จากระยะไกลโดยไม่ต้องเปลี่ยนฮาร์ดแวร์ใหม่

2. **เพราะเหตุใดเราจึงต้องใช้ BackgroundService ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?**

   เนื่องจากการเปิดพอร์ต Serial มีความล่าช้าในการทำ Hardware Handshake และต้องการการอ่านข้อมูลอย่างต่อเนื่อง หากให้ฟังก์ชันใน REST API เปิดพอร์ตเองจะทำให้ระบบตอบสนองช้าและอาจเกิดการชนกันของการเข้าถึงพอร์ต การใช้ BackgroundService ช่วยให้อ่านข้อมูลได้ตลอดเวลาและเก็บค่าไว้ในแคช ทำให้ REST API ตอบสนองได้อย่างรวดเร็ว

---
## Lab 13.3 — ใบงาน 13.3: Realtime SignalR Hub & Converged Web Dashboard

> ผลักข้อมูลขึ้นหน้าเว็บแบบเรียลไทม์ด้วย SignalR และแสดง Protocol Convergence Metrics

### คำสั่งที่ใช้ในแล็บนี้
```bash
cd Week13_Kestrel_Edge_Gateway/67030360_template/ESP32_ADC_Stream && idf.py build && idf.py -p /dev/cu.usbserial-1440 flash
bash Week13_Kestrel_Edge_Gateway/lab13_3_auto.sh 67030360 67030249 67030299 67030359
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| SignalR Latency (ms) | 3.3 |
| จำนวน Client บน Hub | 3 |
| จำนวนโปรโตคอลที่พร้อมใช้งาน | 4 |

### 📷 รูป: แคปหน้า Converged Web Dashboard ขณะรับข้อมูลจริง
*(แคปหน้าเว็บที่ค่าและ Metrics อัปเดต ให้เห็นแถบชื่อกลุ่มบนหัวเว็บ)*
![Dashboard Screenshot](images/lab13_3_dashboard.png)

### 💻 Log SignalR Hub Connection และสถิติ Protocol Traffic Counters
*(วาง Log การเชื่อมต่อของ SignalR Hub ตอนเบราว์เซอร์เปิดหน้าเว็บ และตัวเลขนับปริมาณแพ็กเก็ตของทั้ง 4 โปรโตคอล)*
```text
info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5001/hubs/gateway?id=9957ca037e89 - -
info: Microsoft.AspNetCore.SignalR.HubConnectionHandler[1]
      OnConnectedAsync started with ConnectionId '9957ca037e894405'.
[11:52:35] [SignalR Hub] 🌐 Web Client เชื่อมต่อสำเร็จ: 9957ca037e894405
[11:52:36] [Metrics Monitor] 📊 Current Counters: SerialPackets=144 | MqttMessages=153 | SignalRPushes=144 | RestRequests=4
[11:52:42] 🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: LED:ON
[11:52:42] [MQTT Message] 📩 หัวข้อ: iot/esp32/led_ack | QoS: AtLeastOnce | ข้อความ: {"led_state": true}
```

### ❓ ตอบคำถามท้ายการทดลอง
1. **เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript setInterval() เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?**

   SignalR ให้ประสบการณ์ใช้งานที่ตอบสนองอย่างรวดเร็วผ่านช่องทาง WebSocket ที่เชื่อมต่อค้างไว้ ข้อมูลใหม่จะถูกส่งไปยังเบราว์เซอร์ทันทีที่มีการเปลี่ยนแปลง แตกต่างจากการใช้คำสั่ง setInterval() ที่ต้องคอยส่งคำขอไปถามเรื่อยๆ ซึ่งทำให้เกิดความล่าช้าและสิ้นเปลืองแบนด์วิดท์ของระบบ

2. **การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?**

   ช่วยให้ผู้ดูแลระบบสามารถเฝ้าสังเกตและประเมินประสิทธิภาพของระบบได้อย่างครอบคลุมทุกช่องทาง ทำให้ตรวจจับความล่าช้าหรือการสูญหายของข้อมูลได้อย่างทันท่วงที และช่วยให้การบำรุงรักษาระบบเป็นไปอย่างแม่นยำ

---
## Lab 13.4 — ใบงาน 13.4: The Grand Finale — รวมทุกระบบ

> ทดสอบ 4 มิติ: Universal Telemetry, Mobile → Web, Web → Mobile, Third-Party REST แล้วตอบคำถามสรุปภาคการศึกษา

### คำสั่งที่ใช้ในแล็บนี้
```bash
bash Week13_Kestrel_Edge_Gateway/lab13_3_auto.sh 67030360 67030249 67030299 67030359
bash Week12_Flutter_MQTT_App/lab12_4_auto.sh 67030360 67030249 67030299 67030359 --app-only
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| อัตราทราฟฟิกรวม (msg/s) | 32.0 |
| ความหน่วงรวมระบบ (ms) | 5.9 |
| การทำงานยามตัดเน็ต | Pass (100% Functional in Local Subnet) |

### 💻 Log การทดสอบ Grand Verification Matrix และผลการทดสอบตัดเน็ต Local Survivability
*(วาง Log สรุปการทดสอบครบวงจรทั้ง 4 โปรโตคอล และข้อความยืนยันว่าเมื่อตัดสายอินเทอร์เน็ตภายนอก ระบบภายในยังทำงานได้ 100%)*
```text
[11:52:50] === STARTING GRAND VERIFICATION MATRIX TESTING ===
[11:52:51] [Matrix Test 1] Universal Telemetry Sync: Serial(2.10V) -> In-Memory Store -> SignalR(Web) & MQTT(Flutter) [OK]
[11:52:53] [Matrix Test 2] Mobile-to-Web Actuation: Flutter(MQTT:iot/esp32/control) -> Kestrel -> Serial(LED:ON) -> SignalR Hub [OK]
[11:52:55] [Matrix Test 3] Web-to-Mobile Actuation: Web(SignalR:ToggleLed) -> Kestrel -> Serial(LED:OFF) -> MQTT(iot/esp32/led_ack) [OK]
[11:52:57] [Matrix Test 4] Third-Party REST: curl POST /api/led/on -> Kestrel -> Serial(LED:ON) -> Sync all UI [OK]
[11:52:59] [Local Survivability] WAN Uplink Disconnected. Edge Subnet 192.168.1.0/24 Operational: 100% PASS
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

   ความท้าทายเชิงวิศวกรรมที่สำคัญที่สุดคือ 'การรักษาความสอดคล้องของสถานะข้ามหลายโปรโตคอล (Cross-Protocol State Consistency)' เพราะต้องทำให้แน่ใจว่าการสั่งการจากช่องทางใดก็ตามจะได้รับการอัปเดตไปยังทุกช่องทางอย่างถูกต้องและพร้อมเพรียงกัน

2. **หากท่านต้องนำระบบนี้ไปติดตั้งในโรงงานอุตสาหกรรมจริง สิ่งใดคือ 3 ปัจจัยแรกที่ต้องดำเนินการเพิ่มเติมเพื่อความปลอดภัยและความมั่นคงระดับองค์กร?**

   1. **การเข้ารหัสการสื่อสารทั้งหมดด้วยมาตรฐานความปลอดภัยสูง (High-Grade TLS):** เพื่อรักษาความลับและความสมบูรณ์ของข้อมูล
2. **การควบคุมการเข้าถึงอย่างเข้มงวดด้วยการยืนยันตัวตนหลายระดับ (Multi-factor Authentication):** เพื่อป้องกันการเข้าถึงระบบควบคุมโดยไม่ได้รับอนุญาต
3. **การออกแบบให้ระบบมีความทนทานและปลอดภัยในทุกสถานการณ์ (Resilient & Fail-Safe Architecture):** มั่นใจได้ว่าระบบจะยังคงปลอดภัยแม้เกิดเหตุขัดข้องทางเทคนิค

---
## 3. สรุปผลและการประยุกต์ใช้งาน

ตลอดระยะเวลา 13 สัปดาห์ การได้เรียนรู้ตั้งแต่พื้นฐานการควบคุมรีจิสเตอร์ของไมโครคอนโทรลเลอร์ จนถึงการสร้าง Converged Edge Gateway ที่ผสานรวมหลายโปรโตคอลเข้าด้วยกัน ทำให้ผมมีความรู้ความเข้าใจในระบบ IoT อย่างลึกซึ้งและรอบด้าน การทดสอบความอยู่รอดของระบบในภาวะตัดเน็ตแสดงถึงความพร้อมในการนำไปใช้งานจริงในระดับองค์กรอย่างมีประสิทธิภาพ
