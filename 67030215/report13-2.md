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

## ผลการทดลอง

### การตั้งค่า

| รายการ | ค่า |
|:---|:---|
| Serial Port | COM3, 115200 bps |
| แพ็กเกจเพิ่ม | System.IO.Ports 8.0.0 |
| เฟิร์มแวร์ ESP32 | ESP-IDF v6.0.2, ADC GPIO 34 (ADC1_CH6), LED GPIO 2, ส่ง Telemetry ทุก 500 ms |
| รูปแบบข้อมูลจาก ESP32 | `{"voltage":1.65,"raw":2048,"uptime":10}` |

**ปรับจากใบงาน**
- เปลี่ยน `_portName` เป็น `"COM3"` ตามเครื่อง
- เฟิร์มแวร์เพิ่มฟิลด์ `uptime` และส่ง `{"led_state":true,"ack":true}` กลับทุกครั้งที่เปลี่ยนสถานะไฟ (เตรียมไว้ใช้ในใบงาน 13.4) บรรทัด ACK ไม่มีฟิลด์ `voltage` จึงถูกข้ามโดย `catch` ของ `HandleIncomingSerialLine`
- เฟิร์มแวร์ตรวจคำสั่งจาก Serial ทุก 50 ms แทน 500 ms ไฟตอบสนองเร็วขึ้น
- ใน `Program.cs` เก็บ Log การเชื่อมต่อ MQTT จากใบงาน 13.1 ไว้ แต่ไม่พิมพ์ทุกข้อความ เพราะ Telemetry เข้ามา 2 ครั้งต่อวินาที

### กิจกรรม 13-2.4 ESP32 ส่ง JSON ทาง Serial

หมุน Potentiometer จากฝั่งหนึ่งไปอีกฝั่ง ค่าเปลี่ยนจาก `0.00 V / 0` ถึง `3.30 V / 4095`

<img width="935" height="327" alt="image" src="https://github.com/user-attachments/assets/5814dc60-fa69-4b70-856a-197d1f0ce4f2" />

<img width="918" height="325" alt="image" src="https://github.com/user-attachments/assets/ea18220b-b363-4537-bf8d-15b489fe18f9" />


### กิจกรรม 13-2.5 การทดสอบสะพานเชื่อมต่อ

| ข้อ | การทดสอบ | ผล |
|:---|:---|:---|
| 1 | `dotnet run` | `✅ เชื่อมต่อฮาร์ดแวร์ Serial Port COM3 สำเร็จ!` |
| 2 | `GET /api/telemetry/latest` | ได้ `{"voltage":3.3,"raw":4095,"timestamp":1793095754}` หมุน Pot แล้วรีเฟรชได้ `{"voltage":0,"raw":0,"timestamp":1793095804}` |
| 3 | `mosquitto_sub -t "iot/esp32/telemetry"` | ได้ข้อมูล 2 ข้อความต่อวินาที ค่าเปลี่ยนตามการหมุน Pot `3.30 → 3.07 → 1.47 → 0.59 → 0.00` |
| 4 | `POST /api/led/on` และ `/off` | ได้ `"sentToSerial":true` ไฟ LED ที่ GPIO 2 ติดและดับตามคำสั่ง |

<img width="790" height="240" alt="image" src="https://github.com/user-attachments/assets/1219a197-c91d-4fe8-ae39-092c6c857e69" />


<img width="475" height="221" alt="image" src="https://github.com/user-attachments/assets/bc8058d9-3410-4df3-97c0-4318037b0d82" />
<img width="578" height="193" alt="image" src="https://github.com/user-attachments/assets/1a577b2e-eade-4c29-b9b8-4aa693ee62a9" />


<img width="865" height="112" alt="image" src="https://github.com/user-attachments/assets/8191fa4a-17b5-4399-9adc-184b721168b6" />


<img width="1222" height="540" alt="image" src="https://github.com/user-attachments/assets/0f9719d1-3a36-4b28-ab1b-81ce94d6b53d" />


<img width="935" height="162" alt="image" src="https://github.com/user-attachments/assets/5eb36083-0590-477d-bf2b-2b89ef4c1bec" />


