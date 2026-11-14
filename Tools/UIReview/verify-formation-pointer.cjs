const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const assert=require('node:assert/strict'),fs=require('node:fs');
const checks=[],pass=name=>{checks.push({name,result:'pass'});console.log('PASS',name)};
(async()=>{
 const b=await reviewBrowser(),p=await b.newPage({viewport:{width:1512,height:1450},hasTouch:true}),errors=[];p.on('pageerror',e=>errors.push(e.message));
 const cdp=await p.context().newCDPSession(p),send=(type,touchPoints)=>cdp.send('Input.dispatchTouchEvent',{type,touchPoints});
 const state=()=>p.evaluate(()=>({slots:window.KNUIReviewSnapshot().slots,presets:presets.map(x=>[...x])}));
 const open=async()=>{await p.goto(reviewURL('/?view=formation'));await p.waitForFunction(()=>typeof window.KNUIReviewSnapshot==='function')};
 const point=async(selector,id,dx=0)=>{const r=await p.locator(selector).boundingBox();return{id,x:r.x+r.width/2+dx,y:r.y+r.height/2}};
 await open();let a=await point('[data-slot="0"]',1,-7),z=await point('[data-slot="0"]',2,7);await send('touchStart',[a]);await send('touchStart',[a,z]);await send('touchEnd',[z]);await p.waitForTimeout(660);assert.equal((await state()).slots[0],null);assert.equal((await state()).slots.filter(x=>x!==null).length,4);await send('touchEnd',[]);
 pass('Same-slot secondary release cannot cancel the primary finger’s 600ms removal hold');
 await open();let before=await state();a=await point('[data-slot="0"]',1);z=await point('[data-slot="1"]',2);await send('touchStart',[a]);await send('touchStart',[a,z]);await send('touchEnd',[a]);await p.waitForTimeout(660);assert.deepEqual(await state(),before);await send('touchEnd',[]);
 pass('A secondary slot never starts removal after the primary finger releases');
 await open();a=await point('[data-slot="0"]',1);z=await point('[data-slot="1"]',2);const dest=await point('[data-slot="2"]',1);await send('touchStart',[a]);await send('touchStart',[a,z]);await send('touchMove',[dest,z]);await send('touchEnd',[z]);assert.equal(await p.locator('.formation-drag-ghost').count(),1);await send('touchEnd',[dest]);let s=await state();assert.equal(s.slots[0],null);assert.equal(s.slots[2],0);assert.equal(s.slots[1],1);assert.equal(s.slots.filter(x=>x!==null).length,5);assert.equal(await p.locator('.formation-drag-ghost').count(),0);
 pass('Primary drag survives another finger’s release and moves one guardian without losing the ghost early');
 await open();await p.locator('#auto-form').click();before=await state();assert.notDeepEqual(before.presets[0],before.slots);a=await point('[data-preset="0"]',1,-7);z=await point('[data-preset="0"]',2,7);await send('touchStart',[a]);await send('touchStart',[a,z]);await send('touchEnd',[z]);await p.waitForTimeout(660);assert.deepEqual((await state()).presets[0],before.slots);await send('touchEnd',[]);assert.deepEqual((await state()).slots,before.slots);
 pass('Same-preset secondary release cannot cancel a valid save or turn its release into a load');
 for(const owner of ['slot','preset']){
  await open();await p.locator('#auto-form').click();before=await state();a=await point(owner==='slot'?'[data-slot="0"]':'[data-preset="0"]',1);z=await point(owner==='slot'?'[data-preset="1"]':'[data-slot="1"]',2);await send('touchStart',[a]);await send('touchStart',[a,z]);await p.waitForTimeout(660);s=await state();if(owner==='slot'){assert.equal(s.slots[0],null);assert.deepEqual(s.presets,before.presets)}else{assert.deepEqual(s.slots,before.slots);assert.deepEqual(s.presets[0],before.slots);assert.deepEqual(s.presets[1],before.presets[1])}await send('touchEnd',[]);
 }
 pass('One gesture owns formation across slot/preset controls; the other finger cannot save or remove');
 for(const mode of ['cancel','blur','navigate']){
  await open();before=await state();a=await point('[data-slot="0"]',1,-7);z=await point('[data-slot="0"]',2,7);await send('touchStart',[a]);await send('touchStart',[a,z]);if(mode==='cancel')await send('touchCancel',[]);else{await p.evaluate(mode=>mode==='blur'?window.dispatchEvent(new Event('blur')):document.querySelector('[data-screen="home"]').click(),mode);await send('touchEnd',[])}await p.waitForTimeout(680);assert.deepEqual(await state(),before);assert.equal(await p.locator('.formation-drag-ghost').count(),0);if(mode==='navigate')assert.equal(await p.locator('.formation-board').count(),0);
 }
 await open();a=await point('[data-slot="0"]',1);await send('touchStart',[a]);await send('touchCancel',[]);await send('touchStart',[a]);await p.waitForTimeout(660);await send('touchEnd',[]);assert.equal((await state()).slots[0],null);
 pass('Cancel, blur and navigation clear both fingers’ pending work; a fresh primary hold still works');
 assert.deepEqual(errors,[]);await b.close();fs.writeFileSync(outputPath('formation-pointer-verification.json'),JSON.stringify({date:new Date().toISOString(),scope:'Chromium CDP multi-touch in the browser formation review. Blur cleanup is event-dispatched; not Unity or physical-device certification.',checks,errors},null,2)+'\n');
})().catch(e=>{console.error(e);process.exit(1)});
