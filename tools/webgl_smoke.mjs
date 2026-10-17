// Quick check that the WebGL build boots: loads the page, waits for the Unity instance, takes screenshots,
// prints console errors. Usage: node tools/webgl_smoke.mjs [url] [outDir] [waitSeconds]
import { chromium } from 'playwright-core';
import fs from 'node:fs';

const url = process.argv[2] || 'https://localhost:8443/';
const outDir = process.argv[3] || 'build/smoke';
const waitS = Number(process.argv[4] || 8);
fs.mkdirSync(outDir, { recursive: true });

const browser = await chromium.launch({
  executablePath: process.env.CHROME || '/usr/local/bin/google-chrome',
  headless: process.env.HEADED ? false : true,
  args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--ignore-certificate-errors', '--enable-webgl'],
});
const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, ignoreHTTPSErrors: true });
const logs = [];
page.on('console', (m) => logs.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', (e) => logs.push(`[pageerror] ${e.message}`));
const t0 = Date.now();
await page.goto(url);
await page.waitForFunction(() => window.__poketato && (window.__poketato.marks.instanceReady || window.__poketato.error), null, { timeout: 180000 });
const marks = await page.evaluate(() => window.__poketato);
console.log('ready after', ((Date.now() - t0) / 1000).toFixed(1), 's', JSON.stringify(marks.marks), marks.error || '');
for (let i = 0; i < 3; i++) {
  await page.waitForTimeout((waitS * 1000) / 3);
  await page.screenshot({ path: `${outDir}/shot${i}.png` });
}
const gl = await page.evaluate(() => {
  const c = document.createElement('canvas').getContext('webgl2');
  const d = c && c.getExtension('WEBGL_debug_renderer_info');
  return d ? c.getParameter(d.UNMASKED_RENDERER_WEBGL) : 'n/a';
});
console.log('renderer:', gl);
console.log(logs.filter((l) => /error|exception|warn/i.test(l)).slice(0, 40).join('\n'));
console.log('--- last logs ---\n' + logs.slice(-15).join('\n'));
await browser.close();