<img width="1108" height="1477" alt="image" src="https://github.com/user-attachments/assets/21c87bd9-9210-4c22-8d65-811fd12e1a0e" />
<img width="960" height="1706" alt="image" src="https://github.com/user-attachments/assets/48d37d65-f83c-41a0-ba0e-d061d6518cf3" />


### ปัญหาที่พบและการแก้ไข

| ปัญหา | สาเหตุ | วิธีแก้ |
|:---|:---|:---|
| `idf.py : The term 'idf.py' is not recognized` | เปิด Terminal ธรรมดาใน VS Code | เปิด ESP-IDF Terminal (Ctrl+Shift+P → ESP-IDF: Open ESP-IDF Terminal) ต้องมี `(venv)` นำหน้า |
| ค่า ADC เป็น 0 ตลอดตอนเริ่ม | Potentiometer หมุนค้างไว้ที่ฝั่ง GND | หมุน Pot ไปอีกฝั่ง ค่าขึ้นถึง 4095 |
| Kestrel แสดง `sub_client` เชื่อมต่อและตัดการเชื่อมต่อสลับกันไม่หยุด และได้ข้อมูลแค่ 1 ข้อความต่อวินาทีแทน 2 | `mosquitto_sub` จากใบงาน 13.1 ยังเปิดค้างอยู่ และใช้ Client ID `sub_client` ซ้ำกับตัวใหม่ Broker จึงตัดตัวเก่าทุกครั้งที่ตัวใหม่เชื่อมต่อ (Session Takeover) ข้อความในช่วงที่ถูกตัดหายไป | ปิดหน้าต่างเก่า และใช้ Client ID ที่ไม่ซ้ำ `sub_telemetry` ได้ครบ 2 ข้อความต่อวินาที |
| ใน Windows PowerShell คำสั่ง `curl` ไม่ใช่โปรแกรม curl จริง | `curl` เป็นชื่อเล่นของ `Invoke-WebRequest` | ใช้ `curl.exe` แทน |

(วางภาพ Terminal ที่ sub_client เชื่อมต่อ-ตัดสลับกัน)
---

