# สรุปเนื้อหาสัปดาห์ที่ 13 เพื่อเตรียมสอบ
## The Ultimate Converged Edge Gateway & System Integration (.NET 8 Kestrel)

> สรุปจากไฟล์ `Readme.md`, `01`–`05` (ทฤษฎี + Glossary) และ `06`–`09` (ใบงาน 13.1–13.4)

---

## 0. ภาพรวมใน 1 นาที (Cheat Sheet)

| หัวข้อ | ใจความสำคัญ |
| :--- | :--- |
| **ปัญหา** | อุปกรณ์หน้างานใช้โปรโตคอลต่างกัน (Serial, MQTT, REST, WebSocket) คุยกันตรงๆ ไม่ได้ → **Protocol Heterogeneity** |
| **ทางแก้** | สร้าง **Converged Multi-Protocol Edge Gateway** ตัวเดียว ทำหน้าที่เป็น Universal Protocol Bridge |
| **เทคโนโลยี** | .NET 8 + **Kestrel** + **MQTTnet** + **System.IO.Ports** + **SignalR** + Minimal APIs |
| **หัวใจของการทำงาน** | `BackgroundService` อ่าน Serial → เก็บใน `GatewayStateStore` → กระจายไป MQTT / REST / SignalR |
| **จุดเด่น** | **Local Survivability** (ทำงานได้แม้ไม่มีเน็ต) + **Store-and-Forward** |
| **ความทนทาน/ปลอดภัย** | Circuit Breaker, Rate Limiting, API Key |
| **ฮาร์ดแวร์** | ESP32: Potentiometer → **GPIO34 (ADC1_CH6)**, LED → **GPIO2**, Serial **115200 bps** |
| **พอร์ต** | HTTP/REST/SignalR = **5000**, MQTT = **1883** |

---

## 1. สถาปัตยกรรม Edge Computing และ Converged Gateway (บทที่ 13.1)

### 1.1 Cloud-Centric → Edge Computing
- **Cloud-Centric:** เซนเซอร์ส่งข้อมูลดิบขึ้นคลาวด์ตรง ง่ายตอนเริ่ม แต่เมื่อขยายเป็น Industrial IoT / Smart Facility จะมี **ข้อจำกัดรุนแรง 4 ประการ** (ในเอกสารแสดงเป็นรูป `Fig13.1-1`; โดยทั่วไปคือ ความหน่วง, แบนด์วิดท์/ค่าใช้จ่าย, ความเสถียรเมื่อเน็ตขาด, ความปลอดภัย/ความเป็นส่วนตัวของข้อมูล)
- **Edge Computing:** ย้ายการคัดกรองข้อมูล ตรรกะ และการควบคุมมาไว้ที่ **Edge Gateway** หน้างาน
- **สถาปัตยกรรม 3 ระดับ:** Cloud → Edge → Field Layer

### 1.2 ปัญหาความหลากหลายของโปรโตคอล (No One-Size-Fits-All)

| โปรโตคอล | จุดแข็ง | จุดอ่อน |
| :--- | :--- | :--- |
| **Hardware Serial (UART/RS-485)** | เสถียรสูงสุด ไม่ต้องพึ่งไร้สาย เหมาะระยะใกล้ | ส่งได้ทีละจุด (Point-to-Point) |
| **MQTT** | เหมาะกับไร้สายแบนด์วิดท์จำกัด, Publish/Subscribe | ต้องมี Broker ส่วนกลาง |
| **HTTP REST** | เป็นมาตรฐานสากล ทุกระบบ IT เข้าใจ | Overhead สูง ไม่เหมาะ Push เรียลไทม์ |
| **WebSockets / SignalR** | สตรีมขึ้นหน้าจอสองทางแบบสด | กินหน่วยความจำสูง ไม่เหมาะ MCU เล็ก |

