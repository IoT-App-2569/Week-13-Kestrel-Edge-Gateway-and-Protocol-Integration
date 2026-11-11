#!/bin/bash
cd "$(dirname "$0")" || exit 1
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

echo "============================================================"
echo " 🚀 Lab 13.1: Kestrel Converged Edge Gateway (Embedded MQTT)"
echo " รหัสนักศึกษา: 67030302"
echo "============================================================"

# Helper: รอ Kestrel ติดขึ้นมา แล้วยิง MQTT Packet และ HTTP Request เพื่อให้คอนโซลขึ้น Log สมบูรณ์แบบ
(
  sleep 5
  python3 test_mqtt.py 2>/dev/null
  echo "\n👉 [REST API Response]: $(curl -s http://localhost:5001/)\n"
) &

cd ConvergedEdgeGateway || exit 1
dotnet run
