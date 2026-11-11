// Execute the launcher source patch's setup loader and planner with mocked I/O.
// Run: GHOST_TYPESCRIPT=/path/to/typescript.js node release-ci/test-setup-feed.cjs
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const ts=require(process.env.GHOST_TYPESCRIPT||'typescript');
const root=path.resolve(__dirname,'..');
const rows=JSON.parse(fs.readFileSync(path.join(__dirname,'launcher-patch.json'),'utf8'));
const content=p=>{const row=rows.find(x=>x.path===p);assert.ok(row,p);return row.content;};
const bundled=JSON.parse(content('src/ghost/GhostSetupBundle.json'));
const published=JSON.parse(fs.readFileSync(path.join(root,'docs/setup-release.json'),'utf8'));
function evaluate(source,imports,clock){
 const mod={exports:{}};
 const output=ts.transpileModule(source,{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.CommonJS,esModuleInterop:true}}).outputText;
 class ClockDate extends Date {static now(){return clock?.now||Date.now();}}
 vm.runInNewContext(output,{module:mod,exports:mod.exports,require:n=>{assert.ok(n in imports,'Unexpected import '+n);return imports[n];},Date:ClockDate,console,URL,Buffer});return mod.exports;
}
const repo=evaluate(content('src/ghost/GhostRepositoryCore.ts'),{});
const core=evaluate(content('src/ghost/GhostSetupCore.ts'),{'./GhostRepositoryCore':repo});
function loader(remote=published,cached=null,offline=false){
 const state={writes:[],requests:[],now:100000};
 const io={readFile:async()=>{if(!cached)throw Error('no cache');return Buffer.from(JSON.stringify(cached));},mkdirs:async()=>{},writeFile:async(p,s)=>state.writes.push(JSON.parse(s))};
 const axios={get:async(url,options)=>{state.requests.push({url,options});if(offline)throw Error('offline');return{data:remote};}};
 const imports={'axios':axios,'./GhostSetupBundle.json':bundled,'./GhostSetupCore':core,'./GhostRepositoryCore':repo,'../r2mm/manager/PackageDexieStore':{},'../model/ThunderstoreCombo':class{},'../model/ThunderstoreMod':class{},'../model/ThunderstoreVersion':class{},'../providers/generic/file/FsProvider':{instance:io}};
 return{...evaluate(content('src/ghost/GhostSetupCatalog.ts'),imports,state),state};
}
const profile={joinToProfilePath:(...p)=>p.join('/')};
const retired='Voidey-WoWArmors';let passed=0;
async function test(name,fn){await fn();passed++;console.log('PASS '+name);}
(async()=>{
 await test('revision advances and exactly one retired package is removed',()=>{
  assert.equal(published.revision,4);assert.ok(published.revision>bundled.setup.revision);
  assert.equal(published.packages.length,28);assert.equal(bundled.setup.packages.length,29);
  assert.equal(JSON.stringify(published.packages),JSON.stringify(bundled.setup.packages.filter(p=>p.name!==retired)));
 });
 await test('launcher validator accepts the smaller setup and all dependencies remain complete',()=>{
  const setup=core.validateGhostSetup(published);assert.equal(setup.packages.length,28);
  assert.equal(core.orderSetupPackages(setup.packages).length,28);
  assert.ok(setup.packages.every(p=>(p.dependencies||[]).every(d=>!d.startsWith(retired+'-'))));
 });
 await test('new installation fetches revision 4 instead of reinstalling the bundled retired package',async()=>{
  const l=loader(),setup=await l.loadGhostSetup(profile);assert.equal(setup.revision,4);
  assert.equal(setup.packages.length,28);assert.equal(l.state.writes[0].revision,4);
  assert.ok(!setup.packages.some(p=>p.name===retired));assert.match(l.state.requests[0].url,/setup-release\.json$/);
 });
 await test('existing cached revision 1 upgrades without a launcher binary change',async()=>{
  const l=loader(published,bundled.setup),setup=await l.loadGhostSetup(profile);
  assert.equal(setup.revision,4);assert.equal(setup.packages.length,28);
 });
 await test('repair after a user uninstall never downloads or enables WoWArmors',()=>{
  const required=core.validateGhostSetup(published).packages;
  const installed=required.map(p=>({name:p.name,version:p.version,enabled:true}));
  const payloads=new Map(required.map(p=>[p.name,true]));
  const plan=core.planGhostSetup(required,installed,payloads);
  assert.equal(plan.length,0);
  installed.push({name:retired,version:'1.1.2',enabled:false});
  assert.equal(core.planGhostSetup(required,installed,payloads).length,0);
 });
 await test('a disabled Hotbar stays out of the plan even when outdated or missing its DLL',()=>{
  const required=core.validateGhostSetup(published).packages;
  const installed=required.map(p=>({name:p.name,version:p.version,enabled:true}));
  const payloads=new Map(required.map(p=>[p.name,true]));
  const hotbar=installed.find(p=>p.name==='cjayride-HotbarSwitch');assert.ok(hotbar);
  hotbar.enabled=false;hotbar.version='0.1.0';payloads.delete(hotbar.name);
  assert.equal(core.planGhostSetup(required,installed,payloads).length,0);
  hotbar.enabled=true;
  const plan=core.planGhostSetup(required,installed,payloads);
  assert.equal(plan.length,1);assert.equal(plan[0].name,hotbar.name);
 });
 await test('clean profile installs exactly the current 28 packages',()=>{
  const plan=core.planGhostSetup(published.packages,[],new Map());
  assert.equal(plan.length,28);assert.ok(!plan.some(p=>p.name===retired));
 });
 await test('once cached, revision 4 stays retired during offline use',async()=>{
  const l=loader(published,published,true),setup=await l.loadGhostSetup(profile);
  assert.equal(setup.revision,4);assert.ok(!setup.packages.some(p=>p.name===retired));
 });
 await test('same-revision edits are rejected, proving the revision bump is required',async()=>{
  const invalid={...published,revision:bundled.setup.revision};const l=loader(invalid);
  const result=await l.loadGhostSetup(profile);assert.equal(result.packages.length,29);assert.equal(l.state.writes.length,0);
 });
 await test('a newer cached revision cannot be rolled back by the public feed',async()=>{
  const newer={...published,revision:5},l=loader(published,newer),setup=await l.loadGhostSetup(profile);
  assert.equal(setup.revision,5);assert.equal(l.state.writes.length,0);
 });
 await test('website count and safe uninstall guidance match the active setup',()=>{
  const guide=fs.readFileSync(path.join(root,'docs/install.html'),'utf8');assert.ok(guide.includes('28개'));
  assert.ok(guide.includes('WoWArmors'));assert.ok(guide.includes('Uninstall'));
  assert.ok(!guide.includes('29개'));assert.ok(!guide.includes('전체 29개'));
  const history=JSON.parse(fs.readFileSync(path.join(root,'docs/update-history.json'),'utf8'));
  assert.ok([...history.entries,...history.siteUpdates].every(r=>/^\d+\.\d+\.\d+$/.test(r.version)));
 });
 console.log('RESULT: '+passed+' passed, 0 failed.');
})().catch(e=>{console.error(e);process.exitCode=1;});
