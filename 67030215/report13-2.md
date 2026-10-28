กิจกรรมที่ 13-2.5: การทดสอบสะพานเชื่อมต่อ
[!WARNING] ข้อพึงระวังเรื่องพอร์ตสื่อสารอนุกรม (Serial Port Exclusive Lock):

ตรวจสอบหมายเลข COM Port ของบอร์ด ESP32 ผ่าน Device Manager แล้วแก้ไขตัวแปร _portName ในไฟล์ Services.cs ให้ตรงกับเครื่องของคุณ (เช่น COM24)
ต้องปิด idf monitor, PuTTY หรือ Serial Monitor อื่นๆ ก่อนรัน Kestrel เสมอ เพราะบน Windows พอร์ต COM จะถูกล็อกใช้งานแบบ Exclusive Access (เปิดได้เพียง 1 โปรแกรมในเวลาเดียวกัน) หากมีโปรแกรมอื่นเปิดพอร์ตค้างไว้ C# จะเปิดพอร์ตไม่สำเร็จและส่งคำสั่งไม่ได้ (sentToSerial: false)
เสียบบอร์ด ESP32 เข้ากับเครื่อง และสั่งรัน Kestrel:
dotnet run
ทดสอบเรียกดูค่าเซนเซอร์ผ่านเบราว์เซอร์หรือ cURL:
curl http://localhost:5000/api/telemetry/latest
สังเกตว่าจะได้ข้อมูล JSON ของแรงดันไฟฟ้าทันที! 3. เปิด mosquitto_sub:

mosquitto_sub -h 127.0.0.1 -p 1883 -t "iot/esp32/telemetry" -i "sub_client" -v
สังเกตว่าข้อมูลที่อ่านจากสาย USB Serial ถูกส่งต่อเข้าหัวข้อ MQTT อัตโนมัติ! 4. ทดสอบสั่งเปิด-ปิดไฟผ่าน REST API:

curl -X POST http://localhost:5000/api/led/on
สังเกตว่าคำสั่ง LED:ON ถูกส่งผ่านสาย USB เข้าไปควบคุมบอร์ดทันที!

4. คำถามท้ายการทดลอง
สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?
เพราะเหตุใดเราจึงต้องใช้ BackgroundService ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?
