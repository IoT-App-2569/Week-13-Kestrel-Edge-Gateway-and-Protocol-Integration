#!/bin/zsh
# ทดสอบ Local Survivability: รอจนอินเทอร์เน็ตหลุด (ผู้ทดลองปิด Wi-Fi เอง) แล้วทดสอบทุกเส้นทางบน localhost
# ระหว่างออฟไลน์ เก็บภาพหน้าเว็บ + แอป Flutter ทุกขั้น แล้วรอจนเน็ตกลับมา
# usage: ./survivability_test.sh SHOTS_DIR
SHOTS=${1:?shots dir}
HERE=${0:A:h}
SIM=32B8AB16-3F35-4A03-B677-902BA83F0BC4
mkdir -p $SHOTS
ts() { date +%H:%M:%S; }
online() { curl -s -m 3 -o /dev/null -w "%{http_code}" https://www.google.com 2>/dev/null | grep -q '^[23]'; }
snap() {  # $1 = name, $2 = optional JS action in the web page
  mkdir -p $SHOTS/$1
  CDP_ACTION="$2" node $HERE/cdpshot.mjs http://localhost:5080 $SHOTS/$1 1 0 | sed "s/^shot_01/[$(ts)] web $1/" &
  sleep $([ -n "$2" ] && echo 4.2 || echo 2.7)
  xcrun simctl io $SIM screenshot $SHOTS/$1/phone.png >/dev/null 2>&1
  wait
}

echo "[$(ts)] waiting for internet to go down (turn Wi-Fi off now)..."
for i in $(seq 1 300); do online || break; sleep 2; done
echo "[$(ts)] internet check: curl https://www.google.com -> $(curl -s -m 3 -o /dev/null -w '%{http_code} %{errormsg}' https://www.google.com 2>&1)"
echo "[$(ts)] Wi-Fi: $(networksetup -getairportpower en0)"
echo "[$(ts)] en0: $(ifconfig en0 | awk '/status:/{print $2}')  inet: $(ifconfig en0 | awk '/inet /{print $2}' | tr '\n' ' ')"

echo "\n[$(ts)] 1) telemetry still flowing (Serial -> REST/MQTT/SignalR)"
curl -s http://localhost:5080/api/telemetry/latest; echo
mosquitto_sub -h 127.0.0.1 -p 1883 -t iot/esp32/telemetry -C 3 -v -i offline_sub
snap off1_telemetry

echo "\n[$(ts)] 2) MQTT control (as the Flutter app) -> LED ON"
/usr/bin/python3 -I $HERE/latency_mqtt.py 1
snap off2_mqtt_on

echo "\n[$(ts)] 3) Web (SignalR) -> LED OFF"
snap off3_web_off "toggleLed()"

echo "\n[$(ts)] 4) REST -> LED ON"
curl -s -X POST http://localhost:5080/api/led/on; echo
snap off4_rest_on

echo "\n[$(ts)] 5) turn the potentiometer now (10 s)"
for i in 1 2 3; do sleep 3; curl -s http://localhost:5080/api/telemetry/latest; echo; done
snap off5_pot

echo "\n[$(ts)] still offline? $(online && echo NO-internet-is-back || echo yes)"
echo "[$(ts)] done - turn Wi-Fi back on"
for i in $(seq 1 150); do online && break; sleep 2; done
echo "[$(ts)] internet back: $(online && echo yes || echo no)"
