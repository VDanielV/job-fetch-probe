// The Acquire page from start to finish, on the harness (run_local.py with a NEW data folder): press Start, follow
// the progress, wait for the end, then check the finish banner, the Summary (with its Fit row) and the Runs page.
// Screenshots go to the folder in SHOTS (default: none).   WEB=http://127.0.0.1:5099 node acquire.mjs
import { chromium } from 'playwright';

const web = process.env.WEB ?? 'http://127.0.0.1:5099';
const shots = process.env.SHOTS;
const browser = await chromium.launch(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {});
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const scriptErrors = [];
page.on('pageerror', e => scriptErrors.push(e.message));
const text = async selector => (await page.locator(selector).allInnerTexts()).join(' | ').replace(/\s+/g, ' ').trim();
const shot = async name => { if (shots) await page.screenshot({ path: `${shots}/${name}.png`, fullPage: true }); };
const checks = [];
const check = (name, ok, detail = '') => { checks.push(ok); console.log(`${ok ? 'ok  ' : 'FAIL'} ${name}${detail ? ' — ' + detail : ''}`); };

await page.goto(web + '/acquire');
check('idle page shows Start', (await page.locator('button.start:not([disabled])').count()) === 1);

await Promise.all([page.waitForNavigation(), page.click('button.start')]);
check('pressing Start follows the run', page.url().includes('since='), page.url());
await shot('1-started');

// The page reloads itself every 15 s while the run goes on; wait until it stops reloading (at most 10 minutes).
for (let i = 0; i < 120 && (await page.locator('meta[http-equiv=refresh]').count()) > 0; i++) {
  await page.waitForTimeout(5000);
  await page.reload();
}
const banner = await text('.pp-banner');
check('the run ended with a banner', banner.length > 0, banner);
check('the finish line counts the fits', banner.includes('Fit scored for'), banner);
check('Summary is the tab shown at the end', (await text('.tabs a.on')) === 'Summary');
const summary = await text('.summary-list');
check('Summary has new and known counts', /\d+ new, \d+ already known/.test(summary));
check('Summary has a Fit row', summary.includes('scored'));
await shot('2-summary');

await page.click('.tabs a >> nth=1');
check('Whole run lists step 2 lines', (await page.locator('.wholerun tr, table tr').count()) > 3);
await shot('3-whole-run');

await page.goto(web + '/runs');
const firstRow = await text('table.runs tbody tr >> nth=0');
check('Runs lists this run first', firstRow.length > 0, firstRow);
await Promise.all([page.waitForNavigation(), page.locator('table.runs tbody tr >> nth=0').click()]);
await page.goto(page.url() + '?tab=links');
check('Job links shows the run\'s links', (await page.locator('.run-links-table tbody tr').count()) > 0);
await shot('4-job-links');

check('no JavaScript errors', scriptErrors.length === 0, scriptErrors.join('; '));
await browser.close();
process.exit(checks.every(Boolean) ? 0 : 1);
