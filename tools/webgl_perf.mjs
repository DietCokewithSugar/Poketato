// WebGL performance harness for the Unity build.
//  1. Cold-load timing (fresh browser context each run, optional network throttling via CDP).
//  2. In-game benchmark (?bench=1): 10-species showcase, 60 and 150 enemies, for each quality tier.
// Results: build/perf/<renderer>/results.json (+ a markdown summary on stdout).
//
// Usage: node tools/webgl_perf.mjs [--url https://localhost:8443/] [--renderer swiftshader|llvmpipe]
//                                  [--secs 8] [--tiers 0,1,2] [--loads 3] [--size 1280x720] [--skip-bench]
import { chromium } from 'playwright-core';
import fs from 'node:fs';
import path from 'node:path';

const arg = (k, d) => {
  const i = process.argv.indexOf('--' + k);
  return i >= 0 ? (process.argv[i + 1] && !process.argv[i + 1].startsWith('--') ? process.argv[i + 1] : true) : d;
};
const base = arg('url', 'https://localhost:8443/');
const renderer = arg('renderer', 'swiftshader');
const secs = Number(arg('secs', 8));
const tiers = String(arg('tiers', '0,1,2'));
const loads = Number(arg('loads', 3));
const [vw, vh] = String(arg('size', '1280x720')).split('x').map(Number);
const outDir = path.join('build', 'perf', renderer);
fs.mkdirSync(outDir, { recursive: true });

const rendererArgs = {
  swiftshader: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'],
  llvmpipe: ['--use-gl=angle', '--use-angle=gl', '--ignore-gpu-blocklist', '--enable-gpu-rasterization'],
}[renderer];

async function launch() {
  return chromium.launch({
    executablePath: process.env.CHROME || '/usr/local/bin/google-chrome',
    headless: !process.env.HEADED,
    args: [...rendererArgs, '--ignore-certificate-errors', '--enable-webgl', '--disable-background-timer-throttling', '--disable-renderer-backgrounding'],
  });
}

const throttles = [
  { name: 'local', down: 0 },
  { name: '20Mbps-40ms', down: 20, rtt: 40 },
  { name: '5Mbps-80ms', down: 5, rtt: 80 },
];

async function coldLoad(browser, t) {
  const ctx = await browser.newContext({ viewport: { width: vw, height: vh }, ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  if (t.down) {
    const cdp = await ctx.newCDPSession(page);
    await cdp.send('Network.enable');
    await cdp.send('Network.emulateNetworkConditions', {
      offline: false, latency: t.rtt, downloadThroughput: (t.down * 1e6) / 8, uploadThroughput: (5 * 1e6) / 8,
    });
  }
  await page.goto(base + '?q=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => window.__poketato && window.__poketato.marks && (window.__poketato.marks.firstFrame || window.__poketato.error), null, { timeout: 600000 });
  const r = await page.evaluate(() => {
    const res = performance.getEntriesByType('resource').filter((e) => e.name.includes('/Build/'));
    return {
      marks: window.__poketato.marks,
      bytes: Object.fromEntries(res.map((e) => [e.name.split('/').pop(), { transfer: e.transferSize, encoded: e.encodedBodySize, decoded: e.decodedBodySize, ms: Math.round(e.duration) }])),
      renderer: (() => {
        const gl = document.createElement('canvas').getContext('webgl2');
        const d = gl && gl.getExtension('WEBGL_debug_renderer_info');
        return d ? gl.getParameter(d.UNMASKED_RENDERER_WEBGL) : 'n/a';
      })(),
    };
  });
  await ctx.close();
  return r;
}

const results = { url: base, renderer, viewport: `${vw}x${vh}`, date: new Date().toISOString(), loads: {}, bench: [] };
let browser = await launch();
for (const t of throttles) {
  const n = t.name === 'local' ? loads : 1;
  results.loads[t.name] = [];
  for (let i = 0; i < n; i++) {
    const r = await coldLoad(browser, t);
    results.loads[t.name].push(r);
    console.log(`load ${t.name} #${i + 1}: instanceReady ${r.marks.instanceReady?.toFixed(0)} ms, firstFrame ${r.marks.firstFrame?.toFixed(0)} ms (${r.renderer})`);
  }
}

if (!arg('skip-bench')) {
  const ctx = await browser.newContext({ viewport: { width: vw, height: vh }, ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  page.on('console', (m) => {
    const s = m.text();
    if (s.startsWith('POKETATO_BENCH ')) console.log('  ' + s.slice(15, 220));
  });
  const nTiers = tiers.split(',').length;
  await page.goto(`${base}?bench=1&secs=${secs}&tiers=${tiers}`);
  await page.waitForFunction((n) => window.__poketato && window.__poketato.reports && window.__poketato.reports.length >= n,
    nTiers * 3, { timeout: 60 * 60 * 1000, polling: 2000 });
  results.bench = await page.evaluate(() => window.__poketato.reports);
  await page.screenshot({ path: path.join(outDir, 'bench-final.png') });
  await ctx.close();
}
await browser.close();
fs.writeFileSync(path.join(outDir, 'results.json'), JSON.stringify(results, null, 2));

const f = (v, d = 1) => (v === undefined || v === null || v < 0 ? 'n/a' : Number(v).toFixed(d));
console.log(`\n### Load (${renderer}, ${vw}x${vh})`);
console.log('| network | instance ready (ms) | first frame (ms) |');
console.log('|---|---|---|');
for (const [k, arr] of Object.entries(results.loads)) {
  const med = (key) => arr.map((r) => r.marks[key]).sort((a, b) => a - b)[Math.floor(arr.length / 2)];
  console.log(`| ${k} | ${f(med('instanceReady'), 0)} | ${f(med('firstFrame'), 0)} |`);
}
if (results.bench.length) {
  console.log(`\n### Benchmark (${renderer}, ${vw}x${vh}, ${secs}s per run)`);
  console.log('| quality | scenario | avg FPS | avg ms | p95 ms | p99 ms | max ms | >50ms frames | main-thread CPU ms (p95) | sim ms | sprites | draw calls | tris | heap MB |');
  console.log('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|');
  for (const r of results.bench) {
    console.log(`| ${r.quality} | ${r.scenario} | ${f(r.avgFps)} | ${f(r.avgMs)} | ${f(r.p95Ms)} | ${f(r.p99Ms)} | ${f(r.maxMs, 0)} | ${r.hitches50} | ${f(r.cpuMs)} (${f(r.cpuP95Ms)}) | ${f(r.simMs, 2)} | ${r.actors} | ${r.drawCalls} | ${r.triangles} | ${r.heapMB} |`);
  }
}
