# Week 13 – สมุดบันทึกการทดลอง (Answer)

**ชื่อ-สกุล:** ______________________ **รหัสนักศึกษา:** 67030098

| ใบงาน | หัวข้อ | ไฟล์บันทึก | โค้ดที่ใช้ | สถานะ |
| :--- | :--- | :--- | :--- | :--- |
| 13.1 | Embedded MQTT Broker และ Client ภายใน Kestrel (MQTTnet) | [Lab13-1.md](Lab13-1.md) | `Code/Lab13-1` | ✅ ทดลองแล้ว |
| 13.2 | Hardware Serial → MQTT และ REST Bridge | [Lab13-2.md](Lab13-2.md) | `Code/Lab13-2` | ✅ ทดลองแล้ว (บอร์ดจริง, ค่า ADC สุ่ม) |
| 13.3 | SignalR Hub และ Converged Web Dashboard | [Lab13-3.md](Lab13-3.md) | `Code/Lab13-3` | ✅ ทดลองแล้ว (บอร์ดจริง, ตัวต้านทานปรับค่าได้จริง) |
| 13.4 | Grand Finale: ESP32 + Kestrel + Flutter + Web | Lab13-4.md | `Code/Lab13-4` | ⏳ |

## วิธีใช้สมุดนี้

- แต่ละไฟล์มี: สภาพแวดล้อม → ขั้นตอน → ผลการทดลอง (รูป + ตาราง) → คำตอบคำถามท้ายการทดลอง
- รูปอยู่ในโฟลเดอร์ `Answer/Image/` แนบเฉพาะรูปที่ใบงานให้สังเกตผล
- รหัสผ่าน MQTT และ API key ไม่ถูกเขียนลงในสมุดนี้ ในรูปจะเห็นเป็น `$env:MQTT_PASS` แทน

## เตรียมเครื่องก่อนทดลอง (ทุกหน้าต่าง PowerShell)

```powershell
cd C:\IOT\Week-13-Kestrel-Edge-Gateway-and-Protocol-Integration
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
. .\Code\env.ps1
$env:MQTT_PASS = Read-Host "MQTT password"
```
