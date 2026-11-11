#!/bin/bash
cd "$(dirname "$0")" || exit 1
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

echo "============================================================"
echo " 🚀 Lab 13.3: Realtime SignalR Hub and Converged Dashboard"
echo " รหัสนักศึกษา: 67030302"
echo " เชื่อมต่อ ESP32 จริง: /dev/cu.usbserial-0001 (Baud 115200)"
echo " เปิดแดชบอร์ดที่: http://localhost:5001"
echo "============================================================"

# Helper: เปิดเบราว์เซอร์ไปที่แดชบอร์ดอัตโนมัติเมื่อ Gateway พร้อม
(
  sleep 3
  open http://localhost:5001
) &

cd ConvergedEdgeGateway || exit 1
dotnet run

