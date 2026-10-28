// วัด Round-trip ของเส้นทาง SignalR บนหน้าเว็บจริง: invoke("ToggleLed") -> รับ LedStateUpdated กลับมา
// วางใน DevTools Console ของหน้า http://localhost:5080 (ใช้ตัวแปร connection / isLedOn ของ index.html)
(async () => {
  const rounds = 20, rtts = [];
  for (let i = 0; i < rounds; i++) {
    const target = !isLedOn;
    const got = new Promise((resolve) => {
      const h = (s) => { if (s === target) { connection.off("LedStateUpdated", h); resolve(); } };
      connection.on("LedStateUpdated", h);
    });
    const t0 = performance.now();
    await Promise.all([connection.invoke("ToggleLed", target), got]);
    rtts.push(performance.now() - t0);
    await new Promise((r) => setTimeout(r, 150));
  }
  rtts.sort((a, b) => a - b);
  const mean = rtts.reduce((a, b) => a + b, 0) / rounds;
  const median = (rtts[rounds / 2 - 1] + rtts[rounds / 2]) / 2;
  return `SignalR invoke->LedStateUpdated  n=${rounds}  min=${rtts[0].toFixed(2)}  median=${median.toFixed(2)}  ` +
    `mean=${mean.toFixed(2)}  max=${rtts[rounds - 1].toFixed(2)} ms`;
})()
