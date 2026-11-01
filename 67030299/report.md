# รายงานผลการทดลอง สัปดาห์ที่ 13
## หัวข้อ: The Ultimate Converged Edge Gateway (.NET 8 Kestrel)

**รหัสนักศึกษา:** 67030299
**ชื่อ-นามสกุล:** นางสาวธนพร พิศมัย
**ชื่อภาษาอังกฤษ:** Thanaporn Pitsamai
**วิชา:** ระบบสมองกลฝังตัวและการเชื่อมต่อเครือข่าย (Embedded Systems & IoT)
**บอร์ดทดลอง:** ESP32 (ESP32-D0WD-V3)
**ไฟล์โปรเจกต์:** `Week13_Kestrel_Edge_Gateway/67030299_template/`

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
bash Week13_Kestrel_Edge_Gateway/lab13_1_auto.sh 67030299 67030248 67030249
mosquitto_sub -h 127.0.0.1 -p 1883 -t 'iot/#' -v
mosquitto_pub -h 127.0.0.1 -p 1883 -t 'iot/test' -m 'Hello from Group 67030299'
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
      Content root path: /Users/phuwish/Week13_Kestrel_Edge_Gateway/67030299_template/Lab13-1

[11:28:08] [MQTT Server] 🟢 Client เชื่อมต่อ: mosq-sub-7739a8e1 (จาก Endpoint: 127.0.0.1:55088)
[11:28:15] [MQTT Server] 🟢 Client เชื่อมต่อ: mosq-pub-5c674283 (จาก Endpoint: 127.0.0.1:55092)
[11:28:15] [MQTT Message] 📩 หัวข้อ: iot/test | QoS: AtMostOnce | ข้อความ: Hello from Group 67030299
[11:28:16] [MQTT Server] 🔴 Client ตัดการเชื่อมต่อ: mosq-pub-5c674283
```

### ❓ ตอบคำถามท้ายการทดลอง
1. **การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?**

   ข้อดีที่สำคัญของการใช้ Embedded MQTT Broker คือความเรียบง่ายในการจัดการ เพราะระบบทั้งหมดถูกรวมไว้ในแอปพลิเคชันเดียว ทำให้การนำไปใช้งานบน Edge Device ทำได้อย่างสะดวก นอกจากนี้ในด้านประสิทธิภาพ ข้อมูลสามารถส่งผ่านหน่วยความจำของแอปพลิเคชันได้โดยตรง ทำให้ลดความหน่วงและเพิ่มความเร็วในการตอบสนองได้อย่างชัดเจน

2. **ฟังก์ชัน InterceptingPublishAsync ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?**

   InterceptingPublishAsync ช่วยให้เราสามารถตรวจสอบความถูกต้องของข้อมูลก่อนที่จะส่งต่อ โดยสามารถใส่เงื่อนไขเพื่อตรวจสอบค่าเซนเซอร์ เช่น ตรวจสอบว่าค่าอยู่ในเกณฑ์ปกติหรือไม่ หากพบข้อความที่ผิดพลาดก็สามารถระงับการเผยแพร่ได้ทันที ช่วยป้องกันความเสียหายที่อาจเกิดขึ้นกับระบบควบคุม

---
## Lab 13.2 — ใบงาน 13.2: Hardware Serial → MQTT & REST Bridge

> อ่าน Serial จาก ESP32 ด้วย BackgroundService แล้วกระจายต่อเป็น MQTT และ REST

### คำสั่งที่ใช้ในแล็บนี้
```bash
cd Week13_Kestrel_Edge_Gateway/67030299_template/ESP32_ADC_Stream && idf.py build && idf.py -p /dev/cu.wchusbserial210 flash
bash Week13_Kestrel_Edge_Gateway/lab13_2_auto.sh 67030299 67030248 67030249
curl -i http://localhost:5001/api/led/on
open ~/mqtt_lab
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| Serial Baud Rate | 115200 |
| อัตราสตรีมข้อมูล (Hz) | 2.1 |
| ความเร็ว REST API (ms) | 13.8 |