### 1.3 นิยาม Converged Edge Gateway
คือคอมพิวเตอร์ระดับขอบ (Industrial PC, Raspberry Pi, เซิร์ฟเวอร์ประจำสถานี) ที่รันซอฟต์แวร์เข้าใจหลายโปรโตคอลพร้อมกัน มี **3 บทบาท**:
1. **Universal Protocol Bridge** – แปลง Serial → MQTT → WebSocket อัตโนมัติ
2. **Local Message Arbiter** – เป็น Embedded Broker ในตัว อุปกรณ์คุยกันได้โดยไม่ต้องมีอินเทอร์เน็ต
3. **Local Survivability Engine** – คุมตรรกะฉุกเฉิน บันทึกข้อมูลสำรอง แล้วส่งย้อนหลังเมื่อเน็ตกลับมา (**Store-and-Forward**)

### 1.4 ทำไมเลือก .NET 8 + Kestrel
1. **Performance** – ติดอันดับ TechEmpower Benchmarks รองรับมากกว่า **7 ล้านคำขอ/วินาที**
2. **True Cross-Platform** – คอมไพล์เป็น Single-file Native AOT รันบน Windows 11, Linux, ARM64 (Raspberry Pi 4/5)
3. **Multi-Threading สมบูรณ์** – `BackgroundService`, Task Parallel Library (TPL), `System.Threading.Channels` ไม่เกิด Race Condition

---

## 2. Kestrel Engine และ Worker Services (บทที่ 13.2)

### 2.1 Kestrel Internals
- เว็บเซิร์ฟเวอร์ **Asynchronous I/O** บน Managed Sockets ของ .NET แบ่งเป็น **3 เลเยอร์**
- **ปัญหา GC Pause:** ภาษาที่มี Garbage Collector จะหยุดชั่วขณะเพื่อเก็บขยะ
- **ทางแก้ของ Kestrel = Zero-Allocation** ด้วย **`System.IO.Pipelines`** + **`ArrayPool<byte>`**
  - อ่านข้อมูลลง Buffer ที่ยืมจาก Pool
  - ประมวลผลผ่าน `ReadOnlySpan<byte>` (ไม่ allocate บน Heap)
  - เสร็จแล้วคืน Buffer เข้า Pool ทันที → เสถียร ไม่กระตุกแม้รันหลายเดือน

### 2.2 `BackgroundService`
- ใช้กับงานที่ทำงานต่อเนื่องระยะยาว: เปิด Serial Port ค้างรอรับข้อมูล, ดูแล Embedded MQTT Broker, คำนวณสถิติ/เตือนเกินขีดจำกัด (Threshold Alarming)
- สืบทอดจาก **`IHostedService`** (`StartAsync` / `StopAsync`)
- Override เมธอด `ExecuteAsync(CancellationToken stoppingToken)` แล้ววนลูปตาม `!stoppingToken.IsCancellationRequested`

```csharp
public class SerialGatewayWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // ตรรกะดักฟังฮาร์ดแวร์
            await Task.Delay(100, stoppingToken);
        }
    }
}
```

### 2.3 `System.Threading.Channels` (Producer–Consumer)
- แก้ปัญหา **Race Condition** ที่เกิดจากการใช้ `List<T>` ธรรมดาข้ามเธรด
- เป็นคิว **Thread-Safe, ประสิทธิภาพสูง, ไม่ต้อง Lock**
- **Bounded Channel** จำกัดความจุ (เช่น 500) ป้องกันหน่วยความจำล้น
- `FullMode = BoundedChannelFullMode.DropOldest` → คิวเต็มให้ **ทิ้งค่าเก่าสุด**
- Producer: `Writer.WriteAsync(...)`  |  Consumer: `await foreach (... in Reader.ReadAllAsync(ct))`

### 2.4 `GatewayStateStore`
- ใช้ **`ConcurrentDictionary<string, object>`** เก็บสถานะล่าสุดในหน่วยความจำ
- ทำให้ REST API ตอบได้ในระดับ **Sub-millisecond**
- เมธอดหลัก: `SetState` (AddOrUpdate), `GetState<T>`, `GetAll`

---

## 3. Multi-Protocol Bridging (บทที่ 13.3)

### 3.1 Routing Matrix (4-Way Translation)

