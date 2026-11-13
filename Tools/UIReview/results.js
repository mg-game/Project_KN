/* Combat result art review: local fixtures, no game/save/economy integration. */
(()=>{
  const priorPage=setPage,priorRender=render,priorHome=home;
  home=function(){priorHome();const text={poor:'잔향 부족 · 모이면 다시 불러요',capacity:'소환 상한 · 빈자리를 기다려요',locked:'잠김 · AUTO 1-2 / 배속 1-6 해금',cooldown:'재사용 대기 · 다른 카드 사용 가능'}[battle];if(text){const note=document.createElement('p');note.className='battle-state-note';note.dataset.state=battle;note.setAttribute('role','status');note.textContent=text;screen.querySelector('.arena')?.append(note)}};
  let kind='clear',event=0,receipt=null,details=false,started=0,deadline=0,timer=null,transitioned=false,destination=null,transitions=0;
  const received=new Set();
  const models={
    clear:{stage:4,next:5,title:'오늘도, 불빛을 지켰어요.',tag:'STAGE CLEAR',route:'다음 구역으로',copy:'작은 수선점에 평온이 돌아왔어요.',win:true},
    repeat:{stage:4,next:4,title:'한 번 더, 차곡차곡.',tag:'FARMING COMPLETE',route:'같은 구역 반복',copy:'익숙한 거리에서 다시 힘을 모아요.',win:true},
    boss:{stage:10,next:11,title:'더 먼 밤으로 이어지는 빛.',tag:'BOSS CLEAR',route:'다음 구역으로',copy:'커다란 그림자가 물러갔어요.',win:true,materials:true},
    fail:{stage:4,next:4,title:'아직, 끝난 밤은 아니에요.',tag:'TRY AGAIN',route:'같은 구역 재도전',copy:'이번에 모은 단추는 가지고 돌아가요.',win:false,progress:.6},
    stepback:{stage:4,next:3,title:'한 걸음 쉬어 가도 괜찮아요.',tag:'A LITTLE REST',route:'이전 구역에서 힘 모으기',copy:'두 번의 도전 뒤, 익숙한 거리로 돌아가요.',win:false,progress:.6},
    retreat:{stage:4,next:null,title:'잠시, 불빛 곁으로.',tag:'RETURN SAFELY',route:'전투 리포트 보기',copy:'다시 이어 갈 준비를 해 볼까요?',win:false,retreat:true}
  };
  const model=()=>models[kind];
  const stageName=n=>n===4?'1-4 우산 보관소':`스테이지 ${n}`;
  function settle(){
    if(received.has(event))return;
    const m=model(),base=Math.floor(60*1.13**(m.stage-1));
    receipt={id:event,gold:m.retreat?null:m.win?base:Math.floor(base*.3*m.progress),candy:m.win?(m.stage%10===0?10:3):0,affection:m.win?1:0};
    received.add(event);
  }
  function cancelClock(){clearInterval(timer);timer=null;}
  function remaining(){return Math.max(0,deadline-performance.now())/1000;}
  function runClock(){
    cancelClock();if(!$('#result-play').checked||model().retreat||transitioned)return;
    if(!started){started=performance.now();deadline=started+(details?6000:1500);}
    timer=setInterval(()=>{
      if(page!=='results'){cancelClock();return;}
      const left=remaining(),label=$('[data-result-time]');
      if(label)label.textContent=`${left.toFixed(1)}초 후 자동 이동`;
      const bar=$('.rs-countdown i');if(bar)bar.style.width=`${Math.min(100,left/(details?6:1.5)*100)}%`;
      if(left<=0)advance();
    },40);
  }
  function advance(){
    if(transitioned)return;transitioned=true;transitions++;destination=model().next;cancelClock();
    if(model().retreat){cause='retreat';$('#failure-cause').value='retreat';setPage('failure');return;}
    setPage('home');toast(`${stageName(destination)} · ${model().win?'다음 전투를 시작해요':'다시 힘을 모아요'}`);
    // Destination is a display-only fixture; no actual stage, combat or wallet is changed.
    $('.stage-name').textContent=stageName(destination);
  }
  function newResult(value){
    cancelClock();kind=value;event++;receipt=null;details=false;started=0;deadline=0;transitioned=false;destination=null;
    settle();draw();$('#rs-next').focus({preventScroll:true});runClock();
  }
  const reward=(file,name,value)=>`<button class="rs-reward" data-reward="${name}" aria-label="${name} ${value}개 상세">${img(R+file)}<span>${name}</span><strong>+${value}</strong></button>`;
  function draw(){
    sheet=false;home();screen.querySelectorAll(':scope > *').forEach(x=>x.inert=true);
    const m=model(),fail=!m.win;
    const el=document.createElement('section');el.className=`result-view ${fail?'rs-rest':''}`;el.setAttribute('role','dialog');el.setAttribute('aria-modal','true');el.setAttribute('aria-label','전투 결과');
    el.innerHTML=`<header class="rs-top"><span>${stageName(m.stage)}</span><span>${m.retreat?'긴급 귀환':m.win?'웨이브 4 / 4':'웨이브 3 / 4'}</span></header>
      <div class="rs-scroll">
        <div class="rs-hero"><div class="rs-orbit"></div><span class="rs-spark s1">✦</span><span class="rs-spark s2">✧</span><span class="rs-spark s3">✦</span>${img(R+'lamp.png','rs-lamp')}<span class="rs-hero-tag">${m.tag}</span><h2>${m.title}</h2><p>${m.copy}</p><div class="rs-friends">${[1,0,2].map(i=>img(R+roster[i].file,'',roster[i].name)).join('')}</div></div>
        <div class="rs-card"><div class="rs-section-heading"><h3>${m.retreat?'귀환 안내':'획득 보상'}</h3><span>${m.retreat?'보상 안내 준비 중':'✓ 자동 정산 완료'}</span></div>
          ${m.retreat?'<p class="rs-return-note">귀환했어요. 다음 도전을 준비해 보세요.</p>':`<div class="rs-rewards">${reward('currency_gold.png','단추',receipt.gold)}${m.win?reward('currency_gem.png','별빛 사탕',receipt.candy):`<div class="rs-progress"><b>처치 진행도</b><strong>${Math.round(m.progress*100)}%</strong><span>진행도에 따른 일부 보상</span></div>`}</div>`}
          ${m.win?`<div class="rs-materials">${m.materials?'수선 천 ×1 <i>·</i> 리본 조각 ×1':'이번 전투 재료 보상 없음'}</div>`:''}
        </div>
        ${m.win?`<div class="rs-bond"><div class="rs-bond-faces">${[0,1,2,3,4].map(i=>img(R+roster[i].file)).join('')}</div><div><b>함께 지킨 기억</b><span>편성한 계보 호감도 +1</span></div><em>+1</em></div>`:`<div class="rs-guidance"><b>${kind==='stepback'?'조금 쉬운 곳에서 다시 준비해요':m.retreat?'편성과 성장을 확인해 보세요':'도움이 필요할 땐, 함께 준비해요'}</b><p>${kind==='stepback'?'연속 2회 실패 · 스테이지 3으로 이동':'편성 변경과 강화로 다음 도전을 준비할 수 있어요.'}</p></div>`}
        <div class="rs-route"><span class="rs-route-icon">${m.win?'↗':'↶'}</span><div><b>${m.route}</b><p>${m.next?stageName(m.next):'퇴각 원인과 준비할 항목을 확인해요'}</p></div>${m.next?'<span class="rs-route-arrow">→</span>':''}</div>
        <div class="rs-detail" ${details?'':'hidden'}><h3>이번 전투 자세히</h3><p>${m.retreat?'퇴각 보상 안내는 준비 중이에요.':m.win?`단추 기본 보상 ${receipt.gold} · 보너스 0%<br>별빛 사탕 ${receipt.candy} · 이벤트 배율 1배<br>${m.materials?'재료는 이번 전투의 드롭 결과예요.':'이번 전투에서 재료는 나오지 않았어요.'}`:`단추 기본 보상 86 × 30% × 처치 진행도 60%<br>소수점 아래를 버려 단추 ${receipt.gold}개를 받았어요.`}</p>${m.retreat?'':'<p>획득 보상은 이미 반영되었어요.<br>버튼을 다시 눌러도 중복으로 받지 않아요.</p>'}</div>
      </div>
      <footer class="rs-actions"><div class="rs-wait"><span data-result-time>${m.retreat?'귀환 내용을 확인해 주세요':$('#result-play').checked?'자동 이동 준비 중':'다음 전투 대기 · 1.5초'}</span><button id="rs-details" aria-expanded="${details}">${details?'접기':'자세히 보기'}</button></div><div class="rs-countdown" ${m.retreat?'hidden':''}><i></i></div><button id="rs-next">${m.retreat?'전투 리포트':m.win?(kind==='repeat'?'다시 전투하기':'다음 전투로'):(kind==='stepback'?'이전 구역으로':'지금 다시 도전')} <span>→</span></button>${fail?'<div class="rs-help-actions"><button id="rs-formation">편성 변경</button><button id="rs-growth">강해지기</button></div>':'<p class="rs-auto-note">보상은 자동으로 지급됩니다.</p>'}</footer>`;
    screen.append(el);screen.classList.remove('critical');screen.classList.toggle('reduced',$('#motion').checked);
    $('#rs-next').onclick=advance;
    $('#rs-details').onclick=()=>toggleDetails();
    el.querySelectorAll('[data-reward]').forEach(b=>b.onclick=()=>toggleDetails(true));
    if(fail){$('#rs-formation').onclick=()=>setPage('formation');$('#rs-growth').onclick=()=>setPage('growth');}
    fit();
  }
  function toggleDetails(force){
    details=force===true?true:!details;
    // Extension is bounded by the original result end, never by the last click.
    if(details&&started)deadline=started+6000;
    const scroll=$('.rs-scroll').scrollTop;draw();$('.rs-scroll').scrollTop=scroll;
    $('#rs-details').focus({preventScroll:true});if(details)$('.rs-detail').scrollIntoView({block:'nearest'});
    runClock();
  }
  setPage=function(next){
    cancelClock();$('#result-controls').hidden=next!=='results';
    if(next==='results'){
      priorPage('home');page='results';$('#home-controls').hidden=true;$('#failure-controls').hidden=true;$('#growth-controls').hidden=true;
      document.querySelectorAll('[data-screen]').forEach(b=>b.classList.toggle('active',b.dataset.screen==='results'));
      $('#screen-caption').textContent='06 / COMBAT RESULTS';newResult($('#result-state').value);
    }else priorPage(next);
  };
  render=function(){if(page==='results'){draw();runClock()}else priorRender()};
  document.querySelector('[data-screen="results"]').onclick=()=>setPage('results');
  $('#result-state').onchange=e=>newResult(e.target.value);
  $('#result-restart').onclick=()=>newResult(kind);
  $('#result-play').onchange=()=>{cancelClock();started=0;deadline=0;draw();runClock()};
  screen.addEventListener('click',e=>{
    if(!e.target.closest('.sheet-alert'))return;
    const state=$('#alert-state').value;
    if(state!=='clear'&&state!=='fail')return;
    e.preventDefault();e.stopImmediatePropagation();
    $('#result-state').value=state==='clear'?'clear':'fail';setPage('results');
  },true);
  document.addEventListener('keydown',e=>{
    if(page!=='results')return;const panel=$('.result-view');
    if(e.key==='Escape'&&details){e.preventDefault();toggleDetails(false);}
    if(e.key==='Tab'&&panel.contains(document.activeElement)){
      const list=[...panel.querySelectorAll('button:not(:disabled)')],first=list[0],last=list.at(-1);
      if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus()}
      else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus()}
    }
  });
  window.KNResultsSnapshot=()=>({kind,event,receipt:{...receipt},receiptsForEvent:received.has(event)?1:0,details,started,deadline,remaining:remaining(),transitioned,destination,transitions,playing:!!timer});
  if(new URLSearchParams(location.search).get('view')==='results')setPage('results');
})();
