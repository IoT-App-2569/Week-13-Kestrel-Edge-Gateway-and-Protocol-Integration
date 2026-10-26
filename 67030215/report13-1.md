#### 3. เปิดหน้าต่าง PowerShell ที่สาม: ทดสอบ Publish ข้อความ
ส่งข้อความผ่าน `mosquitto_pub`:
```powershell
mosquitto_pub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!" -i "pub_client"
```

#### 4. สังเกตผลลัพธ์
<img width="1028" height="67" alt="image" src="https://github.com/user-attachments/assets/5d9fe5fa-864b-4f5a-8d22-26198d761947" />
<img width="1038" height="75" alt="image" src="https://github.com/user-attachments/assets/58f67060-94e4-402d-9b7c-44d66e086cdc" />

* หน้าต่าง Kestrel จะพิมพ์ Log สีฟ้าว่าดักจับข้อความได้ถูกต้อง
* หน้าต่าง `mosquitto_sub` จะได้รับข้อความที่ถูกส่งต่อผ่าน Broker สำเร็จทันที

---

## 4. คำถามท้ายการทดลอง (Review Questions)
1. การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้งซอฟต์แวร์ Mosquitto แยกต่างหากอย่างไรในแง่ของการกระจายงาน (Deployment) และการประมวลผลข้อมูลในหน่วยความจำ?
   
   **ตอบ:** ด้าน Deployment การฝัง Broker ไว้ในแอป ASP.NET Core ทำให้ deploy แค่ตัวเดียว ไม่ต้องติดตั้งหรือดูแล Mosquitto เพิ่ม ถ้าใช้ Docker ก็ใช้ container เดียวจบ และ config ก็รวมอยู่ในโค้ดเดียวกัน ส่วนด้านหน่วยความจำ ข้อความที่เข้ามาอยู่ใน process เดียวกับแอปเลย เข้าถึงข้อมูลได้ตรงๆ ผ่าน event โดยไม่ต้องสร้าง MQTT Client อีกตัวมา subscribe จาก Broker ภายนอก จึงลด network hop และ overhead ของการ serialize/deserialize ทำให้ latency ต่ำกว่าและนำข้อมูลไปประมวลผลต่อได้ทันที แต่ข้อเสียคือถ้าแอปล่ม Broker ก็ล่มไปด้วย

2. ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?

   **ตอบ:** ฟังก์ชันนี้จะทำงานทุกครั้งที่มี Client publish ข้อความเข้ามา และก่อนที่ Broker จะส่งต่อให้ subscriber เราจึงตรวจ payload ได้ก่อน เช่น แปลง JSON แล้วเช็คว่าค่าอุณหภูมิอยู่ในช่วงที่สมเหตุสมผลหรือไม่ ถ้าข้อมูลผิดปกติให้ตั้งค่า `ProcessPublish = false` เพื่อหยุดไม่ให้ข้อความถูกส่งต่อ (และ log ไว้ดูภายหลังได้) แต่ถ้าปกติก็ปล่อยผ่านตามเดิม วิธีนี้ช่วยกันค่า sensor ที่เพี้ยนไม่ให้ไปถึง Client อื่นหรือฐานข้อมูลตั้งแต่ต้นทาง

   ```csharp
   server.InterceptingPublishAsync += e =>
   {
       var payload = e.ApplicationMessage.ConvertPayloadToString();
       if (!IsValid(payload))
       {
           e.ProcessPublish = false; // ทิ้งข้อความที่ผิดปกติ
       }
       return Task.CompletedTask;
   };
   ```
