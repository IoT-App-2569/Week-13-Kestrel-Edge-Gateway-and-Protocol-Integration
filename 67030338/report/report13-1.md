# ใบงานการทดลองที่ 13.1
### การสร้าง Embedded MQTT Broker และ Client ภายใน Kestrel .NET 8 ด้วยไลบรารี MQTTnet

---

## ผลการทดลอง

### 1. การรัน Kestrel Edge Gateway พร้อม Embedded MQTTnet Broker (พอร์ต 5000 และ 1883)
<!-- แคปรูปหน้าต่าง Terminal ขณะรัน dotnet run แสดงพอร์ต 5000/1883 และ Log เมื่อมี Client เชื่อมต่อ/รับข้อความ -->
![ผลการรัน Kestrel Embedded MQTT Broker](image-13-1-1.png)

### 2. การทดสอบ Subscribe และ Publish ข้อความด้วย `mosquitto_sub` และ `mosquitto_pub`
<!-- แคปรูปหน้าต่าง Terminal ของ mosquitto_sub และ mosquitto_pub ที่รับ-ส่งข้อความผ่านหัวข้อ test/edge/topic -->
![ผลการทดสอบ mosquitto_sub และ mosquitto_pub](image-13-1-2.png)

### 3. การทดสอบเข้าถึง REST Endpoint เบื้องต้น (`http://localhost:5000`)
<!-- แคปรูปหน้าเว็บเบราว์เซอร์หรือคำสั่ง curl ที่เปิด http://localhost:5000 -->
![ผลการทดสอบ REST Endpoint เบื้องต้น](image-13-1-3.png)

---

## 4. คำถามท้ายการทดลอง (Review Questions)

### ข้อ 1: การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?
* **คำตอบ:**
  1. **ความได้เปรียบด้านการกระจายงานและติดตั้งระบบ (Single-Binary Deployment & Zero External Dependency):**
     * **ลดความซับซ้อนในการติดตั้งและดูแลรักษา:** การฝัง MQTT Broker (`MQTTnet`) เข้าไปในโปรเจกต์ ASP.NET Core Kestrel ทำให้ทั้ง Web Server, REST API, Background Worker และ MQTT Broker ถูกรวมอยู่ในแอปพลิเคชันเดียว (Single Process / Single Deployment Unit) สามารถคอมไพล์และเผยแพร่เป็นไฟล์ไบนารีเดียวหรือคอนเทนเนอร์เดียวได้ทันที โดยไม่ต้องติดตั้ง ตั้งค่าไฟล์ `mosquitto.conf` หรือคอยจัดการ System Service ของซอฟต์แวร์ Mosquitto แยกต่างหากบนเครื่องปลายทาง (เช่น Raspberry Pi หรือ Industrial PC)
     * **บริหารจัดการวงจรชีวิต (Lifecycle Management) ร่วมกัน:** เมื่อสั่งรันเกตเวย์ด้วย `dotnet run` หรือรันเป็นบริการของระบบ ทั้งพอร์ต HTTP (5000) และพอร์ต MQTT TCP (1883) จะเปิดทำงานและปิดการทำงานพร้อมกันอย่างเป็นเอกภาพ
  2. **ความได้เปรียบด้านการประมวลผลข้อมูลในหน่วยความจำ (In-Memory Processing & Zero Inter-Process Overhead):**
     * **เข้าถึงข้อมูลโดยตรงใน Process เดียวกัน (In-Process Memory Sharing):** หากใช้ Mosquitto แยกต่างหาก แอปพลิเคชัน C# จะต้องสร้างการเชื่อมต่อ TCP Socket ย้อนกลับไปหา Mosquitto ในฐานะ MQTT Client ทำให้เกิดความหน่วงจากการแปลงข้อมูลผ่านเครือข่าย (Loopback TCP/IP Stack Serialization) แต่การรัน Embedded Broker ภายใน Kestrel ทำให้ C# สามารถดักจับข้อความผ่าน Event Pipeline (`InterceptingPublishAsync`) หรือฉีดข้อความเข้า Broker โดยตรงผ่านเมธอด `InjectApplicationMessage()` ในหน่วยความจำ (RAM) เดียวกันได้ทันที
     * **ลดความหน่วงเวลา (Ultra-Low Latency) และแชร์ State ร่วมกันได้ทันที:** ข้อมูลที่รับเข้ามาทาง MQTT สามารถนำไปอัปเดตลง `GatewayStateStore` (`ConcurrentDictionary`) หรือผลักต่อไปยัง WebSockets (`SignalR Hub`) ได้โดยตรงในระดับ Sub-millisecond โดยไม่ต้องเสียเวลาส่งข้ามโปรเซส (Inter-Process Communication: IPC)

---

### ข้อ 2: ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?
* **คำตอบ:**
  * **กลไกการทำงานของ `InterceptingPublishAsync`:**
    * `InterceptingPublishAsync` ทำหน้าที่เปรียบเสมือน **Middleware หรือด่านตรวจสกัดกั้นข้อความขาเข้า (Inbound Message Interceptor)** ที่ทำงานทุกครั้งเมื่อมีอุปกรณ์ Publish ข้อความเข้ามาที่ Broker ก่อนที่ Broker จะกระจาย (Route/Forward) ข้อความนั้นไปยัง Subscriber รายอื่นๆ
  * **แนวทางการประยุกต์ใช้ทำ Edge Validation (การตรวจสอบและกรองข้อมูลที่ขอบเครือข่าย):**
    1. **การตรวจสอบรูปแบบและโครงสร้างข้อมูล (Schema & Payload Validation):** ดึงข้อมูล Payload ออกมาตรวจสอบว่าเป็น JSON ที่ถูกต้องตามโครงสร้างหรือไม่ หากเป็นข้อความขยะ (Malformed JSON) หรือมีฟิลด์ขาดหาย สามารถตั้งค่า `args.ProcessPublish = false;` เพื่อสั่งให้ Broker **ระงับการส่งต่อข้อความนั้นทันที** ไม่ให้ไปทำลายการทำงานของแอปมือถือหรือแดชบอร์ดที่ Subscribe อยู่
    2. **การกรองค่าผิดปกติทางกายภาพ (Out-of-Range / Anomaly Filtering):** ตรวจสอบค่าตัวเลขเซนเซอร์ว่าอยู่ในช่วงที่เป็นไปได้ทางฟิสิกส์หรือไม่ เช่น ค่า ADC 12-bit ต้องอยู่ระหว่าง `0–4095` หรือแรงดันไฟฟ้าต้องอยู่ในช่วง `0.00V – 3.30V` หากพบค่ากระโดดผิดปกติจากสัญญาณรบกวน (Spike/Noise เช่น `-999` หรือ `99.9V`) สามารถบล็อกข้อความ (`args.ProcessPublish = false`) หรือปรับค่าให้อยู่ในขอบเขตที่ปลอดภัย (Clamping/Sanitization) ก่อนส่งต่อได้
    3. **การตรวจสอบสิทธิ์และป้องกันการสั่งงานแปลกปลอม (Topic & Command Authorization):** ตรวจสอบ `args.ClientId` และ `args.ApplicationMessage.Topic` ว่า Client ที่ส่งคำสั่งเข้าหัวข้อควบคุม (เช่น `iot/esp32/control`) มีสิทธิ์สั่งงานจริงหรือไม่ และคำสั่งอยู่ในรูปแบบที่อนุญาต (`ON` / `OFF`) เท่านั้น เพื่อป้องกันอุปกรณ์แปลกปลอมยิงคำสั่งอันตรายเข้าสู่ฮาร์ดแวร์
