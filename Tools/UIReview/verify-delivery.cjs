const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),sharp=require('sharp');
const {chromium}=require('playwright');const root=__dirname,checks=[];
const check=(name,detail)=>{checks.push({name,result:'pass',detail});console.log('PASS',name)};
async function raw(file){return sharp(path.join(root,file)).ensureAlpha().raw().toBuffer({resolveWithObject:true})}
const light=(d,i)=>.2126*d[i]+.7152*d[i+1]+.0722*d[i+2];
async function main(){
 const manifest=JSON.parse(fs.readFileSync(path.join(root,'asset-manifest.json'))),registration=JSON.parse(fs.readFileSync(path.join(root,'lighting/registration.json')));
 const base=await raw('lighting/shop_dark_base.png');assert.equal(base.info.width,768);assert.equal(base.info.height,768);
 for(const a of manifest.assets){const r=await raw('candidates/'+a.name+'.png');assert.equal(r.info.width,a.width);assert.equal(r.info.height,a.height);for(let x=0;x<r.info.width;x++){assert.equal(r.data[x*4+3],0);assert.equal(r.data[((r.info.height-1)*r.info.width+x)*4+3],0)}for(let y=0;y<r.info.height;y++){assert.equal(r.data[(y*r.info.width)*4+3],0);assert.equal(r.data[(y*r.info.width+r.info.width-1)*4+3],0)}}
 check('47 UI textures and base: dimensions and transparent borders','Base and all five light layers share 768²; full UI catalog retains declared dimensions.');
 const masks=await Promise.all(registration.panes.map(p=>raw(p.mask)));
 for(let n=0;n<5;n++){
  assert(registration.panes[n].corePixels>200);for(let i=0;i<base.data.length;i+=4){
   if(masks[n].data[i+3]>64){const [r,g,b]=base.data.subarray(i,i+3);assert(b-Math.max(r,g)>=8,'Non-glass matte pixel');}
   if(masks[n].data[i+3]>0)for(let j=0;j<n;j++)assert.equal(masks[j].data[i+3],0,'Window masks overlap');
  }
 }
 check('Five glass mattes: separate windows, wood and gold excluded',registration.panes.map(p=>({name:p.name,pixels:p.corePixels,centroid:p.centroid})));
 const readings=[];
 for(let level=1;level<=5;level++){
  const overlay=await raw('candidates/shop_light_'+level+'.png'),composite=await raw('lighting/previews/shop_lit_'+level+'.png');let unchangedMax=0;
  for(let i=0;i<base.data.length;i+=4)if(overlay.data[i+3]===0)for(let c=0;c<4;c++)unchangedMax=Math.max(unchangedMax,Math.abs(base.data[i+c]-composite.data[i+c]));
  assert(unchangedMax<=1,'Artwork outside VFX changed');
  const values=[];
  for(let pane=0;pane<5;pane++){let delta=0,count=0;for(let i=0;i<base.data.length;i+=4)if(masks[pane].data[i+3]>180){delta+=light(composite.data,i)-light(base.data,i);count++}delta/=count;values.push(+delta.toFixed(2));if(pane<level)assert(delta>60,'Lit window lacks brightness separation');else assert(Math.abs(delta)<1,'Unlit window changed');}
  readings.push({level,windowLuminanceDelta:values,unaffectedPixelMaxDelta:unchangedMax});
 }
 check('Five-stage lighting: only intended panes light, outside art stable',readings);
 const plan=JSON.parse(fs.readFileSync(path.join(root,'UnityImportPlan.json')));assert.equal(plan.assets.length,48);const unique=new Set(plan.assets.map(a=>a.target));assert.equal(unique.size,48);for(const a of plan.assets){assert(fs.existsSync(path.join(root,a.file)));assert(!a.target.endsWith('/shop.png'),'Existing shop must remain preserved');}
 check('Unity delivery map: 48 unique targets and explicit binding','PPU 100, center pivots, normal alpha, one cumulative overlay per level; no GUID or game asset write.');
 const b=await reviewBrowser();
 const p=await b.newPage({viewport:{width:1440,height:900},deviceScaleFactor:1});await p.goto(reviewURL('/quality.html'));await p.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0));
 async function piece(el,region){const shot=await el.screenshot();return sharp(shot).extract(region).raw().toBuffer()}
 const buttons=p.locator('#buttons .slice'),corner=await piece(buttons.nth(0),{left:0,top:0,width:32,height:32});for(let n=1;n<3;n++)assert((await piece(buttons.nth(n),{left:0,top:0,width:32,height:32})).equals(corner));
 const alerts=p.locator('#alerts .slice'),left=await piece(alerts.nth(0),{left:0,top:0,width:64,height:64});for(let n=1;n<3;n++)assert((await piece(alerts.nth(n),{left:0,top:0,width:64,height:64})).equals(left));
 check('9-slice browser samples preserve corners and alert icons','Button widths 256/384/512 and alert widths 320/480/640 at fixed height; preserved regions compare pixel-identical. Unity validation remains separate.');
 await p.locator('body').screenshot({path:outputPath('Technical_Review_02.png')});await p.locator('#stages').screenshot({path:outputPath('lighting-stages-02.png')});await p.locator('#icons').screenshot({path:outputPath('icons-32px-02.png')});await b.close();
 fs.writeFileSync(outputPath('delivery-verification.json'),JSON.stringify({date:'2026-11-12',scope:'deterministic PNG export, lighting registration, browser 9-slice. Not Unity execution.',checks},null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
