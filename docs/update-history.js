/* Ghost update history 1.0.0 — no external dependencies. */
(()=>{'use strict';
 const dialog=document.getElementById('ghost-update-history');if(!dialog)return;
 const list=dialog.querySelector('[data-history-list]'),status=dialog.querySelector('[data-history-status]');let lastOpener=null,loading=null,current=null,kind='all';
 const node=(tag,text,cls)=>{const e=document.createElement(tag);if(text!=null)e.textContent=text;if(cls)e.className=cls;return e;};
 const valid=h=>h&&h.schema===1&&Array.isArray(h.entries)&&h.entries.every(r=>r&&/^\d+\.\d+\.\d+$/.test(r.version)&&typeof r.title==='string'&&Array.isArray(r.changes)&&r.changes.every(x=>typeof x==='string')&&(!r.notes||Array.isArray(r.notes)&&r.notes.every(x=>typeof x==='string')));
 const compare=(a,b)=>{const x=a.version.split('.').map(Number),y=b.version.split('.').map(Number);return y[0]-x[0]||y[1]-x[1]||y[2]-x[2];};
 function render(h){if(!valid(h))throw Error('Invalid history');current=h;const opened=new Set([...list.querySelectorAll('details[open]')].map(x=>x.dataset.key));list.replaceChildren();
  const games=[...h.entries].sort(compare).map(r=>({...r,kind:'game',id:'suite:'+r.version}));const extras=(h.siteUpdates||[]).filter(r=>['site','board'].includes(r.kind)&&typeof r.id==='string'&&valid({schema:1,entries:[r]}));const rows=[...extras.map(r=>({...r,kind:r.component==='launcher'?'launcher':r.kind})),...games].filter(r=>kind==='all'||r.kind===kind);dialog.querySelector('[data-history-count]').textContent=rows.length+'개 버전';
  for(const r of rows){const row=node('details',null,'gh-record'),summary=node('summary'),top=node('span',null,'gh-record-top'),version=node('strong',({game:'통합팩',site:'홈페이지',board:'게시판',launcher:'런처'}[r.kind])+' v'+r.version);row.dataset.key=r.id;row.open=opened.has(r.id);top.append(version);
   if(r.kind==='game'&&r.version===h.currentVersion)top.append(node('span','최신 배포','gh-current'));
   if(r.date&&/^\d{4}-\d{2}-\d{2}$/.test(r.date)){const time=node('time',r.date.replaceAll('-','.'));time.dateTime=r.date;top.append(time);}else top.append(node('span','날짜 미기록','gh-date'));
   summary.append(top,node('span',r.title,'gh-record-title'),node('span','자세히 보기','gh-expand'));row.append(summary);
   const body=node('div',null,'gh-record-body');if(r.wardrobeVersion)body.append(node('p','포함 외형 모듈 · Wardrobe '+r.wardrobeVersion,'gh-component'));
   body.append(node('h3','변경 내용'));const ul=node('ul');for(const c of r.changes)ul.append(node('li',c));body.append(ul);
   if(r.notes&&r.notes.length){const box=node('div',null,'gh-record-note');for(const text of r.notes)box.append(node('p',text));body.append(box);}
   row.append(body);row.addEventListener('toggle',()=>summary.querySelector('.gh-expand').textContent=row.open?'접기':'자세히 보기');list.append(row);
  }
  if(!rows.length)list.append(node('p','아직 등록된 업데이트 기록이 없습니다.'));
 }
 function fallback(){const el=document.getElementById('ghost-update-history-data');if(!el)return false;try{render(JSON.parse(el.textContent));return true;}catch{return false;}}
 async function load(){if(loading)return loading;loading=(async()=>{const hasFallback=fallback();status.textContent='버전별 기록을 확인하고 있습니다…';try{if(location.protocol==='file:'){if(!hasFallback)throw Error();status.textContent='저장된 업데이트 기록입니다.';return;}const res=await fetch('update-history.json?t='+Date.now(),{cache:'no-store'});if(!res.ok)throw Error();render(await res.json());status.textContent='버전을 누르면 상세 변경 내용을 볼 수 있습니다.';}catch{status.textContent=hasFallback?'저장된 기록을 표시합니다. 최신 기록은 잠시 후 다시 확인해 주세요.':'기록을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.';}finally{loading=null;}})();return loading;}
 document.querySelectorAll('[data-update-history]').forEach(b=>b.addEventListener('click',e=>{e.preventDefault();lastOpener=b;if(!dialog.open)dialog.showModal();load();}));
 dialog.querySelector('[data-history-close]').addEventListener('click',()=>dialog.close());dialog.addEventListener('close',()=>{if(lastOpener)lastOpener.focus();});
 dialog.addEventListener('click',e=>{if(e.target!==dialog)return;const b=dialog.getBoundingClientRect();if(e.clientX<b.left||e.clientX>b.right||e.clientY<b.top||e.clientY>b.bottom)dialog.close();});
 const filters=node('div',null,'gh-filters');filters.setAttribute('role','group');filters.setAttribute('aria-label','업데이트 종류');for(const [value,label] of Object.entries({all:'전체',game:'게임·모드',site:'홈페이지',board:'게시판',launcher:'런처'})){const b=node('button',label);b.type='button';b.setAttribute('aria-pressed',String(value===kind));b.addEventListener('click',()=>{kind=value;filters.querySelectorAll('button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));if(current)render(current);});filters.append(b);}list.before(filters);
 fallback();
})();
