# 06-Labsheet-13-1-Embedded-MQTT-Broker-and-Client-with-MQTTnet

---

## 3. ขั้นตอนการทดลอง (Step-by-Step Activities)

### กิจกรรมที่ 13-1.3: การสั่งรันและทดสอบด้วยเครื่องมือ CLI

#### 2. เปิดหน้าต่าง PowerShell ที่สอง: ทดสอบ Subscribe หัวข้อ

<img width="1273" height="316" alt="ภาพถ่ายหน้าจอ 2569-10-26 เวลา 09 44 23" src="https://github.com/user-attachments/assets/58d182ff-a961-4ef5-901f-7887107d177c" />

#### 4. สังเกตผลลัพธ์

<img width="1512" height="982" alt="ภาพถ่ายหน้าจอ 2569-10-26 เวลา 10 00 00" src="https://github.com/user-attachments/assets/38492de3-0af7-4cfa-97a0-8d3d34e8de4c" />

---

## 4. คำถามท้ายการทดลอง (Review Questions)
1. การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?

```
Deployment: Broker ถูกเปิดในโปรเจกต์เดียวกันด้วย AddMqttServer และ listenOptions.UseMqtt() บนพอร์ต 1883 ใน Program.cs จึงรัน dotnet run ครั้งเดียวได้ทั้ง Broker, REST และ SignalR ไม่ต้องติดตั้งหรือตั้งค่า mosquitto.conf แยก และเวอร์ชันของ Broker ผูกกับแพ็กเกจ MQTTnet 4.3.7 ในโปรเจกต์ ย้ายเครื่องแล้วส่งไปชุดเดียว
หน่วยความจำ: SerialBridgeWorker ส่งข้อความเข้า Broker ด้วย _mqttServer.InjectApplicationMessage ในโปรเซสเดียวกัน ไม่ต้องเปิด TCP connection ไปหา Broker อีกตัว และใช้ GatewayStateStore กับ ProtocolMetrics ร่วมกับโค้ดอื่นได้ตรงๆ
ข้อเสีย: Gateway กลายเป็นจุดเดียวที่ล้มได้ และ Mosquitto โตเต็มที่กว่าในด้านฟีเจอร์และความเสถียร นอกจากนี้ Broker ในแล็บตั้งแบบ WithDefaultEndpoint() ไม่มี username/password และไม่มี TLS
```

2. ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?

```
InterceptingPublishAsync ทำงานทุกครั้งที่มี Client publish เข้ามา ก่อนที่ Broker จะกระจายข้อความให้ subscriber จึงอ่าน args.ApplicationMessage.Topic และ ConvertPayloadToString() มาตรวจได้ เช่น ตรวจว่า payload ของ iot/esp32/telemetry เป็น JSON ที่มี voltage กับ raw และ voltage อยู่ในช่วง 0 ถึง 3.3 V ถ้าไม่ผ่านให้ตั้ง args.ProcessPublish = false เพื่อทิ้งข้อความนั้นก่อนถึง Flutter หรือเว็บ
```

---
---

# 07-Labsheet-13-2-Hardware-Serial-to-MQTT-and-REST-Bridge

---

## 3. ขั้นตอนการทดลอง (Step-by-Step Activities)

### กิจกรรมที่ 13-2.5: การทดสอบสะพานเชื่อมต่อ

2. ทดสอบเรียกดูค่าเซนเซอร์ผ่านเบราว์เซอร์หรือ cURL:

<img width="1512" height="601" alt="ภาพถ่ายหน้าจอ 2569-10-26 เวลา 10 39 44" src="https://github.com/user-attachments/assets/54b02c78-e918-4e6f-b584-9c7739258efe" />

3. เปิด `mosquitto_sub`:

<img width="1512" height="982" alt="ภาพถ่ายหน้าจอ 2569-10-26 เวลา 10 40 54" src="https://github.com/user-attachments/assets/87996f34-8edd-4739-8f53-58a8e63cf0c2" />

