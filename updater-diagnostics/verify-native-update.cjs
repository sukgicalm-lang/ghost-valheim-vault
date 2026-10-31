// Exercises the shipped script and spawn options through a REAL parent exit on Windows PowerShell 5.1.
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),cp=require('node:child_process'),crypto=require('node:crypto');
const delay=ms=>new Promise(r=>setTimeout(r,ms));
const root=path.resolve(__dirname,'../_smelter_build/launcher');
let ts;try{ts=require(path.join(root,'node_modules/typescript'));}catch{ts=require('typescript');}
function load(file){const m={exports:{}};new Function('exports','require','module',ts.transpileModule(fs.readFileSync(path.join(root,file),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS}}).outputText)(m.exports,require,m);return m.exports;}
const ps=path.join(process.env.SystemRoot,'System32/WindowsPowerShell/v1.0/powershell.exe');
async function controller(config){
 const c=JSON.parse(fs.readFileSync(config,'utf8')),helper=path.join(c.temp,'update.ps1'),ready=path.join(c.temp,'ready.txt');
 let script=load('src-electron/GhostSelfUpdateHelper.ts').GHOST_SELF_UPDATE_HELPER;
 const trace=path.join(c.temp,'trace.txt').replaceAll("'","''");
 for(const token of ["$parent = Get-Process","$exe = Install-GhostRuntime","Set-GhostRuntimeLinks $exe","Start-GhostRuntime $exe","$record = Join-Path"]){script=script.replace(token,"Add-Content -LiteralPath '"+trace+"' -Value ('TRACE "+token+" pid='+$PID+' local='+$env:LOCALAPPDATA+' parent='+$ParentProcessId)\n"+token);}
 fs.writeFileSync(helper,'\ufeff'+script);
 const fd=fs.openSync(path.join(c.temp,'helper.log'),'a');
 const child=cp.spawn(ps,['-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',helper,'-ParentProcessId',String(process.pid),'-ArchivePath',c.zip,'-ExpectedHash',c.hash,'-Version','1.0.11','-CurrentExePath',c.current,'-ReadyPath',ready],{...load('src-electron/GhostUpdateProcess.ts').ghostHelperSpawnOptions(fd),env:{...process.env,LOCALAPPDATA:c.temp,GHOST_RESTART_PROOF:c.proof}});fs.closeSync(fd);
 fs.writeFileSync(path.join(c.temp,'helper-pid.txt'),String(child.pid));
 let failure;child.on('error',e=>failure=e);
 for(let i=0;i<300;i++){if(failure)throw failure;if(fs.existsSync(ready)){child.unref();console.log('Real helper ready; parent exiting');return;}if(child.exitCode!==null)throw Error('Helper exited '+child.exitCode);await delay(100);}
 child.kill();throw Error('Native ready timeout');
}
async function main(){
 if(process.platform!=='win32')throw Error('Windows runner required');
 const temp=fs.mkdtempSync(path.join(os.tmpdir(),'Ghost native 설치 검사 '));
 const source=path.join(temp,'fixture.cs'),fixture=path.join(temp,'fixture');fs.mkdirSync(path.join(fixture,'resources'),{recursive:true});
 fs.writeFileSync(source,'using System;using System.IO;class Proof{static void Main(){File.WriteAllText(Environment.GetEnvironmentVariable("GHOST_RESTART_PROOF"),"restarted:"+System.Reflection.Assembly.GetExecutingAssembly().Location);}}');
 const compiler=path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319/csc.exe');
 cp.execFileSync(compiler,['/nologo','/target:winexe','/out:'+path.join(fixture,'GhostR2.exe'),source]);fs.writeFileSync(path.join(fixture,'resources/app.asar'),'native-lifecycle-fixture');
 const zip=path.join(temp,'runtime.zip'),zipScript=path.join(temp,'zip.ps1');fs.writeFileSync(zipScript,'\ufeffAdd-Type -AssemblyName System.IO.Compression.FileSystem\n[IO.Compression.ZipFile]::CreateFromDirectory($args[0],$args[1])');cp.execFileSync(ps,['-NoProfile','-ExecutionPolicy','Bypass','-File',zipScript,fixture,zip]);
 const proof=path.join(temp,'restarted.txt');fs.mkdirSync(path.join(temp,'GhostR2'),{recursive:true});fs.writeFileSync(path.join(temp,'GhostR2/keep.cfg'),'preserved');
 const config=path.join(temp,'config.json');fs.writeFileSync(config,JSON.stringify({temp,zip,hash:crypto.createHash('sha256').update(fs.readFileSync(zip)).digest('hex'),current:path.join(fixture,'GhostR2.exe'),proof}));
 // This runner is disposable; record links so the test does not leave dead fixture targets.
 let stderr='';const parent=cp.spawn(process.execPath,[__filename,'--controller',config],{stdio:['ignore','pipe','pipe']});parent.stdout.on('data',d=>process.stdout.write(d));parent.stderr.on('data',d=>stderr+=d);
 const exit=await new Promise((resolve,reject)=>{parent.once('error',reject);parent.once('exit',resolve);});
 if(exit!==0)throw Error('Controller failed '+stderr+' '+fs.readFileSync(path.join(temp,'helper.log'),'utf8'));
 const record=path.join(temp,'GhostR2/self-update-result.json');
 for(let i=0;i<750&&!fs.existsSync(proof);i++)await delay(100);
 console.log('TRACE:',fs.existsSync(path.join(temp,'trace.txt'))?fs.readFileSync(path.join(temp,'trace.txt'),'utf8'):'none');
 console.log('Helper pid',fs.readFileSync(path.join(temp,'helper-pid.txt'),'utf8'),'temp files',fs.readdirSync(temp));
 const result=fs.existsSync(record)?JSON.parse(fs.readFileSync(record,'utf8').replace(/^\ufeff/,'')):{};
 if(result.status!=='success'||!fs.existsSync(proof)||!fs.readFileSync(proof,'utf8').includes(path.join('GhostR2','1.0.11','GhostR2.exe'))||fs.readFileSync(path.join(temp,'GhostR2/keep.cfg'),'utf8')!=='preserved')throw Error('Native restart failed '+JSON.stringify(result)+' '+fs.readFileSync(path.join(temp,'helper.log'),'utf8'));
 console.log('PASS: native Windows PowerShell 5.1 helper ready -> actual parent exit -> verified replacement -> new EXE restart; Unicode/spaces/config retained.');
 const cleanup=path.join(temp,'cleanup.ps1');fs.writeFileSync(cleanup,'Remove-Item HKCU:\\Software\\Classes\\ghostvault -Recurse -Force -ErrorAction SilentlyContinue\nRemove-Item (Join-Path ([Environment]::GetFolderPath("Desktop")) "Ghost r2modman.lnk") -Force -ErrorAction SilentlyContinue');cp.execFileSync(ps,['-NoProfile','-ExecutionPolicy','Bypass','-File',cleanup]);fs.rmSync(temp,{recursive:true,force:true});
}
(process.argv[2]==='--controller'?controller(process.argv[3]):main()).catch(e=>{console.error(e);process.exitCode=1;});