| แหล่งข้อมูล | → MQTT Broker | → Serial (UART) | → REST | → SignalR |
| :--- | :--- | :--- | :--- | :--- |
| **Hardware Serial** (ESP32) | Publish `iot/esp32/telemetry` | (รับตรง) | อัปเดตแคช State Store | เรียก `ReceiveTelemetry` |
| **REST API** | Publish `iot/esp32/control` | ยิง `LED:ON\n` | (ประมวลผลคำขอ) | อัปเดตสวิตช์บนเว็บ |
| **MQTT Client** (Flutter) | (รับผ่าน Broker) | ยิงคำสั่งเข้า ESP32 | อัปเดต State Store | อัปเดตสวิตช์บนเว็บ |
| **SignalR Web** | Publish `iot/esp32/control` | ยิงคำสั่งเข้า ESP32 | อัปเดต State Store | ส่งผลหา Client อื่น |

**Topic ที่ต้องจำ:** `iot/esp32/telemetry` (ข้อมูลเซนเซอร์) และ `iot/esp32/control` (คำสั่งควบคุม)

### 3.2 Embedded MQTT Broker ด้วย `MQTTnet` (v4.x)
- แพ็กเกจ: `MQTTnet` และ `MQTTnet.AspNetCore` (ใบงานใช้เวอร์ชัน **4.3.7.1207**)
- ขั้นตอนใน `Program.cs`:
  1. `ConfigureKestrel` → `ListenAnyIP(5000)` และ `ListenAnyIP(1883, l => l.UseMqtt())`
  2. `builder.Services.AddMqttServer(o => o.WithDefaultEndpoint())`
  3. `app.UseMqttServer(server => { ...events... })`
- **Event สำคัญ:**

| Event | หน้าที่ |
| :--- | :--- |
| `ValidatingConnectionAsync` | ตรวจสอบการเชื่อมต่อ; ถ้า ClientId ว่างให้ตั้ง `AssignedClientIdentifier` เป็น GUID กัน *identifier rejected* |
| `ClientConnectedAsync` | Client เชื่อมต่อเข้ามา |
| `ClientDisconnectedAsync` | Client ตัดการเชื่อมต่อ |
| `InterceptingPublishAsync` | ดักทุกข้อความที่ Publish **ก่อน**ส่งต่อให้ Subscriber |

- ฉีดข้อความจากภายในโค้ด: `mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(msg))`

### 3.3 Hardware Serial Bridge (`System.IO.Ports`)
- ใช้คลาส `SerialPort` (Port เช่น `COM3` / `/dev/ttyUSB0` บน Linux, Baud **115200**, `NewLine="\n"`, `ReadTimeout`)
- อ่านด้วย `ReadLine()` ใน `BackgroundService` → ดักจับ `TimeoutException` เป็นเรื่องปกติ
- รูปแบบข้อมูลจาก ESP32: `{"voltage":1.65,"raw":2048}`
- ส่งคำสั่งกลับ: `SendSerialCommand("LED:ON")` → `_serial.WriteLine(cmd)`
- **ข้อควรระวัง:** พอร์ต COM ถูกล็อกแบบ **Exclusive** (เปิดได้ทีละโปรแกรม) ต้องปิด `idf monitor`/PuTTY ก่อน

### 3.4 REST Minimal APIs

| Method | Endpoint | หน้าที่ |
| :--- | :--- | :--- |
| GET | `/api/gateway/health` | สถานะสุขภาพ Gateway |
| GET | `/api/telemetry/latest` | ค่าเซนเซอร์ล่าสุด (404 ถ้ายังไม่มีข้อมูล) |
| POST | `/api/led/{state}` (ใบงาน) / `/api/actuator/led` (เนื้อหา) | สั่งเปิด/ปิด LED ผ่าน Serial + MQTT |