4. ทดสอบสั่งเปิด-ปิดไฟผ่าน REST API:

<img width="802" height="471" alt="ภาพถ่ายหน้าจอ 2569-10-26 เวลา 10 42 12" src="https://github.com/user-attachments/assets/7f422e70-8950-44d1-9575-d750055fcfef" />

<img width="549" height="728" alt="image" src="https://github.com/user-attachments/assets/e231c197-29de-4fd6-a0d2-bb6779613694" />

---

## 4. คำถามท้ายการทดลอง
1. สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?

```
อุปกรณ์รุ่นเก่าที่ไม่มี Wi-Fi ส่งข้อมูลทางสาย Serial ได้อย่างเดียว แต่เมื่อต่อเข้ากับ Gateway ข้อมูลที่อ่านได้จะถูก publish ไป MQTT Broker ทำให้แอปมือถือและเว็บแดชบอร์ดดูค่าได้จากเครือข่ายโดยไม่ต้องเปลี่ยนอุปกรณ์ และสั่งงานกลับได้ เช่น เปิด-ปิดไฟผ่าน SendSerialCommand อุปกรณ์เดิมจึงเชื่อมต่อและใช้งานแบบออนไลน์ร่วมกับระบบสมัยใหม่ได้ผ่าน Gateway ตัวเดียว
```

2. เพราะเหตุใดเราจึงต้องใช้ `BackgroundService` ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?

```
พอร์ต Serial ต้องเปิดค้างและอ่านต่อเนื่อง แต่ REST จบเป็นรายคำขอ ถ้าเปิดทุกคำขอจะพลาดข้อมูลระหว่างคำขอ และเปิดพอร์ตซ้อนกันไม่ได้ BackgroundService เปิดพอร์ตครั้งเดียวในลูป ExecuteAsync ลองเปิดใหม่ทุก 3 วินาทีด้วย Task.Delay(3000) เมื่อพอร์ตหลุด และให้ API ใช้ Worker ตัวเดียวกันผ่าน SendSerialCommand ตอนทดลองบน macOS เมื่อ idf monitor เปิดพอร์ตซ้อนกับ Kestrel ข้อมูลถูกแย่งกันอ่านและ /api/telemetry/latest ตอบ No telemetry yet
```

---
---

# 08-Labsheet-13-3-Realtime-SignalR-Hub-and-Converged-Web-Dashboard

---

## 3. ขั้นตอนการทดลอง (Step-by-Step Activities)

### กิจกรรมที่ 13-3.4: ผูก Static Files และ SignalR ใน `Program.cs`

https://github.com/user-attachments/assets/827c8688-a890-49ee-b0f0-ca8744edad5b

---

## 4. คำถามท้ายการทดลอง
1. เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript `setInterval()` เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?

```
SignalR ให้เซิร์ฟเวอร์ผลักข้อมูลมาหาหน้าเว็บทันทีที่มีข้อมูลใหม่ โดย SerialBridgeWorker เรียก _hubContext.Clients.All.SendAsync("ReceiveTelemetry", ...) ทุกครั้งที่อ่านค่าจากบอร์ดได้ ค่าบนหน้าเว็บจึงเปลี่ยนตามบอร์ดแทบทันที ส่วน Polling ต้องยิง GET ซ้ำทุก 500 ms แม้ไม่มีอะไรเปลี่ยน และข้อมูลอาจช้าได้สูงสุดเท่าคาบที่ตั้งไว้ นอกจากนี้ SignalR ใช้การเชื่อมต่อถาวรแบบ WebSockets และ Hub กระจาย LedStateUpdated ให้ทุกหน้าพร้อมกัน ทำให้สถานะไฟบนเว็บตรงกับที่สั่งจากแอปหรือ REST ทันที
```

2. การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?

