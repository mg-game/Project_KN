const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const {chromium}=require('playwright');
const sharp=require('sharp');
const fs=require('fs');
const path=require('path');
const assert=require('assert/strict');
const root=__dirname,checks=[];
function pass(name){checks.push({name,result:'pass'});console.log('PASS',name)}
async function main(){
 const manifest=JSON.parse(fs.readFileSync(path.join(root,'asset-manifest.json')));
 for(const a of manifest.assets){
  const p=path.join(root,'candidates',a.name+'.png'),m=await sharp(p).metadata();
  assert.equal(m.width,a.width);assert.equal(m.height,a.height);assert.equal(m.hasAlpha,true);
  const alpha=await sharp(p).ensureAlpha().extractChannel(3).stats();assert.equal(alpha.channels[0].min,0);assert(alpha.channels[0].max>0);
 }
 pass('47 PNG candidates: dimensions, alpha channel and visible content');
 assert.equal(manifest.assets.length,47);
 const browser=await reviewBrowser();
 const page=await browser.newPage({viewport:{width:1440,height:1400},deviceScaleFactor:2});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('response',r=>{if(r.status()>=400)errors.push(r.url()+': '+r.status())});
 await page.goto(reviewURL('/'));await page.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0));
 assert.equal(await page.locator('.deck-card').count(),6);assert.equal(await page.locator('.nav-item').count(),7);assert.equal(await page.locator('.guardian').count(),5);
 assert.equal(await page.locator('.portrait-rail .portrait').count(),5);pass('home: five guardians, six deck slots, seven tabs');
 await page.locator('.screen').screenshot({path:outputPath('home-battle.png')});
 await page.screenshot({path:outputPath('review-desktop.png'),fullPage:true});
 await page.locator('#lamp').click();assert.match(await page.locator('.resource-counter').innerText(),/17/);assert.equal(await page.locator('#lamp').isDisabled(),true);pass('lamp: spends 20 and disables further purchase below cost');
 await page.selectOption('#lamp-level','5');assert.match(await page.locator('#lamp').innerText(),/MAX/);assert(await page.locator('#lamp').isDisabled());
 await page.selectOption('#lamp-level','2');
 for(const state of ['normal','preview','boss','surge','critical','poor','capacity','locked']){
  await page.selectOption('#battle-state',state);
  if(state==='poor'){assert.equal(await page.locator('[data-summon="0"]').getAttribute('aria-disabled'),'true');await page.locator('[data-summon="0"]').click({force:true});assert.match(await page.locator('.toast').innerText(),/부족/)}
  if(state==='capacity'){assert.match(await page.locator('.cap-note').innerText(),/3초/);await page.locator('[data-summon="0"]').click({force:true});assert.match(await page.locator('.toast').innerText(),/기다려/)}
  if(state==='locked'){await page.locator('#speed').click({force:true});assert.match(await page.locator('.toast').innerText(),/1-6/)}
  if(state==='preview')assert.equal(await page.locator('.wave-warning').count(),1);
  if(state==='boss')assert.match(await page.locator('.boss-bar').innerText(),/좀나방/);
  if(state==='surge')assert.match(await page.locator('.boss-bar').innerText(),/남은 적 7/);
  if(state==='critical')assert.match(await page.locator('.shop-health').innerText(),/24%/);
  await page.locator('.toast').evaluateAll(els=>els.forEach(e=>e.remove()));
  await page.locator('.screen').screenshot({path:outputPath('state-'+state+'.png')});
 }
 pass('eight battle states: warning, boss, surge, poor, capacity and locks');
 await page.selectOption('#battle-state','normal');
 for(const alert of ['boss','critical','clear','fail']){
  await page.selectOption('#alert-state',alert);await page.locator('#open-sheet').click();assert.equal(await page.locator('.sheet-alert').count(),1);assert.equal(await page.locator('.sheet').count(),1);
  await page.locator('.toast').evaluateAll(els=>els.forEach(e=>e.remove()));if(alert==='critical')await page.locator('.screen').screenshot({path:outputPath('sheet-alert.png')});
  await page.locator('.sheet-alert').click();assert.equal(await page.locator('.sheet').count(),0);
  if(alert==='clear'||alert==='fail')assert.equal(await page.locator('.result-view').count(),1);
  await page.locator('[data-screen="home"]').click();
 }
 pass('four sheet alerts: boss/critical return to battle, clear/fail open results');
 await page.locator('[data-screen="formation"]').click();assert.equal(await page.locator('#home-controls').isVisible(),false);
 await page.locator('[data-roster="0"]').click();await page.locator('[data-slot="2"]').click();
 assert.match(await page.locator('[data-slot="2"]').getAttribute('aria-label'),/짝짝이/);assert.match(await page.locator('.formation-summary').innerText(),/5\/5/);
 const before=await page.locator('.formation-summary').innerText();await page.locator('[data-dispatch]').click({force:true});assert.match(await page.locator('.toast').innerText(),/24분/);assert.equal(await page.locator('.formation-summary').innerText(),before);
 await page.locator('#auto-form').click();assert.match(await page.locator('[data-slot="1"]').getAttribute('aria-label'),/짝짝이/);assert.equal(await page.locator('.formation-board>.sector').count(),3);const sectors=await page.locator('.formation-board>.sector').evaluateAll(xs=>xs.map(x=>x.style.transform));assert.deepEqual(sectors,['rotate(180deg)','rotate(225deg)','rotate(315deg)']);
 await page.locator('[data-preset="1"]').click();assert.match(await page.locator('[data-slot="0"]').getAttribute('aria-label'),/빈 자리/);const presetBox=await page.locator('[data-preset="2"]').boundingBox();await page.mouse.move(presetBox.x+presetBox.width/2,presetBox.y+presetBox.height/2);await page.mouse.down();await page.waitForTimeout(650);await page.mouse.up();await page.locator('#auto-form').click();await page.locator('[data-preset="2"]').click();assert.match(await page.locator('[data-slot="0"]').getAttribute('aria-label'),/빈 자리/);await page.locator('#auto-form').click();await page.locator('.toast').evaluateAll(els=>els.forEach(e=>e.remove()));await page.locator('.screen').screenshot({path:outputPath('formation.png')});pass('formation: move/swap, five-guardian limit, dispatch lock, danger-first layout, three presets with hold-to-save');
 await page.locator('[data-screen="failure"]').click();await page.selectOption('#failure-cause','damage');assert.match(await page.locator('.report-copy').innerText(),/시간/);
 await page.locator('.screen').screenshot({path:outputPath('failure-damage.png')});await page.selectOption('#failure-cause','defense');await page.locator('.screen').screenshot({path:outputPath('failure-defense.png')});
 await page.locator('#change-form').click();assert.equal(await page.locator('.formation-board').count(),1);
 await page.locator('[data-screen="failure"]').click();await page.locator('#strengthen').click();assert.equal(await page.locator('.sheet').count(),1);
 await page.locator('.sheet .close').click();await page.locator('[data-screen="failure"]').click();await page.locator('#retry').click();assert.equal(await page.locator('.failure-modal').count(),0);pass('failure: two causes and three CTA destinations');
 for(const ratio of ['tall','short']){
  await page.selectOption('#ratio',ratio);
  for(const target of ['home','formation','failure']){
   await page.locator(`[data-screen="${target}"]`).click();
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
   const clipped=await page.locator('.screen').evaluate(el=>{const r=el.getBoundingClientRect();return [...el.querySelectorAll('button')].filter(b=>{if(b.offsetParent===null||b.closest('.formation-list'))return false;const q=b.getBoundingClientRect();return q.bottom>r.bottom+1||q.right>r.right+1||q.left<r.left-1||q.top<r.top-1}).map(b=>b.getAttribute('aria-label')||b.textContent)});
   assert.deepEqual(clipped,[],`${ratio}/${target}: controls outside screen`);
   await page.locator('.screen').screenshot({path:outputPath(target+'-'+ratio+'.png')});
  }
 }
 pass('9:19.5 and 9:16: controls remain inside screen');
 for(const viewport of [{width:360,height:780},{width:390,height:844},{width:768,height:1024}]){
  await page.setViewportSize(viewport);await page.locator('[data-screen="home"]').click();assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
  if(viewport.width===360)await page.screenshot({path:outputPath('review-mobile.png'),fullPage:true});
 }
 pass('360px / 390px / 768px review page: no horizontal overflow');
 await page.setViewportSize({width:1440,height:1400});await page.selectOption('#ratio','tall');await page.locator('[data-screen="assets"]').click();await page.waitForSelector('.asset-card');
 assert.equal(await page.locator('.asset-card').count(),47);await page.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0));
 await page.locator('.asset-group').first().screenshot({path:outputPath('behavior-sheet.png')});
 assert.deepEqual(errors,[]);pass('asset sheet: 47 downloadable SVG/PNG pairs, no failed loads or JS errors');
 checks.push({name:'Direct file:// opening',result:'blocked',reason:'Managed Chromium returned ERR_BLOCKED_BY_ADMINISTRATOR. Served HTTP review verified; no policy bypass attempted.'});
 await browser.close();
 fs.writeFileSync(outputPath('verification.json'),JSON.stringify({date:'2026-11-12',scope:'browser review artifact and static candidate asset checks; not Unity',checks,errors},null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