### 3.5 SignalR (WebSockets)
- **Hub** = จุดรวมศูนย์การสื่อสาร (ฝั่งเซิร์ฟเวอร์)
- Server Push: `hubContext.Clients.All.SendAsync("ReceiveTelemetry", data)`
- Client เรียก Hub: `connection.invoke("ToggleLed", state)`
- Hub ส่งสถานะหาทุกหน้า: `Clients.All.SendAsync("LedStateUpdated", state)`
- ใน `Program.cs`: `AddSignalR()`, `UseDefaultFiles()`, `UseStaticFiles()`, `MapHub<GatewayHub>("/hubs/gateway")`
- Worker ต้องใช้ **`IHubContext<GatewayHub>`** (Dependency Injection) เพื่อ Push จากงานเบื้องหลัง

---

## 4. Edge Resilience & Security (บทที่ 13.4)

### 4.1 Local Survivability & Store-and-Forward
- กฎเหล็ก: **Zero Cloud Dependency for Local Operations**
- เมื่อเน็ตขาด → บันทึกแพ็กเก็ตลง **SQLite** ในเครื่อง (ตาราง `OfflineBuffer`: Voltage, RawAdc, Timestamp, `IsSynced`=0)
- เมื่อเน็ตกลับมา → ส่งย้อนหลัง แล้วตั้ง `IsSynced`

### 4.2 Circuit Breaker (3 สถานะ)

| สถานะ | ความหมาย | พฤติกรรม |
| :--- | :--- | :--- |
| **Closed** | ปกติ | ส่งคำขอไปคลาวด์ตามปกติ |
| **Open** | ตัดวงจร | ล้มเหลวติดกัน **> 5 ครั้ง** → หยุดส่ง **30 วินาที** และเก็บลง Local Buffer |
| **Half-Open** | ทดสอบ | ครบ 30 วินาที ลองส่ง **1 คำขอ** สำเร็จ → กลับ Closed / ล้มเหลว → Open อีก |

- **เหตุผล:** ป้องกัน **Thread Starvation** จากการกระหน่ำส่งซ้ำเมื่อปลายทางล่ม

### 4.3 Rate Limiting
- ป้องกัน **DoS** จากอุปกรณ์ติดมัลแวร์ในวงแลน (Gateway มี CPU/RAM จำกัด)
- ใช้ `AddRateLimiter` + **FixedWindowLimiter** แยกตาม IP: `PermitLimit=20`, `Window=1 วินาที`, `QueueLimit=5`
- ผูกกับ Route ด้วย `.RequireRateLimiting("FixedEdgePolicy")`

### 4.4 API Key Middleware
- ใช้ HTTP Header **`X-Api-Key`** (เบากว่า OAuth2/JWT เหมาะกับ Edge วงแลน)
- ตรวจเฉพาะเส้นทางสั่งการ `/api/actuator` ไม่ผ่านคืน **401 Unauthorized**
- ข้อควรระวัง: ในตัวอย่างฮาร์ดโค้ดคีย์ไว้ในโค้ด (ใช้เพื่อสอน) ในงานจริงควรเก็บเป็น Secret/Config

---

## 5. ตารางเปรียบเทียบ 4 โปรโตคอล (จาก Readme — ออกสอบบ่อย)

| มิติ | Hardware Serial | Embedded MQTT | HTTP REST | WebSockets (SignalR) |
| :--- | :--- | :--- | :--- | :--- |
| **Transport** | สาย USB-Serial | TCP/IP (พอร์ต **1883**) | TCP/IP (พอร์ต **5000/80**) | HTTP Upgrade → WebSocket |
| **รูปแบบ** | Byte Stream สองทาง | Publish/Subscribe | Request/Response | Bi-directional Push |
| **Overhead** | ต่ำที่สุด | ต่ำมาก (Fixed Header **2 ไบต์**) | สูง (Header **> 100 B**) | ต่ำมากหลัง Handshake (**2–10 B**) |
| **บทบาท** | เชื่อมฮาร์ดแวร์ตรง | เชื่อมโหนด/แอปมือถือ | อินเทอร์เฟซระบบภายนอก | อัปเดตกราฟเว็บสดๆ |
| **Latency** | **< 1 ms** | **2–10 ms** | **10–50 ms** | **1–5 ms** |

---

## 6. สรุปใบงาน (Labsheet)

