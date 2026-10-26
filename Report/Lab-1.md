# รายงานผลการทดลองที่ 13.1
## Embedded MQTT Broker และ Client ภายใน Kestrel

## 1. สิ่งที่ต้องการและผลการทดลอง

### 1.1 สิ่งที่ต้องการ

- คอมไพล์และรันโปรเจกต์ด้วย `.NET 10.0`
- ให้ Kestrel เปิดพอร์ต HTTP `5000` และ MQTT `1883`
- ตรวจสอบการเชื่อมต่อของ MQTT Client
- ตรวจสอบการรับส่งข้อความระหว่าง `mosquitto_pub` และ `mosquitto_sub`
- ตรวจสอบการดักจับข้อความด้วย `InterceptingPublishAsync`
- ตรวจสอบ HTTP Endpoint สำหรับแสดงสถานะของระบบ

### 1.2  การคอมไพล์และเริ่มต้นระบบ

คำสั่งที่ใช้:

```powershell
dotnet restore Lab\Lab-1\ConvergedEdgeGateway.csproj
dotnet build Lab\Lab-1\ConvergedEdgeGateway.csproj --no-restore
dotnet run --project Lab\Lab-1\ConvergedEdgeGateway.csproj
```

ผลการ Build:

```text
ConvergedEdgeGateway -> ...\Lab\Lab-1\bin\Debug\net10.0\ConvergedEdgeGateway.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

ผล Log จาก Kestrel Server:

```text
Now listening on: http://[::]:5000
Now listening on: http://[::]:1883
Application started. Press Ctrl+C to shut down.
```

สรุป: โปรเจกต์คอมไพล์ด้วย `.NET 10.0` สำเร็จ และ Kestrel เปิดพอร์ต
HTTP `5000` กับ MQTT `1883` ได้สำเร็จ

### 1.3 การทดสอบ MQTT

#### หน้าต่างที่ 1 — Kestrel Server

Log เมื่อ Client เชื่อมต่อและมีการ Publish:

```text
[MQTT Server] 🟢 Client เชื่อมต่อ: sub_client (จาก Endpoint: ...)
[MQTT Server] 🟢 Client เชื่อมต่อ: pub_client (จาก Endpoint: ...)
[MQTT Message] 📩 หัวข้อ: test/edge/topic | QoS: ExactlyOnce/AtMostOnce | ข้อความ: Hello from CLI via Kestrel Embedded MQTT!
```

ผลที่ได้: Server ตรวจพบ Client ทั้งสองตัว และ `InterceptingPublishAsync`
สามารถดักจับ Topic, QoS และ Payload ของข้อความได้

#### หน้าต่างที่ 2 — Subscribe

คำสั่งที่ใช้:

```powershell
mosquitto_sub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -i "sub_client" -v
```

ผลลัพธ์:

```text
test/edge/topic Hello from CLI via Kestrel Embedded MQTT!
```

#### หน้าต่างที่ 3 — Publish

คำสั่งที่ใช้:

```powershell
mosquitto_pub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!" -i "pub_client"
```

ผลที่ได้: คำสั่ง Publish สำเร็จ และข้อความถูกส่งไปยัง `sub_client`
โดย Subscriber ได้รับข้อความตรงตาม Topic และ Payload ที่กำหนด

### 1.4  การทดสอบ HTTP Endpoint

คำสั่งที่ใช้:

```powershell
curl http://localhost:5000
```

ผลลัพธ์:

```text
🚀 Kestrel Converged Edge Gateway with Embedded MQTTnet Broker is RUNNING!
```

ผลการตรวจสอบ:

| รายการ | ผล |
|---|---|
| Target Framework | `.NET 10.0` |
| HTTP Endpoint | พอร์ต `5000` เปิดใช้งาน |
| MQTT Endpoint | พอร์ต `1883` เปิดใช้งาน |
| HTTP Status | `200 OK` |
| MQTT Publish/Subscribe | รับส่งข้อมูลสำเร็จ |
| Message Interception | ดักจับข้อความสำเร็จ |

## 2. คำถามและคำตอบท้ายการทดลอง

### คำถามที่ 1

การรัน Embedded MQTT Broker ภายใน Kestrel มีข้อได้เปรียบกว่าการติดตั้ง
Mosquitto แยกต่างหากอย่างไรในแง่ของ Deployment และ In-Memory Processing?

### คำตอบ

**ด้าน Deployment:** สามารถแจกจ่ายเป็น Single Artifact หรือ Container เดียว
ที่รวม Web Server และ MQTT Broker ไว้ด้วยกัน ไม่ต้องติดตั้งหรือดูแล Mosquitto
เป็น Service แยกต่างหาก ลด Dependency ภายนอก ลดขั้นตอนการคอนฟิก และลดภาระ
การดูแลรักษาในสภาพแวดล้อม Edge

**ด้าน In-Memory Processing:** ข้อความที่เข้ามาใน Broker สามารถส่งต่อไปยัง
`BackgroundService`, In-Memory Queue เช่น `Channel<T>`, ฐานข้อมูล หรือ WebSocket
ได้โดยตรงภายใน Process เดียวกันผ่านหน่วยความจำ RAM ไม่ต้องส่งผ่าน Network Stack
แบบ Loopback Socket ระหว่างโปรเซส จึงลด Latency และลดการใช้ทรัพยากร CPU
เหมาะกับงานที่ต้องประมวลผลข้อมูลใกล้แหล่งกำเนิด

ข้อควรพิจารณาคือ หาก Kestrel หยุดทำงานทั้ง HTTP และ MQTT จะหยุดพร้อมกัน
จึงต้องออกแบบเรื่องการจัดการทรัพยากร ความปลอดภัย และความทนทานให้เหมาะสม
สำหรับระบบ Production

### คำถามที่ 2

ฟังก์ชัน `InterceptingPublishAsync` ใน MQTTnet สามารถนำไปประยุกต์ใช้
ในการกรองข้อมูลที่ผิดปกติ (Edge Validation) ก่อนส่งต่อให้ Client อื่นได้อย่างไร?

### คำตอบ

สามารถอ่าน Payload แล้วพาร์สเป็น JSON เพื่อตรวจสอบความถูกต้องก่อนส่งต่อได้ เช่น

1. ตรวจว่าฟิลด์ข้อมูลครบและตรงตาม Schema
2. ตรวจช่วงค่าของ Sensor เช่น อุณหภูมิไม่เกินช่วงที่กำหนด
3. ตรวจชนิดข้อมูล Timestamp ขนาด Payload และสิทธิ์ของ Client
4. บันทึก Log หรือแจ้งเตือนเมื่อพบข้อมูลผิดปกติ
5. ตั้งค่า `args.ProcessPublish = false` เพื่อ Drop ข้อความที่ไม่ผ่าน
   การตรวจสอบ ทำให้ Broker ไม่ส่งต่อข้อความนั้นไปยัง Subscriber อื่น
6. Transform หรือ Enrich Payload แบบเรียลไทม์ก่อนส่งต่อ เมื่อข้อมูลอยู่ใน
   ขอบเขตที่ยอมรับได้

ดังนั้น `InterceptingPublishAsync` จึงสามารถทำหน้าที่เป็นด่านตรวจข้อมูล
ที่ Edge ก่อนเข้าสู่ระบบส่วนกลางหรือถูกส่งต่อไปยัง Client อื่นได้
