(()=>{
 const data=window.KNHandoff,$=s=>document.querySelector(s);let selected='home',region=null;
 data.screens.forEach((s,i)=>{const b=document.createElement('button');b.dataset.view=s.id;b.innerHTML=s.name+`<span>${String(i+1).padStart(2,'0')}</span>`;b.onclick=()=>{selected=s.id;region=null;render()};$('#screens').append(b)});
 const readable={candidate:'시안 · 채택 전','outside-ui':'UI 범위 밖','code-preview':'코드 프리뷰'};
 function highlight(){document.querySelectorAll('[data-region]').forEach(b=>{b.classList.toggle('selected',b.dataset.region===region);b.setAttribute('aria-pressed',String(b.dataset.region===region))})}
 function render(){
  const ratio=$('#ratio').value,d=data.screens.find(s=>s.id===selected),layout=data.layouts.find(s=>s.id===selected&&s.ratio===ratio);
  document.querySelectorAll('[data-view]').forEach(b=>{b.classList.toggle('active',b.dataset.view===selected);b.setAttribute('aria-pressed',String(b.dataset.view===selected))});
  $('#title').textContent=d.name;$('#source').textContent=d.source;$('#input').textContent=d.input;$('#screen-image').src=layout.screenshot;$('#screen-image').alt=d.name+' '+layout.size.join(' × ')+' 규격';$('#caption').textContent='REVIEW 08 / '+layout.size.join(' × ')+' LOGICAL PX';
  $('#regions').replaceChildren();$('#measurements').replaceChildren();$('#states').replaceChildren();$('#open-preview').href='index.html?view='+d.page;
  Object.entries(layout.blocks).forEach(([id,r])=>{
   const b=document.createElement('button');b.className='region';b.dataset.region=id;b.setAttribute('aria-label',id+' 영역');b.innerHTML='<span>'+id+'</span>';Object.assign(b.style,{left:r.x/layout.size[0]*100+'%',top:r.y/layout.size[1]*100+'%',width:r.width/layout.size[0]*100+'%',height:r.height/layout.size[1]*100+'%'});b.onclick=()=>{region=id;highlight()};$('#regions').append(b);
   const line=document.createElement('button');line.className='measurement';line.dataset.region=id;line.innerHTML=id+(r.scrollable?' · scroll':'')+`<small>X ${r.x} / Y ${r.y} · ${r.width} × ${r.height}</small>`;line.onclick=()=>{region=id;highlight()};$('#measurements').append(line);
  });
  d.states.forEach(s=>{const b=document.createElement('span');b.textContent=s;$('#states').append(b)});highlight();
 }
 $('#ratio').onchange=render;$('#bounds').onchange=e=>$('#regions').hidden=!e.target.checked;
 Object.entries(data.tokens).filter(([,v])=>typeof v==='string').forEach(([name,color])=>{const el=document.createElement('div');el.innerHTML=`<b style="background:${color}"></b>${name}<code>${color}</code>`;$('#palette').append(el)});
 function coverage(){
  $('#coverage').replaceChildren();data.coverage.filter(s=>!$('#candidates-only').checked||s.status==='candidate').forEach(s=>{const tr=document.createElement('tr');tr.innerHTML=`<td>${s.id}</td><td>${s.name}</td><td class="${s.status}">${readable[s.status]}</td><td>${s.count||'—'}</td><td>${s.files}</td>`;$('#coverage').append(tr)})
 }
 $('#candidates-only').onchange=coverage;data.pending.forEach(text=>{const li=document.createElement('li');li.textContent=text;$('#pending').append(li)});render();coverage();
})();