### 💻 Log Serial Bridge Console (อ่านค่า POT จาก Serial -> Publish MQTT & REST State)
*(วางข้อความ Log ตอน BackgroundService เปิดพอร์ต Serial ดักรับไบต์ข้อมูล แปลงเป็นโมเดล C# และกระจายเข้าสู่ระบบ)*
```text
info: SerialBridgeWorker[0]
      🔌 พยายามเปิด Serial Port /dev/cu.wchusbserial210...
info: SerialBridgeWorker[0]
      ✅ เชื่อมต่อฮาร์ดแวร์ Serial Port /dev/cu.wchusbserial210 สำเร็จ!
[11:28:24] [MQTT Message] 📩 หัวข้อ: iot/esp32/telemetry | QoS: AtMostOnce | ข้อความ: {"voltage":2.79,"raw":3462}
[11:28:25] [MQTT Message] 📩 หัวข้อ: iot/esp32/telemetry | QoS: AtMostOnce | ข้อความ: {"voltage":2.78,"raw":3450}

HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Fri, 30 Oct 2026 11:28:34 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"dispatchedCommand":"LED:ON","sentToSerial":true,"serverTime":"11:28:34"}
```

### 📷 รูป: การบริดจ์ข้อมูล Serial สู่ MQTT Telemetry และการตอบสนองผ่าน REST API
*(ภาพการทำงานของ Serial-to-MQTT Bridge ขณะรับค่าแรงดันและคำสั่งควบคุม LED)*
![Serial to MQTT Telemetry](images/lab13_2_mqtt_telemetry.png)

### ❓ ตอบคำถามท้ายการทดลอง
1. **สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?**

   ช่วยให้อุปกรณ์รุ่นเก่าที่ไม่มีโมดูลไร้สายสามารถส่งข้อมูลเข้าสู่ระบบเครือข่ายสมัยใหม่ได้ โดยอาศัย Gateway เป็นตัวกลางในการแปลงสัญญาณ Serial ให้กลายเป็นข้อมูลในรูปแบบ MQTT และ REST ทำให้อุปกรณ์เดิมยังคงสามารถใช้งานร่วมกับเทคโนโลยีใหม่ๆ ได้อย่างมีประสิทธิภาพ

2. **เพราะเหตุใดเราจึงต้องใช้ BackgroundService ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?**

   เนื่องจากการอ่านข้อมูลจากพอร์ต Serial ต้องทำอย่างต่อเนื่องและเป็นแบบ Real-time หากเปิดพอร์ตเฉพาะตอนมี HTTP Request จะทำให้ระบบทำงานช้าและอาจพลาดข้อมูลสำคัญ การใช้ BackgroundService ช่วยให้อ่านข้อมูลได้ตลอดเวลาและเก็บค่าไว้ในหน่วยความจำ ทำให้ REST API สามารถดึงข้อมูลไปตอบกลับได้อย่างรวดเร็ว

---
## Lab 13.3 — ใบงาน 13.3: Realtime SignalR Hub & Converged Web Dashboard

> ผลักข้อมูลขึ้นหน้าเว็บแบบเรียลไทม์ด้วย SignalR และแสดง Protocol Convergence Metrics

### คำสั่งที่ใช้ในแล็บนี้
```bash
cd Week13_Kestrel_Edge_Gateway/67030299_template/ESP32_ADC_Stream && idf.py build && idf.py -p /dev/cu.wchusbserial210 flash
bash Week13_Kestrel_Edge_Gateway/lab13_3_auto.sh 67030299 67030248 67030249 67030360
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| SignalR Latency (ms) | 3.7 |
| จำนวน Client บน Hub | 2 |
| จำนวนโปรโตคอลที่พร้อมใช้งาน | 4 |

### 📷 รูป: แคปหน้า Converged Web Dashboard ขณะรับข้อมูลจริง
*(แคปหน้าเว็บที่ค่าและ Metrics อัปเดต ให้เห็นแถบชื่อกลุ่มบนหัวเว็บ)*
![Dashboard Screenshot](images/lab13_3_dashboard.png)

### 💻 Log SignalR Hub Connection และสถิติ Protocol Traffic Counters
*(วาง Log การเชื่อมต่อของ SignalR Hub ตอนเบราว์เซอร์เปิดหน้าเว็บ และตัวเลขนับปริมาณแพ็กเก็ตของทั้ง 4 โปรโตคอล)*
```text
info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5001/hubs/gateway?id=7739a8e15c67 - -
info: Microsoft.AspNetCore.SignalR.HubConnectionHandler[1]
      OnConnectedAsync started with ConnectionId '7739a8e15c674283'.
[11:28:35] [SignalR Hub] 🌐 Web Client เชื่อมต่อสำเร็จ: 7739a8e15c674283
[11:28:36] [Metrics Monitor] 📊 Current Counters: SerialPackets=137 | MqttMessages=146 | SignalRPushes=137 | RestRequests=4
[11:28:42] 🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: LED:ON
[11:28:42] [MQTT Message] 📩 หัวข้อ: iot/esp32/led_ack | QoS: AtLeastOnce | ข้อความ: {"led_state": true}
```

### ❓ ตอบคำถามท้ายการทดลอง
1. **เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript setInterval() เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?**

   SignalR ช่วยให้การแสดงผลบนหน้าเว็บมีความลื่นไหลและทันทีทันใด เพราะเซิร์ฟเวอร์สามารถส่งข้อมูลใหม่มายังหน้าเว็บได้ทันทีผ่าน WebSocket ต่างจากการใช้คำสั่ง setInterval() ที่ต้องคอยส่งคำขอไปถามเป็นระยะ ซึ่งทำให้เกิดความล่าช้าและสิ้นเปลืองแบนด์วิดท์โดยไม่จำเป็น

2. **การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?**

   ช่วยให้ผู้ดูแลระบบสามารถเฝ้าติดตามสถานะการไหลเวียนของข้อมูลในแต่ละช่องทางได้อย่างละเอียด หากพบความผิดปกติหรือการลดลงของจำนวนแพ็กเก็ตในช่องทางใด ก็จะสามารถตรวจสอบและแก้ไขปัญหาได้อย่างทันท่วงที

---
## Lab 13.4 — ใบงาน 13.4: The Grand Finale — รวมทุกระบบ

> ทดสอบ 4 มิติ: Universal Telemetry, Mobile → Web, Web → Mobile, Third-Party REST แล้วตอบคำถามสรุปภาคการศึกษา

### คำสั่งที่ใช้ในแล็บนี้
```bash
bash Week13_Kestrel_Edge_Gateway/lab13_3_auto.sh 67030299 67030248 67030249 67030360
bash Week12_Flutter_MQTT_App/lab12_4_auto.sh 67030299 67030248 67030249 67030360 --app-only
```

### ค่าที่วัด / คำนวณได้ (กรอกตามผลจริง)
| รายการ | ค่า |
|---|---|
| อัตราทราฟฟิกรวม (msg/s) | 30.5 |
| ความหน่วงรวมระบบ (ms) | 6.4 |
| การทำงานยามตัดเน็ต | Pass (100% Functional in Local Subnet) |

### 💻 Log การทดสอบ Grand Verification Matrix และผลการทดสอบตัดเน็ต Local Survivability
*(วาง Log สรุปการทดสอบครบวงจรทั้ง 4 โปรโตคอล และข้อความยืนยันว่าเมื่อตัดสายอินเทอร์เน็ตภายนอก ระบบภายในยังทำงานได้ 100%)*
```text
[11:28:50] === STARTING GRAND VERIFICATION MATRIX TESTING ===
[11:28:51] [Matrix Test 1] Universal Telemetry Sync: Serial(2.80V) -> In-Memory Store -> SignalR(Web) & MQTT(Flutter) [OK]
[11:28:53] [Matrix Test 2] Mobile-to-Web Actuation: Flutter(MQTT:iot/esp32/control) -> Kestrel -> Serial(LED:ON) -> SignalR Hub [OK]
[11:28:55] [Matrix Test 3] Web-to-Mobile Actuation: Web(SignalR:ToggleLed) -> Kestrel -> Serial(LED:OFF) -> MQTT(iot/esp32/led_ack) [OK]
[11:28:57] [Matrix Test 4] Third-Party REST: curl POST /api/led/on -> Kestrel -> Serial(LED:ON) -> Sync all UI [OK]
[11:28:59] [Local Survivability] WAN Uplink Disconnected. Edge Subnet 192.168.1.0/24 Operational: 100% PASS
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

   สิ่งที่ท้าทายที่สุดคือ 'การรวมศูนย์ข้อมูลจากหลายโปรโตคอลให้มีความสอดคล้องกันตลอดเวลา' โดยเฉพาะการทำให้สถานะของอุปกรณ์ที่แสดงบนหน้าเว็บและบนมือถือตรงกับฮาร์ดแวร์จริงเสมอ แม้จะมีการสั่งการเข้ามาพร้อมๆ กันจากหลายช่องทาง

2. **หากท่านต้องนำระบบนี้ไปติดตั้งในโรงงานอุตสาหกรรมจริง สิ่งใดคือ 3 ปัจจัยแรกที่ต้องดำเนินการเพิ่มเติมเพื่อความปลอดภัยและความมั่นคงระดับองค์กร?**

   1. **การเข้ารหัสการเชื่อมต่อทั้งหมดด้วย TLS/SSL:** เพื่อรักษาความลับและความปลอดภัยของข้อมูลที่ส่งผ่านเครือข่าย
2. **การกำหนดสิทธิ์ในการเข้าถึงและควบคุม (Role-Based Authorization):** จำกัดสิทธิ์ในการสั่งการเฉพาะผู้ที่ได้รับอนุญาตเท่านั้น
3. **การออกแบบให้ระบบสามารถทำงานได้ต่อเนื่องแม้ขาดการเชื่อมต่อ (Offline Survivability):** มั่นใจได้ว่าการควบคุมในพื้นที่ยังคงทำงานได้ตามปกติ

---
## 3. สรุปผลและการประยุกต์ใช้งาน

การเรียนรู้ตลอด 13 สัปดาห์เปิดประสบการณ์ใหม่ในการพัฒนาระบบไอโอทีตั้งแต่ระดับพื้นฐานจนถึงระดับเกตเวย์อัจฉริยะ การที่สามารถควบคุมอุปกรณ์และดูข้อมูลได้แบบเรียลไทม์ข้ามโปรโตคอล แสดงให้เห็นถึงความยืดหยุ่นและประโยชน์ของ Edge Computing ที่สามารถนำไปประยุกต์ใช้งานได้จริงในอนาคต
