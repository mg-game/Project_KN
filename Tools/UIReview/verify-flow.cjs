const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const {chromium}=require('playwright'),assert=require('assert/strict'),fs=require('fs'),path=require('path');
const checks=[],pass=(name,detail)=>{checks.push({name,result:'pass',detail});console.log('PASS',name)};
(async()=>{
 const b=await reviewBrowser();const p=await b.newPage({viewport:{width:1512,height:1500}}),errors=[];p.on('pageerror',e=>errors.push(e.message));
 const goto=async view=>{await p.goto(reviewURL('/?view=')+view);await p.waitForFunction(()=>typeof window.KNResultsSnapshot==='function')};
 const ui=()=>p.evaluate(()=>window.KNUIReviewSnapshot()),growth=()=>p.evaluate(()=>window.KNGrowthSnapshot());
 const down=async selector=>{await p.locator(selector).scrollIntoViewIfNeeded();const r=await p.locator(selector).boundingBox();await p.mouse.move(r.x+r.width/2,r.y+r.height/2);await p.mouse.down();return r};
 // A second input can change review screens while the first pointer is captured.
 const switchTo=async view=>p.locator(`[data-screen="${view}"]`).evaluate(e=>e.click());
 const ratio=async value=>p.locator('#ratio').evaluate((e,v)=>{e.value=v;e.dispatchEvent(new Event('change',{bubbles:true}))},value);
 for(const target of ['home','growth','results']){
  await goto('formation');const slots=(await ui()).slots;await down('[data-slot="0"]');await switchTo(target);await p.waitForTimeout(720);await p.mouse.up();
  assert.deepEqual((await ui()).slots,slots);assert.equal(await p.locator('.formation-board').count(),0);assert.equal(await p.locator(`[data-screen="${target}"]`).getAttribute('class'),'active');
  if(target==='home')await p.locator('.screen').screenshot({path:outputPath('flow-home-cancelled-08.png')});
 }
 pass('Held formation slot → home/growth/results: no delayed removal or old-screen resurrection');
 await goto('formation');let slots=(await ui()).slots;await down('[data-slot="0"]');await ratio('short');await p.waitForTimeout(720);await p.mouse.up();assert.deepEqual((await ui()).slots,slots);assert.equal(await p.locator('.formation-board').count(),1);
 pass('Ratio redraw cancels formation removal and preserves all five placements');
 for(const cancel of ['navigate','blur','move']){
  await goto('formation');const original=await p.evaluate(()=>presets.map(x=>[...x]));await p.locator('#auto-form').click();slots=(await ui()).slots;const r=await down('[data-preset="0"]');
  if(cancel==='navigate')await switchTo('home');if(cancel==='blur')await p.evaluate(()=>window.dispatchEvent(new Event('blur')));if(cancel==='move')await p.mouse.move(r.x+r.width/2+12,r.y+r.height/2);
  await p.waitForTimeout(720);await p.mouse.up();assert.deepEqual(await p.evaluate(()=>presets.map(x=>[...x])),original);assert.deepEqual((await ui()).slots,slots);
 }
 pass('Preset hold: navigation/window blur/movement cancel without save or accidental load');
 await goto('formation');await p.locator('#auto-form').click();slots=(await ui()).slots;await down('[data-preset="0"]');await p.waitForTimeout(670);await p.mouse.up();assert.deepEqual(await p.evaluate(()=>presets[0]),slots);await p.locator('[data-preset="1"]').click();await p.locator('[data-preset="0"]').click();assert.deepEqual((await ui()).slots,slots);
 await p.locator('.toast').evaluateAll(xs=>xs.forEach(x=>x.remove()));await p.locator('.screen').screenshot({path:outputPath('flow-formation-preserved-08.png')});
 pass('Valid preset hold still saves and a later tap restores the saved formation');
 for(const cancel of ['ratio','blur']){
  await goto('home');const echo=(await ui()).echo;await down('[data-summon="0"]');await p.waitForTimeout(240);assert.equal((await ui()).aiming,true);
  if(cancel==='ratio')await ratio('short');else await p.evaluate(()=>window.dispatchEvent(new Event('blur')));
  await p.waitForTimeout(280);await p.mouse.up();assert.equal((await ui()).aiming,false);assert.equal((await ui()).echo,echo);assert.equal(await p.locator('.aim-preview').count(),0);
  await p.locator('[data-summon="0"]').click();assert.equal((await ui()).echo,echo-5);
 }
 pass('Summon aim: ratio/window blur cancels for free and the next tap works normally');
 await goto('growth');const before=await growth();await down('[data-level-up="0"]');await switchTo('results');await p.waitForTimeout(650);await p.mouse.up();assert.deepEqual((await growth()).levels,before.levels);assert.equal((await growth()).wallet,before.wallet);
 pass('Leaving growth before hold threshold cancels the pending purchase');
 await goto('growth');await down('[data-level-up="0"]');await p.waitForTimeout(510);await p.evaluate(()=>window.dispatchEvent(new Event('blur')));const stopped=await growth();assert.ok(stopped.levels[0]>12);await p.waitForTimeout(650);assert.deepEqual((await growth()).levels,stopped.levels);assert.equal((await growth()).wallet,stopped.wallet);await p.mouse.up();
 await p.locator('.gr-scroll').evaluate(e=>e.scrollTop=0);await p.locator('.screen').screenshot({path:outputPath('flow-growth-stopped-08.png')});await p.locator('[data-level-up="0"]').click();assert.equal((await growth()).levels[0],stopped.levels[0]+1);
 pass('Growth focus loss stops repeats, keeps completed levels and allows a fresh single purchase');
 await goto('growth');await p.locator('[data-detail="0"]').click();await p.locator('#gr-resonate').click();const fragments=(await growth()).fragments;await switchTo('results');assert.equal((await growth()).confirming,false);assert.equal((await growth()).fragments,fragments);assert.equal(await p.locator('.growth-confirm-overlay').count(),0);assert.equal(await p.locator('.result-view').count(),1);
 pass('Leaving resonance confirmation preserves fragments and removes the old dialog');
 assert.deepEqual(errors,[]);await b.close();fs.writeFileSync(outputPath('flow-verification.json'),JSON.stringify({date:'2026-11-12',scope:'Browser review gesture and navigation cancellation; window blur event dispatched to exercise cleanup, not an actual device/app lifecycle test.',checks,errors},null,2)+'\n');
})().catch(e=>{console.error(e);process.exit(1)});
