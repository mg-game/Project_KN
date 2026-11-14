const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const assert=require('node:assert/strict'),fs=require('node:fs');
const checks=[],pass=name=>{checks.push({name,result:'pass'});console.log('PASS',name)};
(async()=>{
 const b=await reviewBrowser(),p=await b.newPage({viewport:{width:1512,height:1450}}),errors=[];p.on('pageerror',e=>errors.push(e.message));
 const focusIs=selector=>p.locator(selector).evaluate(el=>el===document.activeElement);
 const open=async view=>{await p.goto(reviewURL('/?view='+view));await p.waitForFunction(()=>typeof window.KNGrowthSnapshot==='function')};
 for(const ratio of ['tall','short']){
  await open('home');await p.selectOption('#ratio',ratio);await p.locator('[data-nav="0"]').focus();await p.keyboard.press('Enter');assert(await focusIs('.gr-close'));await p.keyboard.press('Tab');assert(await focusIs('#gr-shop-one'));await p.locator('.gr-close').click();assert(await focusIs('[data-nav="0"]'));
 }
 pass('Both ratios: keyboard growth entry focuses the sheet, Tab reaches upgrades and close returns to growth navigation');
 await p.locator('#open-sheet').click();assert(await focusIs('.gr-close'));await p.locator('.gr-close').click();assert(await focusIs('#open-sheet'));await p.keyboard.press('Enter');assert(await focusIs('.gr-close'));await p.keyboard.press('Escape');assert(await focusIs('#open-sheet'));
 pass('Toolbar entry returns focus to its live trigger after either click or Escape closure');
 await open('failure');await p.locator('#strengthen').focus();await p.keyboard.press('Enter');assert(await focusIs('.gr-close'));assert.equal(await p.locator('.failure-modal').count(),0);await p.keyboard.press('Escape');assert(await focusIs('[data-nav="0"]'));await p.keyboard.press('Enter');assert(await focusIs('.gr-close'));
 pass('Failure strengthen starts in the sheet and closes to usable growth navigation after its old trigger is removed');
 await open('growth');assert(await focusIs('.gr-close'));await p.locator('[data-detail="4"]').click();assert(await focusIs('#gr-back'));await p.locator('#gr-back').click();assert(await focusIs('[data-detail="4"]'));await p.locator('.gr-close').focus();await p.keyboard.down('Enter');await p.keyboard.down('Enter');await p.keyboard.down('Enter');assert.equal(await p.locator('.growth-panel').count(),0);assert(await focusIs('[data-nav="0"]'));await p.keyboard.up('Enter');await p.keyboard.press('Enter');assert(await focusIs('.gr-close'));
 pass('Direct growth/detail return preserves focus; held Enter cannot reopen the sheet while a fresh press can');
 await p.locator('[data-detail="0"]').click();await p.locator('#gr-resonate').click();await p.keyboard.press('Escape');assert(await focusIs('#gr-resonate'));assert.equal(await p.locator('.growth-panel').count(),1);await p.keyboard.press('Escape');assert(await focusIs('[data-nav="0"]'));assert.equal(await p.evaluate(()=>window.KNGrowthSnapshot().fragments),36);
 pass('Resonance cancellation returns to its own trigger before sheet closure, with no fragment spending');
 await p.locator('#open-sheet').click();await p.locator('#ratio').focus();await p.selectOption('#ratio','tall');assert(await focusIs('#ratio'));assert.equal(await p.locator('.growth-panel').count(),1);assert.equal(await p.locator('.growth-panel').getAttribute('role'),'region');await p.locator('[data-nav="1"]').click();assert.equal(await p.locator('.growth-panel').count(),0);assert.equal(await p.locator('#back-home').count(),1);
 pass('Review-control redraw does not steal focus and the nonmodal sheet keeps bottom navigation available');
 assert.deepEqual(errors,[]);await b.close();fs.writeFileSync(outputPath('sheet-focus-verification.json'),JSON.stringify({date:new Date().toISOString(),scope:'Native Chromium focus and keyboard navigation in the browser review, two reference ratios. Not Unity or physical-device certification.',checks,errors},null,2)+'\n');
})().catch(e=>{console.error(e);process.exit(1)});