```
ตัวนับใน ProtocolMetrics ที่แสดงบนแดชบอร์ดช่วยบอกว่าข้อมูลหยุดที่ขั้นไหนของสายส่ง เช่น ถ้า Serial Packets หยุดเพิ่มก็สงสัยบอร์ดหรือสายเชื่อมต่อ แต่ถ้า Serial เพิ่มต่อแล้ว SignalR ไม่เพิ่มก็สงสัยฝั่งส่งออกของ Gateway ช่วยจำกัดจุดที่ต้องตรวจให้แคบลงและเห็นปัญหาได้ก่อนที่ผู้ใช้จะแจ้ง ข้อควรระวังจากโค้ด: ตัวนับเพิ่มเฉพาะใน HandleIncomingSerialLine ฟิลด์ RestRequests ยังไม่ได้ผูกให้นับ และตัวนับรีเซ็ตเป็น 0 ทุกครั้งที่รีสตาร์ท
```

---
---

# 09-Labsheet-13-4-The-Grand-Finale-All-in-One-IoT-System-Integration

---

## 3. ขั้นตอนการทดลอง (Step-by-Step Activities)

### กิจกรรมที่ 13-4.2: การทดสอบ 4 มหาการทดลอง (The Grand Verification Matrix)

| มิติการทดสอบ | การกระทำเชิงปฏิบัติการ | ปรากฏการณ์ที่เกิดขึ้นจริงในระบบ | ผลการทดลอง (ผ่าน/ไม่ผ่าน) |
| :--- | :--- | :--- | :--- |
| **1. Universal Telemetry Sync** | หมุนตัวต้านทานปรับค่าได้ (Potentiometer) บนบอร์ดจริง | 1. ค่าแรงดันวิ่งผ่านสาย Serial เข้า Kestrel<br/>2. ตัวเลขบนเว็บแดชบอร์ดวิ่งสดทันที<br/>3. เข็ม Radial Gauge บน Flutter หมุนตามทันทีพร้อมกัน | ผ่าน|
| **2. Mobile-to-Web Actuation** | แตะสวิตช์เปิดไฟบนแอป Flutter (ส่งผ่าน MQTT) | 1. หลอดไฟ LED จริงบนบอร์ด ESP32 ติดสว่างขึ้น<br/>2. ปุ่มสวิตช์บนหน้าเว็บเปลี่ยนเป็นสีเขียว "ON" อัตโนมัติ |ผ่าน |
| **3. Web-to-Mobile Actuation** | คลิกปุ่มปิดไฟบนหน้าเว็บ (ส่งผ่าน SignalR) | 1. หลอดไฟ LED จริงบนบอร์ด ESP32 ดับลงทันที<br/>2. สวิตช์บนแอปมือถือ Flutter สลับเป็น "OFF" ทันที |ผ่าน |
| **4. Third-Party REST Override** | ยิงคำสั่งผ่าน cURL หรือ Postman:<br/>`curl -X POST http://localhost:5000/api/led/on` | 1. หลอดไฟ LED สว่างขึ้น<br/>2. ทั้งหน้าเว็บและแอปมือถืออัปเดตสถานะเป็น "ON" พร้อมกัน | ผ่าน|

https://github.com/user-attachments/assets/4ff884ed-3631-49e0-b7d2-491da32c6342

---

### กิจกรรมที่ 13-4.3: การทดสอบความทนทานในสภาวะตัดเน็ต (Local Survivability Challenge)

2. สังเกตการทำงาน:
   * บอร์ด ESP32 ยังคงส่งข้อมูลได้หรือไม่?
  ```
    ได้ ESP32 ส่งค่าผ่านสาย USB-UART เข้าคอมพิวเตอร์โดยตรง ไม่ใช้อินเทอร์เน็ต
  ```
   * แอปมือถือ Flutter และเว็บแดชบอร์ดยังสามารถเปิดดูค่าและสั่งงานหลอดไฟได้หรือไม่?
   ```
    ได้ทั้งสองอย่าง แอป Flutter ต่อ MQTT Broker ใน Kestrel ผ่าน LAN ส่วนเว็บใช้ SignalR และ REST บน Kestrel ในเครื่อง ทุกอย่างไม่พึ่งอินเทอร์เน็ต
   ```

