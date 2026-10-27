// Converged Edge Command Center - SignalR client (Lab 13.4)
// สถานะ LED มาจากเกตเวย์เท่านั้น (LedStateUpdated) ไม่ว่าคำสั่งมาจากมือถือ เว็บ หรือ REST
// ใช้ textContent เท่านั้น (ไม่ใช้ innerHTML) เพื่อกันการฉีดสคริปต์จากข้อมูลที่รับมา
"use strict";

const $ = (id) => document.getElementById(id);
let isLedOn = false;
let hubOnline = false;
let deviceOnline = false;

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/gateway")
    .withAutomaticReconnect()
    .build();

function render() {
    const status = $("connStatus");
    if (!hubOnline) {
        status.textContent = "○ Gateway Disconnected";
        status.className = "status offline";
    } else if (!deviceOnline) {
        status.textContent = "● SignalR Online · ESP32 Offline";
        status.className = "status offline";
    } else {
        status.textContent = "● SignalR Online · ESP32 Online";
        status.className = "status online";
    }

    const btn = $("ledBtn");
    btn.disabled = !(hubOnline && deviceOnline);
    btn.classList.toggle("btn-active", isLedOn);
    btn.textContent = isLedOn ? "💡 หลอดไฟกำลังสว่าง (ON)" : "🌑 หลอดไฟปิดอยู่ (OFF)";
}

connection.on("ReceiveTelemetry", (data) => {
    const v = Number(data.voltage) || 0;
    $("voltageText").textContent = v.toFixed(2);
    $("voltageBar").style.width = Math.min(100, (v / 3.3) * 100) + "%";
    $("rawText").textContent = "Raw ADC: " + (Number(data.raw) || 0) + " / 4095";

    if (data.metrics) {
        $("mSerial").textContent = data.metrics.serialPackets;
        $("mMqtt").textContent = data.metrics.mqttMessages;
        $("mRest").textContent = data.metrics.restRequests;
        $("mSignalR").textContent = data.metrics.signalRPushes;
    }
});

connection.on("LedStateUpdated", (state) => {
    isLedOn = state === true;
    render();
});

connection.on("DeviceStatus", (online) => {
    deviceOnline = online === true;
    render();
});

connection.on("LedCommandFailed", () => {
    $("ledBtn").textContent = "⚠️ ส่งคำสั่งไม่สำเร็จ (บอร์ดไม่ได้เชื่อมต่อ)";
    setTimeout(render, 2000);
});

$("ledBtn").addEventListener("click", () => {
    connection.invoke("ToggleLed", !isLedOn).catch((err) => console.error(err));
});

connection.onreconnecting(() => { hubOnline = false; render(); });
connection.onreconnected(() => { hubOnline = true; render(); });
connection.onclose(() => { hubOnline = false; render(); });

connection.start()
    .then(() => { hubOnline = true; render(); })
    .catch(() => { hubOnline = false; render(); });
