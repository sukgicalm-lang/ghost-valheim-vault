/* ghost-wardrobe-five-v1.4.13 */
(()=>{'use strict';
 const root=document.querySelector('[data-wardrobe-carousel]');if(!root||root.dataset.carouselReady)return;
 const hero=root.closest('.hero'),portraits=[...root.querySelectorAll('[data-character]')],dots=[...root.querySelectorAll('[data-wardrobe-index]')];
 const slots=['front','right','back-right','back-left','left'];if(portraits.length!==slots.length)return;root.dataset.carouselReady='true';
 const reduce=matchMedia('(prefers-reduced-motion: reduce)');let current=0,timer=null,visible=true,animations=[];
 function stopped(){return document.hidden||!visible||reduce.matches||hero?.dataset.ghostMotion==='still';}
 function go(index,animate=true){
  animations.forEach(a=>a.cancel());animations=[];
  const old=portraits.map(el=>({transform:getComputedStyle(el).transform,opacity:getComputedStyle(el).opacity,filter:getComputedStyle(el).filter}));
  current=(index+portraits.length)%portraits.length;
  portraits.forEach((el,i)=>{
   el.dataset.slot=slots[(i-current+portraits.length)%portraits.length];el.setAttribute('aria-hidden',String(i!==current));
   const end=getComputedStyle(el);
   if(animate&&!stopped()&&el.animate){const a=el.animate([old[i],{transform:end.transform,opacity:end.opacity,filter:end.filter}],{duration:1900,easing:'cubic-bezier(.4,0,.2,1)'});animations.push(a);a.onfinish=()=>{animations=animations.filter(x=>x!==a);};}
  });
  root.dataset.front=portraits[current].dataset.character;
  root.querySelector('[data-wardrobe-name]').textContent=portraits[current].dataset.label;
  root.querySelector('[data-wardrobe-subtitle]').textContent=portraits[current].dataset.subtitle;
  dots.forEach((d,i)=>{d.dataset.active=String(i===current);d.setAttribute('aria-pressed',String(i===current));});
 }
 function advance(){go(current+1);}
 function sync(){if(timer!==null){clearInterval(timer);timer=null;}if(stopped()){animations.forEach(a=>a.finish());animations=[];return;}timer=setInterval(advance,6000);}
 dots.forEach((dot,i)=>dot.addEventListener('click',()=>{go(i);sync();}));
 if(hero)new MutationObserver(sync).observe(hero,{attributes:true,attributeFilter:['data-ghost-motion']});
 document.addEventListener('visibilitychange',sync);reduce.addEventListener('change',sync);
 if('IntersectionObserver'in window)new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;sync();},{threshold:.05}).observe(hero||root);
 go(0,false);sync();
})();
