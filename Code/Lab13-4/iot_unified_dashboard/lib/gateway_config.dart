// ค่าการเชื่อมต่อ Kestrel Edge Gateway อ่านตอน build จาก --dart-define-from-file=gateway.env.json
// (ไฟล์ gateway.env.json ไม่ถูก commit ลง git ดูตัวอย่างใน gateway.env.example.json)
//
// หมายเหตุ: ค่าที่ใส่ผ่าน dart-define ถูกฝังอยู่ในไฟล์แอปที่ build แล้ว ใช้รหัสผ่านเฉพาะสำหรับ Lab
// อย่าใช้รหัสผ่าน Wi-Fi หรือรหัสผ่านส่วนตัวซ้ำกับรหัสนี้
class GatewayConfig {
  // IP วง LAN ของคอมพิวเตอร์ที่รัน Kestrel (ดูด้วย ipconfig) หรือ 127.0.0.1 ถ้ารันแอปบน Windows เครื่องเดียวกัน
  static const String host = String.fromEnvironment('GATEWAY_HOST', defaultValue: '127.0.0.1');
  static const int mqttPort = int.fromEnvironment('GATEWAY_MQTT_PORT', defaultValue: 1883);
  static const String mqttUsername = String.fromEnvironment('MQTT_USERNAME');
  static const String mqttPassword = String.fromEnvironment('MQTT_PASSWORD');

  static bool get hasCredentials => mqttUsername.isNotEmpty && mqttPassword.isNotEmpty;
}