### Lab 13.1 – Embedded MQTT Broker
- **เป้าหมาย:** เปลี่ยน Kestrel เป็น MQTT Broker ในตัว ไม่ต้องติดตั้ง Mosquitto
- **คำสั่งหลัก:** `dotnet new web -n ConvergedEdgeGateway` → `dotnet add package MQTTnet --version 4.3.7.1207` + `MQTTnet.AspNetCore` → `dotnet run`
- **ทดสอบ:** `mosquitto_sub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -i "sub_client" -v` และ `mosquitto_pub ... -m "..." -i "pub_client"`
- **ข้อสังเกต:** MQTT ต้องมี Client ID — `mosquitto_sub` บน Windows ถ้าไม่ใส่ `-i` จะส่ง ID ว่าง → Broker สุ่มให้ผ่าน `ValidatingConnectionAsync`

### Lab 13.2 – Hardware Serial → MQTT + REST
- **เพิ่ม:** `dotnet add package System.IO.Ports --version 8.0.0`
- **คลาสสำคัญ:** `GatewayStateStore`, `TelemetryData(Voltage, Raw, Timestamp)`, `SerialBridgeWorker`
- **Flow:** Serial → Parse JSON → State Store + Publish `iot/esp32/telemetry` ; REST `GET /api/telemetry/latest`, `POST /api/led/on|off` → `LED:ON`/`LED:OFF` ลง Serial
- **ESP32 (ESP-IDF):** `adc_oneshot` 12-bit (0–4095), Atten 12 dB (0–3.3 V), `voltage = raw/4095*3.3`, ส่ง JSON ด้วย `printf` ทุก ~450 ms, รับคำสั่งผ่าน `uart_read_bytes`

### Lab 13.3 – SignalR + Web Dashboard
- **คลาส:** `GatewayHub : Hub` (เมธอด `ToggleLed(bool)`), `ProtocolMetrics` (SerialPackets, MqttMessages, RestRequests, SignalRPushes)
- **Worker ใหม่:** รับ `IHubContext<GatewayHub>` + `ProtocolMetrics` ผ่าน DI
- **เว็บ:** `wwwroot/index.html` สไตล์ Dark Glassmorphism (`backdrop-filter: blur`), ใช้ `signalr.min.js`, endpoint `/hubs/gateway`, `withAutomaticReconnect()`
- **ผลที่เห็น:** แรงดันวิ่งสด, ตัวนับ 3 โปรโตคอลเพิ่มพร้อมกัน, กดปุ่มเปิดไฟ LED จริงติดทันที

### Lab 13.4 – Grand Finale (All-in-One)
- **ระบบ:** ESP32 + Kestrel Gateway + Flutter App (`iot_dashboard_app` สัปดาห์ 12) + Web Command Center
- **Grand Verification Matrix (4 การทดสอบ):**
  1. **Universal Telemetry Sync** – หมุน Pot → เว็บ + Flutter เปลี่ยนพร้อมกัน
  2. **Mobile-to-Web Actuation** – แตะสวิตช์ Flutter (MQTT) → LED ติด + ปุ่มเว็บเป็น ON
  3. **Web-to-Mobile Actuation** – กดปิดบนเว็บ (SignalR) → LED ดับ + Flutter เป็น OFF
  4. **Third-Party REST Override** – `curl -X POST http://localhost:5000/api/led/on` → ทุกอย่างเป็น ON
- **Local Survivability Challenge:** ถอด LAN/เน็ตภายนอก (เหลือเฉพาะ Wi-Fi วงแลน) → ESP32 ยังส่งข้อมูล, แอป+เว็บยังดูค่าและสั่ง LED ได้ → ระบบอยู่รอด 100%
- **วัตถุประสงค์ 4 ข้อ:** ประกอบ 4 โปรโตคอล / ซิงก์สถานะสามประสาน (Hardware–Mobile–Web) / พิสูจน์ Local Survivability / วัดความหน่วงแต่ละเส้นทาง

---

## 7. เฉลยคำถามท้ายใบงาน (สรุปสั้น)

