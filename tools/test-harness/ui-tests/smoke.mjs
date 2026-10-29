// Opens every page of the web app and fails on an HTTP error, a red error pane or a JavaScript error.
// Run against the harness (run_local.py), or against any web app: WEB=http://127.0.0.1:5099 node smoke.mjs
import { chromium } from 'playwright';

const web = process.env.WEB ?? 'http://127.0.0.1:5099';
const pages = ['/', '/?archived=1', '/?sort=fit', '/applications', '/applications?closed=1', '/applications?q=net',
  '/acquire', '/runs', '/runs?empty=1', '/chat'];

const browser = await chromium.launch(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {});
const page = await browser.newPage();
const scriptErrors = [];
page.on('pageerror', e => scriptErrors.push(`${page.url()}: ${e.message}`));
let failures = 0;

// The newest run, if any, gets its three tabs checked too.
await page.goto(web + '/runs?empty=1');
const firstRun = await page.locator('table.runs tbody tr[data-href]').first().getAttribute('data-href').catch(() => null);
if (firstRun) {
  pages.push(firstRun, firstRun + '?tab=run', firstRun + '?tab=links');
}

for (const path of pages) {
  const response = await page.goto(web + path);
  const status = response?.status() ?? 0;
  const errorPane = await page.locator('.error-pane').count();
  const ok = status === 200 && errorPane === 0;
  if (!ok) {
    failures++;
  }
  console.log(`${ok ? 'ok  ' : 'FAIL'} ${status} ${path}${errorPane ? ' (error pane shown)' : ''}`);
}
for (const e of scriptErrors) {
  console.log(`FAIL JavaScript error ${e}`);
}
await browser.close();
process.exit(failures + scriptErrors.length === 0 ? 0 : 1);
