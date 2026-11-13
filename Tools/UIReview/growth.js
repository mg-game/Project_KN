/* GR-1 / GR-2 / GR-3 review model. Numeric fixtures are not balance data. */
(()=>{
  const originalSheet=showSheet,originalHome=home,originalPage=setPage,originalRender=render;
  const roles=[['방어','style_defense'],['방어','style_defense'],['힐','style_heal'],['힐 · 제어','style_heal'],['지원','style_support']];
  const placement=['지킴이 N','지킴이 NE','잔향이 덱','잔향이 덱','미편성'];
  let wallet=12450,fragments=36,levels=[12,10,11,9,8],resonances=[2,1,1,0,0],shopLevel=12;
  let view='list',character=0,locked=false,showAlert=false,pending=null,feedbackTimer=null,feedbackTotal=0,lastToast=-Infinity;
  const activeHolds=new Set();
  const fmt=n=>Math.floor(n).toLocaleString('ko-KR');
  const cost=level=>Math.max(1,Math.floor(50*1.09**(level-1)));
  const shopCost=level=>Math.floor(240*1.1**(level-12));
  const stats=i=>({attack:40+17*levels[i],hp:100+80*levels[i]});
  const resonanceStats=(i,r)=>({common:r*5,cost:Math.max(3,(i===1?6:5)-Math.floor(r/3)),duration:13+r*.5});
  function budget(level,max,price,limit=Infinity){let n=0,spent=0;while(level+n<max&&n<limit){const c=price(level+n);if(spent+c>wallet)break;spent+=c;n++;}return{n,spent};}
  function notify(text){if(performance.now()-lastToast<1500)return;lastToast=performance.now();toast(text);}
  function stopHolds(){[...activeHolds].forEach(stop=>stop());}
  window.addEventListener('blur',stopHolds);
  document.addEventListener('visibilitychange',()=>{if(document.hidden)stopHolds()});
  function syncSheetInput(){
    const panel=screen.querySelector('.growth-panel');if(!panel)return;
    // This sheet keeps the bottom navigation and combat alert available.
    [...screen.children].forEach(e=>e.inert=!e.matches('.growth-panel,.navbar,.sheet-alert,.growth-feedback,.toast,.home-indicator'));
  }
  function updateMeta(){const gold=screen.querySelector('.meta .currency');if(gold)gold.innerHTML=img(R+'currency_gold.png','meta-gold')+fmt(wallet);const level=screen.querySelector('.shop-lv strong');if(level)level.textContent='Lv.'+shopLevel;}
  function feedback(delta,text){
    let chip=screen.querySelector('.growth-feedback');if(!chip){chip=document.createElement('div');chip.className='growth-feedback';chip.setAttribute('role','status');screen.append(chip);feedbackTotal=0;}
    feedbackTotal+=delta;chip.textContent=text||(feedbackTotal?'팀 전투력 +'+fmt(feedbackTotal):'레벨이 올랐어요');
    clearTimeout(feedbackTimer);feedbackTimer=setTimeout(()=>{chip.remove();feedbackTotal=0},1200);
  }
  function blockedLevel(i){if(locked)return'1-1을 클리어하면 열려요';if(levels[i]>=100)return'최대 레벨이에요';if(wallet<cost(levels[i]))return'단추가 '+fmt(cost(levels[i])-wallet)+' 부족해요';return'';}
  function buyLevel(i,mode=1){
    const reason=blockedLevel(i);if(reason){notify(reason);return false;}
    const purchase=budget(levels[i],100,cost,mode);if(!purchase.n)return false;
    wallet-=purchase.spent;levels[i]+=purchase.n;paint();feedback(i===4?0:84*purchase.n,i===4?'유실자 레벨 +'+purchase.n:null);return true;
  }
  function buyShop(mode){
    if(locked){notify('1-1을 클리어하면 열려요');return false;}
    const p=budget(shopLevel,1000,shopCost,mode==='max'?Infinity:mode);
    if(!p.n||(mode==='max'&&p.n<2)){notify(shopLevel>=1000?'수선점이 최대 레벨이에요':'단추가 부족해요');return false;}
    const before=shopLevel;wallet-=p.spent;shopLevel+=p.n;paint();feedback(0,Math.floor(shopLevel/10)>Math.floor(before/10)?'Lv.'+Math.floor(shopLevel/10)*10+' · 별조각 보상을 우편에서 확인해요':'수선점 레벨 +'+p.n);return true;
  }
  function bindHold(button,action){
    let gesture=null;
    const stop=()=>{if(gesture){clearTimeout(gesture.timer);clearInterval(gesture.interval);}gesture=null;activeHolds.delete(stop);};
    button.onpointerdown=e=>{
      if(e.button!==0)return;e.preventDefault();button.focus({preventScroll:true});button.setPointerCapture(e.pointerId);
      gesture={id:e.pointerId,x:e.clientX,y:e.clientY,held:false,cancel:false};activeHolds.add(stop);
      gesture.timer=setTimeout(()=>{if(!gesture)return;gesture.held=true;if(!action()){stop();return}if(!gesture)return;gesture.interval=setInterval(()=>{if(!action())stop()},140)},450);
    };
    button.onpointermove=e=>{if(gesture&&Math.hypot(e.clientX-gesture.x,e.clientY-gesture.y)>8){gesture.cancel=true;clearTimeout(gesture.timer);clearInterval(gesture.interval)}};
    button.onpointerup=()=>{if(!gesture)return;const tap=!gesture.held&&!gesture.cancel;stop();if(tap)action()};
    button.onpointercancel=button.onlostpointercapture=stop;
    button.onclick=e=>{if(e.detail===0)action()};button.oncontextmenu=e=>e.preventDefault();
  }
  function closeConfirm(restore=true){
    const trigger=pending?.trigger;pending=null;screen.querySelector('.growth-confirm-overlay')?.remove();[...screen.children].forEach(e=>e.inert=false);
    syncSheetInput();
    if(restore&&trigger)screen.querySelector('#'+trigger)?.focus();
  }
  function confirmResonance(){
    if(locked){notify('1-1을 클리어하면 열려요');return;}
    if(resonances[character]>=10){notify('공명이 최대 단계예요');return;}
    if(fragments<20){notify('기억 조각이 '+(20-fragments)+' 부족해요');return;}
    stopHolds();const i=character,r=resonances[i],before=resonanceStats(i,r),after=resonanceStats(i,r+1);
    pending={i,r,trigger:'gr-resonate'};const overlay=document.createElement('div');overlay.className='growth-confirm-overlay';
    overlay.innerHTML=`<section class="growth-confirm" role="dialog" aria-modal="true" aria-label="공명 확인"><p class="gr-eyebrow">다시 이어지는 기억</p>${img(R+'resonance.png','confirm-art')}<h2>${roster[i].name}와 공명할까요?</h2><p class="gr-confirm-copy">기억 조각 20개를 사용해요.</p><div class="confirm-effects"><span>공명 ${r} → ${r+1}</span><b>공용 능력치 +${before.common}% → +${after.common}%</b><span>소환 비용 ${before.cost} → ${after.cost}</span><span>지속 ${before.duration.toFixed(1)}초 → ${after.duration.toFixed(1)}초</span></div><button class="gr-primary" id="gr-confirm-yes">기억 조각 20 · 공명하기</button><button class="gr-secondary" id="gr-confirm-no">아직은 돌아갈게요</button></section>`;
    [...screen.children].forEach(e=>e.inert=true);screen.append(overlay);overlay.querySelector('#gr-confirm-no').focus();
    overlay.querySelector('#gr-confirm-no').onclick=()=>closeConfirm();
    overlay.querySelector('#gr-confirm-yes').onclick=()=>{
      if(!pending)return;const {i,r}=pending;if(resonances[i]!==r||fragments<20){closeConfirm();return;}
      fragments-=20;resonances[i]++;closeConfirm(false);paint();feedback(0,'공명 '+r+' → '+(r+1)+' · 기억을 다시 이었어요');$('#gr-resonate')?.focus();
    };
  }
  function heading(){return `<header class="gr-header"><div class="gr-title-row">${view==='detail'?'<button class="gr-back" id="gr-back" aria-label="강화 목록으로">←</button>':''}<div><p class="gr-eyebrow">${view==='list'?'작은 수선, 큰 힘':'함께 자라는 기억'}</p><h2>${view==='list'?'유실자를 더 단단하게':roster[character].name}</h2></div><button class="close gr-close" aria-label="강화 시트 닫기">×</button></div><div class="gr-wallet"><span>${img(R+'currency_gold.png')}<b data-wallet></b><small data-income></small></span><span class="gr-memory">${img(R+'shards.png')}<b data-fragments></b><small>기억 조각</small></span></div></header>`;}
  function list(){return `<div class="gr-scroll" tabindex="-1"><section class="gr-shop"><div class="gr-shop-info">${img(R+'shop.png')}<div><h3>수선점 <span data-shop-level></span></h3><p data-shop-stats></p><small data-milestone></small></div></div><p class="gr-shop-status" data-shop-status role="status" hidden></p><div class="gr-shop-actions"><button class="gr-gold-button hold-level" id="gr-shop-one" aria-label="수선점 한 레벨 강화"></button><button class="gr-soft-button" id="gr-shop-ten">×10</button><button class="gr-soft-button" id="gr-shop-max"></button></div></section><button class="gr-recommend" id="gr-recommend">${img(R+'power.png')}<span>추천 · <b>짝짝이 레벨업</b><small data-recommend></small></span><b>›</b></button><div class="gr-grid">${roster.map((r,i)=>`<article class="gr-lineage" data-lineage="${i}"><span class="gr-resonance-dot" data-ready="${i}" aria-label="공명 가능"></span><button class="gr-portrait" data-detail="${i}" aria-label="${r.name} 성장 상세">${img(R+r.file)}<span data-position="${i}">${placement[i]}</span></button><div class="gr-card-title">${img(R+roles[i][1]+'.png','',roles[i][0])}<h3>${r.name}</h3><b data-level="${i}"></b></div><p class="gr-power" data-power="${i}"></p><button class="gr-level-button hold-level" data-level-up="${i}" aria-label="${r.name} 레벨업"></button></article>`).join('')}</div><p class="gr-hint">초상을 누르면 성장 전후를 자세히 볼 수 있어요.</p></div>`;}
  function detail(){const i=character,r=roster[i];return `<div class="gr-scroll"><section class="gr-character-head">${img(R+r.file)}<div><p>${img(R+roles[i][1]+'.png')}${roles[i][0]} · ${placement[i]}</p><h3>Lv.<span data-detail-level></span></h3><span class="gr-shared">지킴이 · 잔향이 레벨 공유</span></div></section><section class="gr-detail-section"><div class="gr-section-heading"><h3>한 단계 더 단단하게</h3><span data-next-level></span></div><div class="gr-stat-row"><span>공격력</span><b data-attack></b></div><div class="gr-stat-row"><span>체력</span><b data-hp></b></div><p class="gr-shared-note">레벨은 지킴이 · 잔향이에 함께 적용돼요</p><div class="gr-level-actions"><button id="gr-detail-one" class="gr-primary hold-level"></button><button id="gr-detail-ten" class="gr-soft-button"></button></div></section><section class="gr-resonance-section"><div class="gr-section-heading"><h3>${img(R+'resonance.png')}기억과 공명하기</h3><span data-resonance-level></span></div><p class="gr-fragment-cost">${img(R+'shards.png')}기억 조각 <b data-fragment-cost></b></p><div class="gr-stat-row"><span>공용 공격력 · 체력</span><b data-common></b></div><div class="gr-stat-row"><span>잔향이 소환 비용</span><b data-res-cost></b></div><div class="gr-stat-row"><span>잔향이 지속 시간</span><b data-duration></b></div><button class="gr-primary" id="gr-resonate"></button><p class="gr-shared-note">기억 조각은 중복 소환에서 얻을 수 있어요</p></section></div>`;}
  function markButton(el,disabled,text){if(!el)return;el.setAttribute('aria-disabled',String(disabled));el.classList.toggle('unavailable',disabled);el.innerHTML=text;}
  function paint(){
    updateMeta();screen.querySelectorAll('[data-wallet]').forEach(el=>el.textContent=fmt(wallet));screen.querySelectorAll('[data-fragments]').forEach(el=>el.textContent=fmt(fragments));screen.querySelectorAll('[data-income]').forEach(el=>el.textContent='+'+(12.4+(shopLevel-12)*.7).toFixed(1)+'/초');
    const levelLabel=i=>locked?'1-1 후 해금':levels[i]>=100?'최대 레벨':img(R+'currency_gold.png')+fmt(cost(levels[i]))+' · '+(wallet<cost(levels[i])?'부족':'레벨업');
    for(let i=0;i<roster.length;i++){
      const level=screen.querySelector(`[data-level="${i}"]`),power=screen.querySelector(`[data-power="${i}"]`),ready=screen.querySelector(`[data-ready="${i}"]`);
      if(level)level.textContent='Lv.'+levels[i];if(power){power.textContent=i===4?'미편성':levels[i]>=100?'최대 레벨':'전투력 +84';power.classList.toggle('unassigned',i===4);}
      if(ready)ready.hidden=locked||fragments<20||resonances[i]>=10;
      const levelButton=screen.querySelector(`[data-level-up="${i}"]`);markButton(levelButton,!!blockedLevel(i),levelLabel(i));if(levelButton)levelButton.setAttribute('aria-label',roster[i].name+' '+(blockedLevel(i)||('단추 '+cost(levels[i]))+' 레벨업'));
    }
    const shop=screen.querySelector('[data-shop-level]');if(shop){
      shop.textContent='Lv.'+shopLevel;screen.querySelector('[data-shop-stats]').innerHTML=`초당 단추 ${(12.4+(shopLevel-12)*.7).toFixed(1)} → ${(13.1+(shopLevel-12)*.7).toFixed(1)}<br>내구 ${fmt(1200+(shopLevel-12)*60)} → ${fmt(1260+(shopLevel-12)*60)}`;
      screen.querySelector('[data-milestone]').textContent='Lv.'+(Math.floor(shopLevel/10)+1)*10+'까지 '+(10-shopLevel%10)+' · 별조각 50 우편';
      const ten=budget(shopLevel,1000,shopCost,10),max=budget(shopLevel,1000,shopCost);
      const status=screen.querySelector('[data-shop-status]');status.hidden=!(locked||!ten.n);status.textContent=locked?'잠김 · 1-1을 클리어하면 열려요':shopLevel>=1000?'수선점이 최대 레벨이에요':'단추 '+fmt(Math.max(0,shopCost(shopLevel)-wallet))+' 부족 · 모은 뒤 다시 강화해요';
      markButton($('#gr-shop-one'),locked||!ten.n,locked?'1-1 후 해금':shopLevel>=1000?'최대':img(R+'currency_gold.png')+fmt(shopCost(shopLevel))+(ten.n?'':' · 부족'));
      markButton($('#gr-shop-ten'),locked||!ten.n,'×10'+(ten.n<10?' ('+ten.n+')':''));markButton($('#gr-shop-max'),locked||max.n<2,'최대 +'+max.n);
      screen.querySelector('[data-recommend]').textContent=levels[0]>=100?'다음 성장을 준비해요':'Lv.'+levels[0]+' → '+(levels[0]+1)+' · 전투력 +84';
    }
    if(view==='detail'){
      const i=character,l=levels[i],r=resonances[i],s=stats(i),before=resonanceStats(i,r),after=resonanceStats(i,Math.min(10,r+1));
      $('[data-detail-level]').textContent=l;$('[data-next-level]').textContent=l>=100?'최대 레벨':'Lv.'+l+' → '+(l+1);
      $('[data-attack]').innerHTML=l>=100?fmt(s.attack):fmt(s.attack)+' → <em>'+fmt(s.attack+17)+' (+17)</em>';
      $('[data-hp]').innerHTML=l>=100?fmt(s.hp):fmt(s.hp)+' → <em>'+fmt(s.hp+80)+' (+80)</em>';
      markButton($('#gr-detail-one'),!!blockedLevel(i),levelLabel(i));const ten=budget(l,100,cost,10);markButton($('#gr-detail-ten'),locked||!ten.n,'×10'+(ten.n<10?' ('+ten.n+')':''));
      $('[data-resonance-level]').textContent=r>=10?'최대 공명':'공명 '+r+' → '+(r+1);$('[data-fragment-cost]').textContent=r>=10?'최대':fmt(fragments)+' / 20';$('[data-fragment-cost]').classList.toggle('insufficient',fragments<20);
      $('[data-common]').innerHTML='+'+before.common+'%'+(r<10?' → <em>+'+after.common+'%</em>':'');
      $('[data-res-cost]').innerHTML=before.cost+(r<10?' → <em>'+after.cost+'</em>':'');$('[data-duration]').innerHTML=before.duration.toFixed(1)+'초'+(r<10?' → <em>'+after.duration.toFixed(1)+'초</em>':'');
      markButton($('#gr-resonate'),locked||fragments<20||r>=10,locked?'1-1 해금':r>=10?'최대 공명':fragments<20?'기억 조각 부족':'기억 조각 20 · 공명하기');
    }
  }
  function showGrowth(nextView=view){
    stopHolds();closeConfirm(false);view=nextView;
    originalSheet();const panel=screen.querySelector('.sheet');panel.classList.add('growth-panel');panel.classList.toggle('with-alert',showAlert);
    panel.setAttribute('aria-label',view==='list'?'강화 시트':'캐릭터 성장 상세');panel.innerHTML=heading()+(view==='list'?list():detail());
    const alert=screen.querySelector('.sheet-alert');if(!showAlert)alert?.remove();else{alert.classList.add('growth-alert');alert.onclick=()=>{const text=alert.innerText;showAlert=false;sheet=false;home();toast(text)}}
    panel.querySelector('.gr-close').onclick=()=>{stopHolds();sheet=false;showAlert=false;view='list';if(page==='growth')setPage('home');else home()};
    panel.querySelector('.gr-scroll').addEventListener('scroll',stopHolds,{passive:true});
    if(view==='list'){
      bindHold($('#gr-shop-one'),()=>buyShop(1));$('#gr-shop-ten').onclick=()=>buyShop(10);$('#gr-shop-max').onclick=()=>buyShop('max');
      panel.querySelectorAll('[data-level-up]').forEach(b=>bindHold(b,()=>buyLevel(Number(b.dataset.levelUp))));
      panel.querySelectorAll('[data-detail]').forEach(b=>b.onclick=()=>{character=Number(b.dataset.detail);showGrowth('detail');$('#gr-back').focus()});
      $('#gr-recommend').onclick=()=>{const target=panel.querySelector('[data-lineage="0"]');target.scrollIntoView({block:'nearest',behavior:'auto'});target.classList.add('recommended');setTimeout(()=>target.classList.remove('recommended'),1400)};
    }else{
      $('#gr-back').onclick=()=>{showGrowth('list');screen.querySelector(`[data-detail="${character}"]`)?.focus()};
      bindHold($('#gr-detail-one'),()=>buyLevel(character));$('#gr-detail-ten').onclick=()=>buyLevel(character,10);$('#gr-resonate').onclick=confirmResonance;
    }
    panel.setAttribute('role','region');syncSheetInput();paint();return panel;
  }
  function fixture(name){
    screen.querySelector('.toast')?.remove();screen.querySelector('.growth-feedback')?.remove();clearTimeout(feedbackTimer);feedbackTotal=0;
    closeConfirm(false);stopHolds();wallet=12450;fragments=36;levels=[12,10,11,9,8];resonances=[2,1,1,0,0];shopLevel=12;locked=false;character=0;lastToast=-Infinity;
    if(name==='poor')wallet=0;if(name==='partial')wallet=Array.from({length:7},(_,i)=>cost(12+i)).reduce((a,b)=>a+b,0);
    if(name==='max')levels=levels.map(()=>100);if(name==='locked')locked=true;if(name==='fragments')fragments=0;if(name==='resonance-max')resonances=Array(5).fill(10);
    if(name==='resonance-floor')resonances[0]=8;
    showGrowth();
  }
  showSheet=()=>showGrowth();
  home=function(){originalHome();updateMeta()};
  render=function(){if(page==='growth'){originalHome();updateMeta();fit()}else originalRender()};
  setPage=function(next){
    stopHolds();closeConfirm(false);
    if(next==='growth'){
      showAlert=false;view='list';originalPage('home');page='growth';sheet=true;showGrowth('list');
      document.querySelectorAll('[data-screen]').forEach(b=>b.classList.toggle('active',b.dataset.screen==='growth'));
      $('#home-controls').hidden=true;$('#failure-controls').hidden=true;$('#growth-controls').hidden=false;$('#screen-caption').textContent='05 / GROWTH & RESONANCE';
      screen.querySelector('.gr-close')?.focus({preventScroll:true});
    }else{$('#growth-controls').hidden=true;originalPage(next);}
  };
  document.querySelector('[data-screen="growth"]').onclick=()=>setPage('growth');
  $('#growth-state').onchange=e=>fixture(e.target.value);
  $('#open-sheet').onclick=()=>{showAlert=true;sheet=true;view='list';showGrowth()};
  document.addEventListener('keydown',e=>{
    const overlay=screen.querySelector('.growth-confirm-overlay');if(!overlay){
      if(e.key==='Escape'&&screen.querySelector('.growth-panel')){e.preventDefault();screen.querySelector('.gr-close').click();screen.querySelector('[data-nav="0"]')?.focus({preventScroll:true})}
      return;
    }
    if(e.key==='Escape'){e.preventDefault();closeConfirm();}
    if(e.key==='Tab'){const buttons=[...overlay.querySelectorAll('button')],first=buttons[0],last=buttons.at(-1);if(e.shiftKey&&(document.activeElement===first||!overlay.contains(document.activeElement))){e.preventDefault();last.focus()}else if(!e.shiftKey&&(document.activeElement===last||!overlay.contains(document.activeElement))){e.preventDefault();first.focus()}}
  },true);
  if(new URLSearchParams(location.search).get('view')==='growth')setPage('growth');
  window.KNGrowthSnapshot=()=>({wallet,fragments,levels:[...levels],resonances:[...resonances],shopLevel,character,view,locked,confirming:!!pending});
})();
