# รายงานผลการทดลอง: กิจกรรมที่ 13-1.3 การสั่งรันและทดสอบด้วยเครื่องมือ CLI

## 1. วัตถุประสงค์การทดลอง
เพื่อทดสอบการทำงานของ **Kestrel Edge Gateway** ร่วมกับ **Embedded MQTT Broker** บนโปรโตคอล HTTP (Port 5000) และ MQTT (Port 1883) โดยใช้เครื่องมือ Command Line Interface (CLI) ได้แก่ `mosquitto_sub` และ `mosquitto_pub` ในการรับ-ส่งข้อมูล

---

## 2. ขั้นตอนและผลการทดลอง

### ขั้นตอนที่ 1: การสั่งรัน Kestrel Edge Gateway
ทำการรันโปรเจกต์ด้วยคำสั่ง:
```bash
dotnet run
```
<img width="1007" height="327" alt="image" src="https://github.com/user-attachments/assets/9db72d00-e7a4-4c64-9d59-0a3317e6188b" />

### ปิดหน้าต่าง PowerShell ที่สอง: ทดสอบ Subscribe หัวข้อ
ทใช้คำสั่ง mosquitto_sub เพื่อดักฟังข้อความจาก Embedded Broker ของเรา (ระบุพารามิเตอร์ -i เพื่อกำหนด Client ID):
```bash
mosquitto_sub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -i "sub_client" -v
```

<img width="1013" height="161" alt="image" src="https://github.com/user-attachments/assets/57b67b9d-9961-41ff-a6e2-79ad8c1cf5f2" />

### เปิดหน้าต่าง PowerShell ที่สาม: ทดสอบ Publish ข้อความ
ส่งข้อความผ่าน mosquitto_pub:
```bash
mosquitto_pub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!" -i "pub_client"
```

### 1. ข้อได้เปรียบของ Embedded MQTT Broker บน Kestrel
* **Deployment:** รวมระบบเป็นไฟล์เดียว (Single Binary/Container) ติดตั้งลงอุปกรณ์ Edge ได้ทันที ไม่ต้องรัน Mosquitto แยกต่างหาก
* **In-Memory:** ข้อมูลส่งตรงเข้ากระบวนการประมวลผลใน RAM ได้ทันที โดยไม่ต้องผ่าน Loopback TCP ช่วยลด Latency และประหยัด

### 2. การใช้ `InterceptingPublishAsync` ทำ Edge Validation
* **ดักจับและกรอง:** เช็ก Payload ก่อนส่งต่อ หากค่าเซ็นเซอร์หลุดช่วง (Out-of-range) หรือฟอร์แมตผิด สามารถสั่ง `ProcessPublish = false` เพื่อดรอปข้อความทิ้งได้ทันที
* **ปรับแต่งข้อมูล:** สามารถตัดกรองข้อมูลขยะ หรือเติม Timestamp และ Metadata ก่อนส่งต่อไปยัง Client อื่น
