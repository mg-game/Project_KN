const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');const root=__dirname,checks=[];
const pass=(name,detail)=>{checks.push({name,result:'pass',detail});console.log('PASS',name)};
async function main(){
 const browser=await reviewBrowser();
 const context=await browser.newContext({viewport:{width:1440,height:1400},deviceScaleFactor:2,hasTouch:true});
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 const snapshot=()=>page.evaluate(()=>window.KNUIReviewSnapshot());
 const load=async()=>{await page.goto(reviewURL('/'));await page.waitForFunction(()=>typeof window.KNUIReviewSnapshot==='function'&&[...document.images].every(i=>i.complete&&i.naturalWidth>0));};
 const center=async locator=>{const r=await locator.boundingBox();return{x:r.x+r.width/2,y:r.y+r.height/2}};
 await load();await page.locator('[data-summon="0"]').click();assert.equal((await snapshot()).echo,32);
 await page.locator('[data-summon="0"]').focus();await page.keyboard.press('Enter');assert.equal((await snapshot()).echo,27);assert.equal(await page.locator('[data-summon="0"]').evaluate(el=>el===document.activeElement),true);
 pass('Tap and keyboard activation: spend once and retain keyboard focus');
 for(const sector of [0,1,4,6]){
  await load();const start=await center(page.locator('[data-summon="0"]')),r=await page.locator('.battle-ring').boundingBox();
  const angle=sector*Math.PI/4,target={x:r.x+r.width/2+Math.sin(angle)*r.width*.3,y:r.y+r.height/2-Math.cos(angle)*r.width*.3};
  await page.mouse.move(start.x,start.y);await page.mouse.down();await page.waitForTimeout(240);await page.mouse.move(target.x,target.y,{steps:6});
  assert.equal((await snapshot()).aiming,true);assert.equal((await snapshot()).echo,37);
  if(sector===1)await page.locator('.screen').screenshot({path:outputPath('aim-03.png')});
  await page.mouse.up();const state=await snapshot();assert.equal(state.echo,32);assert.equal(state.lastSummon.sector,sector);assert.equal(state.aiming,false);
 }
 pass('0.2s hold, four compass sectors, release-only resource spending');
 await load();let start=await center(page.locator('[data-summon="0"]'));
 await page.mouse.move(start.x,start.y);await page.mouse.down();await page.waitForTimeout(240);await page.mouse.up();assert.equal((await snapshot()).echo,37);
 await page.mouse.down();await page.waitForTimeout(240);await page.keyboard.press('Escape');await page.mouse.up();assert.equal((await snapshot()).echo,37);assert.equal((await snapshot()).aiming,false);
 pass('Return to deck and Escape cancel without spending');
 const touch=await context.newCDPSession(page);
 const touchAt=(type,point)=>touch.send('Input.dispatchTouchEvent',{type,touchPoints:point?[{x:point.x,y:point.y}]:[]});
 await load();start=await center(page.locator('[data-summon="1"]'));let target=await center(page.locator('.battle-ring'));
 await touchAt('touchStart',start);await page.waitForTimeout(240);await touchAt('touchMove',{x:target.x+90,y:target.y});await touchAt('touchCancel');assert.equal((await snapshot()).echo,37);assert.equal((await snapshot()).aiming,false);
 pass('Browser touch cancel safely releases aim with no cost');
 await page.selectOption('#battle-state','cooldown');assert.equal(await page.locator('.cooldown-cover').count(),1);await page.locator('[data-summon="0"]').click({force:true});assert.equal((await snapshot()).echo,37);
 await page.locator('[data-summon="1"]').click();assert.equal((await snapshot()).echo,31);await page.locator('.screen').screenshot({path:outputPath('cooldown-03.png')});
 pass('Card cooldown blocks that card while another card remains usable');
 await load();await page.locator('#pause').click();assert.equal((await snapshot()).paused,true);
 assert.equal(await page.locator('.screen').evaluate(el=>[...el.children].filter(n=>!n.classList.contains('modal-overlay')).every(n=>n.inert)),true);
 await page.locator('[data-summon="0"]').evaluate(el=>el.click());assert.equal((await snapshot()).echo,37);
 for(let i=0;i<12;i++){await page.keyboard.press('Tab');assert.equal(await page.locator('[role="dialog"]').evaluate(el=>el.contains(document.activeElement)),true)}
 await page.locator('#pause-auto').click();assert.equal((await snapshot()).auto,false);assert.equal((await snapshot()).autoInvest,true);
 await page.locator('#resume-battle').click();assert.equal((await snapshot()).paused,false);assert.equal(await page.locator('#auto').getAttribute('aria-pressed'),'mixed');assert.equal(await page.locator('#pause').evaluate(el=>el===document.activeElement),true);
 pass('Pause: background inert, resource blocked, focus trap and separate AUTO options');
 await page.locator('#auto').click();assert.equal((await snapshot()).auto,true);assert.equal((await snapshot()).autoInvest,true);
 await page.locator('#pause').click();await page.locator('.screen').screenshot({path:outputPath('pause-03.png')});await page.locator('#open-help').click();assert.equal(await page.locator('.behavior-legend img').count(),3);await page.locator('.screen').screenshot({path:outputPath('help-03.png')});
 await page.keyboard.press('Escape');assert.equal(await page.locator('#resume-battle').count(),1);assert.equal((await snapshot()).paused,true);
 await page.locator('#open-retreat').click();await page.locator('.screen').screenshot({path:outputPath('retreat-03.png')});let overlay=await page.locator('.modal-overlay').boundingBox();await page.mouse.click(overlay.x+4,overlay.y+4);assert.equal(await page.locator('#confirm-retreat').count(),1);
 await page.keyboard.press('Escape');assert.equal((await snapshot()).paused,true);await page.locator('#open-retreat').click();await page.locator('#confirm-retreat').click();assert.equal((await snapshot()).paused,false);assert.equal(await page.locator('.cause-label').innerText(),'퇴각');
 pass('Help and two-step retreat: dim ignored, cancel stays paused, confirm records retreat');
 await load();await page.locator('[data-screen="formation"]').click();const from=await center(page.locator('[data-slot="0"]')),to=await center(page.locator('[data-slot="2"]'));
 await touchAt('touchStart',from);await touchAt('touchMove',{x:from.x+20,y:from.y+10});await touchAt('touchMove',to);await touchAt('touchEnd');assert.equal((await snapshot()).slots[2],0);assert.equal((await snapshot()).slots[0],null);
 start=await center(page.locator('[data-slot="2"]'));await touchAt('touchStart',start);await page.waitForTimeout(650);await touchAt('touchEnd');assert.equal((await snapshot()).slots.filter(x=>x!==null).length,4);
 await page.locator('[data-roster="0"]').click();await page.locator('[data-slot="0"]').click();assert.equal((await snapshot()).slots.filter(x=>x!==null).length,5);assert.equal((await snapshot()).slots[0],0);
 pass('Touch formation: drag to slot, long press remove, tap to place back');
 for(const ratio of ['tall','short']){
  await page.locator('[data-screen="home"]').click();await page.selectOption('#ratio',ratio);
  const overlap=await page.evaluate(()=>{
   const rect=s=>{const e=document.querySelector(s);return{top:e.offsetTop,bottom:e.offsetTop+e.offsetHeight}};
   return [rect('.resource-row').bottom<=rect('.deck').top,rect('.deck').bottom<=rect('.navbar').top];
  });assert.deepEqual(overlap,[true,true]);
  const small=await page.locator('.screen').evaluate(el=>[...el.querySelectorAll('#pause,#speed,#auto,#lamp,[data-summon],.nav-item[data-nav]')].filter(e=>e.offsetWidth<44||e.offsetHeight<44).map(e=>e.id||e.getAttribute('aria-label')));assert.deepEqual(small,[]);
  await page.locator('.screen').screenshot({path:outputPath('home-'+ratio+'-03.png')});
  for(const view of ['settings','help','retreat']){
   if(view==='settings')await page.locator('#pause').click();else if(view==='help')await page.locator('#open-help').click();else{await page.locator('#back-pause').click();await page.locator('#open-retreat').click();}
   const clipped=await page.locator('.modal-overlay').evaluate(el=>{const r=el.getBoundingClientRect();return [...el.querySelectorAll('button')].some(b=>{const q=b.getBoundingClientRect();return q.left<r.left||q.right>r.right||q.bottom>r.bottom||q.top<r.top})});assert.equal(clipped,false);
   if(ratio==='short')await page.locator('.screen').screenshot({path:outputPath('pause-'+view+'-short-03.png')});
  }
  await page.keyboard.press('Escape');await page.keyboard.press('Escape');
 }
 pass('Two aspect ratios: lower controls do not overlap; core targets at least 44 logical px; dialogs fit');
 await page.setViewportSize({width:390,height:844});await page.locator('[data-screen="home"]').click();await page.selectOption('#ratio','short');await page.locator('.screen').scrollIntoViewIfNeeded();
 start=await center(page.locator('[data-summon="0"]'));target=await center(page.locator('.battle-ring'));
 const before=(await snapshot()).echo;await touchAt('touchStart',start);await page.waitForTimeout(240);await touchAt('touchMove',{x:target.x,y:target.y-40});await touchAt('touchEnd');assert.equal((await snapshot()).echo,before-5);
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);pass('390px touch viewport: aim and release work without page overflow');
 assert.deepEqual(errors,[]);await browser.close();fs.writeFileSync(outputPath('interaction-verification.json'),JSON.stringify({date:'2026-11-12',scope:'Browser review interactions. Logical hit size at 390px reference; not Unity or physical-device validation.',checks,errors},null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
