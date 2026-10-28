### กิจกรรมที่ 13-2.4: โค้ดฝั่ง ESP32 สำหรับสื่อสารสองทางผ่าน Serial (`main.c`)

สำหรับฝั่งไมโครคอนโทรลเลอร์ ให้เขียนโค้ดในโปรเจกต์ ESP-IDF เพื่ออ่านค่าแอนะล็อกและสตรีม JSON ออกทาง Serial รวมถึงอ่านคำสั่งควบคุมหลอดไฟจาก C# Gateway
<img width="1024" height="358" alt="image" src="https://github.com/user-attachments/assets/ee07cce0-575b-40cd-959e-f9a152903a86" />

### กิจกรรมที่ 13-2.5: การทดสอบสะพานเชื่อมต่อ

> [!WARNING] **ข้อพึงระวังเรื่องพอร์ตสื่อสารอนุกรม (Serial Port Exclusive Lock):**
> 1. ตรวจสอบหมายเลข COM Port ของบอร์ด ESP32 ผ่าน Device Manager แล้วแก้ไขตัวแปร `_portName` ในไฟล์ `Services.cs` ให้ตรงกับเครื่องของคุณ (เช่น `COM24`)
> 2. **ต้องปิด `idf monitor`, PuTTY หรือ Serial Monitor อื่นๆ ก่อนรัน Kestrel เสมอ** เพราะบน Windows พอร์ต COM จะถูกล็อกใช้งานแบบ Exclusive Access (เปิดได้เพียง 1 โปรแกรมในเวลาเดียวกัน) หากมีโปรแกรมอื่นเปิดพอร์ตค้างไว้ C# จะเปิดพอร์ตไม่สำเร็จและส่งคำสั่งไม่ได้ (`sentToSerial: false`)

1. เสียบบอร์ด ESP32 เข้ากับเครื่อง และสั่งรัน Kestrel:
```powershell
dotnet run
```
2. ทดสอบเรียกดูค่าเซนเซอร์ผ่านเบราว์เซอร์หรือ cURL:
```powershell
curl http://localhost:5000/api/telemetry/latest
```
*สังเกตว่าจะได้ข้อมูล JSON ของแรงดันไฟฟ้าทันที!*
3. เปิด `mosquitto_sub`:
```powershell
mosquitto_sub -h 127.0.0.1 -p 1883 -t "iot/esp32/telemetry" -i "sub_client" -v
```
*สังเกตว่าข้อมูลที่อ่านจากสาย USB Serial ถูกส่งต่อเข้าหัวข้อ MQTT อัตโนมัติ!*
4. ทดสอบสั่งเปิด-ปิดไฟผ่าน REST API:
```powershell
curl -X POST http://localhost:5000/api/led/on
```
*สังเกตว่าคำสั่ง `LED:ON` ถูกส่งผ่านสาย USB เข้าไปควบคุมบอร์ดทันที!*
### ผลทดลอง
<img width="1024" height="311" alt="image" src="https://github.com/user-attachments/assets/9ac25a30-fc1b-4060-b62d-08fc301282a7" />
<img width="989" height="306" alt="image" src="https://github.com/user-attachments/assets/376d6c07-e670-4469-a6d0-c05e4e0a154c" />
<img width="1012" height="342" alt="ce4182f1-558c-4825-a9cb-089ffb96db49" src="https://github.com/user-attachments/assets/ed0ef2c8-ecbe-4821-a0ba-eaafe15d7081" />
<img width="1020" height="85" alt="image" src="https://github.com/user-attachments/assets/ba6d4804-1f27-4a88-ac7c-3d1dce9bf68d" />
<img width="424" height="150" alt="image" src="https://github.com/user-attachments/assets/15b92a2d-871e-428e-abe5-4eab415e518c" />
<img width="1024" height="152" alt="image" src="https://github.com/user-attachments/assets/9fdd93cd-d2e8-47d0-ad15-e0d17f912cab" />
<img width="742" height="1034" alt="Gemini_Generated_Image_rccxwzrccxwzrccx" src="https://github.com/user-attachments/assets/66c34bb8-cde4-44b1-b418-d549d9589c29" />

---

## 4. คำถามท้ายการทดลอง
1. สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?
```
อุปกรณ์รุ่นเก่ามักมีแค่พอร์ต Serial (UART/RS-232/RS-485) ส่งข้อมูลเป็นข้อความหรือไบต์ดิบ ไม่มี Wi-Fi และไม่รู้จัก MQTT
Bridge ทำหน้าที่เป็นตัวกลางที่ต่ออยู่กับพอร์ต Serial ของอุปกรณ์ อ่านข้อมูลเข้ามา แปลงเป็นข้อความ MQTT แล้วส่งขึ้น Broker ผ่านเครือข่าย ส่วนคำสั่งจากฝั่ง Cloud หรือแอปก็วิ่งย้อนกลับมาที่ Bridge แล้วถูกเขียนลงพอร์ต Serial ให้อุปกรณ์
ผลคือไม่ต้องแก้ฮาร์ดแวร์หรือเฟิร์มแวร์เดิมเลย แต่อุปกรณ์เก่าก็ส่งข้อมูลขึ้น Dashboard และรับคำสั่งแบบเดียวกับอุปกรณ์ IoT รุ่นใหม่ได้ ช่วยประหยัดค่าเปลี่ยนเครื่องและใช้งานอุปกรณ์เดิมต่อได้
```

2. เพราะเหตุใดเราจึงต้องใช้ `BackgroundService` ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันขอ
```
การอ่าน Serial ต้องทำต่อเนื่องตลอดเวลา เพราะข้อมูลจากอุปกรณ์เข้ามาเมื่อไรก็ได้ แต่ฟังก์ชันที่รับ Request ทำงานเป็นรอบ เริ่มเมื่อมีคนเรียกแล้วจบลง ถ้าอ่านพอร์ตในนั้นจะพลาดข้อมูลช่วงที่ไม่มีใครเรียก
ถ้าอ่านแบบวนรอบรอข้อมูลในฟังก์ชันนั้น จะบล็อก Thread ของเว็บเซิร์ฟเวอร์ ทำให้ Request อื่นค้างหรือหมดเวลา และเปิดพอร์ตซ้ำทุกครั้งที่ถูกเรียกก็เกิด Error เพราะพอร์ต Serial เปิดพร้อมกันหลายที่ไม่ได้
BackgroundService รันเป็นงานเบื้องหลังตลอดอายุของแอป เปิดพอร์ตครั้งเดียวตอนเริ่มระบบ อ่านและส่งต่อไป MQTT ได้ต่อเนื่อง แยกจากการรับ Request อื่นๆ และจัดการปิดพอร์ตอย่างถูกต้องตอนแอปหยุดทำงานได้
```
