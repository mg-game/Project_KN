const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const {chromium}=require('playwright'),fs=require('fs'),path=require('path');
const root=__dirname;
const definitions=[
 {id:'home',name:'홈 · 전투',source:'UI_Home §2.2 · UI_Battle §1–3 / D-44',page:'home',fixture:null,blocks:{meta:'.meta',hud:'.hud',arena:'.arena',quest:'.quest',resources:'.resource-row',deck:'.deck',navigation:'.navbar'},input:'덱 탭/0.2초 홀드 조준 · 전장 놓기 확정 · 취소 시 비용 보존',states:['일반','웨이브 예고','보스','최후의 쇄도','위기','쿨다운','부족','상한','잠금']},
 {id:'formation',name:'편성',source:'UI_Formation §2 / D-27 · D-52',page:'formation',blocks:{heading:'.formation-title',ring:'.formation-board',summary:'.formation-summary',deck:'.formation-deck',tools:'.formation-tools',roster:'.formation-list',instruction:'.formation-instruction',back:'.back-home'},input:'슬롯 이동/맞바꿈 · 0.6초 홀드 해제 · 프리셋 탭/홀드 저장',states:['빈 방향','위험 방향','파견 중','선택','프리셋 3칸']},
 {id:'failure',name:'실패 리포트',source:'UI_Battle §3 / D-28 #10',page:'failure',blocks:{report:'.failure-modal',primary:'.report-cta.primary',recovery:'.report-cta:not(.primary)'},input:'배경 inert · 3개 CTA 안에서 Tab 순환 · 재도전/편성/강화',states:['방어 문제','화력 부족','퇴각']},
 {id:'growth',name:'강화 목록',source:'UI_Growth GR-1 / D-43',page:'growth',blocks:{panel:'.growth-panel',heading:'.gr-header',content:'.gr-scroll',navigation:'.navbar'},input:'가려진 전투 조작 inert · 하단 탭/전투 알림 유지 · 0.45초 홀드 구매',states:['기본','부족','부분 구매','최대','해금 전']},
 {id:'growth-detail',name:'성장 상세',source:'UI_Growth GR-2 / D-43',page:'growth',action:'detail',blocks:{panel:'.growth-panel',heading:'.gr-header',content:'.gr-scroll'},input:'공유 레벨 · 전후 비교 · ×1/×10 · 공명 확인',states:['기본','기억 조각 부족','공명 최대','소환 비용 하한']},
 {id:'resonance',name:'공명 확인',source:'UI_Growth GR-2 / D-43',page:'growth',action:'confirm',blocks:{overlay:'.growth-confirm-overlay',dialog:'.growth-confirm'},input:'전체 배경 inert · Tab 순환 · 취소 시 보존 · 확인은 1회 차감',states:['확인','취소','Escape']},
 {id:'pause',name:'일시정지',source:'SYS_InGameBattle §9 · UI_InGame BT-002',page:'home',action:'pause',blocks:{overlay:'.modal-overlay',dialog:'.pause-dialog'},input:'전체 배경 inert · 자동 소환/투자 분리 · 복귀 시 포커스 복원',states:['설정','도움말','퇴각 확인']},
 {id:'results',name:'전투 결과',source:'UI_Battle §3 · SYS_InGameBattle §17 K / D-28',page:'results',blocks:{heading:'.rs-top',content:'.rs-scroll',actions:'.rs-actions'},input:'배경 inert · 자동 정산 표시 · 1.5초/최대 6초 · 즉시 이동 1회',states:['승리','반복','보스','실패','한 구역 후퇴','퇴각 보상 미정']}
];
(async()=>{
 const b=await reviewBrowser();const p=await b.newPage({viewport:{width:1512,height:1450},deviceScaleFactor:1});
 const errors=[];p.on('pageerror',e=>errors.push(e.message));const records=[];
 for(const ratio of ['tall','short'])for(const d of definitions){
  await p.goto(reviewURL('/?view=')+d.page);await p.selectOption('#ratio',ratio);
  if(d.action==='detail'||d.action==='confirm')await p.locator('[data-detail="0"]').click();
  if(d.action==='confirm'){await p.locator('#gr-resonate').scrollIntoViewIfNeeded();await p.locator('#gr-resonate').click()}
  if(d.action==='pause')await p.locator('#pause').click();
  await p.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth));
  const blocks=await p.locator('.screen').evaluate((s,defs)=>{const base=s.getBoundingClientRect(),scale=base.width/390;return Object.fromEntries(Object.entries(defs).map(([id,selector])=>{const el=s.querySelector(selector);if(!el)throw Error('Missing handoff selector '+selector);const r=el.getBoundingClientRect();return[id,{selector,x:+((r.x-base.x)/scale).toFixed(1),y:+((r.y-base.y)/scale).toFixed(1),width:+(r.width/scale).toFixed(1),height:+(r.height/scale).toFixed(1),scrollable:(['auto','scroll'].includes(getComputedStyle(el).overflowY)&&el.scrollHeight>el.clientHeight+1)||(['auto','scroll'].includes(getComputedStyle(el).overflowX)&&el.scrollWidth>el.clientWidth+1)}]}))},d.blocks);
  const file=`handoff-${d.id}-${ratio}-08.png`;await p.locator('.screen').screenshot({path:path.join(root,file)});
  records.push({id:d.id,ratio,size:ratio==='tall'?[390,845]:[390,693],blocks,screenshot:file});
 }
 if(errors.length)throw Error(errors.join('\n'));await b.close();
 const importPlan=JSON.parse(fs.readFileSync(path.join(root,'UnityImportPlan.json'))),manifest=JSON.parse(fs.readFileSync(path.join(root,'asset-manifest.json')));
 const coverage=[
 ['ART-01','행동 성향', 'candidate',6,'candidates/behavior_*'],['ART-02','수선등 버튼/아이콘/점','candidate',9,'candidates/lantern_*'],['ART-03','점등 + 소등 기본','candidate',6,'candidates/shop_light_* + lighting/shop_dark_base.png'],['ART-04','링/방위','candidate',5,'candidates/formation_ring,sector_*,direction_*'],['ART-05','요격 부채꼴','code-preview',0,'interactions.js SVG 프리뷰; Unity 메시/셰이더 미적용'],['ART-06','웨이브 마커','candidate',2,'candidates/wave_*'],['ART-07','HUD 소품','candidate',9,'candidates/hud_*'],['ART-08','실패 아이콘','candidate',5,'candidates/report_*'],['ART-09','파견 배지','candidate',1,'candidates/badge_dispatch'],['ART-10','보스 변형','outside-ui',0,'보스 원화/소환 연출 별도'],['ART-11','장소 배경','outside-ui',0,'M1 기존 배경+틴트; 신규 배경 제외'],['ART-12','보름달 잔향','candidate',1,'candidates/moon_echo'],['ART-13','받침 대비','outside-ui',0,'스프라이트 보정 별도'],['ART-14','골무 외곽','outside-ui',0,'적 스프라이트 보정 별도'],['ART-15','프롤로그','outside-ui',0,'스토리 원화 별도'],['D-44','시트 위 전투 알림','candidate',4,'candidates/alert_*']
 ].map(([id,name,status,count,files])=>({id,name,status,count,files}));
 const data={version:'08',status:'review_not_adopted_not_unity_applied',sourceCommit:'755a13a27e2c7b4c55c7dbafb88c8a0197b6ef92',artCommit:'4b73a5a33c22d1b6246794c86fdbf7b27db281b0',measurement:'Browser logical coordinates from rendered DOM, top-left origin; not RectTransform or physical safe-area values.',tokens:{surface:'#211737',panel:'#2b2142',cream:'#fff0ca',gold:'#e7bc73',mint:'#aadcce',lavender:'#c6afe8',warning:'#ef9b9f',coreTarget:44},screens:definitions.map(({blocks,fixture,action,...d})=>d),layouts:records,coverage,runtimeCandidateCount:importPlan.assets.length,reusedReferenceCount:44,importPlan:'UnityImportPlan.json',pending:['DD 최종 채택','기존 수선점 교체 작화 판단','11px 이상으로 보완한 보조 문구의 실제 TMP 가독성','노치/홈 인디케이터 안전 영역','Unity 6000.2.6f2 임포트/압축/실기기','실제 전투/정산/자동 보스 분기 연결']};
 const total=coverage.reduce((n,r)=>n+r.count,0);if(total!==48||data.runtimeCandidateCount!==48)throw Error('Coverage mismatch '+total);
 fs.writeFileSync(path.join(root,'ScreenHandoff.json'),JSON.stringify(data,null,2)+'\n');fs.writeFileSync(path.join(root,'handoff-data.js'),'window.KNHandoff='+JSON.stringify(data)+';\n');console.log('Captured 16 screen layouts; ART-01…15 + D-44 mapped; 48 candidates reconciled.');
})().catch(e=>{console.error(e);process.exit(1)});
