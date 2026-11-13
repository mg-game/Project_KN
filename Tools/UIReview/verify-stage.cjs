const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const fs=require('node:fs'),assert=require('node:assert/strict');
const checks=[],pass=name=>{checks.push({name,result:'pass'});console.log('PASS',name)};
(async()=>{
 const b=await reviewBrowser(),p=await b.newPage({viewport:{width:1512,height:1450}}),errors=[];p.on('pageerror',e=>errors.push(e.message));
 const state=()=>p.evaluate(()=>window.KNUIReviewSnapshot());
 const results=()=>p.evaluate(()=>window.KNResultsSnapshot());
 const open=async(kind='clear')=>{await p.goto(reviewURL('/?view=results'));await p.selectOption('#result-state',kind)};
 const at=async(n,label)=>{assert.equal((await state()).stage,n);assert.equal(await p.locator('.stage-name').innerText(),label)};
 const label5='1-5 우산 보관소 지붕';
 await open();await p.locator('#rs-next').click();await at(5,label5);
 for(const action of ['#auto','#speed']){await p.locator(action).click();await at(5,label5)}
 for(const ratio of ['short','tall']){await p.selectOption('#ratio',ratio);await at(5,label5)}
 await p.locator('#pause').click();assert.match(await p.locator('.battle-summary').innerText(),/1-5 우산 보관소 지붕/);
 await p.locator('.screen').screenshot({path:outputPath('stage-pause-5.png')});await p.locator('#resume-battle').click();await at(5,label5);
 pass('Clear 4→5 persists through AUTO/speed/ratio redraw and pause/resume');
 await p.locator('[data-screen="formation"]').click();assert.match(await p.locator('.formation-stage').innerText(),/1-5 우산 보관소 지붕/);
 for(const ratio of ['short','tall']){await p.selectOption('#ratio',ratio);const fits=await p.locator('.formation-title').evaluate(e=>{const r=e.getBoundingClientRect(),s=e.closest('.screen').getBoundingClientRect(),hint=e.closest('.screen').querySelector('.formation-hint').getBoundingClientRect();return r.right<=s.right&&r.bottom<=hint.top+1});assert.equal(fits,true)}
 await p.locator('.screen').screenshot({path:outputPath('stage-formation-5.png')});await p.locator('#back-home').click();await at(5,label5);
 await p.locator('[data-screen="growth"]').click();await p.locator('[data-level-up="0"]').click();await p.locator('.gr-close').click();await at(5,label5);
 await p.locator('[data-screen="failure"]').click();assert.match(await p.locator('.failure-modal>p').innerText(),/1-5 우산 보관소 지붕/);await p.locator('#retry').click();await at(5,label5);
 pass('Formation header, growth return and failure/retry share the current stage; two-ratio headings fit');
 const alert=async kind=>{await p.selectOption('#alert-state',kind);await p.locator('#open-sheet').click();assert.match(await p.locator('.sheet-alert').innerText(),/1-5 우산 보관소 지붕/);await p.locator('.sheet-alert').click()};
 await alert('clear');let s=await results();assert.equal(s.stage,5);assert.equal(s.receipt.gold,97);assert.equal(s.receipt.candy,3);
 await p.locator('#rs-details').click();const before=await results();await p.selectOption('#ratio','short');assert.deepEqual((await results()).receipt,before.receipt);await p.selectOption('#ratio','tall');
 await p.locator('#rs-next').click();await at(6,'1-6 멈춘 시계 광장');await p.locator('#auto').click();await at(6,'1-6 멈춘 시계 광장');
 pass('Current-stage clear alert settles stage 5 once and continues 5→6 without resetting to the canned fixture');
 await open();await p.locator('#rs-next').click();await alert('fail');s=await results();assert.equal(s.stage,5);assert.equal(s.receipt.gold,17);
 await p.locator('#rs-details').click();assert.match(await p.locator('.rs-detail').innerText(),/기본 보상 97 × 30%/);await p.locator('#rs-next').click();await at(5,label5);
 await p.locator('.screen').screenshot({path:outputPath('stage-home-5.png')});
 pass('Current-stage failure alert uses stage 5 base reward and retries stage 5');
 for(const [kind,next,label] of [['stepback',3,'1-3 오래된 문구점'],['repeat',4,'1-4 우산 보관소'],['boss',11,'스테이지 11']]){
  await open(kind);await p.locator('#rs-next').click();await at(next,label);await p.locator('#auto').click();await at(next,label);await p.locator('#pause').click();assert.ok((await p.locator('.battle-summary').innerText()).startsWith(label));await p.locator('#resume-battle').click();
 }
 pass('Step-back/repeat/boss destinations survive subsequent interactions; undefined chapter locations keep generic labels');
 await p.locator('[data-screen="results"]').click();assert.equal((await results()).stage,10);await p.selectOption('#result-state','clear');assert.equal((await results()).stage,4);await p.selectOption('#result-state','boss');assert.equal((await results()).stage,10);await p.locator('#result-restart').click();assert.equal((await results()).stage,10);
 pass('External fixture selection deliberately resets to 1-4/1-10 and restart preserves the selected result stage');
 assert.deepEqual(errors,[]);await b.close();fs.writeFileSync(outputPath('stage-verification.json'),JSON.stringify({date:new Date().toISOString(),scope:'Browser stage-label/context continuity; combat, balance, saves and chapter progression remain review fixtures.',checks,errors},null,2)+'\n');
})().catch(e=>{console.error(e);process.exit(1)});
