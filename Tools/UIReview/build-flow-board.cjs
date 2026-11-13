const {reviewBrowser,reviewURL,outputPath}=require('./review-runtime.cjs');
const {chromium}=require('playwright'),path=require('path');
(async()=>{const b=await reviewBrowser();const p=await b.newPage({viewport:{width:1512,height:1500}});await p.goto(reviewURL('/presentation.html'));await p.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth));await p.screenshot({path:path.join(__dirname,'UI_Review_Board.png'),fullPage:true});await b.close()})().catch(e=>{console.error(e);process.exit(1)});
