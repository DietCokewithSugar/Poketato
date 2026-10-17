// Capture showcase screenshots from the WebGL build (photo mode, UI hidden unless noted).
// Usage: node tools/webgl_shots.mjs [baseUrl] [outDir] [waitSeconds]
import { chromium } from 'playwright-core';
import fs from 'node:fs';

const base = process.argv[2] || 'https://localhost:8443/';
const outDir = process.argv[3] || 'build/shots';
const wait = Number(process.argv[4] || 20) * 1000;
fs.mkdirSync(outDir, { recursive: true });

const shots = process.env.ONLY ? [{ name: 'only', q: process.env.ONLY }] : [
  { name: '01-title', q: '' },
  { name: '02-map', q: 'photo=2&ui=0&start=pikachu' },
  { name: '03-battle', q: 'start=charmander&auto=1' },
];

const browser = await chromium.launch({
  executablePath: process.env.CHROME || '/usr/local/bin/google-chrome',
  headless: !process.env.HEADED,
  args: (process.env.RENDERER !== 'swiftshader'
    ? ['--use-gl=angle', '--use-angle=gl', '--ignore-gpu-blocklist']
    : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader']).concat(['--ignore-certificate-errors', '--enable-webgl']),
});
for (const s of shots) {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, ignoreHTTPSErrors: true });
  await page.goto(`${base}?${s.q}`);
  await page.waitForFunction(() => window.__poketato && window.__poketato.marks.firstFrame, null, { timeout: 180000 });
  await page.waitForTimeout(wait);
  await page.screenshot({ path: `${outDir}/${s.name}.png`, timeout: 180000 });
  console.log('saved', s.name);
  await page.close();
}
await browser.close();
