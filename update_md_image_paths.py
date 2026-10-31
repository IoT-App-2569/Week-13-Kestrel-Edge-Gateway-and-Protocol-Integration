import os, re

files = [
    "06-Labsheet-13-1-Embedded-MQTT-Broker-and-Client-with-MQTTnet.md",
    "07-Labsheet-13-2-Hardware-Serial-to-MQTT-and-REST-Bridge.md",
    "08-Labsheet-13-3-Realtime-SignalR-Hub-and-Converged-Web-Dashboard.md",
    "09-Labsheet-13-4-The-Grand-Finale-All-in-One-IoT-System-Integration.md"
]

for filename in files:
    if not os.path.exists(filename):
        continue
    with open(filename, 'r', encoding='utf-8') as f:
        content = f.read()

    # Replace ![alt text](image-X.png) or ![alt text](image.png) with Images/image...
    # only if not already prefixed with Images/
    new_content = re.sub(r'!\[(.*?)\]\((?!(?:Images/|http))(image(?:-\d+)?\.png)\)', r'![\1](Images/\2)', content)

    if new_content != content:
        with open(filename, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Updated: {filename}")
    else:
        print(f"No changes: {filename}")
