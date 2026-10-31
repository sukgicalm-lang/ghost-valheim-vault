const fs=require('node:fs');const fsp=require('node:fs/promises');const path=require('node:path');const os=require('node:os');const cp=require('node:child_process');const crypto=require('node:crypto');
async function main(){
 const rows=JSON.parse(fs.readFileSync('release-ci/launcher-patch.json','utf8'));
 const source=rows.find(x=>x.path==='src-electron/GhostSelfUpdateHelper.ts').content;
 const raw=source.match(/^export const GHOST_SELF_UPDATE_HELPER = String\.raw`([\s\S]*)`;\s*$/)[1];
 const root=await fsp.mkdtemp(path.join(os.tmpdir(),'GhostR2 native 한글 path-'));
 const helper=path.join(root,'update.ps1');await fsp.writeFile(helper,'\ufeff'+raw,{encoding:'utf8',mode:0o600});
 const payload=path.join(root,'payload');await fsp.mkdir(path.join(payload,'resources'),{recursive:true});
 await fsp.writeFile(path.join(payload,'GhostR2.exe'),'fixture');await fsp.writeFile(path.join(payload,'resources','app.asar'),'fixture');
 const archive=path.join(root,'runtime.zip'),ready=path.join(root,'ready.txt');
 const powershell=path.join(process.env.SystemRoot||'C:\\Windows','System32/WindowsPowerShell/v1.0/powershell.exe');
 const packed=cp.spawnSync(powershell,['-NoProfile','-NonInteractive','-Command',"Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::CreateFromDirectory($env:GHOST_TEST_PAYLOAD,$env:GHOST_TEST_ARCHIVE)"],{encoding:'utf8',windowsHide:true,env:{...process.env,GHOST_TEST_PAYLOAD:payload,GHOST_TEST_ARCHIVE:archive}});
 if(packed.status!==0)throw new Error('Could not create fixture: '+packed.stderr);
 const hash=crypto.createHash('sha256').update(fs.readFileSync(archive)).digest('hex');
 const parent=cp.spawn(process.execPath,['-e','setInterval(()=>{},1000)'],{stdio:'ignore',windowsHide:true});
 const args=['-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',helper,'-ParentProcessId',String(parent.pid),'-ArchivePath',archive,'-ExpectedHash',hash,'-Version','1.0.10','-CurrentExePath',process.execPath,'-ReadyPath',ready];
 const child=cp.spawn(powershell,args,{detached:true,stdio:['ignore','pipe','pipe'],windowsHide:true,env:{...process.env,LOCALAPPDATA:path.join(root,'local')}});
 let out='',err='',spawnError='';child.stdout.on('data',d=>out+=d);child.stderr.on('data',d=>err+=d);child.on('error',e=>spawnError=e.message);
 let ok=false;
 try{
  for(let i=0;i<300;i++){
   if(fs.existsSync(ready)){ok=true;break;}
   if(spawnError||child.exitCode!==null)break;
   await new Promise(r=>setTimeout(r,100));
  }
  const record=path.join(root,'local','GhostR2','self-update-result.json');
  console.log(JSON.stringify({ready:ok,exitCode:child.exitCode,spawnError,stdout:out,stderr:err,record:fs.existsSync(record)?fs.readFileSync(record,'utf8'):null},null,2));
  if(!ok)throw new Error('Native Windows PowerShell handshake failed before ready');
 }finally{
  child.kill();parent.kill();await new Promise(r=>setTimeout(r,1000));await fsp.rm(root,{recursive:true,force:true});
 }
 console.log('PASS: exact shipped helper starts under native Windows PowerShell 5.1 with Unicode/space paths and production command-line arguments.');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
