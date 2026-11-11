#!/bin/bash
cd "$(dirname "$0")" || exit 1
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="/tmp/dotnet_home"

echo "============================================================"
echo " 🚀 Lab 13.4: The Grand Finale - All-in-One IoT Ecosystem"
echo " รหัสนักศึกษา: 67030302"
echo " แดชบอร์ดศูนย์บัญชาการ: http://localhost:5001"
echo " Embedded MQTT Broker: 127.0.0.1:1883"
echo " ฮาร์ดแวร์ Serial: /dev/cu.usbserial-0001 (115200 bps)"
echo "============================================================"

# Background helper: เปิดเบราว์เซอร์ไปที่แดชบอร์ด และรันการทดสอบ 4 มิติอัตโนมัติ
(
  sleep 4
  echo "\n🌐 กำลังเปิด Web Command Center ที่ http://localhost:5001..."
  open http://localhost:5001 2>/dev/null || true

  sleep 2
  python3 test_e2e_grand_finale.py
) &

cd ConvergedEdgeGateway || exit 1
dotnet run --no-restore