| ข้อ | คำถาม | คำตอบหลัก |
| :--- | :--- | :--- |
| 13.1-1 | Embedded Broker vs Mosquitto แยก | **Deploy:** Single-process/Docker เดียว ไม่ต้องดูแลบริการแยก ลด Version/Port conflict และ Single Point of Failure — **In-Memory:** Inject ข้อความตรง ไม่ต้องเปิด TCP Loopback ซ้ำ latency ต่ำ แชร์ State ได้ทันที |
| 13.1-2 | `InterceptingPublishAsync` ทำ Edge Validation | เป็น Hook ก่อนกระจายข้อความ: ตรวจรูปแบบ/ช่วงค่า (เช่น 0–3.3 V), ทิ้งข้อความผิดปกติด้วย `args.ProcessPublish = false`, เติม Metadata, ตรวจสิทธิ์ ClientId, Rate Limiting |
| 13.2-1 | Serial-to-MQTT Bridge กับ Legacy | Gateway แปลง Serial (ไม่มี Wi-Fi/TLS) → MQTT/REST, เป็น Secure Proxy, คุ้มค่าเพราะไม่ต้องเปลี่ยนเครื่องจักรเดิม |
| 13.2-2 | ทำไมใช้ `BackgroundService` | Serial เป็น Stream ต่อเนื่อง + พอร์ต **Exclusive** → อ่านใน REST จะบล็อกเธรด/พอร์ตชนกัน; Worker ถือพอร์ตเดียว เก็บค่าลง State Store ทำให้ REST ตอบ O(1) เร็ว |
| 13.3-1 | SignalR vs `setInterval()` Polling | Full-duplex persistent, Server **Push** ทันที, ไม่มี HTTP Header ซ้ำ, ลื่นไหล, ซิงก์หลาย Client ได้ทันที |
| 13.3-2 | Protocol Convergence Metrics | Observability ทั้ง Pipeline, แยกจุดเสีย (Serial นิ่ง = สาย/บอร์ดหลุด, Serial ขึ้นแต่ MQTT นิ่ง = Parse/Broker, MQTT ขึ้นแต่ SignalR นิ่ง = WebSocket/เบราว์เซอร์), วางแผน Capacity |
| 13.4-1 | สถาปัตยกรรมท้าทายที่สุด | Converged Edge Gateway — Concurrency 4 โปรโตคอล, Protocol Transcoding เรียลไทม์, Edge Resilience |
| 13.4-2 | 3 ปัจจัยก่อนขึ้นโรงงานจริง | (1) TLS/mTLS (HTTPS, WSS, MQTT 8883) (2) AuthN/AuthZ: JWT/OAuth2/API Key + RBAC (3) Hardware Watchdog + Isolated RS-485 + Fail-Safe State |

---

## 8. อภิธานศัพท์สำคัญ (Glossary)

