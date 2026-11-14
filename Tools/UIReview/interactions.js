/* Review-only M1 interactions. No combat simulation, persistence or game writes. */
(()=>{
  const originalHome=home,originalFormation=formation,originalFailure=failure,originalSetPage=setPage,originalMove=moveTo,originalToast=toast;
  const directions=['N','NE','E','SE','S','SW','W','NW'];
  let autoInvest=true,aim=null,lastSummon=null,modalView='settings',restoreFocus='pause',suppressSlotClick=0;
  const formationStops=new Set();
  const heldModalKeys=new Set();
  const modalKeys=new Set(['Enter','Escape',' ','Spacebar']);
  const modalTriggers='[data-screen],[data-nav],#pause,#back-home,.gr-close,.gr-back,#gr-resonate';
  // A key that navigates a dialog must be released before it can activate the next screen.
  document.addEventListener('keydown',e=>{
    if(!modalKeys.has(e.key))return;
    const key=e.code||e.key;
    const scoped=!!screen.querySelector('.modal-overlay,.growth-confirm-overlay,.failure-modal,.result-view')
      ||(e.key==='Escape'&&(!!aim||!!screen.querySelector('.growth-panel')))
      ||(e.key!=='Escape'&&!!e.target.closest?.(modalTriggers));
    if(heldModalKeys.has(key)||(scoped&&e.repeat)){
      heldModalKeys.add(key);e.preventDefault();e.stopImmediatePropagation();return;
    }
    if(scoped)heldModalKeys.add(key);
  },true);
  document.addEventListener('keyup',e=>heldModalKeys.delete(e.code||e.key),true);
  function stopFormation(){[...formationStops].forEach(stop=>stop());screen.querySelector('.formation-drag-ghost')?.remove();}
  const lock=`<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M7 10V7a5 5 0 0 1 10 0v3" fill="none" stroke="currentColor" stroke-width="2"/><rect x="4" y="9" width="16" height="13" rx="3" fill="currentColor"/></svg>`;
  const reduced=()=>$('#motion').checked;
  toast=function(text){
    const dialog=screen.querySelector('.modal-overlay [role=dialog]');
    if(!dialog){originalToast(text);return;}
    dialog.querySelector('.dialog-status')?.remove();const el=document.createElement('p');
    el.className='dialog-status';el.setAttribute('role','status');el.textContent=text;dialog.append(el);setTimeout(()=>el.remove(),2400);
  };
  function clearAim(){
    const current=aim;aim=null;if(current)clearTimeout(current.timer);
    if(current?.button?.hasPointerCapture(current.id))current.button.releasePointerCapture(current.id);
    screen.querySelector('.aim-preview')?.remove();screen.querySelector('.aim-caption')?.remove();
    screen.querySelector('.deck')?.classList.remove('aiming');
  }
  function reason(i){
    if(paused)return'전투를 계속한 뒤 불러 주세요';
    if(battle==='capacity')return'흩어질 잔향이를 기다려 주세요';
    if(battle==='cooldown'&&i===0)return'3초 뒤 다시 부를 수 있어요';
    if(echo<(i===1?6:5))return'잔향이 부족해요';
    return'';
  }
  function summon(i,sector=null){
    if(page!=='home'||sheet||screen.querySelector('.modal-overlay,.result-view,.failure-modal'))return false;
    const blocked=reason(i);if(blocked){toast(blocked);return false;}
    echo-=i===1?6:5;lastSummon={character:i,sector,remaining:echo};
    home();toast(`${roster[i].name}의 잔향이를 ${sector===null?'불렀어요':directions[sector]+' 방향으로 불렀어요'}`);
    if(sector!==null){
      const ring=$('.battle-ring'),arena=$('.arena');
      const cx=ring.offsetLeft+ring.offsetWidth/2,cy=ring.offsetTop+ring.offsetHeight/2;
      const r=ring.offsetWidth*.29,point=position(sector,r,cx,cy);
      const el=document.createElement('div');el.className='summon-echo';el.style.left=cx+'px';el.style.top=cy+'px';el.innerHTML=img(R+roster[i].file,'',roster[i].name+' 잔향이');arena.append(el);
      if(!reduced())el.animate([{left:cx+'px',top:cy+'px'},{left:point.x+'px',top:point.y+'px'}],{duration:650,fill:'forwards',easing:'ease-out'});
      setTimeout(()=>el.remove(),1600);
    }
    return true;
  }
  function pointIn(x,y,el){const r=el.getBoundingClientRect();return x>=r.left&&x<=r.right&&y>=r.top&&y<=r.bottom;}
  function drawAim(x,y){
    if(!aim)return;
    const arena=$('.arena'),ring=$('.battle-ring'),box=arena.getBoundingClientRect(),rb=ring.getBoundingClientRect();
    const dx=x-(rb.left+rb.width/2),dy=y-(rb.top+rb.height/2);
    const sector=(Math.round(Math.atan2(dx,-dy)/(Math.PI/4))+8)%8;aim.sector=sector;
    const cx=ring.offsetLeft+ring.offsetWidth/2,cy=ring.offsetTop+ring.offsetHeight/2,r=ring.offsetWidth*.43;
    const coord=deg=>[cx+Math.sin(deg*Math.PI/180)*r,cy-Math.cos(deg*Math.PI/180)*r];
    const [x1,y1]=coord(sector*45-22.5),[x2,y2]=coord(sector*45+22.5);
    let overlay=screen.querySelector('.aim-preview');if(!overlay){overlay=document.createElement('div');overlay.className='aim-preview';arena.append(overlay);}
    const valid=pointIn(x,y,arena);overlay.classList.toggle('cancel',!valid);
    overlay.innerHTML=`<svg viewBox="0 0 390 ${arena.offsetHeight}" aria-hidden="true"><path d="M${cx} ${cy}L${x1} ${y1}A${r} ${r} 0 0 1 ${x2} ${y2}Z" fill="${valid?'#AADCCE55':'#DDA1B233'}" stroke="${valid?'#C8F1DD':'#EFB8C5'}" stroke-width="2"/><circle cx="${cx}" cy="${cy}" r="6" fill="#FFF0CA"/></svg>`;
    let caption=screen.querySelector('.aim-caption');if(!caption){caption=document.createElement('div');caption.className='aim-caption';caption.setAttribute('role','status');arena.append(caption);}
    caption.textContent=valid?directions[sector]+' 방향 · 놓으면 소환':'덱 위에서 놓으면 취소';
    screen.querySelector('.deck').classList.add('aiming');
  }
  function bindSummon(button,i){
    button.onpointerdown=e=>{
      if(e.button!==0||aim||!button.isConnected||page!=='home'||sheet)return;const blocked=reason(i);if(blocked){toast(blocked);return;}
      e.preventDefault();button.setPointerCapture(e.pointerId);
      aim={i,id:e.pointerId,x:e.clientX,y:e.clientY,active:false,sector:0,button};
      aim.timer=setTimeout(()=>{if(!aim)return;aim.active=true;drawAim(aim.x,aim.y)},200);
    };
    button.onpointermove=e=>{if(!aim||aim.id!==e.pointerId)return;aim.x=e.clientX;aim.y=e.clientY;if(aim.active)drawAim(e.clientX,e.clientY)};
    button.onpointerup=e=>{
      if(!aim||aim.id!==e.pointerId)return;const current=aim;
      const valid=current.active&&pointIn(e.clientX,e.clientY,$('.arena'));
      clearAim();if(button.hasPointerCapture(e.pointerId))button.releasePointerCapture(e.pointerId);
      if(!current.active)summon(i);else if(valid)summon(i,current.sector);else toast('소환을 취소했어요');
    };
    button.onpointercancel=button.onlostpointercapture=()=>{if(aim){clearAim();toast('소환을 취소했어요')}};
    button.onclick=e=>{if(e.detail===0&&summon(i))screen.querySelector(`[data-summon="${i}"]`)?.focus()};
    button.oncontextmenu=e=>e.preventDefault();
  }
  function clearModal(){
    screen.querySelector('.modal-overlay')?.remove();
    [...screen.children].forEach(el=>el.inert=false);
  }
  function mountDialog(content,label,focusId){
    clearModal();const layer=document.createElement('div');layer.className='modal-overlay';
    layer.innerHTML=`<section class="pause-dialog" role="dialog" aria-modal="true" aria-label="${label}">${content}</section>`;
    [...screen.children].forEach(el=>el.inert=true);screen.append(layer);
    layer.addEventListener('click',e=>{if(e.target===layer)e.stopPropagation()});
    layer.querySelector('#'+focusId)?.focus();return layer;
  }
  function resume(){paused=false;clearModal();home();$('#'+restoreFocus)?.focus();}
  function openPause(view='settings',focusId=null){
    focusId=focusId||(view==='help'?'back-pause':view==='retreat'?'cancel-retreat':'resume-battle');
    clearAim();sheet=false;paused=true;modalView=view;
    const summary=`${stageLabel()} · Wave ${battle==='boss'||battle==='surge'?'4':'2'}/4 · ${battle==='critical'?'0:18':'2:14'}`;
    let content='';
    if(view==='settings'){
      content=`<p class="dialog-eyebrow">잠시 쉬어 가요</p><h2>수선점은 잠시 쉬는 중</h2><p class="battle-summary">${summary}</p><div class="pause-settings"><div class="setting-row"><span>전투 속도</span><div class="speed-options"><button id="pause-speed-1" aria-pressed="${speed===1}">×1</button><button id="pause-speed-2" aria-pressed="${speed===2}" ${battle==='locked'?'aria-disabled="true"':''}>${battle==='locked'?lock:'×2'}</button></div></div><div class="setting-row"><span>자동 소환</span><button class="ui-switch" id="pause-auto" role="switch" aria-checked="${auto}" aria-label="자동 소환">${auto?'켜짐':'꺼짐'}</button></div><div class="setting-row"><span>수선등 자동 투자</span><button class="ui-switch" id="pause-invest" role="switch" aria-checked="${autoInvest}" aria-label="수선등 자동 투자">${autoInvest?'켜짐':'꺼짐'}</button></div></div><button class="dialog-link" id="open-help">탭 · 홀드 조작 도움말 <span>›</span></button><button class="dialog-primary" id="resume-battle">계속 지키기</button><button class="dialog-retreat" id="open-retreat">이번 전투에서 물러나기</button>`;
    }else if(view==='help'){
      content=`<p class="dialog-eyebrow">조작 도움말</p><h2>작은 손짓으로 함께해요</h2><div class="help-step"><b>01 · 탭</b><p>카드를 가볍게 누르면 잔향이가<br>필요한 방향으로 찾아가요.</p></div><div class="help-step"><b>02 · 홀드하고 끌기</b><p>카드를 길게 누르고 전장으로 끌어 주세요.<br>밝아진 방향에 놓으면 그곳으로 불러요.</p></div><div class="help-step"><b>03 · 되돌려 취소</b><p>덱 위로 돌아와 놓으면 취소돼요.<br>잔향은 사용하지 않아요.</p></div><div class="behavior-legend">${[['advance','앞장서기'],['walk','함께 걷기'],['guard','곁 지키기']].map(([k,t])=>`<div>${icon('behavior_'+k+'_on')}<span>${t}</span></div>`).join('')}</div><button class="dialog-primary" id="back-pause">일시정지로 돌아가기</button>`;
    }else{
      content=`<p class="dialog-eyebrow">퇴각 확인</p>${icon('direction_danger','retreat-icon')}<h2>이번에는 물러날까요?</h2><p class="retreat-copy">현재 전투는 실패로 기록돼요.<br>편성을 정비한 뒤 다시 도전할 수 있어요.</p><button class="dialog-primary" id="cancel-retreat">돌아가서 계속 준비하기</button><button class="dialog-danger" id="confirm-retreat">네, 물러날게요</button>`;
    }
    const layer=mountDialog(content,view==='settings'?'일시정지':view==='help'?'조작 도움말':'퇴각 확인',focusId);
    if(view==='settings'){
      layer.querySelector('#resume-battle').onclick=resume;
      layer.querySelector('#pause-speed-1').onclick=()=>{speed=1;openPause('settings','pause-speed-1')};
      layer.querySelector('#pause-speed-2').onclick=()=>{if(battle==='locked'){toast('1-6을 클리어하면 열려요');return}speed=2;openPause('settings','pause-speed-2')};
      layer.querySelector('#pause-auto').onclick=()=>{if(battle==='locked'){toast('1-2를 클리어하면 열려요');return}auto=!auto;openPause('settings','pause-auto')};
      layer.querySelector('#pause-invest').onclick=()=>{if(battle==='locked'){toast('1-2를 클리어하면 열려요');return}autoInvest=!autoInvest;openPause('settings','pause-invest')};
      layer.querySelector('#open-help').onclick=()=>openPause('help','back-pause');
      layer.querySelector('#open-retreat').onclick=()=>openPause('retreat','cancel-retreat');
    }else if(view==='help')layer.querySelector('#back-pause').onclick=()=>openPause('settings','open-help');
    else{
      layer.querySelector('#cancel-retreat').onclick=()=>openPause('settings','open-retreat');
      layer.querySelector('#confirm-retreat').onclick=()=>{paused=false;clearModal();cause='retreat';$('#failure-cause').value='retreat';setPage('failure')};
    }
  }
  home=function(){
    clearAim();stopFormation();
    originalHome();
    const mixed=auto!==autoInvest,master=$('#auto');
    master.innerHTML=battle==='locked'?lock:`AUTO${mixed?'<small>일부</small>':''}`;
    master.classList.toggle('on',auto||autoInvest);master.setAttribute('aria-pressed',mixed?'mixed':String(auto&&autoInvest));
    master.setAttribute('aria-label',battle==='locked'?'자동 전투 잠김':mixed?'자동 전투 일부 켜짐':auto?'자동 전투 켜짐':'자동 전투 꺼짐');
    master.onclick=()=>{if(battle==='locked'){toast('1-2를 클리어하면 열려요');return}auto=autoInvest=!(auto&&autoInvest);home()};
    $('#pause').onclick=()=>openPause();
    if(battle==='locked'){$('#speed').innerHTML=lock;$('#speed').setAttribute('aria-label','배속 잠김, 1-6 클리어 후 해금');}
    screen.querySelectorAll('[data-summon]').forEach((b,i)=>{
      const blocked=reason(i);b.setAttribute('aria-disabled',String(!!blocked));b.classList.toggle('poor',!!blocked);
      if(battle==='cooldown'&&i===0){b.classList.add('cooling');b.insertAdjacentHTML('beforeend','<span class="cooldown-cover"><b>3초</b></span>');}
      bindSummon(b,i);
    });
    screen.querySelectorAll('.portrait').forEach((el,i)=>{
      el.style.setProperty('--cooldown-angle',((i+1)*45)+'deg');
      el.setAttribute('role','button');el.tabIndex=0;el.setAttribute('aria-label',roster[i].name+' 위치 확인');
      const highlight=()=>{const guardian=screen.querySelectorAll('.guardian')[slots.filter(x=>x!==null).indexOf(i)];if(!guardian)return;guardian.classList.add('position-highlight');setTimeout(()=>guardian.classList.remove('position-highlight'),1000)};
      el.onclick=highlight;el.onkeydown=e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();highlight()}};
    });
    if(paused)openPause(modalView);
    screen.classList.toggle('reduced',reduced());
  };
  moveTo=function(a){
    if(selected===null)return;
    if(slots.indexOf(selected)>=0){originalMove(a);return;}
    if(slots.filter(x=>x!==null).length>=5){toast('지킴이는 다섯 명까지 함께할 수 있어요');return;}
    if(slots[a]!==null){toast('빈 방향을 골라 주세요');return;}
    slots[a]=selected;selected=null;formation();toast('새 자리에 함께했어요');
  };
  formation=function(){
    clearAim();stopFormation();
    originalFormation();
    screen.querySelectorAll('[data-slot]').forEach(button=>{
      const a=Number(button.dataset.slot);if(slots[a]===null)return;
      button.ondragstart=e=>e.preventDefault();button.querySelectorAll('img').forEach(image=>image.draggable=false);
      let gesture=null;
      const stop=()=>{const current=gesture;gesture=null;if(current)clearTimeout(current.timer);formationStops.delete(stop);if(current&&button.hasPointerCapture(current.id))button.releasePointerCapture(current.id);screen.querySelector('.formation-drag-ghost')?.remove();};
      button.onpointerdown=e=>{
        if(e.button!==0||e.isPrimary===false||gesture||formationStops.size)return;button.setPointerCapture(e.pointerId);
        gesture={id:e.pointerId,startX:e.clientX,startY:e.clientY,character:slots[a],drag:false};formationStops.add(stop);
        const current=gesture;
        gesture.timer=setTimeout(()=>{if(gesture!==current)return;if(page!=='formation'||!button.isConnected){stop();return}slots[a]=null;selected=null;suppressSlotClick=performance.now()+300;stop();formation();toast('유실자가 목록으로 돌아왔어요')},600);
      };
      button.onpointermove=e=>{
        if(!gesture||gesture.id!==e.pointerId)return;
        if(!gesture.drag&&Math.hypot(e.clientX-gesture.startX,e.clientY-gesture.startY)>8){gesture.drag=true;clearTimeout(gesture.timer);}
        if(!gesture.drag)return;
        const r=screen.getBoundingClientRect(),scale=r.width/390;let ghost=screen.querySelector('.formation-drag-ghost');
        if(!ghost){ghost=document.createElement('div');ghost.className='formation-drag-ghost';ghost.innerHTML=img(R+roster[gesture.character].file);screen.append(ghost);}
        ghost.style.left=(e.clientX-r.left)/scale+'px';ghost.style.top=(e.clientY-r.top)/scale+'px';
      };
      button.onpointerup=e=>{
        if(!gesture||gesture.id!==e.pointerId)return;const current=gesture;
        const target=document.elementFromPoint(e.clientX,e.clientY)?.closest('[data-slot]');stop();
        if(current.drag){suppressSlotClick=performance.now()+300;if(target){selected=current.character;moveTo(Number(target.dataset.slot))}else toast('자리 이동을 취소했어요');}
      };
      button.onpointercancel=button.onlostpointercapture=e=>{if(gesture?.id===e.pointerId)stop()};button.oncontextmenu=e=>e.preventDefault();
    });
    screen.querySelectorAll('[data-preset]').forEach(button=>{
      const i=Number(button.dataset.preset);let gesture=null,saved=false,cancelled=false;
      const stop=()=>{const current=gesture;gesture=null;if(current)clearTimeout(current.timer);formationStops.delete(cancel);if(current&&button.hasPointerCapture(current.id))button.releasePointerCapture(current.id)};
      const cancel=()=>{cancelled=true;stop()};
      button.onpointerdown=e=>{
        if(e.button!==0||e.isPrimary===false||gesture||formationStops.size)return;saved=false;cancelled=false;button.setPointerCapture(e.pointerId);
        gesture={id:e.pointerId,x:e.clientX,y:e.clientY};formationStops.add(cancel);
        const current=gesture;
        gesture.timer=setTimeout(()=>{if(gesture!==current)return;if(page!=='formation'||!button.isConnected){cancel();return}presets[i]=[...slots];saved=true;toast('프리셋 '+(i+1)+'에 저장했어요')},600);
      };
      button.onpointermove=e=>{if(gesture?.id===e.pointerId&&Math.hypot(e.clientX-gesture.x,e.clientY-gesture.y)>8)cancel()};
      button.onpointerup=e=>{if(gesture?.id===e.pointerId)stop()};
      button.onpointercancel=button.onlostpointercapture=e=>{if(gesture?.id===e.pointerId)cancel()};
      button.onpointerleave=null;
      button.onclick=e=>{if(saved){saved=false;return}if(cancelled&&e.detail!==0)return;slots=[...presets[i]];selected=null;formation();toast('프리셋 '+(i+1)+'을 불러왔어요')};
      button.oncontextmenu=e=>e.preventDefault();
    });
  };
  failure=function(){
    originalFailure();
    const dialog=screen.querySelector('.failure-modal');
    [...screen.children].forEach(e=>e.inert=e!==dialog);
    dialog.setAttribute('aria-modal','true');dialog.querySelector('#retry').focus({preventScroll:true});
    if(cause==='retreat'){
      screen.querySelector('.cause-label').textContent='퇴각';screen.querySelector('.report-copy h3').textContent='이번 전투에서 물러났어요';
      screen.querySelector('.report-copy p').textContent='편성을 정비하고 수선점을 다시 지켜 주세요.';
    }
  };
  setPage=function(p){clearAim();stopFormation();clearModal();paused=false;originalSetPage(p);};
  const cancelInput=()=>{heldModalKeys.clear();clearAim();stopFormation()};
  window.addEventListener('blur',cancelInput);
  document.addEventListener('visibilitychange',()=>{if(document.hidden)cancelInput()});
  screen.addEventListener('click',e=>{if(e.detail>0&&performance.now()<suppressSlotClick&&e.target.closest('[data-slot]')){e.preventDefault();e.stopImmediatePropagation()}},true);
  screen.addEventListener('pointerdown',()=>{suppressSlotClick=0},true);
  document.addEventListener('keydown',e=>{
    if(e.key==='Escape'&&aim){e.preventDefault();clearAim();toast('소환을 취소했어요');return;}
    const report=screen.querySelector('.failure-modal');
    if(report&&e.key==='Tab'){
      const items=[...report.querySelectorAll('button:not(:disabled)')],first=items[0],last=items.at(-1);
      if(e.shiftKey&&(document.activeElement===first||!report.contains(document.activeElement))){e.preventDefault();last.focus()}
      else if(!e.shiftKey&&(document.activeElement===last||!report.contains(document.activeElement))){e.preventDefault();first.focus()}
      return;
    }
    const dialog=screen.querySelector('.modal-overlay [role=dialog]');if(!dialog)return;
    if(e.key==='Escape'){e.preventDefault();if(modalView==='retreat'||modalView==='help')openPause('settings',modalView==='help'?'open-help':'open-retreat');else resume();}
    if(e.key==='Tab'){
      const items=[...dialog.querySelectorAll('button:not([disabled]),[tabindex="0"]')],first=items[0],last=items[items.length-1];
      if(e.shiftKey&&(document.activeElement===first||!dialog.contains(document.activeElement))){e.preventDefault();last.focus()}
      else if(!e.shiftKey&&(document.activeElement===last||!dialog.contains(document.activeElement))){e.preventDefault();first.focus()}
    }
  },true);
  window.KNUIReviewSnapshot=()=>({stage:currentStage,echo,paused,auto,autoInvest,lastSummon,slots:[...slots],aiming:!!aim?.active});
  home();
})();
