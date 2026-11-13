const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root=__dirname, dir=path.join(root,'candidates');
fs.mkdirSync(dir,{recursive:true});
const records=[];
const C={ink:'#211737',purple:'#6F5299',lavender:'#C6AFE8',gold:'#E7BC73',cream:'#FFF0CA',mint:'#AADCCE',red:'#EF9B9F',blue:'#A4CFEC',dim:'#887F9A'};
const defs=`<defs><linearGradient id="gold" x2="0" y2="1"><stop stop-color="#FFF0BF"/><stop offset="1" stop-color="#CF9650"/></linearGradient><linearGradient id="velvet" x2="0" y2="1"><stop stop-color="#514071"/><stop offset="1" stop-color="#241B3C"/></linearGradient><radialGradient id="light"><stop stop-color="#FFF0AD" stop-opacity=".8"/><stop offset=".38" stop-color="#F5C46C" stop-opacity=".42"/><stop offset="1" stop-color="#F3B960" stop-opacity="0"/></radialGradient><pattern id="hatch" width="22" height="22" patternUnits="userSpaceOnUse" patternTransform="rotate(35)"><path d="M0 0V22" stroke="currentColor" stroke-width="7"/></pattern></defs>`;
const common=`stroke="${C.ink}" stroke-width="6" stroke-linecap="round" stroke-linejoin="round"`;
const body={
 advance:`<path d="M29 102L53 68 28 53 91 21 100 84 75 69 53 108Z" fill="url(#gold)"/><path d="M49 50L82 34M41 91L55 73" fill="none" stroke="#FFF7DE" stroke-width="5"/>`,
 walk:`<path d="M28 62A36 36 0 0 1 89 37L99 23 105 64 65 57 79 46A22 22 0 0 0 42 62Z" fill="${C.mint}"/><path d="M100 66A36 36 0 0 1 39 91L29 105 23 64 63 71 49 82A22 22 0 0 0 86 66Z" fill="${C.blue}"/>`,
 guard:`<path d="M64 16C78 28 91 28 103 31L98 72Q91 99 64 113 37 99 30 72L25 31Q49 28 64 16Z" fill="${C.lavender}"/><path d="M64 31L86 39 83 68Q78 85 64 96 50 85 45 68L42 39Z" fill="url(#velvet)"/><path d="M64 48V77M52 62H76" stroke="${C.cream}"/>`,
 timer:`<circle cx="64" cy="70" r="40" fill="url(#velvet)"/><path d="M51 17H77M64 17V29M95 38L104 29" stroke="url(#gold)" stroke-width="10"/><circle cx="64" cy="70" r="29" stroke="url(#gold)" stroke-width="4" fill="none"/><path d="M64 48V70L79 79" stroke="${C.cream}" stroke-width="7"/><circle cx="64" cy="70" r="4" fill="${C.cream}" stroke="none"/>`,
 boss:`<path d="M25 61Q25 25 64 23 103 25 103 61L96 85 81 87V104H47V87L32 85Z" fill="${C.cream}"/><path d="M39 51Q55 49 55 66 44 77 38 65ZM89 51Q73 49 73 66 84 77 90 65Z" fill="${C.purple}"/><path d="M64 70L57 82H71ZM56 92V103M72 92V103" fill="${C.purple}"/>`,
 remaining:`<path d="M64 57Q31 23 17 49 8 77 48 80 18 88 32 108 52 120 64 88 76 120 96 108 110 88 80 80 120 77 111 49 97 23 64 57Z" fill="${C.lavender}"/><path d="M64 51V97M64 55L48 34M64 55L80 34" fill="none" stroke="${C.cream}" stroke-width="8"/>`,
 warning:`<path d="M64 17L114 105H14Z" fill="${C.red}"/><path d="M64 49V72" stroke="${C.ink}" stroke-width="9"/><circle cx="64" cy="88" r="5" fill="${C.ink}" stroke="none"/>`,
 preview:`<path d="M25 22L64 50 103 22V48L64 77 25 48Z" fill="${C.cream}"/><path d="M25 64L64 92 103 64V88L64 117 25 88Z" fill="${C.gold}"/>`,
 pressure:`<path d="M32 19H55L29 109H6ZM70 19H93L67 109H44ZM108 19H122L105 109H82Z" fill="${C.lavender}"/>`,
 retry:`<path d="M96 54A37 37 0 1 0 101 82" fill="none" stroke="${C.mint}" stroke-width="17"/><path d="M74 45L105 63 112 26" fill="${C.mint}"/>`,
 formation:`<circle cx="64" cy="64" r="33" fill="none" stroke="${C.gold}" stroke-width="7"/>${[0,90,180,270].map(a=>`<circle cx="64" cy="26" r="13" transform="rotate(${a} 64 64)" fill="${C.lavender}"/>`).join('')}<path d="M48 71L64 48 80 71Z" fill="${C.cream}"/>`,
 growth:`<path d="M22 102V80H43V102ZM54 102V62H75V102ZM86 102V42H107V102Z" fill="${C.mint}"/><path d="M23 56L73 22H51M73 22V44" fill="none" stroke="${C.cream}" stroke-width="8"/>`,
 defense_fail:`<path d="M64 16L102 30 97 73Q88 98 64 111 40 98 31 73L26 30Z" fill="${C.lavender}"/><path d="M68 28L54 55 74 66 57 99" fill="none" stroke="${C.ink}" stroke-width="9"/><path d="M91 16L106 10M109 29L121 30" stroke="${C.red}"/>`,
 damage_fail:`<path d="M31 94L55 69 38 53 77 18 104 21 103 47 67 84 56 73 40 104Z" fill="${C.blue}"/><path d="M39 79L60 100M30 95L22 108M75 25L68 46 89 45 80 65" fill="none" stroke="${C.ink}" stroke-width="8"/><path d="M108 64L119 72M99 79L108 90" stroke="${C.red}"/>`,
 dispatch:`<path d="M39 22H89M39 106H89M44 24Q43 48 64 64 85 48 84 24M44 104Q43 80 64 64 85 80 84 104" fill="none" stroke="${C.gold}" stroke-width="9"/><path d="M52 42H76L64 56ZM51 95L64 81 77 95Z" fill="${C.lavender}" stroke="none"/>`,
 moon:`<path d="M85 18A46 46 0 1 0 109 92 43 43 0 0 1 85 18Z" fill="url(#gold)"/><path d="M80 61Q94 46 106 60L109 85Q93 99 77 85Z" fill="${C.lavender}"/><circle cx="87" cy="71" r="3" fill="${C.ink}" stroke="none"/><circle cx="101" cy="71" r="3" fill="${C.ink}" stroke="none"/><path d="M94 40V29M88 35H100" stroke="${C.cream}" stroke-width="4"/>`,
 lantern:`<path d="M46 28Q44 10 64 10 84 10 82 28M36 37H92L86 97H42Z" fill="url(#velvet)"/><path d="M31 35L46 24H82L97 35ZM42 97H86L93 108H35Z" fill="url(#gold)"/><path d="M51 47H77L74 85H54Z" fill="${C.cream}"/><path d="M64 49V84" stroke="${C.gold}" stroke-width="5"/>`
};
function svg(content,w=128,h=w){return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${defs}${content}</svg>`;}
async function emit(name,content,w=128,h=w,group='UI',extra={}){
 let source=svg(content,w,h);if(name.endsWith('_warning'))source=source.replaceAll('#FFF0BF','#FFD2CB').replaceAll('#CF9650','#D27889').replaceAll('#C6AFE8','#EAA2B2');fs.writeFileSync(path.join(dir,name+'.svg'),source);
 await sharp(Buffer.from(source)).png().toFile(path.join(dir,name+'.png'));
 records.push({name,group,width:w,height:h,status:'review_candidate',...extra});
}
function icon(k,dim=false){return `<g ${common}${dim?' opacity=".38"':''}>${body[k]}</g>`;}
function polar(a,r,c=512){let t=(a-90)*Math.PI/180;return [c+Math.cos(t)*r,c+Math.sin(t)*r].map(n=>n.toFixed(3)).join(',');}
async function main(){
 for(const k of ['advance','walk','guard'])for(const state of ['on','dim'])await emit('behavior_'+k+'_'+state,icon(k,state==='dim'),128,128,'ART-01');
 for(const k of ['timer','boss','remaining'])for(const state of ['normal','warning'])await emit('hud_'+k+'_'+state,icon(k).replaceAll(C.gold,state==='warning'?C.red:C.gold).replaceAll(C.cream,state==='warning'?'#FFD1C8':C.cream),128,128,'ART-07');
 for(const [state,fill] of [['complete',C.gold],['current',C.mint],['pending','#59486D']])await emit('hud_wave_'+state,`<rect x="16" y="37" width="96" height="54" rx="17" fill="${fill}" stroke="${C.ink}" stroke-width="7"/><path d="M31 49H84" stroke="${C.cream}" stroke-opacity=".5" stroke-width="5" stroke-linecap="round"/>`,128,128,'ART-07');
 const ticks=Array.from({length:8},(_,i)=>`<g transform="rotate(${i*45} 512 512)"><path d="M512 70V105" stroke="${C.gold}" stroke-width="12" stroke-linecap="round"/><circle cx="512" cy="126" r="5" fill="${C.cream}"/></g>`).join('');
 await emit('formation_ring',`<circle cx="512" cy="512" r="426" fill="none" stroke="${C.ink}" stroke-width="22"/><circle cx="512" cy="512" r="426" fill="none" stroke="${C.gold}" stroke-width="7"/><circle cx="512" cy="512" r="400" fill="none" stroke="${C.lavender}" stroke-width="3" stroke-opacity=".55"/>${ticks}`,1024,1024,'ART-04');
 const sector=`M${polar(-21,360)}A360 360 0 0 1 ${polar(21,360)}L${polar(21,437)}A437 437 0 0 0 ${polar(-21,437)}Z`;
 for(const [state,col] of [['empty',C.lavender],['danger',C.red]]) await emit('sector_'+state,`<g color="${col}"><path d="${sector}" fill="url(#hatch)" opacity=".7"/><path d="${sector}" fill="none" stroke="${col}" stroke-width="4"/></g>`,1024,1024,'ART-04',{pivot:[.5,.5],rotation:'0=N; clockwise 45 degrees'});
 for(const [name,k,g] of [['direction_danger','warning','ART-04'],['direction_pressure','pressure','ART-04'],['wave_preview','preview','ART-06'],['wave_fixed','pressure','ART-06']])await emit(name,icon(k),128,128,g,{pivot:[.5,.5]});
 await emit('lantern',icon('lantern'),128,128,'ART-02',{alternative_to:'reference/lamp.png',note:'existing lamp is preferred if style review passes'});
 for(const state of ['enabled','disabled','max'])await emit('lantern_button_'+state,`<rect x="4" y="4" width="248" height="88" rx="24" fill="${state==='enabled'?'url(#velvet)':state==='max'?'#41375C':'#322C44'}" stroke="${state==='disabled'?'#82768D':C.gold}" stroke-width="3"/><path d="M29 14H227" stroke="${C.cream}" stroke-opacity=".18" stroke-width="3" stroke-linecap="round"/>`,256,96,'ART-02',{border:[32,32,32,32],note:'text and icon are separate runtime layers'});
 for(let n=1;n<=5;n++)await emit('lantern_level_'+n,Array.from({length:5},(_,i)=>`<circle cx="${40+i*44}" cy="32" r="11" fill="${i<n?C.gold:'#50415F'}" stroke="${i<n?C.cream:'#9381A5'}" stroke-width="3"/>`).join(''),256,64,'ART-02');
 const windows=[{x:415,y:570,rx:16,ry:17},{x:268,y:546,rx:14,ry:22},{x:538,y:568,rx:12,ry:18},{x:245,y:423,rx:17,ry:24},{x:418,y:339,rx:24,ry:29}];
 for(let n=1;n<=5;n++)await emit('shop_light_'+n,windows.slice(0,n).map(p=>`<defs><mask id="pane-${p.x}"><ellipse cx="${p.x}" cy="${p.y}" rx="${p.rx}" ry="${p.ry}" fill="white"/><path d="M${p.x-30} ${p.y}H${p.x+30}M${p.x} ${p.y-40}V${p.y+40}" stroke="black" stroke-width="4"/></mask></defs><ellipse cx="${p.x}" cy="${p.y}" rx="${p.rx*2.4}" ry="${p.ry*2}" fill="url(#light)" opacity=".65"/><ellipse cx="${p.x}" cy="${p.y}" rx="${p.rx}" ry="${p.ry}" fill="#F9D58F" opacity=".9" mask="url(#pane-${p.x})"/>`).join(''),768,768,'ART-03',{pivot:[.5,.5],note:'Matched to concepts/shop_dark_base.png using normalized rect. Native base is 1254 square; overlays 768 square. Source art is untouched. Masks and warm color require final art/Unity alignment review.'});
 for(const k of ['defense_fail','damage_fail','retry','formation','growth'])await emit('report_'+k,icon(k),128,128,'ART-08');
 await emit('badge_dispatch',`<g transform="scale(.5)">${icon('dispatch')}</g>`,64,64,'ART-09');
 for(const [name,k,col] of [['boss','boss',C.lavender],['critical','warning',C.red],['clear','guard',C.mint],['fail','defense_fail',C.red]])await emit('alert_'+name,`<rect x="2" y="2" width="316" height="60" rx="18" fill="url(#velvet)" stroke="${col}" stroke-width="2"/><g transform="translate(6 8) scale(.375)">${icon(k)}</g>`,320,64,'D-44',{border:[64,20,20,20],note:'lock height to 64px; left 64px preserves icon under horizontal slicing; runtime localized text placed after icon'});
 await emit('moon_echo',icon('moon'),128,128,'ART-12',{effect:'D-58: resonance regeneration +30%; supersedes the D-50 +20% memo; text separate'});
 fs.writeFileSync(path.join(root,'asset-manifest.json'),JSON.stringify({version:1,date:'2026-11-12',basis:'develop@755a13a; D-44/D-50/D-52',license:'Original programmatic vector UI candidates; existing reference images belong to Project_KN. Team-only review.',reviewOnly:true,assets:records},null,2));
 fs.writeFileSync(path.join(root,'manifest.js'),'window.KN_MANIFEST='+fs.readFileSync(path.join(root,'asset-manifest.json'),'utf8')+';');
 console.log('Rendered',records.length,'SVG/PNG candidates.');
}
main().catch(e=>{console.error(e);process.exit(1)});
