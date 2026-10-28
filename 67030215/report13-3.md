<img width="993" height="296" alt="image" src="https://github.com/user-attachments/assets/f6a66f4b-b612-4e9d-bae9-afd21ba2b0a4" />

รันเซิร์ฟเวอร์ด้วย `dotnet run` และเปิดเบราว์เซอร์ไปที่ `http://localhost:5000`:
* คุณจะพบหน้าจอศูนย์บัญชาการที่ตัวเลขแรงดันไฟฟ้าวิ่งสดๆ ตามการหมุนตัวต้านทานบนบอร์ด!
* ตัวเลขนับทราฟฟิกของทั้ง Serial, MQTT และ SignalR จะวิ่งขึ้นพร้อมๆ กันในเสี้ยววินาที!
* เมื่อคลิกปุ่มเปิดไฟบนหน้าเว็บ หลอดไฟ LED จริงบนบอร์ด ESP32 จะสว่างขึ้นทันที!


# ผลการทดลองกลุ่มร่วมกับWuthichai
### ไฟล์ที่สร้างและแก้ไข

**Hubs.cs**
```
สร้างใหม่ ตามใบงาน (GatewayHub และ ProtocolMetrics)
```

**Services.cs**
```
เพิ่ม IHubContext<GatewayHub> และ ProtocolMetrics ใน SerialBridgeWorker ส่ง ReceiveTelemetry ทุกครั้งที่ได้ข้อมูล และคงบรรทัด _store.Set("latest_telemetry", ...) ไว้ (โค้ดในใบงาน 13.3 ไม่มีบรรทัดนี้ ทำให้ /api/telemetry/latest ตอบ 404)
```

**wwwroot/index.html**
```
สร้างใหม่ ตามใบงาน
```

**Program.cs**
```
เพิ่ม AddSignalR(), AddSingleton<ProtocolMetrics>(), UseDefaultFiles(), UseStaticFiles(), MapHub<GatewayHub>("/hubs/gateway") และลบ app.MapGet("/", ...) จากใบงาน 13.1 ออก เพราะ Endpoint นี้ทำให้หน้าแรกตอบเป็นข้อความแทน index.html
```

### กิจกรรม 13-3.4 เปิดแดชบอร์ด `http://localhost:5000`

**สถานะการเชื่อมต่อ**
```
● SignalR Online
```

**ค่าแรงดัน**
```
แสดงค่าสดจาก ESP32 เช่น 1.94 V, Raw ADC: 2402 / 4095 เปลี่ยนตามการหมุน Potentiometer โดยไม่ต้องรีเฟรชหน้า
```

**Protocol Convergence Metrics**
```
Serial, MQTT และ SignalR เพิ่มขึ้นพร้อมกันและเท่ากันเสมอ (113/113/113, 300/300/300, 698/698/698) เพราะ 1 แพ็กเก็ตจาก Serial ถูกส่งต่อเข้า MQTT 1 ครั้งและขึ้นเว็บ 1 ครั้ง
```
<img width="991" height="340" alt="image" src="https://github.com/user-attachments/assets/edac3d5a-956c-4f26-9b51-7d731661e789" />
<img width="991" height="298" alt="image" src="https://github.com/user-attachments/assets/36f73ed3-73d9-4a2d-8a87-315beba1b13b" />
<img width="991" height="285" alt="image" src="https://github.com/user-attachments/assets/ac53823b-d2ca-44bd-beb1-f0cdc8f60e67" />
<img width="771" height="258" alt="image" src="https://github.com/user-attachments/assets/620b795c-8637-4ceb-af64-1a1091c24925" />

<img width="960" height="1706" alt="image" src="https://github.com/user-attachments/assets/c63d2396-f01b-48c2-a312-9da61884d5f1" />

<img width="907" height="86" alt="image" src="https://github.com/user-attachments/assets/635e37a9-a16b-423c-a72a-4ecd317142c1" />

<img width="1917" height="656" alt="image" src="https://github.com/user-attachments/assets/6e3f0bea-cdcc-4b30-bfbf-ebf7bc3f7e86" />

<img width="960" height="1706" alt="image" src="https://github.com/user-attachments/assets/23bb86ef-c326-4300-9649-d64969e9bfdb" />
### ข้อสังเกต

