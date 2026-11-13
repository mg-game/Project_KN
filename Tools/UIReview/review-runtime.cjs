const fs=require('node:fs'),path=require('node:path');
exports.reviewURL=(route='/')=>new URL(route,process.env.KN_UI_REVIEW_URL||'http://127.0.0.1:8772/').href;
exports.outputPath=(name)=>{
  const dir=path.join(__dirname,'.review-output');fs.mkdirSync(dir,{recursive:true});
  return path.join(dir,name);
};
exports.reviewBrowser=()=>{
  const {chromium}=require('playwright');
  const options={headless:true};
  if(process.env.KN_UI_BROWSER_PATH)options.executablePath=process.env.KN_UI_BROWSER_PATH;
  // The managed Linux executor runs as root. Normal developer installs keep the browser sandbox.
  if(process.platform==='linux'&&process.getuid?.()===0)options.args=['--no-sandbox'];
  return chromium.launch(options);
};
