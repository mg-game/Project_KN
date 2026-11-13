const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
function check(){
  const root=__dirname,plan=require('./UnityImportPlan.json'),refs=require('./reference/manifest.json');
  const entries=[...plan.assets,...refs.map(r=>({file:'reference/'+r.file,sha256:r.sha256}))];
  if(plan.assets.length!==48||refs.length!==44)throw Error('Expected 48 candidate and 44 reference PNGs');
  for(const item of entries){
    const file=path.join(root,item.file);
    if(!fs.existsSync(file))throw Error(`Missing ${item.file}. Run git lfs pull --include="Tools/UIReview/**"`);
    const bytes=fs.readFileSync(file);
    if(!bytes.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10])))throw Error(`LFS content missing: ${item.file}. Run git lfs pull --include="Tools/UIReview/**"`);
    if(crypto.createHash('sha256').update(bytes).digest('hex')!==item.sha256)throw Error('Asset hash mismatch: '+item.file);
  }
  console.log('PASS 48 candidate + 44 reference PNGs: materialized LFS content and SHA-256');
}
exports.check=check;
if(require.main===module)try{check()}catch(err){console.error(err.message);process.exitCode=1}
