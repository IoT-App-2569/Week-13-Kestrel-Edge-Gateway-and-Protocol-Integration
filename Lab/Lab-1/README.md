# Lab 1: Embedded MQTT Broker and Client with MQTTnet

โปรเจกต์นี้ทำตามใบงาน 13.1 โดยใช้ ASP.NET Core Kestrel เป็น Embedded MQTT Broker
และเปิดบริการ HTTP ที่พอร์ต 5000 กับ MQTT TCP ที่พอร์ต 1883

## วิธีรัน

```powershell
dotnet run
```

ตรวจสอบ REST endpoint:

```powershell
Invoke-WebRequest http://127.0.0.1:5000/
```

ทดสอบ MQTT ด้วย Mosquitto CLI:

```powershell
mosquitto_sub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -i "sub_client" -v
mosquitto_pub -h 127.0.0.1 -p 1883 -t "test/edge/topic" -m "Hello from CLI via Kestrel Embedded MQTT!" -i "pub_client"
```
