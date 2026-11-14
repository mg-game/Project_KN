const {spawn}=require('node:child_process'),path=require('node:path');
const scripts=['verify.cjs','verify-interactions.cjs','verify-growth.cjs','verify-results.cjs','verify-handoff.cjs','verify-readability.cjs','verify-flow.cjs','verify-stage.cjs','verify-pointer.cjs','verify-modal-keys.cjs','verify-sheet-focus.cjs','verify-formation-pointer.cjs'];
async function main(){
  require('./check-assets.cjs').check();
  const server=await require('./server.cjs').startServer({port:0});
  try{
    const env={...process.env,KN_UI_REVIEW_URL:`http://127.0.0.1:${server.address().port}/`};
    for(const script of scripts)await new Promise((resolve,reject)=>{
      const child=spawn(process.execPath,[path.join(__dirname,script)],{cwd:__dirname,env,stdio:'inherit'});
      child.once('error',reject);child.once('exit',code=>code===0?resolve():reject(Error(script+' exited '+code)));
    });
    console.log('PASS all twelve browser suites. Outputs: .review-output/');
  }finally{await new Promise(resolve=>server.close(resolve))}
}
main().catch(err=>{console.error(err.message);process.exitCode=1});
