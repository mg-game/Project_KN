const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const {chromium}=require('playwright');const assert=require('assert/strict'),fs=require('fs'),path=require('path');
const root=__dirname,checks=[];const pass=(name,detail)=>{checks.push({name,result:'pass',detail});console.log('PASS',name)};
async function main(){
 const browser=await reviewBrowser();
 const context=await browser.newContext({viewport:{width:1440,height:1400},deviceScaleFactor:2,hasTouch:true}),p=await context.newPage(),errors=[];
 p.on('pageerror',e=>errors.push(e.message));const state=()=>p.evaluate(()=>window.KNGrowthSnapshot());
 const open=async()=>{await p.goto(reviewURL('/'));await p.waitForFunction(()=>typeof window.KNGrowthSnapshot==='function');await p.locator('[data-screen="growth"]').click();await p.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0));};
 const fixture=async name=>p.selectOption('#growth-state',name);
 await open();assert.equal(await p.locator('.gr-lineage').count(),5);assert.equal(await p.locator('.gr-resonance-dot:visible').count(),5);
 const initial=await state();const order=await p.locator('[data-lineage]').evaluateAll(xs=>xs.map(x=>x.dataset.lineage));
 await p.locator('[data-level-up="0"]').click();let s=await state();assert.equal(s.wallet,initial.wallet-129);assert.equal(s.levels[0],13);
 assert.deepEqual(await p.locator('[data-lineage]').evaluateAll(xs=>xs.map(x=>x.dataset.lineage)),order);
 await p.locator('[data-detail="0"]').click();assert.match(await p.locator('[data-detail-level]').innerText(),/13/);await p.locator('#gr-back').click();assert.equal(await p.locator('[data-detail="0"]').evaluate(el=>el===document.activeElement),true);
 pass('Shared lineage level, single cost deduction, stable card order and focus return');
 await fixture('partial');await p.locator('[data-detail="0"]').click();assert.match(await p.locator('#gr-detail-ten').innerText(),/7/);await p.locator('#gr-detail-ten').click();s=await state();assert.equal(s.levels[0],19);assert.equal(s.wallet,0);pass('x10 shrinks to seven affordable levels and consumes displayed total');
 for(const mode of ['poor','locked','max']){
  await fixture(mode);const before=await state();const button=p.locator('#gr-detail-one');assert.equal(await button.getAttribute('aria-disabled'),'true');await button.click({force:true});const after=await state();assert.equal(after.wallet,before.wallet);assert.deepEqual(after.levels,before.levels);
 }
 await p.locator('.screen').screenshot({path:outputPath('growth-maximum-04.png')});pass('Insufficient funds, lock and maximum block level purchases');
 await fixture('normal');await p.locator('#gr-back').click();await p.locator('[data-detail="4"]').click();const beforeUnassigned=await state();await p.locator('#gr-detail-one').click();assert.equal((await state()).levels[4],beforeUnassigned.levels[4]+1);await p.locator('#gr-back').click();assert.equal(await p.locator('[data-power="4"]').innerText(),'미편성');pass('Unassigned character can grow and keeps unassigned label');
 await fixture('normal');await p.locator('[data-detail="0"]').click();await p.locator('#gr-resonate').click();let before=await state();assert.equal(before.confirming,true);assert.equal(before.fragments,36);
 assert.equal(await p.locator('.screen').evaluate(el=>[...el.children].filter(x=>!x.classList.contains('growth-confirm-overlay')).every(x=>x.inert)),true);
 for(let i=0;i<5;i++){await p.keyboard.press('Tab');assert.equal(await p.locator('.growth-confirm').evaluate(el=>el.contains(document.activeElement)),true);}
 let b=await p.locator('.growth-confirm-overlay').boundingBox();await p.mouse.click(b.x+3,b.y+3);assert.equal((await state()).confirming,true);await p.keyboard.press('Escape');assert.equal((await state()).fragments,36);assert.equal((await state()).resonances[0],2);
 await p.locator('#gr-resonate').click();await p.locator('.screen').screenshot({path:outputPath('growth-confirm-04.png')});await p.locator('#gr-confirm-yes').evaluate(el=>{el.click();el.click()});s=await state();assert.equal(s.fragments,16);assert.equal(s.resonances[0],3);
 assert.match(await p.locator('[data-res-cost]').innerText(),/4/);assert.equal(await p.locator('#gr-resonate').evaluate(el=>el===document.activeElement),true);pass('Resonance: confirm-only single spending, cancel preservation, modal focus and updated effect');
 for(const mode of ['fragments','resonance-max']){await fixture(mode);const before=await state();await p.locator('#gr-resonate').click({force:true});assert.equal((await state()).confirming,false);assert.equal((await state()).fragments,before.fragments);assert.deepEqual((await state()).resonances,before.resonances);}
 await fixture('resonance-floor');assert.match(await p.locator('[data-res-cost]').innerText(),/3.*3/);pass('Fragment shortage and resonance maximum are blocked; summon cost preview respects floor three');
 await fixture('normal');await p.locator('#gr-back').click();const levelButton=p.locator('[data-level-up="0"]');await levelButton.scrollIntoViewIfNeeded();await p.locator('.gr-scroll').evaluate(el=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));let box=await levelButton.boundingBox();
 await p.mouse.move(box.x+box.width/2,box.y+box.height/2);await p.mouse.down();await p.waitForTimeout(780);const during=await state();assert(during.levels[0]>=14);await p.locator('.gr-scroll').evaluate(el=>el.scrollTop+=50);const stopped=await state();await p.waitForTimeout(350);assert.deepEqual((await state()).levels,stopped.levels);await p.mouse.up();pass('0.45s hold repeats; scroll stops further purchases without moving card order');
 await fixture('normal');const preShop=await state();await p.locator('#gr-shop-ten').click();const postShop=await state();assert.equal(postShop.shopLevel,preShop.shopLevel+10);assert(postShop.wallet<preShop.wallet);assert.match(await p.locator('[data-milestone]').innerText(),/30/);assert.match(await p.locator('[data-income]').innerText(),/19.4/);pass('Shop batch purchase and next-ten-level milestone preview update together');
 for(const ratio of ['tall','short']){
  await fixture('normal');await p.selectOption('#ratio',ratio);await p.locator('.gr-scroll').evaluate(el=>el.scrollTop=0);
  await p.locator('.screen').screenshot({path:outputPath('growth-list-'+ratio+'-04.png')});await p.locator('[data-detail="0"]').click();await p.locator('#gr-resonate').scrollIntoViewIfNeeded();
  await p.locator('.screen').screenshot({path:outputPath('growth-detail-'+ratio+'-04.png')});await p.locator('#gr-resonate').click();
  const outside=await p.locator('.growth-confirm-overlay').evaluate(el=>{const r=el.getBoundingClientRect();return [...el.querySelectorAll('button')].some(b=>{const q=b.getBoundingClientRect();return q.top<r.top||q.bottom>r.bottom||q.left<r.left||q.right>r.right})});assert.equal(outside,false);
  await p.locator('.screen').screenshot({path:outputPath('growth-confirm-'+ratio+'-04.png')});await p.locator('#gr-confirm-no').click();await p.locator('#gr-back').click();
 }
 pass('Two aspect ratios: scrolling list/detail and confirmation controls fit within screen');
 await p.setViewportSize({width:390,height:844});await fixture('normal');const touch=await context.newCDPSession(p);await p.locator('[data-level-up="0"]').scrollIntoViewIfNeeded();box=await p.locator('[data-level-up="0"]').boundingBox();const point={x:box.x+box.width/2,y:box.y+box.height/2},beforeTouch=await state();
 await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[point]});await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});assert.equal((await state()).levels[0],beforeTouch.levels[0]+1);assert.equal(await p.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
 pass('390px browser touch: level tap spends once and page has no horizontal overflow');
 assert.deepEqual(errors,[]);await browser.close();fs.writeFileSync(outputPath('growth-verification.json'),JSON.stringify({date:'2026-11-12',scope:'Growth art UI review fixtures and browser interactions, not balance or Unity tests.',checks,errors},null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
