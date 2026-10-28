<img width="993" height="296" alt="image" src="https://github.com/user-attachments/assets/f6a66f4b-b612-4e9d-bae9-afd21ba2b0a4" />

รันเซิร์ฟเวอร์ด้วย `dotnet run` และเปิดเบราว์เซอร์ไปที่ `http://localhost:5000`:
* คุณจะพบหน้าจอศูนย์บัญชาการที่ตัวเลขแรงดันไฟฟ้าวิ่งสดๆ ตามการหมุนตัวต้านทานบนบอร์ด!
* ตัวเลขนับทราฟฟิกของทั้ง Serial, MQTT และ SignalR จะวิ่งขึ้นพร้อมๆ กันในเสี้ยววินาที!
* เมื่อคลิกปุ่มเปิดไฟบนหน้าเว็บ หลอดไฟ LED จริงบนบอร์ด ESP32 จะสว่างขึ้นทันที!


# ผลการทดลองกลุ่มร่วมกับWuthichai
<img width="991" height="340" alt="image" src="https://github.com/user-attachments/assets/edac3d5a-956c-4f26-9b51-7d731661e789" />
<img width="991" height="298" alt="image" src="https://github.com/user-attachments/assets/36f73ed3-73d9-4a2d-8a87-315beba1b13b" />
<img width="991" height="285" alt="image" src="https://github.com/user-attachments/assets/ac53823b-d2ca-44bd-beb1-f0cdc8f60e67" />
<img width="771" height="258" alt="image" src="https://github.com/user-attachments/assets/620b795c-8637-4ceb-af64-1a1091c24925" />

<img width="960" height="1706" alt="image" src="https://github.com/user-attachments/assets/c63d2396-f01b-48c2-a312-9da61884d5f1" />

<img width="907" height="86" alt="image" src="https://github.com/user-attachments/assets/635e37a9-a16b-423c-a72a-4ecd317142c1" />

<img width="1917" height="656" alt="image" src="https://github.com/user-attachments/assets/6e3f0bea-cdcc-4b30-bfbf-ebf7bc3f7e86" />

<img width="960" height="1706" alt="image" src="https://github.com/user-attachments/assets/23bb86ef-c326-4300-9649-d64969e9bfdb" />

---

## 4. คำถามท้ายการทดลอง
1. เพราะเหตุใด SignalR จึงให้ประสบการณ์การใช้งาน (UX) บนหน้าเว็บที่ดีกว่าการใช้คำสั่ง JavaScript `setInterval()` เพื่อทำ HTTP GET Polling ซ้ำๆ ทุก 500 ms?
2. การคำนวณและแสดงผล Protocol Convergence Metrics ส่งผลดีต่อการตรวจสอบสุขภาพของระบบเครือข่ายไอโอทีในระดับโรงงานอย่างไร?