- **สถานะปุ่มไม่ซิงก์กับช่องทางอื่น:** มีแค่ `ToggleLed` ใน Hub ที่แจ้ง `LedStateUpdated` ถ้าสั่งไฟผ่าน REST API ไฟบนบอร์ดจะติด แต่หน้าเว็บยังโชว์ OFF
- **ปุ่มเปลี่ยนทันทีที่กด (Optimistic Update):** JavaScript เปลี่ยนปุ่มก่อนรู้ว่าบอร์ดรับคำสั่งจริงไหม และ `ToggleLed` ไม่ตรวจค่าที่ `SendSerialCommand` คืนมา ถ้าสาย USB หลุด ปุ่มก็ยังขึ้น ON
- **ตัวนับ REST ไม่ถูกใช้:** `ProtocolMetrics` มีฟิลด์ `RestRequests` แต่ไม่มีโค้ดเพิ่มค่า และหน้าเว็บไม่ได้แสดง
- **โหลด SignalR จาก CDN:** ถ้าไม่มีอินเทอร์เน็ต หน้าเว็บจะเชื่อมต่อไม่ได้ แม้ Gateway อยู่เครื่องเดียวกัน

ใบงาน 13.4 จะแก้ให้ทุกช่องทางอัปเดตสถานะจาก ACK ที่ ESP32 ส่งกลับหลังเปลี่ยน GPIO จริง

### ปัญหาที่พบและการแก้ไข

**`Serial Read Warning: The operation was canceled.` ตอนกด Ctrl+C**
```
สาเหตุ: Worker กำลังรออ่าน COM3 อยู่ แล้วถูกสั่งหยุด
วิธีแก้: ไม่ใช่ข้อผิดพลาด
```

**เปิด `localhost:5000` แล้วเห็นข้อความ RUNNING แทนแดชบอร์ด (ถ้าไม่ลบ)**
```
สาเหตุ: app.MapGet("/") จากใบงาน 13.1 รับ Request หน้าแรกก่อน Static Files
วิธีแก้: ลบ MapGet("/") ออก
```

**`/api/telemetry/latest` ตอบ 404 ถ้าใช้โค้ดตามใบงาน**
```
สาเหตุ: HandleIncomingSerialLine ในใบงาน 13.3 ไม่มี _store.Set("latest_telemetry")
วิธีแก้: เพิ่มบรรทัดนี้กลับเข้าไป
```
---

## 4. คำถามท้ายการทดลอง
1. เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript `setInterval()` เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?
```
SignalR เป็นแบบ Push เซิร์ฟเวอร์ส่งข้อมูลมาทันทีที่มีค่าใหม่ผ่านการเชื่อมต่อที่เปิดค้างไว้ (WebSocket) หน้าเว็บจึงอัปเดตแทบทันที ส่วน Polling ต้องรอรอบ 500 ms ก่อนถึงจะเห็นค่าใหม่ จึงมีความหน่วงเสมอ และถ้าค่าเปลี่ยนระหว่างรอบก็อาจพลาดไปเลย
Polling ยังส่ง HTTP Request ซ้ำทุกรอบแม้ไม่มีอะไรเปลี่ยน ต้องแบก Header ทุกครั้ง เปลืองแบนด์วิดท์และทรัพยากรเซิร์ฟเวอร์ ยิ่งมีหลายผู้ใช้โหลดยิ่งหนัก ขณะที่ SignalR ส่งเฉพาะตอนมีข้อมูลจริง
นอกจากนี้ SignalR ส่งข้อมูลจากเซิร์ฟเวอร์ไปทุกหน้าจอที่เปิดอยู่พร้อมกันได้ เช่น สถานะไฟ LED เปลี่ยนที่หน้าหนึ่งอีกหน้าก็เปลี่ยนตาม และมีการเชื่อมต่อใหม่อัตโนมัติเมื่อสัญญาณหลุด
```

2. การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?
```
Metrics นี้นับจำนวนแพ็กเก็ตที่ผ่านแต่ละช่องทาง (Serial, MQTT, SignalR) เทียบกัน ในสภาวะปกติตัวเลขต้องเท่ากันเสมอ เพราะ 1 แพ็กเก็ตจาก Serial ถูกส่งต่อเข้า MQTT 1 ครั้งและขึ้นเว็บ 1 ครั้ง
ถ้าตัวเลขไม่เท่ากัน ก็รู้ทันทีว่ามีข้อมูลหายระหว่างทาง และรู้ด้วยว่าหายที่ช่วงไหน เช่น Serial เพิ่มแต่ MQTT ไม่เพิ่ม แปลว่าปัญหาอยู่ที่การเชื่อมต่อ Broker ส่วน MQTT เพิ่มแต่ SignalR ไม่เพิ่ม แปลว่าปัญหาอยู่ที่ฝั่งเว็บ
ในโรงงานที่มีอุปกรณ์จำนวนมาก วิธีนี้ช่วยให้วิศวกรเห็นสุขภาพของระบบได้ในมุมเดียวโดยไม่ต้องไล่ดู Log ทีละส่วน ตรวจจับปัญหาได้ตั้งแต่เนิ่นๆ ก่อนที่ข้อมูลสำคัญจะขาดหายโดยไม่มีใครรู้ และลดเวลาในการหาสาเหตุเมื่อระบบขัดข้อง
```
