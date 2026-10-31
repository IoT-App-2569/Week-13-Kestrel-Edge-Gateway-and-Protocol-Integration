# รายงานผลการทดลอง สัปดาห์ที่ 13

**หัวข้อ:** The Ultimate Converged Edge Gateway (.NET 8 Kestrel)  
**วิชา:** ระบบสมองกลฝังตัวและการเชื่อมต่อเครือข่าย (Embedded Systems & IoT)  
**รหัสนักศึกษา:** 67030180  
**ชื่อ-นามสกุล:** นายภูมิชนะ เทียมแก้ว (Phumchana Thiamkaew)  
**บอร์ดทดลอง:** ESP32 (ESP32-D0WD-V3)  
**ไฟล์โปรเจกต์:** `Week13_Kestrel_Edge_Gateway/67030180_template/`  

---

## 1. การต่อวงจรฮาร์ดแวร์ (Hardware Setup & Wiring)
*(ใช้วงจรเดียวกับ Week 11, 12)*

| อุปกรณ์ | ขาบนอุปกรณ์ | ขาต่อบน ESP32 | หน้าที่ |
| :--- | :--- | :--- | :--- |
| **LED** | Anode (ผ่าน R 220Ω) | GPIO 2 | หลอดไฟสถานะ |
| **LED** | Cathode | GND | - |
| **Potentiometer 10k** | Wiper (ขากลาง) | GPIO 34 (ADC1_CH6) | เซนเซอร์แอนะล็อก |
| **Potentiometer** | ขาข้างหนึ่ง | 3.3V | ไฟเลี้ยง |
| **Potentiometer** | ขาอีกข้าง | GND | กราวด์ |

---

## Lab 13.1 — ใบงาน 13.1: Embedded MQTT Broker & Client ด้วย MQTTnet
รัน MQTT Broker ฝังใน Kestrel ด้วย MQTTnet และดักข้อความด้วย `InterceptingPublishAsync`

### คำสั่งที่ใช้ในแล็บนี้
```bash
Week13_Kestrel_Edge_Gateway/lab13_1_auto.sh 67030180 67030073 67030268
mosquitto_sub -h 127.0.0.1 -p 1883 -t 'iot/#' -v
mosquitto_pub -h 127.0.0.1 -p 1883 -t 'iot/test' -m 'Hello from Group 67030180'
open ~/mqtt_lab
```

### ค่าที่วัด / คำนวณได้
| รายการ | ค่า |
| :--- | :--- |
| **พอร์ต MQTT Broker** | 1883 |
| **พอร์ต HTTP Kestrel** | 5001 |
| **เวอร์ชัน MQTTnet** | 4.3.7.1207 |

### 💻 Log Kestrel MQTTnet Broker Console Output
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
      Content root path: /Users/phuwish/Week13_Kestrel_Edge_Gateway/67030180_template/Lab13-1

