const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const root=__dirname;
const types={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.md':'text/plain; charset=utf-8','.txt':'text/plain; charset=utf-8','.svg':'image/svg+xml','.png':'image/png'};
function inside(file){const rel=path.relative(root,file);return !rel.startsWith('..'+path.sep)&&rel!=='..'&&!path.isAbsolute(rel)}
exports.startServer=({port=8772}={})=>new Promise((resolve,reject)=>{
  const server=http.createServer(async(req,res)=>{
    try{
      const pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
      if(pathname.split('/').some(p=>p.startsWith('.')||p==='node_modules')){res.writeHead(403);res.end('Forbidden');return}
      let file=path.resolve(root,'.'+pathname);
      if(!inside(file)){res.writeHead(403);res.end('Forbidden');return}
      if((await fs.promises.stat(file)).isDirectory())file=path.join(file,'index.html');
      file=await fs.promises.realpath(file);
      if(!inside(file)){res.writeHead(403);res.end('Forbidden');return}
      const bytes=await fs.promises.readFile(file);
      res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream','Cache-Control':'no-store'});
      res.end(bytes);
    }catch(err){res.writeHead(err.code==='ENOENT'?404:400);res.end('File unavailable')}
  });
  server.once('error',reject);
  server.listen(port,'127.0.0.1',()=>resolve(server));
});
if(require.main===module){
  const port=Number(process.env.PORT||8772);
  if(!Number.isInteger(port)||port<1||port>65535)throw Error('PORT must be 1–65535');
  exports.startServer({port}).then(()=>console.log(`UI review: http://127.0.0.1:${port}/\nHandoff: http://127.0.0.1:${port}/handoff.html`)).catch(err=>{console.error(err.message);process.exitCode=1});
}
