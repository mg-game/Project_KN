const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const {chromium}=require('playwright'),assert=require('assert/strict'),fs=require('fs'),path=require('path'),crypto=require('crypto');
const checks=[],pass=(name,detail)=>{checks.push({name,result:'pass',detail});console.log('PASS',name)};
(async()=>{
 const browser=await reviewBrowser();
 const p=await browser.newPage({viewport:{width:1512,height:1450}}),errors=[],failed=[];p.on('pageerror',e=>errors.push(e.message));p.on('response',r=>{if(r.status()>=400)failed.push(r.url())});
 const goto=async view=>{await p.goto(reviewURL('/?view=')+view);await p.waitForFunction(()=>typeof window.KNResultsSnapshot==='function')};
 await goto('growth');assert.equal(await p.locator('.gr-close').evaluate(e=>e===document.activeElement),true);
 const before=await p.evaluate(()=>window.KNUIReviewSnapshot());
 const blocked=async()=>p.evaluate(()=>['#pause','#speed','#auto','#lamp','[data-summon="0"]'].every(s=>{const e=document.querySelector(s);e.focus();return !!e.closest('[inert]')&&document.activeElement!==e}));
 assert.equal(await blocked(),true);assert.equal(await p.locator('.navbar').evaluate(e=>e.inert),false);
 await p.locator('.gr-close').focus();for(let i=0;i<30;i++){await p.keyboard.press('Tab');assert.equal(await p.evaluate(()=>document.activeElement.closest('.arena,.deck,.resource-row')!==null),false)}
 assert.equal(await p.locator('.pause-dialog').count(),0);assert.deepEqual(await p.evaluate(()=>window.KNUIReviewSnapshot()),before);
 pass('Growth sheet prevents keyboard access to hidden pause/speed/AUTO/summon/investment; bottom navigation stays available');
 await p.locator('[data-detail="0"]').click();await p.locator('#gr-resonate').scrollIntoViewIfNeeded();await p.locator('#gr-resonate').click();await p.locator('#gr-confirm-no').click();
 assert.equal(await blocked(),true);assert.equal(await p.locator('#gr-resonate').evaluate(e=>e===document.activeElement),true);
 await p.locator('#gr-resonate').click();await p.locator('#gr-confirm-yes').click();assert.equal(await blocked(),true);
 await p.selectOption('#ratio','short');assert.equal(await blocked(),true);
 pass('Cancel/confirm resonance and ratio redraw preserve sheet background blocking');
 await p.locator('.gr-close').focus();await p.keyboard.press('Escape');assert.equal(await p.locator('.growth-panel').count(),0);assert.equal(await p.locator('[data-nav="0"]').evaluate(e=>e===document.activeElement),true);
 await p.locator('#pause').click();assert.equal(await p.locator('.pause-dialog').count(),1);
 pass('Escape closes growth, restores navigation focus and re-enables combat controls');
 await goto('home');await p.locator('#open-sheet').click();assert.equal(await blocked(),true);assert.equal(await p.locator('.sheet-alert').evaluate(e=>e.inert),false);await p.locator('.sheet-alert').click();assert.equal(await p.locator('.growth-panel').count(),0);
 pass('Sheet combat alert remains operable while hidden combat controls are blocked');
 for(const cause of ['defense','damage','retreat']){
  await goto('failure');await p.selectOption('#failure-cause',cause);assert.equal(await p.locator('.failure-modal').getAttribute('aria-modal'),'true');assert.equal(await blocked(),true);
  assert.equal(await p.locator('#retry').evaluate(e=>e===document.activeElement),true);await p.locator('#retry').focus();await p.keyboard.press('Shift+Tab');assert.equal(await p.locator('#strengthen').evaluate(e=>e===document.activeElement),true);await p.keyboard.press('Tab');assert.equal(await p.locator('#retry').evaluate(e=>e===document.activeElement),true);
  await p.locator('#retry').click();assert.equal(await p.locator('#pause').evaluate(e=>!!e.closest('[inert]')),false);
 }
 pass('All three failure reports block background, cycle three CTA buttons and restore usable battle controls');
 for(const ratio of ['tall','short']){
  await goto('formation');await p.selectOption('#ratio',ratio);
  const rows=await p.locator('.screen').evaluate(s=>['.formation-summary','.formation-deck','.formation-tools','.formation-list','.formation-instruction','.back-home'].map(sel=>{const r=s.querySelector(sel).getBoundingClientRect();return{sel,top:r.top,bottom:r.bottom}}));
  for(let i=1;i<rows.length;i++)assert.ok(rows[i].top>=rows[i-1].bottom-0.1,JSON.stringify(rows));
  const ringFits=await p.locator('.screen').evaluate(s=>{const limit=s.querySelector('.formation-summary').getBoundingClientRect().top;return [...s.querySelectorAll('.formation-board>.ring,.formation-board .slot')].every(e=>e.getBoundingClientRect().bottom<=limit)});assert.equal(ringFits,true);
 }
 pass('Formation at both ratios: summary, deck, presets, roster, hint and return button never overlap');
 const data=JSON.parse(fs.readFileSync(path.join(__dirname,'ScreenHandoff.json'))),plan=JSON.parse(fs.readFileSync(path.join(__dirname,'UnityImportPlan.json')));
 assert.equal(data.layouts.length,16);assert.equal(new Set(data.screens.map(s=>s.id)).size,8);
 for(const record of data.layouts){assert.ok(fs.existsSync(path.join(__dirname,record.screenshot)));for(const r of Object.values(record.blocks)){assert.ok(r.width>0&&r.height>0);assert.ok(r.x>=-.1&&r.y>=-.1);assert.ok(r.x+r.width<=record.size[0]+.1);assert.ok(r.y+r.height<=record.size[1]+.1)}}
 for(const row of data.coverage){assert.equal(plan.assets.filter(a=>a.id===row.id).length,row.count)}assert.equal(data.coverage.reduce((n,r)=>n+r.count,0),48);
 const old=path.join(__dirname,'../kn-ui-review-07');for(const a of plan.assets){const bytes=fs.readFileSync(path.join(__dirname,a.file));assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'),a.sha256);if(fs.existsSync(path.join(old,a.file)))assert.deepEqual(bytes,fs.readFileSync(path.join(old,a.file)))}
 pass('Sixteen measured layouts fit screens; ART coverage matches all 48 import candidates and PNGs remain byte-identical');
 await p.goto(reviewURL('/handoff.html'));assert.equal(await p.locator('[data-view]').count(),8);assert.equal(await p.locator('#coverage tr').count(),16);
 for(const ratio of ['tall','short']){await p.selectOption('#ratio',ratio);for(const s of data.screens){await p.locator(`[data-view="${s.id}"]`).click();await p.waitForFunction(()=>document.querySelector('#screen-image').complete&&document.querySelector('#screen-image').naturalWidth>0);assert.equal(await p.locator('#title').innerText(),s.name);assert.equal(await p.locator('.measurement').count(),Object.keys(data.layouts.find(x=>x.id===s.id&&x.ratio===ratio).blocks).length)}}
 await p.locator('.measurement').first().click();assert.equal(await p.locator('.region.selected').count(),1);await p.locator('#bounds').uncheck();assert.equal(await p.locator('#regions').isVisible(),false);await p.locator('#bounds').check();await p.locator('#candidates-only').check();assert.equal(await p.locator('#coverage tr').count(),10);await p.locator('#candidates-only').uncheck();
 pass('Handoff inspector switches eight screens/two ratios, highlights measurements and filters candidate coverage');
 for(const width of [360,390,768]){await p.setViewportSize({width,height:1000});assert.equal(await p.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false)}
 pass('Handoff page has no horizontal overflow at 360/390/768px');
 await p.setViewportSize({width:1512,height:1250});await p.locator('[data-view="home"]').click();await p.selectOption('#ratio','tall');await p.screenshot({path:outputPath('handoff-inspector-08.png')});
 assert.deepEqual(errors,[]);assert.deepEqual(failed,[]);await browser.close();fs.writeFileSync(outputPath('handoff-verification.json'),JSON.stringify({date:'2026-11-12',scope:'Browser integration, keyboard isolation, measured handoff layout and unchanged candidate provenance; not Unity/safe-area certification.',checks,errors,failed},null,2)+'\n');
})().catch(e=>{console.error(e);process.exit(1)});
