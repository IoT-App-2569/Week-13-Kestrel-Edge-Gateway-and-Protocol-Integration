import socket
import time

def send_mqtt_test():
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.connect(('127.0.0.1', 1883))
    
    # 1. MQTT CONNECT packet (Protocol: MQTT 3.1.1, ClientId: "test_client_67030302")
    client_id = b"test_client_67030302"
    var_header = b"\x00\x04MQTT\x04\x02\x00<\x00" + bytes([len(client_id)]) + client_id
    connect_pkt = bytes([0x10, len(var_header)]) + var_header
    s.sendall(connect_pkt)
    time.sleep(0.5)
    
    # 2. MQTT PUBLISH packet (Topic: "test/edge/topic", Payload: "Hello from CLI via Kestrel Embedded MQTT!")
    topic = b"test/edge/topic"
    payload = b"Hello from CLI via Kestrel Embedded MQTT!"
    msg = bytes([0, len(topic)]) + topic + payload
    pub_pkt = bytes([0x30, len(msg)]) + msg
    s.sendall(pub_pkt)
    time.sleep(1)
    
    # 3. DISCONNECT
    s.sendall(b"\xe0\x00")
    s.close()
    print("MQTT Test sent successfully!")

if __name__ == '__main__':
    send_mqtt_test()

