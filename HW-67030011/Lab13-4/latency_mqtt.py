"""วัด Round-trip ของเส้นทาง MQTT: publish iot/esp32/control -> รับ iot/esp32/led_ack กลับมา
ใช้ MQTT 3.1.1 แบบ Raw socket (QoS 0) เพื่อไม่ให้เวลา spawn โปรเซส mosquitto_pub ปนในผลวัด
usage: python3 latency_mqtt.py [rounds]
"""
import json
import socket
import statistics
import sys
import time

HOST, PORT = "127.0.0.1", 1883


def remaining_length(n):
    out = bytearray()
    while True:
        b, n = n % 128, n // 128
        out.append(b | (0x80 if n else 0))
        if not n:
            return bytes(out)


def utf8(s):
    b = s.encode()
    return len(b).to_bytes(2, "big") + b


def packet(ptype, body):
    return bytes([ptype]) + remaining_length(len(body)) + body


def read_packet(sock):
    first = sock.recv(1)[0]
    mult, length = 1, 0
    while True:
        b = sock.recv(1)[0]
        length += (b & 0x7F) * mult
        mult *= 128
        if not b & 0x80:
            break
    data = b""
    while len(data) < length:
        data += sock.recv(length - len(data))
    return first, data


def main():
    rounds = int(sys.argv[1]) if len(sys.argv) > 1 else 20
    s = socket.create_connection((HOST, PORT))
    s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    s.sendall(packet(0x10, utf8("MQTT") + bytes([4, 0x02]) + (60).to_bytes(2, "big") + utf8("latency_probe")))
    read_packet(s)  # CONNACK
    s.sendall(packet(0x82, (1).to_bytes(2, "big") + utf8("iot/esp32/led_ack") + bytes([0])))
    read_packet(s)  # SUBACK
    s.settimeout(0.3)
    try:  # ทิ้ง Retained message ที่ได้ตอน Subscribe
        while True:
            read_packet(s)
    except socket.timeout:
        pass
    s.settimeout(5)

    rtts, state = [], True
    for _ in range(rounds):
        body = json.dumps({"led": "ON" if state else "OFF"}).encode()
        t0 = time.perf_counter()
        s.sendall(packet(0x30, utf8("iot/esp32/control") + body))
        while True:
            ptype, data = read_packet(s)
            if ptype >> 4 == 3:  # PUBLISH
                tlen = int.from_bytes(data[:2], "big")
                payload = json.loads(data[2 + tlen:])
                if payload["led_state"] == state:
                    break
        rtts.append((time.perf_counter() - t0) * 1000)
        state = not state
        time.sleep(0.15)
    s.sendall(bytes([0xE0, 0]))  # DISCONNECT
    print(f"MQTT control->led_ack  n={rounds}  min={min(rtts):.2f}  median={statistics.median(rtts):.2f}  "
          f"mean={statistics.mean(rtts):.2f}  max={max(rtts):.2f} ms")


if __name__ == "__main__":
    main()
