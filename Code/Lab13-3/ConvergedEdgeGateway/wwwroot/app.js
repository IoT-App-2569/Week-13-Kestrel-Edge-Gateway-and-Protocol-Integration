// Converged Edge Command Center - SignalR client
// ใช้ textContent เท่านั้น (ไม่ใช้ innerHTML) เพื่อกันการฉีดสคริปต์จากข้อมูลที่รับมา
"use strict";

const $ = (id) => document.getElementById(id);
let isLedOn = false;
let lastCommandAt = 0;

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/gateway")
    .withAutomaticReconnect()
    .build();

function setStatus(text, cls) {
    $("connStatus").textContent = text;
    $("connStatus").className = "status " + cls;
    $("ledBtn").disabled = cls !== "online";
}

function updateLedButton() {
    const btn = $("ledBtn");
    btn.classList.toggle("btn-active", isLedOn);
    btn.textContent = isLedOn ? "💡 หลอดไฟกำลังสว่าง (ON)" : "🌑 หลอดไฟปิดอยู่ (OFF)";
}

connection.on("ReceiveTelemetry", (data) => {
    const v = Number(data.voltage) || 0;
    $("voltageText").textContent = v.toFixed(2);
    $("voltageBar").style.width = Math.min(100, (v / 3.3) * 100) + "%";
    $("rawText").textContent = "Raw ADC: " + (Number(data.raw) || 0) + " / 4095";

    // สถานะจริงของ LED ที่บอร์ดรายงานกลับมา (เว้นช่วงหลังกดปุ่ม เพราะข้อมูลที่ค้างในสายยังเป็นสถานะเก่า)
    if (typeof data.led === "boolean" && data.led !== isLedOn && Date.now() - lastCommandAt > 1500) {
        isLedOn = data.led;
        updateLedButton();
    }

    if (data.metrics) {
        $("mSerial").textContent = data.metrics.serialPackets;
        $("mMqtt").textContent = data.metrics.mqttMessages;
        $("mRest").textContent = data.metrics.restRequests;
        $("mSignalR").textContent = data.metrics.signalRPushes;
    }
});

connection.on("LedStateUpdated", (state) => {
    isLedOn = state === true;
    updateLedButton();
});

connection.on("LedCommandFailed", () => {
    $("ledBtn").textContent = "⚠️ ส่งคำสั่งไม่สำเร็จ (บอร์ดไม่ได้เชื่อมต่อ)";
    setTimeout(updateLedButton, 2000);
});

$("ledBtn").addEventListener("click", () => {
    lastCommandAt = Date.now();
    connection.invoke("ToggleLed", !isLedOn).catch((err) => console.error(err));
});

connection.onreconnecting(() => setStatus("● Reconnecting...", ""));
connection.onreconnected(() => setStatus("● SignalR Online", "online"));
connection.onclose(() => setStatus("○ Disconnected", "offline"));

connection.start()
    .then(() => setStatus("● SignalR Online", "online"))
    .catch(() => setStatus("○ Disconnected", "offline"));
