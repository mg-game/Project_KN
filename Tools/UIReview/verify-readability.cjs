const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const {chromium}=require('playwright'),assert=require('assert/strict'),fs=require('fs'),path=require('path');
const checks=[],pass=(name,detail)=>{checks.push({name,result:'pass',detail});console.log('PASS',name)};
(async()=>{
 const browser=await reviewBrowser();const p=await browser.newPage({viewport:{width:1512,height:1500}}),errors=[];p.on('pageerror',e=>errors.push(e.message));
 const root=__dirname,goto=async view=>{await p.goto(reviewURL('/?view=')+view);await p.waitForFunction(()=>typeof window.KNGrowthSnapshot==='function')};
 const fonts=async selectors=>p.evaluate(selectors=>selectors.flatMap(sel=>[...document.querySelectorAll(sel)].filter(e=>e.checkVisibility()).map(e=>({selector:sel,size:parseFloat(getComputedStyle(e).fontSize)}))).filter(r=>r.size<11),selectors);
 const overflow=async selectors=>p.evaluate(selectors=>selectors.flatMap(sel=>[...document.querySelectorAll(sel)].filter(e=>e.checkVisibility()).filter(e=>e.scrollWidth>e.clientWidth+1).map(e=>({selector:sel,text:e.innerText}))),selectors);
 for(const ratio of ['tall','short']){
  await goto('home');await p.selectOption('#ratio',ratio);
  assert.deepEqual(await fonts(['.recommend','.shop-health small','.portrait small','.basket span','.lamp-button small','.resource-counter small','.deck-card .cost','.nav-item span']),[]);
  assert.deepEqual(await overflow(['.recommend','.basket span','.lamp-button','.resource-row','.quest']),[]);
  for(const kind of ['poor','capacity','locked','cooldown']){
   await p.selectOption('#battle-state',kind);const note=p.locator('.battle-state-note');assert.equal(await note.count(),1);assert.equal(await note.getAttribute('data-state'),kind);assert.equal(await note.getAttribute('role'),'status');assert.equal(await note.evaluate(e=>getComputedStyle(e).pointerEvents),'none');assert.deepEqual(await overflow(['.battle-state-note']),[]);
   const s=await p.evaluate(()=>window.KNUIReviewSnapshot());if(kind==='poor'||kind==='capacity'){await p.locator('[data-summon="0"]').click({force:true});assert.equal((await p.evaluate(()=>window.KNUIReviewSnapshot())).echo,s.echo)}
  }
  await p.selectOption('#battle-state','critical');await p.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth));await p.locator('.screen').screenshot({path:outputPath(`readability-critical-${ratio}-07.png`)});
  await p.selectOption('#battle-state','poor');await p.locator('.screen').screenshot({path:outputPath(`readability-battle-poor-${ratio}-07.png`)});
 }
 pass('Two ratios: battle cost/health/cooldown/income/navigation labels ≥11px and four explicit state reasons do not block input');
 for(const ratio of ['tall','short']){
  await goto('growth');await p.selectOption('#ratio',ratio);
  assert.deepEqual(await fonts(['.gr-wallet small','.gr-shop-info p','.gr-shop-info small','.gr-recommend small','.gr-portrait>span','.gr-card-title h3','.gr-card-title>b','.gr-power','.gr-level-button','.gr-hint']),[]);
  assert.deepEqual(await overflow(['.gr-wallet','.gr-shop-info','.gr-card-title','.gr-level-button','.gr-portrait>span']),[]);
  if(ratio==='tall'){const fits=await p.locator('.gr-scroll').evaluate(el=>[...el.querySelectorAll('[data-level-up="0"],[data-level-up="1"]')].every(b=>b.getBoundingClientRect().bottom<=el.getBoundingClientRect().bottom));assert.equal(fits,true)}
  await p.locator('.gr-scroll').evaluate(e=>e.scrollTop=0);await p.locator('.screen').screenshot({path:outputPath(`readability-growth-${ratio}-07.png`)});
  for(const [kind,copy] of [['poor','부족'],['locked','해금'],['max','최대 레벨']]){
   await p.selectOption('#growth-state',kind);assert.match(await p.locator('[data-level-up="0"]').innerText(),new RegExp(copy));assert.equal(await p.locator('[data-level-up="0"]').getAttribute('aria-disabled'),'true');
   const before=await p.evaluate(()=>window.KNGrowthSnapshot());await p.locator('[data-level-up="0"]').click({force:true});const after=await p.evaluate(()=>window.KNGrowthSnapshot());assert.equal(after.wallet,before.wallet);assert.deepEqual(after.levels,before.levels);
   assert.deepEqual(await overflow(['.gr-level-button','.gr-shop-actions button','.gr-shop-status']),[]);
   if(kind!=='max'){assert.equal(await p.locator('[data-shop-status]').isVisible(),true);assert.match(await p.locator('[data-shop-status]').innerText(),kind==='poor'?/단추 240 부족/:/1-1/)}
   await p.locator('.gr-scroll').evaluate(e=>e.scrollTop=0);await p.locator('.screen').screenshot({path:outputPath(`readability-${kind}-${ratio}-07.png`)});
  }
 }
 pass('Growth labels ≥11px; first row actions fit tall default; poor/locked/max have text reasons and preserve wallet/levels');
 for(const ratio of ['tall','short']){
  await goto('growth');await p.selectOption('#ratio',ratio);await p.locator('[data-detail="0"]').click();
  assert.deepEqual(await fonts(['.gr-shared','.gr-character-head p','.gr-stat-row>span','.gr-stat-row b','.gr-shared-note','.gr-section-heading>span']),[]);
  assert.deepEqual(await overflow(['.gr-shared','.gr-stat-row','.gr-level-actions button','.gr-fragment-cost']),[]);
  await p.locator('.gr-scroll').evaluate(e=>e.scrollTop=0);await p.locator('.screen').screenshot({path:outputPath(`readability-detail-${ratio}-07.png`)});
  await p.locator('#gr-resonate').scrollIntoViewIfNeeded();await p.locator('#gr-resonate').click();assert.deepEqual(await overflow(['.growth-confirm','.confirm-effects','.growth-confirm button']),[]);
  const fits=await p.locator('.growth-confirm-overlay').evaluate(el=>{const r=el.getBoundingClientRect();return [...el.querySelectorAll('button')].every(b=>{const q=b.getBoundingClientRect();return q.top>=r.top&&q.bottom<=r.bottom})});assert.equal(fits,true);
 }
 pass('Two ratios: shared-level/stat/resonance explanations remain readable, and confirmations keep actions inside screen');
 const samples=[];
 const sample=async(name,selector,backdrop=selector)=>samples.push(await p.evaluate(({name,selector,backdrop})=>{
  const style=getComputedStyle(document.querySelector(selector)),back=getComputedStyle(document.querySelector(backdrop));
  const colors=back.backgroundImage==='none'?[back.backgroundColor]:(back.backgroundImage.match(/rgba?\([^)]*\)/g)||[]);
  function rgb(s){return s.match(/[\d.]+/g).map(Number)}
  function luma(c){return c.slice(0,3).map(n=>n/255).map(n=>n<=.04045?n/12.92:((n+.055)/1.055)**2.4).reduce((n,v,i)=>n+v*[.2126,.7152,.0722][i],0)}
  const fg=rgb(style.color),ratios=colors.map(c=>{const bg=rgb(c),alpha=bg[3]??1,opaque=bg.slice(0,3).map(n=>n*alpha+255*(1-alpha));const f=luma(fg),b=luma(opaque);return(Math.max(f,b)+.05)/(Math.min(f,b)+.05)});
  return{name,foreground:style.color,background:back.backgroundImage==='none'?back.backgroundColor:back.backgroundImage,ratio:+Math.min(...ratios).toFixed(2)};
 },{name,selector,backdrop}));
 await goto('growth');await sample('placement-badge','.gr-portrait>span');await sample('growth-helper','.gr-power','.gr-lineage');await p.selectOption('#growth-state','poor');await sample('disabled-action','[data-level-up="0"]');await sample('shop-reason','[data-shop-status]');
 await goto('home');await p.selectOption('#battle-state','poor');await sample('battle-reason','.battle-state-note');await p.selectOption('#battle-state','critical');await sample('health-label','.shop-health small');
 samples.forEach(s=>assert.ok(s.ratio>=4.5,JSON.stringify(s)));pass('Six live text/background pairs exceed 4.5:1; translucent backgrounds conservatively composited on white',samples);
 for(const width of [360,390,768]){await p.setViewportSize({width,height:900});await p.waitForFunction(()=>document.documentElement.scrollWidth<=innerWidth);assert.equal(await p.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false)}
 pass('360/390/768px review page has no horizontal overflow after typography changes');
 assert.deepEqual(errors,[]);await browser.close();fs.writeFileSync(outputPath('readability-verification.json'),JSON.stringify({date:'2026-11-12',scope:'Selected browser reference labels, layout/state copy and six specified color pairs; not a full accessibility audit or Unity/device certification.',checks,errors},null,2)+'\n');
})().catch(e=>{console.error(e);process.exit(1)});
