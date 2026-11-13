const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const fs=require('node:fs'),assert=require('node:assert/strict');
const checks=[],pass=(name,detail)=>{checks.push({name,result:'pass',detail});console.log('PASS',name)};
(async()=>{
 const b=await reviewBrowser(),p=await b.newPage({viewport:{width:1512,height:1450},hasTouch:true}),errors=[];p.on('pageerror',e=>errors.push(e.message));
 const touch=await p.context().newCDPSession(p),state=()=>p.evaluate(()=>window.KNGrowthSnapshot());
 const open=async()=>{await p.goto(reviewURL('/?view=growth'));await p.waitForFunction(()=>typeof window.KNGrowthSnapshot==='function')};
 const point=async(selector,id,dx=0)=>{const r=await p.locator(selector).boundingBox();return{id,x:r.x+r.width/2+dx,y:r.y+r.height/2}};
 const send=(type,points)=>touch.send('Input.dispatchTouchEvent',{type,touchPoints:points});
 const stable=async stopped=>{await p.waitForTimeout(550);const s=await state();assert.deepEqual(s.levels,stopped.levels);assert.equal(s.wallet,stopped.wallet);assert.equal(s.shopLevel,stopped.shopLevel)};
 await open();await p.locator('[data-level-up="0"]').scrollIntoViewIfNeeded();const a=await point('[data-level-up="0"]',1,-8),other=await point('[data-level-up="0"]',2,8);
 await send('touchStart',[a,other]);await p.waitForTimeout(660);await send('touchEnd',[]);const released=await state();assert.ok(released.levels[0]>12);await stable(released);
 pass('Two touches on one upgrade: release stops all repeats and preserves the release-time wallet',{releaseLevel:released.levels[0],afterWaitLevel:(await state()).levels[0]});
 for(const selector of ['[data-level-up="0"]','[data-level-up="1"]']){
  await open();await p.locator('[data-level-up="0"]').scrollIntoViewIfNeeded();const owner=await point('[data-level-up="0"]',1,-8),secondary=await point(selector,2,8);
  await send('touchStart',[owner]);await send('touchStart',[owner,secondary]);await send('touchEnd',[secondary]);const early=await state();assert.equal(early.levels[0],12);assert.equal(early.levels[1],10);
  await p.waitForTimeout(510);const held=await state();assert.ok(held.levels[0]>12);assert.equal(held.levels[1],10);await send('touchEnd',[]);await stable(await state());
 }
 pass('Secondary finger release never commits a tap or stops the original hold, on same/different cards');
 for(const selector of ['[data-level-up="0"]','#gr-shop-ten']){
  await open();const owner=await point('#gr-shop-one',1),secondary=await point(selector,2);
  await send('touchStart',[owner,secondary]);await p.waitForTimeout(660);const beforeRelease=await state();assert.ok(beforeRelease.shopLevel>12);assert.deepEqual(beforeRelease.levels,[12,10,11,9,8]);
  await send('touchEnd',[]);const after=await state();assert.equal(after.shopLevel,beforeRelease.shopLevel);assert.equal(after.wallet,beforeRelease.wallet);await stable(after);
 }
 pass('A shop hold owns the gesture: another card/batch button does not add a purchase on release');
 await open();let r=await p.locator('[data-level-up="0"]').boundingBox();await p.mouse.move(r.x+r.width/2,r.y+r.height/2);await p.mouse.down();
 await p.locator('#gr-shop-ten').focus();await p.keyboard.press('Enter');await p.locator('#gr-shop-max').focus();await p.keyboard.press('Enter');assert.equal((await state()).shopLevel,12);await p.mouse.up();const beforeBatch=(await state()).shopLevel;
 await p.locator('#gr-shop-ten').focus();await p.keyboard.press('Enter');assert.equal((await state()).shopLevel,beforeBatch+10);
 await open();await p.locator('[data-detail="0"]').click();r=await p.locator('#gr-detail-one').boundingBox();await p.mouse.move(r.x+r.width/2,r.y+r.height/2);await p.mouse.down();
 await p.locator('#gr-detail-ten').evaluate(e=>e.focus({preventScroll:true}));await p.keyboard.press('Enter');await p.locator('#gr-resonate').evaluate(e=>e.focus({preventScroll:true}));await p.keyboard.press('Enter');assert.equal((await state()).confirming,false);assert.equal((await state()).fragments,36);assert.equal((await state()).levels[0],12);await p.mouse.up();assert.equal((await state()).levels[0],13);
 pass('Keyboard batch/resonance clicks are ignored during a pointer hold and work again after release');
 await open();const cancelPoints=[await point('[data-level-up="0"]',1,-8),await point('[data-level-up="0"]',2,8)];await send('touchStart',cancelPoints);await p.waitForTimeout(510);await send('touchCancel',[]);const cancelled=await state();await stable(cancelled);
 const fresh=await point('[data-level-up="0"]',1);await send('touchStart',[fresh]);await send('touchEnd',[]);assert.equal((await state()).levels[0],cancelled.levels[0]+1);await stable(await state());
 pass('Touch cancel clears the hold; a new touch buys exactly one level without reviving old timers');
 for(const mode of ['blur','navigate']){
  await open();const initial=await state(),points=[await point('[data-level-up="0"]',1,-8),await point('[data-level-up="0"]',2,8)];await send('touchStart',points);
  if(mode==='blur')await p.evaluate(()=>window.dispatchEvent(new Event('blur')));else await p.locator('[data-screen="home"]').evaluate(e=>e.click());
  await send('touchEnd',[]);await stable(initial);
 }
 pass('Blur/navigation before the threshold leaves no delayed purchases after two-finger input');
 assert.deepEqual(errors,[]);await b.close();fs.writeFileSync(outputPath('pointer-verification.json'),JSON.stringify({date:new Date().toISOString(),scope:'Chromium CDP multi-touch and native keyboard/mouse browser review. Not Unity or physical-device certification.',checks,errors},null,2)+'\n');
})().catch(e=>{console.error(e);process.exit(1)});
