#!/bin/bash
cd "$(dirname "$0")" || exit 1
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

echo "============================================================"
echo " 🚀 Lab 13.2: Hardware Serial to MQTT and REST Bridge"
echo " รหัสนักศึกษา: 67030302"
echo " เชื่อมต่อ ESP32 จริง: /dev/cu.usbserial-0001 (Baud 115200)"
echo "============================================================"

# Background helper to test the REST endpoints after Kestrel starts
(
  sleep 4
  echo "\n--- [1. ทดสอบอ่านค่า Telemetry จริงจากบอร์ด ESP32 ผ่าน REST API] ---"
  curl -s http://localhost:5001/api/telemetry/latest
  echo "\n"

  sleep 2
  echo "--- [2. ทดสอบสั่งเปิดไฟ LED บนบอร์ดผ่าน REST API: POST /api/led/on] ---"
  curl -s -X POST http://localhost:5001/api/led/on
  echo "\n"

  sleep 3
  echo "--- [3. ทดสอบสั่งปิดไฟ LED บนบอร์ดผ่าน REST API: POST /api/led/off] ---"
  curl -s -X POST http://localhost:5001/api/led/off
  echo "\n------------------------------------------------------------------"
) &

cd ConvergedEdgeGateway || exit 1
dotnet run
