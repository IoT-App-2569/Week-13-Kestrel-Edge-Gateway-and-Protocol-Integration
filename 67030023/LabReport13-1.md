### Lab13-1

### กิจกรรมที่ 13-1.3: การสั่งรันและทดสอบด้วยเครื่องมือ CLI

#### 1. สั่งรัน Kestrel Edge Gateway
```powershell
dotnet run
```
*สังเกตข้อความบนคอนโซลว่าเซิร์ฟเวอร์กำลังฟังที่พอร์ต 5000 (HTTP) และ 1883 (MQTT)*

#### 2. เปิดหน้าต่าง PowerShell ที่สอง: ทดสอบ Subscribe หัวข้อ
ใช้คำสั่ง `mosquitto_sub` เพื่อดักฟังข้อความจาก Embedded Broker ของเรา (ระบุพารามิเตอร์ `-i` เพื่อกำหนด Client ID):
```powershell
mosquitto_sub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -i "sub_client" -v
```
> **ข้อสังเกต:** มาตรฐาน MQTT กำหนดให้ Client ต้องมี Client ID เสมอ สำหรับเครื่องมือ `mosquitto_sub` บน Windows หากไม่ระบุออปชัน `-i` มันจะส่ง Client ID ว่างเปล่ามา ซึ่งในโค้ดฝั่ง Broker เราได้ตั้งค่า `ValidatingConnectionAsync` ให้สุ่ม Client ID ให้โดยอัตโนมัติแล้ว

*สังเกตหน้าต่างคอนโซลของ Kestrel จะขึ้นข้อความสีเขียวว่ามี Client เชื่อมต่อเข้ามา!*

#### 3. เปิดหน้าต่าง PowerShell ที่สาม: ทดสอบ Publish ข้อความ
ส่งข้อความผ่าน `mosquitto_pub`:
```powershell
mosquitto_pub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!" -i "pub_client"
```

#### 4. สังเกตผลลัพธ์
* หน้าต่าง Kestrel จะพิมพ์ Log สีฟ้าว่าดักจับข้อความได้ถูกต้อง
* หน้าต่าง `mosquitto_sub` จะได้รับข้อความที่ถูกส่งต่อผ่าน Broker สำเร็จทันที

---

## 4. คำถามท้ายการทดลอง (Review Questions)
1. การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?
    Single Process & Single Deployment:
    ไม่จำเป็นต้องติดตั้ง จัดตั้งค่า (Configure) หรือดูแล Service ของ Broker แยกต่างหาก แอปพลิเคชันถูกแพ็กเป็น Single Executable หรือ Container เดียว รวมทั้ง Web Server, REST API และ MQTT Broker ไว้ด้วยกัน ทำให้การติดตั้งใช้งานบนอุปกรณ์ Edge (เช่น Raspberry Pi หรือ Industrial PC) ทำได้ง่ายและลดความผิดพลาดในการเตรียม Environment
    
    Zero Network Overhead & In-Memory Interception:
    การประมวลผลข้อความภายในเซิร์ฟเวอร์สามารถดักจับและส่งผ่านไปยัง Business Logic, Database หรือ Cache ภายใน RAM process เดียวกันได้ทันที โดยไม่ต้องส่งข้อความผ่าน Localhost Loopback socket หรือแปลงรูปแบบข้อมูลโปรโตคอลซ้ำหลายรอบ ช่วยลด latency และ CPU cost อย่างมีนัยสำคัญ

    Unified Security & Configuration Management:
    สามารถแชร์ AppSettings, Logging Framework (เช่น Serilog), Dependency Injection (DI) และระบบยืนยันตัวตน (Authentication/Authorization) ร่วมกับ ASP.NET Core ได้โดยตรง ไม่ต้องแยกจัดการไฟล์ตั้งค่า 2 ชุด

2. ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?
    InterceptingPublishAsync เป็น Event Hook ที่ทำงานก่อนที่ Broker จะกระจายข้อความ (Relay) ไปยัง Subscribed Clients รายอื่น

    การตรวจสอบโครงสร้างและขอบเขตข้อมูล (Data & Payload Validation):
    แปลง Payload เป็น JSON เพื่อเช็คว่าเซนเซอร์ส่งค่ามาถูกต้องตาม Schema หรือไม่ หากพบค่า outliers (เช่น เซนเซอร์อุณหภูมิส่งค่า 999°C) หรือ Format ผิดปกติ สามารถตั้งค่า args.ProcessPublish = false หรือกำหนด args.CloseConnection = true เพื่อยกเลิกการส่งต่อข้อความทันที
    
    การทำ Data Sanitization & Enforcement:
    สามารถแก้ไข (Mutate) เนื้อหาใน Payload เช่น การตัดช่องว่าง, การเติม Timestamp จาก Edge Server หรือการแนบ Metadata เข้าไปใน Message Payload ก่อนกระจายไปยัง Subscriber
    
    การป้องกันอันตรายทางไซเบอร์ (Edge Security Filtering):
    ตรวจสอบข้อความเพื่อป้องกัน Script Injection, Malicious Payload หรือการตรวจจับอุปกรณ์ที่ส่งข้อความถี่ผิดปกติ (Rate Limiting/DDoS Attack ในระดับ IoT) เพื่อตัดการเชื่อมต่อของ Client นั้นก่อนที่จะส่งผลกระทบต่อระบบหลัก