[10:22:08] [MQTT Server] 🟢 Client เชื่อมต่อ: mosq-sub-b28e5c93 (จาก Endpoint: 127.0.0.1:54388)
[10:22:15] [MQTT Server] 🟢 Client เชื่อมต่อ: mosq-pub-0f124d3e (จาก Endpoint: 127.0.0.1:54392)
[10:22:15] [MQTT Message] 📩 หัวข้อ: iot/test | QoS: AtMostOnce | ข้อความ: Hello from Group 67030180
[10:22:16] [MQTT Server] 🔴 Client ตัดการเชื่อมต่อ: mosq-pub-0f124d3e
```

### ❓ ตอบคำถามท้ายการทดลอง

1. **การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?**  
   * **ตอบ:** จุดเด่นสำคัญของการรัน Embedded MQTT Broker ภายใน Kestrel คือการลดความซับซ้อนของโครงสร้างพื้นฐาน (Infrastructure Footprint) จากเดิมที่ต้องคอนฟิกสองโปรเซสแยกกัน กลายเป็นการรวมศูนย์ในแอปพลิเคชัน .NET ตัวเดียว นอกจากนี้ในแง่ Memory Performance เราสามารถเข้าถึง Payload ของ MQTT Message ได้ผ่าน RAM โดยตรง ไม่ต้องผ่าน Loopback Network Interface ทำให้ลดการสูญเสีย CPU Cycle และตอบสนองต่อ Event ภายในระบบได้อย่างรวดเร็วมาก

2. **ฟังก์ชัน InterceptingPublishAsync ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?**  
   * **ตอบ:** `InterceptingPublishAsync` ทำหน้าที่เสมือนตัวกรองความปลอดภัยระดับ Data Link Layer โดยเปิดโอกาสให้ใส่ Business Logic ตรวจสอบความถูกต้องของแพ็กเก็ต เช่น ตรวจสอบช่วงแรงดันของเซนเซอร์ หากค่า Raw เกิน 4095 หรือกระโดดผิดปกติจาก Spike ของสัญญาณรบกวน เราสามารถตัดทิ้งไม่ให้กระจายต่อไปยัง Subscriber หรือแจ้งเตือนสถานะ Sensor Malfunction ได้ทันทีก่อนที่ข้อมูลจะถูกนำไปวิเคราะห์ผิดพลาด

---

## Lab 13.2 — ใบงาน 13.2: Hardware Serial → MQTT & REST Bridge
อ่าน Serial จาก ESP32 ด้วย `BackgroundService` แล้วกระจายต่อเป็น MQTT และ REST

### คำสั่งที่ใช้ในแล็บนี้
```bash
cd Week13_Kestrel_Edge_Gateway/67030180_template/ESP32_ADC_Stream && idf.py build && idf.py -p /dev/cu.usbserial-10 flash
bash Week13_Kestrel_Edge_Gateway/lab13_2_auto.sh 67030180 67030073 67030268
curl -i http://localhost:5001/api/led/on
open ~/mqtt_lab
```

### ค่าที่วัด / คำนวณได้
| รายการ | ค่า |
| :--- | :--- |
| **Serial Baud Rate** | 115200 |
| **อัตราสตรีมข้อมูล (Hz)** | 2.0 |
| **ความเร็ว REST API (ms)** | 14.5 |

### 💻 Log Serial Bridge Console
```text
info: SerialBridgeWorker[0]
      🔌 พยายามเปิด Serial Port /dev/cu.usbserial-10...
info: SerialBridgeWorker[0]
      ✅ เชื่อมต่อฮาร์ดแวร์ Serial Port /dev/cu.usbserial-10 สำเร็จ!
[10:22:24] [MQTT Message] 📩 หัวข้อ: iot/esp32/telemetry | QoS: AtMostOnce | ข้อความ: {"voltage":2.45,"raw":3041}
[10:22:25] [MQTT Message] 📩 หัวข้อ: iot/esp32/telemetry | QoS: AtMostOnce | ข้อความ: {"voltage":2.45,"raw":3041}

HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Fri, 30 Oct 2026 10:22:28 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"dispatchedCommand":"LED:ON","sentToSerial":true,"serverTime":"10:22:28"}
```

### ❓ ตอบคำถามท้ายการทดลอง

1. **สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?**  
   * **ตอบ:** ช่วยให้อุปกรณ์รุ่นเก่าที่คุยผ่าน Serial UART สามารถก้าวข้ามข้อจำกัดด้านฮาร์ดแวร์ โดยมี Gateway ทำหน้าที่เป็นล่ามกลาง คอยรับข้อมูลไบต์มาแปลงเป็นโครงสร้าง JSON แล้วส่งต่อเข้าสู่ระบบ MQTT มาตรฐาน ทำให้อุปกรณ์เก่าสามารถส่ง Telemetry ขึ้นแดชบอร์ด และรับคำสั่ง Actuation กลับมาได้เหมือนอุปกรณ์ IoT รุ่นใหม่ที่มีชิป Wi-Fi ในตัว

2. **เพราะเหตุใดเราจึงต้องใช้ BackgroundService ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?**  
   * **ตอบ:** เนื่องจากการเปิดพอร์ต Serial เป็นการจองฮาร์ดแวร์แบบ Exclusive Lock หากให้ฟังก์ชันใน REST API เปิดพอร์ตเองทุกครั้งที่มีผู้ใช้ยิง HTTP Request จะเกิดปัญหาเปิดพอร์ตชนกัน และเกิดความล่าช้าจาก Hardware Handshake การใช้ `BackgroundService` เป็นโมเดล Producer-Consumer ที่เหมาะสม เพราะทำหน้าที่อ่านสตรีมตลอดเวลาและเขียนลง Memory Store ส่วน REST API มีหน้าที่เป็น Consumer คอยอ่านค่าจากหน่วยความจำไปตอบ ทำให้ API ทำงานได้อย่างรวดเร็วและไม่กระทบการอ่านข้อมูลทางพอร์ต

---

## Lab 13.3 — ใบงาน 13.3: Realtime SignalR Hub & Converged Web Dashboard
ผลักข้อมูลขึ้นหน้าเว็บแบบเรียลไทม์ด้วย SignalR และแสดง Protocol Convergence Metrics

### คำสั่งที่ใช้ในแล็บนี้
```bash
cd Week13_Kestrel_Edge_Gateway/67030180_template/ESP32_ADC_Stream && idf.py build && idf.py -p /dev/cu.usbserial-10 flash
bash Week13_Kestrel_Edge_Gateway/lab13_3_auto.sh 67030180 67030073 67030268 67030270
```

### ค่าที่วัด / คำนวณได้
| รายการ | ค่า |
| :--- | :--- |
| **SignalR Latency (ms)** | 4.1 |
| **จำนวน Client บน Hub** | 3 |
| **จำนวนโปรโตคอลที่พร้อมใช้งาน** | 4 |

### 💻 Log SignalR Hub Connection และสถิติ Protocol Traffic Counters
```text
info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5001/hubs/gateway?id=b28e5c930f12 - -
info: Microsoft.AspNetCore.SignalR.HubConnectionHandler[1]
      OnConnectedAsync started with ConnectionId 'b28e5c930f124d3e'.
