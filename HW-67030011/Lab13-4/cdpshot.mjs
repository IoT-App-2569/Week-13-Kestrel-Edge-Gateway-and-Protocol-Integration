// usage: node cdpshot.mjs URL OUTDIR COUNT INTERVAL_MS [W H]
// Opens URL in headless Chrome, keeps the page live, saves COUNT screenshots
// (2x DPR) every INTERVAL_MS plus a JSON line of the page's key values per shot.
import { spawn } from "node:child_process";
import { mkdirSync, writeFileSync, mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const [url, out, count = "10", interval = "3000", W = "1280", H = "720"] = process.argv.slice(2);
mkdirSync(out, { recursive: true });
const CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
const port = 9333;
const chrome = spawn(CHROME, ["--headless=new", `--remote-debugging-port=${port}`, "--hide-scrollbars",
  `--user-data-dir=${mkdtempSync(join(tmpdir(), "cdp-"))}`, `--window-size=${W},${H}`, "about:blank"], { stdio: "ignore" });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

let targets;
for (let i = 0; i < 50; i++) {
  try { targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); break; } catch { await sleep(200); }
}
const page = targets.find((t) => t.type === "page");
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener("open", r));
let id = 0; const pending = new Map();
ws.addEventListener("message", (e) => { const m = JSON.parse(e.data); if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); } });
const send = (method, params = {}) => new Promise((r) => { const i = ++id; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });

await send("Emulation.setDeviceMetricsOverride", { width: +W, height: +H, deviceScaleFactor: 2, mobile: false });
await send("Page.enable");
await send("Page.navigate", { url });
await sleep(2500);
// optional: CDP_ACTION=<js> runs in the page once before the first shot (e.g. "toggleLed()")
if (process.env.CDP_ACTION) {
  await send("Runtime.evaluate", { expression: process.env.CDP_ACTION, awaitPromise: true });
  await sleep(+(process.env.CDP_ACTION_WAIT || 1500));
}
const probe = `JSON.stringify({t:new Date().toLocaleTimeString('en-GB'),v:document.getElementById('voltageText')?.innerText,raw:document.getElementById('rawText')?.innerText,led:document.getElementById('ledBtn')?.innerText,s:document.getElementById('mSerial')?.innerText,rest:document.getElementById('mRest')?.innerText,m:document.getElementById('mMqtt')?.innerText,r:document.getElementById('mSignalR')?.innerText,conn:document.getElementById('connStatus')?.innerText})`;
for (let n = 1; n <= +count; n++) {
  const shot = await send("Page.captureScreenshot", { format: "png" });
  const name = `shot_${String(n).padStart(2, "0")}`;
  writeFileSync(join(out, name + ".png"), Buffer.from(shot.result.data, "base64"));
  const ev = await send("Runtime.evaluate", { expression: probe, returnByValue: true });
  console.log(name, ev.result.result.value);
  if (n < +count) await sleep(+interval);
}
ws.close(); chrome.kill();
