// Retouching is authored with image_gen. This script performs deterministic
// export, registration of new light VFX, and delivery metadata generation.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const root=__dirname, size=768;
const panes=[
 {id:1,name:'정문 창',x:416,y:570,rx:17,ry:18},
 {id:2,name:'왼쪽 아래',x:272,y:544,rx:18,ry:24},
 {id:3,name:'오른쪽 아래',x:540,y:573,rx:15,ry:23},
 {id:4,name:'왼쪽 위',x:249,y:416,rx:21,ry:26},
 {id:5,name:'중앙 큰 창',x:420,y:338,rx:27,ry:31}
];
const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v));
async function main(){
 for(const d of ['lighting','lighting/masks','lighting/previews'])fs.mkdirSync(path.join(root,d),{recursive:true});
 const master=path.join(root,'concepts/shop_dark_refined_master.png');
 const base=path.join(root,'lighting/shop_dark_base.png');
 await sharp(master).resize(size,size,{fit:'fill',kernel:'lanczos3'}).png().toFile(base);
 const {data}=await sharp(base).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const masks=[],entries=[];
 for(const p of panes){
  const mask=Buffer.alloc(size*size*4);let count=0,sx=0,sy=0;
  for(let y=Math.floor(p.y-p.ry-1);y<=p.y+p.ry+1;y++)for(let x=Math.floor(p.x-p.rx-1);x<=p.x+p.rx+1;x++){
   const i=(y*size+x)*4,[r,g,b,a]=data.subarray(i,i+4);
   const ellipse=((x-p.x)/p.rx)**2+((y-p.y)/p.ry)**2;
   // A bounded glass matte: blue/indigo glass passes; brown mullions, gold
   // rivets and flowers are excluded. The source artwork remains untouched.
   const glass=clamp((b-Math.max(r,g)-5)/12)*clamp((1-ellipse)*12)*clamp((a-200)/40);
   const alpha=Math.round(glass*255);if(alpha<3)continue;
   mask[i]=mask[i+1]=mask[i+2]=255;mask[i+3]=alpha;
   if(alpha>64){count++;sx+=x;sy+=y;}
  }
  const buf=await sharp(mask,{raw:{width:size,height:size,channels:4}}).png().toBuffer();
  const filename=`lighting/masks/window_${p.id}.png`;fs.writeFileSync(path.join(root,filename),buf);masks.push(mask);
  entries.push({...p,mask:filename,corePixels:count,centroid:[+(sx/count).toFixed(2),+(sy/count).toFixed(2)],maskData:buf.toString('base64')});
 }
 const defs=`<defs><radialGradient id="halo"><stop stop-color="#FFD382" stop-opacity=".22"/><stop offset=".5" stop-color="#F3B95B" stop-opacity=".09"/><stop offset="1" stop-color="#E8AC58" stop-opacity="0"/></radialGradient><linearGradient id="pane" x2="0" y2="1"><stop stop-color="#FFF0B6"/><stop offset=".55" stop-color="#F9CF82"/><stop offset="1" stop-color="#DCA15B"/></linearGradient></defs>`;
 for(let n=1;n<=5;n++){
  const parts=entries.slice(0,n).map(p=>`<defs><mask id="m${p.id}" maskUnits="userSpaceOnUse" x="0" y="0" width="768" height="768"><image href="data:image/png;base64,${p.maskData}" width="768" height="768"/></mask></defs><ellipse cx="${p.x}" cy="${p.y}" rx="${p.rx*2.1}" ry="${p.ry*1.8}" fill="url(#halo)"/><rect x="${p.x-p.rx}" y="${p.y-p.ry}" width="${p.rx*2}" height="${p.ry*2}" fill="url(#pane)" mask="url(#m${p.id})"/>`).join('');
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="768" height="768" viewBox="0 0 768 768">${defs}${parts}</svg>`;
  const target=path.join(root,`candidates/shop_light_${n}`);fs.writeFileSync(target+'.svg',svg);await sharp(Buffer.from(svg)).png().toFile(target+'.png');
  await sharp(base).composite([{input:target+'.png',blend:'over'}]).png().toFile(path.join(root,`lighting/previews/shop_lit_${n}.png`));
 }
 const manifest=JSON.parse(fs.readFileSync(path.join(root,'asset-manifest.json')));manifest.version=2;
 manifest.deliveryRevision='02';manifest.artSource='image_gen refined master; deterministic 768px export';
 for(const a of manifest.assets){a.sha256=crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'candidates',a.name+'.png'))).digest('hex');
  if(a.group==='ART-03')a.note='768x768 base and cumulative overlay. Blue-glass masks exclude wooden mullions and gold rivets. Same pivot and logical rect; material uses normal alpha blending.';
 }
 manifest.additionalRuntimeAssets=[{name:'shop_dark_base',file:'lighting/shop_dark_base.png',group:'ART-03',width:768,height:768,status:'review_candidate',sha256:crypto.createHash('sha256').update(fs.readFileSync(base)).digest('hex')}];
 fs.writeFileSync(path.join(root,'asset-manifest.json'),JSON.stringify(manifest,null,2));fs.writeFileSync(path.join(root,'manifest.js'),'window.KN_MANIFEST='+JSON.stringify(manifest)+';');
 const registration={canvas:[768,768],pivot:[.5,.5],alphaBlend:'normal',levels:'0 = base only; 1..5 = base + exactly one cumulative overlay',sourceMaster:'concepts/shop_dark_refined_master.png',sourceDimensions:(await sharp(master).metadata()).width,panes:entries.map(({maskData,...p})=>p)};
 fs.writeFileSync(path.join(root,'lighting/registration.json'),JSON.stringify(registration,null,2));
 function category(a){if(a.group==='ART-03'||a.group==='ART-04')return'Battle';if(a.group==='ART-06')return'FX';if(a.name.startsWith('lantern_button')||a.name.startsWith('lantern_level')||a.group==='D-44')return'UI';return'Icons'}
 const importAssets=manifest.assets.map(a=>({id:a.group,name:a.name,file:`candidates/${a.name}.png`,target:`Assets/_Project/Art/${category(a)}/${a.name}.png`,width:a.width,height:a.height,borderLTRB:a.border?[a.border[0],a.border[3],a.border[2],a.border[1]]:[0,0,0,0],borderUnityXY_ZW:a.border||[0,0,0,0],pivot:a.pivot||[.5,.5],fixedHeight:a.group==='D-44'?64:null}));
 importAssets.push({id:'ART-03',name:'shop_dark_base',file:'lighting/shop_dark_base.png',target:'Assets/_Project/Art/Battle/shop_dark_ui.png',width:768,height:768,pivot:[.5,.5],borderUnityXY_ZW:[0,0,0,0]});
 fs.writeFileSync(path.join(root,'UnityImportPlan.json'),JSON.stringify({status:'proposal_not_applied',engine:'Unity 6000.2.6f2',defaults:{textureType:'Sprite',spriteMode:'Single',spriteMeshType:'FullRect',spritePixelsPerUnit:100,sRGB:true,alphaIsTransparency:true,mipmapEnabled:false,wrapMode:'Clamp',filterMode:'Bilinear',compression:'Uncompressed for first visual QA; platform compression requires in-Editor validation'},borderOrder:'Unity XYZW = left,bottom,right,top; borderLTRB is a reading aid',assets:importAssets.map(a=>({...a,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,a.file))).digest('hex')})),shopBinding:{base:'shop_dark_base',levels:['shop_light_1','shop_light_2','shop_light_3','shop_light_4','shop_light_5'],sharedRect:[768,768],sharedPivot:[.5,.5],blend:'normal alpha',stacking:'one overlay at a time, not all previous levels',raycastTarget:false},adoption:'Review and Unity-generated meta required before copying to game repository.'},null,2));
 console.log('48 runtime candidates prepared; 768px base and five registered light layers.');
}
main().catch(e=>{console.error(e);process.exit(1)});