| ศัพท์ | ความหมายสั้น |
| :--- | :--- |
| **Edge Computing** | ประมวลผลใกล้แหล่งข้อมูล ลด latency/แบนด์วิดท์ |
| **Edge Gateway** | อุปกรณ์กลางเชื่อมและแปลงโปรโตคอลระหว่างฟิลด์กับเครือข่ายนอก |
| **Converged Gateway** | รวม Web+MQTT+WebSocket+Serial ในโปรเซสเดียว |
| **Protocol Heterogeneity** | อุปกรณ์ใช้โปรโตคอลต่างกันจนคุยกันตรงไม่ได้ |
| **Protocol Bridge** | ตัวกลางแปลงข้อมูลจากโปรโตคอลหนึ่งไปอีกโปรโตคอล |
| **Kestrel** | เว็บเซิร์ฟเวอร์ Async I/O ความเร็วสูงของ ASP.NET Core |
| **Minimal APIs** | สร้าง REST Endpoint แบบไม่ต้องมี Controller |
| **BackgroundService / IHostedService** | งานเบื้องหลังระยะยาว / อินเทอร์เฟซวงจรชีวิต Start–Stop |
| **System.Threading.Channels** | คิว Producer-Consumer แบบ Thread-Safe ไม่ Lock |
| **Zero-Allocation** | ไม่สร้างขยะบน Heap เลี่ยง GC Pause |
| **MQTTnet** | ไลบรารี MQTT Client/Broker สำหรับ .NET |
| **Embedded MQTT Broker** | Broker ฝังในแอป ไม่ต้องใช้ Mosquitto |
| **SerialPort** | คลาส .NET คุม COM/USB-UART |
| **SignalR / SignalR Hub** | เฟรมเวิร์ก Real-time ผ่าน WebSocket / จุดรวมการสื่อสารฝั่งเซิร์ฟเวอร์ |
| **Local Survivability** | ทำงานต่อได้แม้ตัดเน็ตภายนอก |
| **Store-and-Forward** | เก็บข้อมูลตอนออฟไลน์ แล้วส่งภายหลัง |
| **Circuit Breaker** | ตัดวงจรเมื่อปลายทางล่ม (Closed/Open/Half-Open) |
| **Rate Limiting** | จำกัดจำนวนคำขอ/วินาที กัน DoS |
| **ConcurrentDictionary** | Hash table ที่ปลอดภัยต่อมัลติเธรด |
| **Graceful Shutdown** | ปิดระบบอย่างนุ่มนวล คืนทรัพยากร เคลียร์คิวก่อนดับ |

---

## 9. แนวข้อสอบ (ฝึกทบทวน)

1. อธิบายข้อจำกัดของ Cloud-Centric และเหตุผลที่ต้องใช้ Edge Gateway
2. Converged Edge Gateway ทำหน้าที่อะไรบ้าง (3 บทบาท)?
3. Kestrel ใช้เทคนิคใดหลีกเลี่ยง GC Pause? (`System.IO.Pipelines`, `ArrayPool<byte>`, `ReadOnlySpan<byte>`)
4. `BoundedChannelFullMode.DropOldest` ทำงานอย่างไร และทำไมต้องใช้ Bounded Channel?
5. เหตุใดต้องใช้ `ConcurrentDictionary` ใน `GatewayStateStore`?
6. วาด/อธิบาย Routing Matrix เมื่อข้อมูลมาจาก Serial, REST, MQTT, SignalR
7. จงเปรียบเทียบ Latency/Overhead ของทั้ง 4 โปรโตคอล
8. อธิบาย 3 สถานะของ Circuit Breaker และเงื่อนไขการเปลี่ยนสถานะ (5 ครั้ง / 30 วินาที)
9. Rate Limiting กำหนดอย่างไร (20 req/s ต่อ IP, Queue 5) และป้องกันอะไร?
10. เหตุใดต้องปิด Serial Monitor อื่นก่อนรัน Kestrel?
11. อธิบายขั้นตอน Local Survivability Challenge และผลที่คาดหวัง
12. ต่อวงจร ESP32 ตามใบงาน: ขา GPIO34, GPIO2, ตัวต้านทาน 220 Ω, Pot 10 kΩ

---

## 10. จุดที่ต้องจำให้ได้ (Quick Facts)

- พอร์ต: **5000** (HTTP) / **1883** (MQTT) / Hub path **`/hubs/gateway`**
- Topic: **`iot/esp32/telemetry`**, **`iot/esp32/control`**
- คำสั่ง Serial: **`LED:ON`**, **`LED:OFF`**
- JSON จาก ESP32: **`{"voltage":x.xx,"raw":n}`**, Raw ADC **0–4095** (12-bit), แรงดัน **0–3.3 V**
- Baud: **115200**; Pot → **GPIO34 (ADC1_CH6)**; LED → **GPIO2**
- เวอร์ชัน: **.NET 8 (LTS)**, MQTTnet **4.3.7.1207**, System.IO.Ports **8.0.0**
- Circuit Breaker: ล้ม **>5 ครั้ง** → Open **30 วินาที**
- Rate Limit: **20 ครั้ง/วินาที ต่อ IP**, Queue **5**
- Auth Header: **`X-Api-Key`** → ไม่ผ่านตอบ **401**
