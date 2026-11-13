const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const {chromium}=require('playwright'),assert=require('assert/strict'),fs=require('fs'),path=require('path');
const checks=[],pass=(name,detail)=>{checks.push({name,result:'pass',detail});console.log('PASS',name)};
(async()=>{
 const browser=await reviewBrowser();
 const context=await browser.newContext({viewport:{width:1512,height:1450},hasTouch:true}),p=await context.newPage(),errors=[],failed=[];
 p.on('pageerror',e=>errors.push(e.message));p.on('response',r=>{if(r.status()>=400)failed.push(r.url())});
 const state=()=>p.evaluate(()=>window.KNResultsSnapshot());
 const open=async kind=>{await p.locator('[data-screen="results"]').click();await p.selectOption('#result-state',kind)};
 await p.goto(reviewURL('/?view=results'));await p.waitForFunction(()=>typeof window.KNResultsSnapshot==='function');
 await p.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth));
 const growth=await p.evaluate(()=>window.KNGrowthSnapshot()),echo=await p.evaluate(()=>window.KNUIReviewSnapshot().echo);
 for(const [kind,gold,candy,next] of [['clear',86,3,5],['repeat',86,3,4],['boss',180,10,11],['fail',15,0,4],['stepback',15,0,3]]){
  await open(kind);const s=await state();assert.equal(s.receipt.gold,gold);assert.equal(s.receipt.candy,candy);assert.equal(s.receiptsForEvent,1);
  await p.locator('#rs-details').click();await p.locator('#rs-details').click();assert.deepEqual((await state()).receipt,s.receipt);
  await p.selectOption('#ratio','short');assert.deepEqual((await state()).receipt,s.receipt);await p.selectOption('#ratio','tall');
  const count=(await state()).transitions;
  await p.evaluate(()=>{const handler=document.querySelector('#rs-next').onclick;handler();handler()});
  assert.equal((await state()).destination,next);assert.equal((await state()).transitions,count+1);
  assert.equal(await p.locator('.result-view').count(),0);
 }
 assert.deepEqual(await p.evaluate(()=>window.KNGrowthSnapshot()),growth);assert.equal(await p.evaluate(()=>window.KNUIReviewSnapshot().echo),echo);
 pass('Five source-based reward/route fixtures; redraw and duplicate advance do not settle twice or change growth wallet');
 await open('retreat');assert.equal((await state()).receipt.gold,null);assert.match(await p.locator('.rs-card').innerText(),/보상 안내 준비 중/);
 await p.locator('#rs-details').click();assert.doesNotMatch(await p.locator('.rs-detail').innerText(),/이미 반영/);
 await p.locator('#rs-next').click();assert.match(await p.locator('.cause-label').innerText(),/퇴각/);
 pass('Retreat shows undecided rewards and leads to existing retreat report');
 for(const [selector,target] of [['#rs-formation','[data-slot]'],['#rs-growth','.growth-panel']]){
  await open('fail');await p.locator(selector).click();assert.ok(await p.locator(target).count()>0);
 }
 pass('Failure recovery routes open existing formation and growth');
 for(const kind of ['clear','fail']){
  await p.locator('[data-screen="home"]').click();await p.selectOption('#alert-state',kind);await p.locator('#open-sheet').click();await p.locator('.sheet-alert').click();assert.equal((await state()).kind,kind);assert.equal(await p.locator('.result-view').count(),1);
 }
 pass('Combat clear/fail alerts connect to results without leaving a growth sheet');
 await open('clear');assert.equal(await p.locator('#rs-next').evaluate(e=>e===document.activeElement),true);
 const background=await p.locator('#lamp').evaluate(e=>{e.focus();return {inert:e.closest('[inert]')!==null,focused:e===document.activeElement}});assert.equal(background.inert,true);assert.equal(background.focused,false);
 const buttons=p.locator('.result-view button');await buttons.last().focus();await p.keyboard.press('Tab');assert.equal(await buttons.first().evaluate(e=>e===document.activeElement),true);
 await p.keyboard.press('Shift+Tab');assert.equal(await buttons.last().evaluate(e=>e===document.activeElement),true);
 pass('Result blocks underlying summon/investment controls and cycles keyboard focus');
 for(const ratio of ['tall','short']){
  await p.selectOption('#ratio',ratio);
  for(const kind of ['clear','repeat','boss','fail','stepback','retreat']){
   await open(kind);await p.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth));
   const issues=await p.locator('.result-view').evaluate(el=>{const r=el.getBoundingClientRect(),scale=r.width/390;return [...el.querySelectorAll('button')].flatMap(b=>{const q=b.getBoundingClientRect();return q.width/scale<43.9||q.height/scale<43.9||q.left<r.left-.1||q.right>r.right+.1?['bad target '+b.id]:[]})});assert.deepEqual(issues,[]);
   await p.locator('.screen').screenshot({path:outputPath(`result-${kind}-${ratio}-05.png`)});
   await p.locator('#rs-details').click();assert.equal(await p.locator('.rs-detail').isVisible(),true);
   const detailFits=await p.locator('.rs-detail').evaluate(el=>{const r=el.getBoundingClientRect(),scroll=el.closest('.rs-scroll').getBoundingClientRect();return r.bottom<=scroll.bottom+1});assert.equal(detailFits,true);
   const fixed=await p.locator('#rs-next').evaluate(b=>{const q=b.getBoundingClientRect(),r=b.closest('.result-view').getBoundingClientRect();return q.top>=r.top&&q.bottom<=r.bottom});assert.equal(fixed,true);
  }
 }
 pass('Six states × two aspect ratios: 44px targets, scrollable details and fixed next action');
 await p.selectOption('#ratio','tall');await p.setViewportSize({width:390,height:844});await open('clear');
 await p.locator('[data-reward="단추"]').scrollIntoViewIfNeeded();const cdp=await context.newCDPSession(p),box=await p.locator('[data-reward="단추"]').boundingBox();
 await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:box.x+box.width/2,y:box.y+box.height/2}]});await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
 assert.equal((await state()).details,true);assert.equal(await p.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
 pass('390px browser touch opens reward details without horizontal overflow');
 await p.setViewportSize({width:1512,height:1450});await open('clear');await p.locator('#result-play').check();let s=await state();assert.equal(s.deadline-s.started,1500);
 await p.waitForFunction(()=>window.KNResultsSnapshot().transitioned);assert.equal((await state()).destination,5);assert.equal((await state()).playing,false);
 pass('Automatic clear advances after 1.5 seconds of real time');
 await open('repeat');s=await state();await p.locator('#rs-details').click();const limit=(await state()).deadline;
 assert.equal(limit-s.started,6000);await p.locator('#rs-details').click();await p.waitForTimeout(180);await p.locator('#rs-details').click();assert.equal((await state()).deadline,limit);
 await p.waitForTimeout(1600);assert.equal(await p.locator('.result-view').count(),1);
 await p.waitForFunction(()=>window.KNResultsSnapshot().transitioned,{},{timeout:6500});assert.equal((await state()).destination,4);
 pass('Details extend to original end +6 seconds; repeated clicks cannot extend indefinitely');
 await open('boss');await p.locator('[data-screen="growth"]').click();const left=(await state()).transitions;await p.waitForTimeout(1650);assert.equal((await state()).transitions,left);assert.equal((await state()).playing,false);
 pass('Leaving results cancels pending automatic transition');
 assert.deepEqual(errors,[]);assert.deepEqual(failed,[]);await browser.close();
 fs.writeFileSync(outputPath('results-verification.json'),JSON.stringify({date:'2026-11-12',scope:'Browser art review and deterministic result fixtures; not Unity, economy, save, drop RNG or real combat.',checks,errors,failed},null,2));
})().catch(e=>{console.error(e);process.exit(1)});