## 4. คำถามท้ายการทดลอง
1. สถาปัตยกรรมแบบ Serial-to-MQTT Bridge นี้ช่วยให้อุปกรณ์รุ่นเก่า (Legacy Hardware) ที่ไม่มี Wi-Fi สามารถเข้าร่วมระบบนิเวศ IoT สมัยใหม่ได้อย่างไร?
```
อุปกรณ์รุ่นเก่า เช่น PLC เครื่องวัดในโรงงาน มิเตอร์ไฟฟ้า หรือไมโครคอนโทรลเลอร์ที่ไม่มี Wi-Fi ส่วนใหญ่สื่อสารได้แค่ทาง Serial (UART, RS-232, RS-485) ซึ่งเป็นการเชื่อมต่อแบบจุดต่อจุดกับเครื่องเดียว ไม่มี IP Address ไม่รู้จัก TCP/IP และไม่รู้จัก MQTT

Serial-to-MQTT Bridge ทำหน้าที่เป็นล่าม
- ขาขึ้น: อ่านข้อมูลดิบจากสาย Serial แล้วแปลงเป็น MQTT Topic (iot/esp32/telemetry) และ REST API (/api/telemetry/latest) ในการทดลองนี้ ESP32 ไม่ได้ใช้ Wi-Fi เลย แต่ mosquitto_sub และเบราว์เซอร์ยังได้ข้อมูลเหมือนกับอุปกรณ์ที่ต่อเครือข่ายจริง
- ขาลง: รับคำสั่งจากเครือข่าย (POST /api/led/on) แล้วแปลงเป็นคำสั่ง Serial (LED:ON) ส่งกลับไปที่อุปกรณ์

ประโยชน์
- ไม่ต้องแก้หรือเปลี่ยนอุปกรณ์เดิม อุปกรณ์ยังพูดภาษาเดิม คือแค่พิมพ์ข้อความออกสาย Serial ส่วนการแปลงเป็นหน้าที่ของ Gateway ช่วยประหยัดค่าใช้จ่ายและไม่ต้องหยุดเครื่องจักร
- ฝั่งผู้ใช้ข้อมูล (แอป Flutter, แดชบอร์ด, คลาวด์) Subscribe หัวข้อเดิมได้เลย ไม่รู้ด้วยซ้ำว่าข้อมูลมาจากสาย Serial ในการทดลองข้อมูลที่ได้มีรูปแบบเหมือนกับที่ ESP32 ส่งผ่าน Wi-Fi ในสัปดาห์ 12 ทุกอย่าง
- จากข้อมูล 1 แหล่ง กระจายได้หลายโปรโตคอลพร้อมกัน ผู้ใช้หลายรายอ่านพร้อมกันได้ ต่างจาก Serial ที่เปิดได้ทีละโปรแกรม
- ความปลอดภัยรวมศูนย์ที่ Gateway เพิ่ม TLS หรือการยืนยันตัวตนที่ Gateway ได้ อุปกรณ์เก่าที่ไม่มีกำลังพอทำ TLS ก็ไม่ต้องทำ

ข้อจำกัดคืออุปกรณ์ต้องอยู่ใกล้ Gateway ตามความยาวสาย (USB ไม่กี่เมตร, RS-485 ได้ถึงประมาณ 1 กม.) และถ้า Gateway ล่ม อุปกรณ์ที่ต่ออยู่ก็ขาดจากระบบทั้งหมด
```
2. เพราะเหตุใดเราจึงต้องใช้ `BackgroundService` ในการอ่านข้อมูล Serial แทนที่จะเปิดพอร์ตอ่านตรงๆ ภายในฟังก์ชันของ REST API?
```
1. Serial ต้องถูกอ่านตลอดเวลา ไม่ใช่เฉพาะตอนมีคนเรียก
ESP32 ส่งข้อมูลทุก 500 ms ไม่ว่าจะมีใครเรียก API หรือไม่ ถ้าไม่มีคนอ่าน ข้อมูลจะค้างในบัฟเฟอร์ของพอร์ตจนเต็มแล้วถูกทิ้ง BackgroundService วนอ่านตลอด จึงเก็บค่าล่าสุดไว้ใน GatewayStateStore และส่งต่อเข้า MQTT ได้แบบเรียลไทม์ ในการทดลอง mosquitto_sub ได้ข้อมูลต่อเนื่องโดยไม่มีใครเรียก REST API เลย

2. พอร์ต Serial เปิดได้ทีละโปรแกรม
บน Windows พอร์ต COM เป็นแบบ Exclusive Access (เห็นได้จากที่ต้องปิด idf monitor ก่อนรัน Gateway) ถ้าเปิดพอร์ตในฟังก์ชัน REST API แล้วมี 2 คำขอเข้ามาพร้อมกัน คำขอที่สองจะเปิดพอร์ตไม่ได้และล้มเหลว การให้ Worker เป็นเจ้าของพอร์ตเพียงตัวเดียวตลอดอายุโปรแกรม แล้วให้ส่วนอื่นเรียกผ่าน SendSerialCommand จึงไม่มีการแย่งกัน

3. API ตอบเร็ว
ถ้าเปิดพอร์ตตอนมีคำขอ ต้องรอเปิดพอร์ต รอข้อมูลบรรทัดถัดไปอาจนานถึง 500 ms แล้วปิดพอร์ต ทุกครั้งที่เรียก การเปิด-ปิดพอร์ตบน ESP32 บางรุ่นยังทำให้บอร์ดรีเซ็ตเพราะสัญญาณ DTR/RTS เปลี่ยน แต่แบบ BackgroundService ค่าถูกอ่านไว้แล้ว API แค่หยิบจากหน่วยความจำมาตอบได้ทันที

4. จัดการความผิดพลาดและเชื่อมต่อใหม่ได้เอง
Worker มีลูปตรวจว่าพอร์ตยังเปิดอยู่ไหม ถ้าถอดสายหรือยังไม่เสียบบอร์ดจะรอ 3 วินาทีแล้วลองใหม่ ระหว่างนั้น REST API และ MQTT Broker ยังทำงานต่อ ไม่ล่มตาม

5. แยกหน้าที่ชัดเจน
Worker ทำหน้าที่รับข้อมูลจากฮาร์ดแวร์ (Producer) ส่วน REST API, MQTT และ SignalR เป็นผู้ใช้ข้อมูล (Consumer) อ่านจาก GatewayStateStore ร่วมกัน เพิ่มโปรโตคอลใหม่ได้โดยไม่ต้องแก้ส่วนที่อ่าน Serial
```