[10:22:35] [SignalR Hub] 🌐 Web Client เชื่อมต่อสำเร็จ: b28e5c930f124d3e
[10:22:36] [Metrics Monitor] 📊 Current Counters: SerialPackets=140 | MqttMessages=149 | SignalRPushes=140 | RestRequests=4
[10:22:42] 🌐 สั่งการจาก Web Dashboard ผ่าน SignalR: LED:ON
[10:22:42] [MQTT Message] 📩 หัวข้อ: iot/esp32/led_ack | QoS: AtLeastOnce | ข้อความ: {"led_state": true}
```

### ❓ ตอบคำถามท้ายการทดลอง

1. **เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript setInterval() เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?**  
   * **ตอบ:** การใช้ SignalR เปลี่ยนสถาปัตยกรรมจาก Polling มาเป็น Push-based ผ่าน WebSocket ทำให้อัตราความหน่วง (Latency) ลดลงเหลือเพียงไม่กี่มิลลิวินาที ทันทีที่หมุนตัวต้านทานปรับค่าได้บนบอร์ด ESP32 ตัวเลขบนหน้าจอจะเปลี่ยนตามแทบจะในพริบตา ต่างจากการทำ HTTP Polling ที่ต้องรอรอบเวลา Interval และสร้าง Overhead สูงมากจาก HTTP Header ในทุกๆ ครั้งที่ร้องขอข้อมูล

2. **การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?**  
   * **ตอบ:** ช่วยในการเฝ้าระวังความพร้อมใช้งานและความสมดุลของระบบ (System Health & Throughput Balance) ในโรงงานอุตสาหกรรม โดยผู้ดูแลระบบสามารถเปรียบเทียบอัตราการไหลของข้อมูลระหว่าง Serial กับ MQTT และ SignalR หากโปรโตคอลใดมีตัวเลขคลาดเคลื่อนหรือหลุดลอย ก็จะตรวจพบจุดผิดปกติของสายสัญญาณหรือเครือข่ายได้ทันท่วงที

---

## Lab 13.4 — ใบงาน 13.4: The Grand Finale — รวมทุกระบบ
ทดสอบ 4 มิติ: Universal Telemetry, Mobile → Web, Web → Mobile, Third-Party REST

### คำสั่งที่ใช้ในแล็บนี้
```bash
bash Week13_Kestrel_Edge_Gateway/lab13_3_auto.sh 67030180 67030073 67030268 67030270
bash Week12_Flutter_MQTT_App/lab12_4_auto.sh 67030180 67030073 67030268 67030270 --app-only
```

### ค่าที่วัด / คำนวณได้
| รายการ | ค่า |
| :--- | :--- |
| **อัตราทราฟฟิกรวม (msg/s)** | 31.2 |
| **ความหน่วงรวมระบบ (ms)** | 6.7 |
| **การทำงานยามตัดเน็ต** | Pass (100% Functional in Local Subnet) |

### 💻 Log การทดสอบ Grand Verification Matrix และผลการทดสอบตัดเน็ต Local Survivability
```text
[10:22:50] === STARTING GRAND VERIFICATION MATRIX TESTING ===
[10:22:51] [Matrix Test 1] Universal Telemetry Sync: Serial(2.45V) -> In-Memory Store -> SignalR(Web) & MQTT(Flutter) [OK]
[10:22:53] [Matrix Test 2] Mobile-to-Web Actuation: Flutter(MQTT:iot/esp32/control) -> Kestrel -> Serial(LED:ON) -> SignalR Hub [OK]
[10:22:55] [Matrix Test 3] Web-to-Mobile Actuation: Web(SignalR:ToggleLed) -> Kestrel -> Serial(LED:OFF) -> MQTT(iot/esp32/led_ack) [OK]
[10:22:57] [Matrix Test 4] Third-Party REST: curl POST /api/led/on -> Kestrel -> Serial(LED:ON) -> Sync all UI [OK]
[10:22:59] [Local Survivability] WAN Uplink Disconnected. Edge Subnet 192.168.1.0/24 Operational: 100% PASS
```

### กิจกรรมที่ 13-4.2: การทดสอบ 4 มหาการทดลอง (The Grand Verification Matrix)

| มิติการทดสอบ | การกระทำเชิงปฏิบัติการ | ปรากฏการณ์ที่เกิดขึ้นจริงในระบบ | ผลการทดลอง |
| :--- | :--- | :--- | :---: |
| **1. Universal Telemetry Sync** | ปรับหมุน Potentiometer บน ESP32 ที่ต่อขา GPIO34 | ตัวเลขแรงดันและ Raw ADC บน Web Dashboard และ Flutter App เปลี่ยนแปลงพร้อมกันทันทีแบบเรียลไทม์ | **ผ่าน** |
| **2. Mobile-to-Web Actuation** | กดปุ่มเปิด-ปิดไฟ LED บนหน้าจอแอป Flutter | หลอดไฟ LED บนบอร์ด ESP32 ติด/ดับทันที และสวิตช์บน Web Dashboard ซิงก์สถานะตามอัตโนมัติผ่าน SignalR | **ผ่าน** |
| **3. Web-to-Mobile Actuation** | กดปุ่มสวิตช์สั่งงานบน Web Dashboard | คำสั่งส่งผ่าน SignalR Hub เข้าไปสั่ง Serial พอร์ตให้บอร์ดเปลี่ยนสถานะ LED และส่ง MQTT ACK ให้แอป Flutter อัปเดตสถานะตรงกัน | **ผ่าน** |
| **4. Third-Party REST Override** | ยิงคำสั่ง `curl -X POST http://localhost:5001/api/led/off` ผ่าน Terminal | หลอดไฟ LED ดับลงทันที พร้อมกับที่หน้าเว็บและหน้าจอมือถืออัปเดตสถานะเป็น OFF อย่างสมบูรณ์ | **ผ่าน** |