---

## 4. บทสรุปและคำถามส่งท้ายภาคการศึกษา (Capstone Retrospective)

1. ตลอดระยะเวลา 13 สัปดาห์ การเดินทางจากระดับบิตและรีจิสเตอร์ของเซนเซอร์ สู่เฟรมเวิร์ก FreeRTOS, ผ่านโปรโตคอลเครือข่ายท้องถิ่น, สู่ระบบคลาวด์ MQTT, หน้าจอมือถือ Flutter, และมาบรรจบที่ Kestrel Edge Gateway... สถาปัตยกรรมชิ้นใดที่ท่านคิดว่ามีความท้าทายเชิงวิศวกรรมมากที่สุด? เพราะเหตุใด?

```
ส่วนที่ท้าทายที่สุดคือ Kestrel Edge Gateway ที่ต้องรวม 4 โปรโตคอล (Serial, MQTT, REST, SignalR) ให้สถานะตรงกันตลอด เพราะแต่ละช่องทางสั่งไฟได้และมีจังหวะต่างกัน ในงานนี้ต้องรวมทางสั่งไฟไว้ที่ SetLedAsync จุดเดียว เพื่อให้ทุกครั้งส่งเข้า Serial, publish led_ack ให้ Flutter และผลัก LedStateUpdated ให้เว็บพร้อมกัน นอกจากนี้ยังเจอปัญหาจังหวะเวลา เช่น Serial เปิดสำเร็จก่อน MQTT Broker พร้อม ทำให้ status ที่ publish ครั้งแรกหายและแอปขึ้น OFFLINE จนต้องรอ _mqttServer.IsStarted และใช้ retain กับ status/led_ack รวมถึงปัญหา Serial Port ที่ idf monitor เปิดซ้อนกับ Kestrel ทำให้แย่งอ่านข้อมูล
```

2. หากท่านต้องนำระบบนี้ไปติดตั้งในโรงงานอุตสาหกรรมจริง สิ่งใดคือ 3 ปัจจัยแรกที่ต้องดำเนินการเพิ่มเติมเพื่อความปลอดภัยและความมั่นคงระดับองค์กร?
```
1. การยืนยันตัวตนและการเข้ารหัส ตอนนี้ Broker ตั้งแบบ WithDefaultEndpoint() ใครต่อเข้า 1883 ก็ publish ไปที่ iot/esp32/control สั่งไฟได้ และ POST /api/led/{state} กับ SignalR Hub ก็ไม่มีการล็อกอิน ต้องเพิ่ม username/password หรือใบรับรอง กำหนดสิทธิ์ต่อ topic และใช้ TLS ทั้ง MQTT, REST และ SignalR
2. ความทนทานของ Gateway: Gateway เป็นจุดเดียวที่ล้มได้ และสถานะอยู่ในหน่วยความจำ หายเมื่อรีสตาร์ท ต้องรันเป็น service ที่ restart อัตโนมัติ มีเครื่องสำรองหรือ watchdog และเก็บข้อมูลลงฐานข้อมูลหรือคิวเพื่อบัฟเฟอร์เมื่อปลายทางล่ม
3. การตรวจสอบข้อมูลและการเฝ้าระวัง: ตอนนี้ InterceptingPublishAsync แค่พิมพ์ log ยังไม่ได้ตรวจค่าที่ผิดปกติ และ HandleIncomingSerialLine ข้ามข้อมูลเสียเงียบๆ ต้องเพิ่ม Edge Validation บันทึก log ถาวร และตั้งการแจ้งเตือนจากตัวนับใน ProtocolMetrics เมื่อข้อมูลหยุดไหล
```
