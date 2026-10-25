/* ghost-wardrobe-script-v1 */
(()=>{'use strict';
 const root=document.querySelector('[data-wardrobe-carousel]');if(!root)return;
 const hero=root.closest('.hero'), portraits=[...root.querySelectorAll('[data-character]')],dots=[...root.querySelectorAll('.wardrobe-dots i')];
 const labels=[['DOOM LIGHT','DARK ELF MALE · 둠 경갑'],['BERSERKER','BLACK ARMOR · 광전사'],['SKULL KNIGHT','BONE ARMOR · 해골기사']];
 const reduce=matchMedia('(prefers-reduced-motion: reduce)');let current=0,timer=null,visible=true,animations=[];
 const slots=['front','right','left'];
 function advance(){
  const old=portraits.map(el=>({transform:getComputedStyle(el).transform,opacity:getComputedStyle(el).opacity,filter:getComputedStyle(el).filter}));
  current=(current+1)%3;
  portraits.forEach((el,i)=>{el.dataset.slot=slots[(i-current+3)%3];const end=getComputedStyle(el);if(el.animate){const a=el.animate([old[i],{transform:end.transform,opacity:end.opacity,filter:end.filter}],{duration:1900,easing:'cubic-bezier(.4,0,.2,1)'});animations.push(a);a.onfinish=()=>{animations=animations.filter(x=>x!==a);};}});
  root.dataset.front=portraits[current].dataset.character;root.querySelector('[data-wardrobe-name]').textContent=labels[current][0];root.querySelector('[data-wardrobe-subtitle]').textContent=labels[current][1];dots.forEach((d,i)=>d.dataset.active=String(i===current));
 }
 function sync(){if(timer){clearInterval(timer);timer=null;}if(document.hidden||!visible||reduce.matches||hero.dataset.ghostMotion==='still'){animations.forEach(a=>a.finish());animations=[];return;}timer=setInterval(advance,6000);}
 new MutationObserver(sync).observe(hero,{attributes:true,attributeFilter:['data-ghost-motion']});document.addEventListener('visibilitychange',sync);reduce.addEventListener('change',sync);
 if('IntersectionObserver'in window)new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;sync();},{threshold:.05}).observe(hero);
 root.dataset.front='doom';sync();
})();