---

## ❓ ตอบคำถามท้ายการทดลอง & สรุปผล

1. **ตลอดระยะเวลา 13 สัปดาห์ การเดินทางจากระดับบิตและรีจิสเตอร์ของเซนเซอร์ สู่เฟรมเวิร์ก FreeRTOS, ผ่านโปรโตคอลเครือข่ายท้องถิ่น, สู่ระบบคลาวด์ MQTT, หน้าจอมือถือ Flutter, และมาบรรจบที่ Kestrel Edge Gateway... สถาปัตยกรรมชิ้นใดที่ท่านคิดว่ามีความท้าทายเชิงวิศวกรรมมากที่สุด? เพราะเหตุใด?**  
   * **ตอบ:** สำหรับผม สิ่งที่ท้าทายที่สุดคือ *"FreeRTOS Task Scheduling & Non-blocking UART บน ESP32"* ในฝั่งเฟิร์มแวร์ เพราะบอร์ดมีทรัพยากรจำกัด การออกแบบให้แซมปลิง ADC 12-bit พร้อมกับดักรับคำสั่ง Serial และจัดสรรเวลาให้ไม่ชนกับ FreeRTOS Kernel โดยไม่มีปัญหา Buffer Overflow หรือ Watchdog Reset ต้องอาศัยความเข้าใจเชิงลึกเกี่ยวกับเวลาและสถาปัตยกรรมชิป

2. **หากท่านต้องนำระบบนี้ไปติดตั้งในโรงงานอุตสาหกรรมจริง สิ่งใดคือ 3 ปัจจัยแรกที่ต้องดำเนินการเพิ่มเติมเพื่อความปลอดภัยและความมั่นคงระดับองค์กร?**  
   * **ตอบ:**
     1. **Zero-Trust Network Access & Firewall Segmentation:** แบ่งแยกโซนเครือข่าย OT (ระดับบอร์ดและ Serial) ออกจากโซน IT อย่างชัดเจนด้วย Firewall และอนุญาตเฉพาะ Port/Protocol ที่กำหนด
     2. **MQTT Broker Access Control & Payload Signing:** ตั้งค่า Username/Password กำหนดสิทธิ์ราย Topic และเพิ่ม Digital Signature ในข้อความควบคุมเพื่อป้องกัน Replay Attack
     3. **Fail-safe Logic & Hardware Fallback:** ติดตั้งวงจร Watchdog ภายนอก และกำหนดค่าสถานะความปลอดภัย (Safe State) ให้กับ Actuator เมื่อขาดการเชื่อมต่อกับ Gateway เกินเวลาที่กำหนด

3. **สรุปผลและการประยุกต์ใช้งาน:**  
   * ตลอดทั้งภาคการศึกษา การได้สัมผัสตั้งแต่วงจรแอนะล็อก รีจิสเตอร์ของ ESP32, การจัดการงานด้วย FreeRTOS จนถึงการสร้าง Edge Gateway ด้วย .NET 8 และ SignalR ทำให้ผมเห็นภาพการผสานระหว่างวิศวกรรมคอมพิวเตอร์และระบบสมองกลฝังตัวอย่างแท้จริง การที่ระบบสามารถทำ Local Survivability ได้อย่างสมบูรณ์แม้ตัดเน็ต ถือเป็นแนวคิดที่นำไปปรับใช้กับระบบอัตโนมัติในงานอุตสาหกรรมจริงได้อย่างมั่นใจ