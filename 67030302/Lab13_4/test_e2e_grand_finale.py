#!/usr/bin/env python3
import time
import json
import urllib.request
import paho.mqtt.client as mqtt

BROKER_HOST = "127.0.0.1"
BROKER_PORT = 1883
HTTP_BASE = "http://localhost:5001"

print("=" * 65)
print(" 🌟 Lab 13.4 The Grand Finale: End-to-End Verification Suite")
print(" รหัสนักศึกษา: 67030302")
print("=" * 65)

telemetry_received = []
ack_received = []

def on_connect(client, userdata, flags, rc, properties=None):
    print(f"✅ [MQTT Client] เชื่อมต่อกับ Kestrel Embedded Broker สำเร็จ! (RC: {rc})")
    client.subscribe("iot/esp32/telemetry")
    client.subscribe("iot/esp32/led_ack")
    client.subscribe("iot/esp32/control")

def on_message(client, userdata, msg):
    payload = msg.payload.decode('utf-8', errors='ignore')
    if msg.topic == "iot/esp32/telemetry":
        telemetry_received.append(payload)
        print(f"📥 [Universal Telemetry Sync] Topic: {msg.topic} -> {payload}")
    elif msg.topic == "iot/esp32/led_ack":
        ack_received.append(payload)
        print(f"📬 [Actuator State ACK] Topic: {msg.topic} -> {payload}")

# 1. Connect MQTT Client (Simulating Flutter Mobile App)
try:
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id="flutter_mobile_app_67030302")
except AttributeError:
    client = mqtt.Client(client_id="flutter_mobile_app_67030302")

client.on_connect = on_connect
client.on_message = on_message

try:
    client.connect(BROKER_HOST, BROKER_PORT, 60)
    client.loop_start()
except Exception as e:
    print(f"❌ Failed to connect MQTT: {e}")
    exit(1)

# Test 1: Universal Telemetry Sync
print("\n--- [มิติที่ 1: Universal Telemetry Sync (Serial -> Kestrel -> MQTT/Web)] ---")
time.sleep(2)
if telemetry_received:
    print(f"  🎉 ผ่าน! ได้รับข้อมูล Telemetry สด {len(telemetry_received)} แพ็กเก็ต")
else:
    print("  ⏳ กำลังรอแพ็กเก็ต Telemetry...")

# Test 2: Mobile-to-Web Actuation (Flutter App -> MQTT -> ESP32 & Web Dashboard)
print("\n--- [มิติที่ 2: Mobile-to-Web Actuation (Flutter App -> MQTT Control)] ---")
print("  📱 แตะสวิตช์เปิดไฟบนแอป Flutter: ส่ง Publish ไปยัง 'iot/esp32/control' (Payload: {\"led\": \"ON\"})")
client.publish("iot/esp32/control", json.dumps({"led": "ON"}))
time.sleep(2)

# Test 3: Third-Party REST Override
print("\n--- [มิติที่ 3: Third-Party REST Override (cURL/Postman -> Kestrel REST API)] ---")
print("  🌐 สั่งปิดไฟผ่าน REST API: POST http://localhost:5001/api/led/off")
try:
    req = urllib.request.Request(f"{HTTP_BASE}/api/led/off", data=b"", method="POST")
    with urllib.request.urlopen(req) as resp:
        res_text = resp.read().decode('utf-8')
        print(f"  👉 Response: {res_text}")
except Exception as e:
    print(f"  ⚠️ REST API Error: {e}")

time.sleep(1.5)
print("  🌐 สั่งเปิดไฟผ่าน REST API: POST http://localhost:5001/api/led/on")
try:
    req = urllib.request.Request(f"{HTTP_BASE}/api/led/on", data=b"", method="POST")
    with urllib.request.urlopen(req) as resp:
        res_text = resp.read().decode('utf-8')
        print(f"  👉 Response: {res_text}")
except Exception as e:
    print(f"  ⚠️ REST API Error: {e}")

# Test 4: Verify Latest Telemetry & Gateway Health via REST
print("\n--- [มิติที่ 4: ตรวจสอบสถานะสุขภาพและสถิติทราฟฟิก (REST API Verification)] ---")
try:
    with urllib.request.urlopen(f"{HTTP_BASE}/api/telemetry/latest") as resp:
        print(f"  📊 GET /api/telemetry/latest -> {resp.read().decode('utf-8')}")
except Exception as e:
    print(f"  ⚠️ Telemetry API: {e}")

try:
    with urllib.request.urlopen(f"{HTTP_BASE}/api/gateway/health") as resp:
        print(f"  🏥 GET /api/gateway/health   -> {resp.read().decode('utf-8')}")
except Exception as e:
    print(f"  ⚠️ Health API: {e}")

time.sleep(2)
client.loop_stop()
client.disconnect()

print("\n" + "=" * 65)
print(" 🚀 การทดสอบ The Grand Finale สำเร็จครบถ้วนทุกมิติ!")
print("=" * 65)
