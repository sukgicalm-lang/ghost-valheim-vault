/* Ghost Vault · The Quiet Keeper · procedural geometry, no remote assets. */
const hosts = [...document.querySelectorAll('[data-ghost-reaper]')];
const reduced = matchMedia('(prefers-reduced-motion: reduce)');
const clamp = (v,a,b)=>Math.max(a,Math.min(b,v));
const smooth = t => {t=clamp(t,0,1);return t*t*(3-2*t)};
// Four unhurried acts. Yaw is continuous; the full back really exists.
export function keeperPose(seconds, still=false) {
  const t=((seconds%88)+88)%88, bob=still?0:Math.sin(seconds*1.35)*.045;
  if(still) return {act:0,x:0,z:0,y:0,yaw:-.4,head:0,nod:0,arm:0,breath:0};
  if(t<20) {const envelope=Math.sin(Math.PI*t/20)**2;return {act:0,x:0,z:0,y:bob,yaw:-.4+Math.sin(t*.17)*.12*envelope,head:Math.sin(t*.23)*.1*envelope,nod:Math.sin(t*.3)*.025*envelope,arm:0,breath:Math.sin(seconds*1.1)};}
  if(t<46) {const p=(t-20)/26,travel=Math.sin(Math.PI*p*2);
    return {act:1,x:travel*.42,z:Math.sin(Math.PI*p)*.14,y:bob,yaw:-.4+Math.sin(Math.PI*p*2)*.95,head:Math.sin(p*Math.PI*2)*.12,nod:0,arm:Math.sin(p*Math.PI*6)*.06,breath:Math.sin(seconds*1.1)};}
  if(t<70) {const p=smooth((t-46)/24);return {act:2,x:0,z:0,y:bob,yaw:-.4+Math.PI*2*p,head:Math.sin(p*Math.PI*2)*.12,nod:0,arm:0,breath:Math.sin(seconds*1.1)};}
  const p=(t-70)/18,look=Math.sin(Math.PI*p)**2;
  return {act:3,x:0,z:0,y:bob,yaw:Math.PI*2-.4+look*.26,head:look*.19,nod:Math.sin(p*Math.PI*4)*look*.1,arm:-look*.24,breath:Math.sin(seconds*1.1)};
}

async function createKeeper(host) {
  // Import after the document and primary images; no external CDN at runtime.
  const T=await import('./ghost-three.module.js');
  const scene=new T.Scene();
  let renderer;
  try {renderer=new T.WebGLRenderer({alpha:true,antialias:true,powerPreference:'low-power'});}
  catch {renderer=createCanvasRenderer(T);host.dataset.reaperEngine='canvas3d';}
  renderer.setPixelRatio(Math.min(devicePixelRatio,1.6));
  renderer.outputColorSpace=T.SRGBColorSpace;
  renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.3;
  renderer.setClearColor(0,0);
  const canvas=renderer.domElement;canvas.setAttribute('aria-hidden','true');host.append(canvas);
  const camera=new T.PerspectiveCamera(31,1,.1,60);camera.position.set(0,2.45,9.6);camera.lookAt(0,2.05,0);
  scene.add(new T.HemisphereLight(0xb8dfd8,0x172a28,2.2));
  const light=(c,i,x,y,z)=>{const l=new T.DirectionalLight(c,i);l.position.set(x,y,z);scene.add(l);};
  light(0xf1dfbf,3.4,-3,6,5);light(0x74c9c7,4,3,4,-3);light(0xc8b38a,.9,3,1,4);
  const root=new T.Group();root.position.y=.1;scene.add(root);
  const body=new T.Group();root.add(body);
  const mats={};
  // A restrained woven surface, generated deterministically in the browser.
  const texCanvas=document.createElement('canvas');texCanvas.width=texCanvas.height=128;
  const ctx=texCanvas.getContext('2d'),data=ctx.createImageData(128,128);
  for(let i=0;i<128*128;i++){const x=i%128,y=Math.floor(i/128);const n=115+((x*73+y*151+x*y*13)%47)+(x%3===0?16:0)+(y%3===0?10:0);data.data.set([n,n,n,255],i*4)}
  ctx.putImageData(data,0,0);const weave=new T.CanvasTexture(texCanvas);weave.wrapS=weave.wrapT=T.RepeatWrapping;weave.repeat.set(7,12);
  mats.cloth=new T.MeshStandardMaterial({color:0x273c3e,roughness:.93,metalness:.06,bumpMap:weave,bumpScale:.012,side:T.DoubleSide});
  mats.trim=new T.MeshStandardMaterial({color:0x405657,roughness:.82,metalness:.08});
  mats.dark=new T.MeshStandardMaterial({color:0x020809,roughness:1,side:T.DoubleSide});
  mats.bone=new T.MeshStandardMaterial({color:0xd3c9ad,roughness:.74,metalness:.02,bumpMap:weave,bumpScale:.004});
  mats.ivory=new T.MeshStandardMaterial({color:0xe5d9bd,roughness:.6});
  mats.leather=new T.MeshStandardMaterial({color:0x422d22,roughness:.94,bumpMap:weave,bumpScale:.01});
  mats.gold=new T.MeshStandardMaterial({color:0x9c7b45,roughness:.46,metalness:.7});
  mats.steel=new T.MeshStandardMaterial({color:0x839b9c,roughness:.36,metalness:.88});
  mats.edge=new T.MeshStandardMaterial({color:0xd2e0dc,roughness:.24,metalness:.91});
  mats.eye=new T.MeshBasicMaterial({color:0x9bd9c1,transparent:true,opacity:.85});
  const sphereGeo=new T.SphereGeometry(1,24,16);
  const ellipsoid=(parent,mat,x,y,z,sx,sy,sz)=>{const m=new T.Mesh(sphereGeo,mat);m.position.set(x,y,z);m.scale.set(sx,sy,sz);parent.add(m);return m;};
  const pathMesh=(parent,points,radius,mat,segments=36)=>{
    const curve=new T.CatmullRomCurve3(points.map(p=>new T.Vector3(...p)));
    const m=new T.Mesh(new T.TubeGeometry(curve,segments,radius,7,false),mat);parent.add(m);return m;
  };
  const bone=(parent,a,b,r=.025)=>{const d=new T.Vector3(...b).sub(new T.Vector3(...a));const m=new T.Mesh(new T.CylinderGeometry(r*.85,r,d.length(),8),mats.bone);m.position.copy(new T.Vector3(...a).add(new T.Vector3(...b)).multiplyScalar(.5));m.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),d.normalize());parent.add(m);ellipsoid(parent,mats.ivory,...a,r*1.2,r*1.2,r*1.2);return m;};
  function surface(rows,cols,fn,material,parent=body){const pts=[],uv=[],indices=[];
    for(let j=0;j<=rows;j++)for(let i=0;i<=cols;i++){const p=fn(i/cols,j/rows);pts.push(...p);uv.push(i/cols,j/rows)}
    for(let j=0;j<rows;j++)for(let i=0;i<cols;i++){const a=j*(cols+1)+i,b=a+cols+1;indices.push(a,b,a+1,b,b+1,a+1)}
    const geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute(pts,3));geo.setAttribute('uv',new T.Float32BufferAttribute(uv,2));geo.setIndex(indices);geo.computeVertexNormals();
    const m=new T.Mesh(geo,material);parent.add(m);return m;
  }
  // A continuous bell-shaped robe with a tapered waist and flowing hem.
  const profile=[[0,.82,.48],[.18,.78,.47],[.55,.66,.39],[1,.45,.32],[1.45,.34,.28],[1.95,.46,.31],[2.32,.65,.32],[2.5,.4,.25]];
  function robeAt(u,v){const f=v*(profile.length-1),a=Math.min(profile.length-2,Math.floor(f)),q=f-a;
    const P=profile[a],Q=profile[a+1],y=T.MathUtils.lerp(P[0],Q[0],q),rx=T.MathUtils.lerp(P[1],Q[1],q),rz=T.MathUtils.lerp(P[2],Q[2],q),theta=u*Math.PI*2;
    const fold=Math.cos(theta*14+.25*Math.sin(y*2))*.04+Math.cos(theta*23)*.009;
    const trailing=Math.pow(Math.max(0,-Math.cos(theta)),3)*(1-v)**3;
    return [Math.sin(theta)*(rx+fold),y+Math.sin(theta*7+.5)*.065*(1-v)**5,(Math.cos(theta)*(rz+fold))-trailing*.42];
  }
  const robe=surface(56,104,robeAt,mats.cloth);const robeBase=robe.geometry.attributes.position.array.slice();
  const hem=[];for(let i=0;i<=110;i++){const p=robeAt(i/110,0);p[1]+=.02;hem.push(p)}pathMesh(body,hem,.014,mats.trim,110);
  // Stitched centre panels and a weathered waist cord.
  for(const sign of [-1,1])pathMesh(body,[[sign*.15,2.35,.32],[sign*.1,1.85,.31],[sign*.09,1.45,.3],[sign*.19,.75,.38],[sign*.33,.06,.48]],.009,mats.trim);
  const belt=[];for(let i=0;i<=64;i++){let a=i/64*Math.PI*2;belt.push([Math.sin(a)*.377,1.49+Math.cos(a)*.02,Math.cos(a)*.315])}pathMesh(body,belt,.022,mats.leather,64);
  pathMesh(body,[[.16,1.5,.34],[.2,1.2,.35],[.14,.87,.4]],.018,mats.leather);ellipsoid(body,mats.gold,.16,1.5,.355,.055,.06,.025);
  // Shoulder cowl is sculpted as overlapping folded fabric, not a flat card.
  const cowl=surface(22,80,(u,v)=>{const a=u*Math.PI*2,r=.24+v*.45;return [Math.sin(a)*r,2.62-v*.29+Math.cos(a*3)*.035,Math.cos(a)*(r*.57)+.025]},mats.cloth);
  const cowlEdge=[];for(let i=0;i<=80;i++){const a=i/80*Math.PI*2;cowlEdge.push([Math.sin(a)*.69,2.33+Math.cos(a*3)*.035,Math.cos(a)*.394+.025])}pathMesh(body,cowlEdge,.022,mats.trim,80);
  // Sleeves hang from articulated shoulders; skeletal fingers remain separate.
  const arms=[];
  for(const side of [-1,1]){const arm=new T.Group();arm.position.set(side*.49,2.31,0);body.add(arm);arms.push(arm);
    surface(25,40,(u,v)=>{const a=u*Math.PI*2,r=.19+v*.085,centerX=side*v*.24;return [centerX+Math.cos(a)*r,-v*.91+Math.cos(a*5)*.025*v*v,Math.sin(a)*r*.85+.06*v]},mats.cloth,arm);
    const rim=[];for(let i=0;i<=48;i++){let a=i/48*Math.PI*2;rim.push([side*.24+Math.cos(a)*.273,-.91+Math.cos(a*5)*.025,Math.sin(a)*.235+.06])}pathMesh(arm,rim,.014,mats.trim,48);
    ellipsoid(arm,mats.dark,side*.24,-.9,.06,.25,.018,.22);
    const hand=new T.Group();hand.position.set(side*.24,-.86,.1);arm.add(hand);hand.rotation.z=side*.14;
    bone(hand,[-.045,0,0],[-.038,-.19,.015],.027);bone(hand,[.045,0,0],[.038,-.19,.015],.025);
    ellipsoid(hand,mats.bone,0,-.24,.015,.095,.115,.045);
    for(let f=0;f<4;f++){const x=(f-1.5)*.047,len=.2-Math.abs(f-1.5)*.035;
      const A=[x,-.29,.025],B=[x*1.23,-.29-len*.58,.07],C=[x*1.32,-.29-len,.13];bone(hand,A,B,.016);bone(hand,B,C,.014);bone(hand,C,[C[0],C[1]-.035,C[2]+.045],.011);}
    bone(hand,[side*.08,-.19,.04],[side*.145,-.27,.1],.02);bone(hand,[side*.145,-.27,.1],[side*.13,-.35,.15],.016);
  }
  // Open hood: closed rear surface, thick rolled rim, shadowed inner lining.
  const head=new T.Group();head.position.y=2.62;body.add(head);
  function hoodPoint(u,v,inner=false){const y=v*1.16,opening=(1-smooth((v-.68)/.32))*.78;
    const angle=opening+u*(Math.PI*2-opening*2),width=(.47+.13*Math.sin(v*Math.PI))*(1-.95*smooth((v-.81)/.19));
    const fold=.012*Math.cos(angle*11+v*3),r=width+fold-(inner?.03:0);
    return [Math.sin(angle)*r,y,Math.cos(angle)*r*.84-.09-v*.03];
  }
  surface(40,72,(u,v)=>hoodPoint(u,v),mats.cloth,head);
  surface(40,72,(u,v)=>hoodPoint(u,v,true),mats.dark,head);
  const hoodRim=[];for(let i=0;i<=38;i++)hoodRim.push(hoodPoint(0,i/38));for(let i=38;i>=0;i--)hoodRim.push(hoodPoint(1,i/38));pathMesh(head,hoodRim,.032,mats.trim,100);
  // Rear crown seam and fabric ridges catch the cold rim light while turning.
  pathMesh(head,[[0,0,-.49],[0,.45,-.58],[0,.85,-.46],[0,1.13,-.12]],.009,mats.trim);
  // Skull with inset sockets, cheekbones, nasal aperture and individual teeth.
  const skull=new T.Group();skull.position.set(0,.43,.19);head.add(skull);
  const cranial=new T.SphereGeometry(1,48,32);const pos=cranial.attributes.position;
  for(let i=0;i<pos.count;i++){let x=pos.getX(i)*.365,y=pos.getY(i)*.435,z=pos.getZ(i)*.29;
    if(z>0){const socket=Math.exp(-((Math.abs(x)-.157)**2/.006+(y-.035)**2/.006));z-=socket*.1;}
    if(y<-.08)x*=1+Math.min(0,(y+.08))*.6;pos.setXYZ(i,x,y,z)}cranial.computeVertexNormals();
  skull.add(new T.Mesh(cranial,mats.bone));
  for(const sign of [-1,1]){
    const socket=ellipsoid(skull,mats.dark,sign*.157,.035,.253,.112,.111,.044);socket.rotation.z=sign*.13;
    // Bone orbital rims and a heavy brow create real depth at oblique angles.
    pathMesh(skull,[[sign*.055,.12,.26],[sign*.12,.161,.276],[sign*.215,.139,.257],[sign*.265,.06,.225]],.028,mats.ivory,25);
    pathMesh(skull,[[sign*.062,-.014,.263],[sign*.1,-.076,.269],[sign*.196,-.08,.254],[sign*.255,-.027,.222]],.021,mats.bone,22);
    ellipsoid(skull,mats.bone,sign*.256,-.12,.2,.09,.075,.08);
    ellipsoid(skull,mats.eye,sign*.156,.034,.296,.018,.015,.008);
    pathMesh(skull,[[sign*.24,-.14,.18],[sign*.22,-.29,.16],[sign*.13,-.35,.23],[0,-.36,.255]],.041,mats.bone,24);
  }
  const noseShape=new T.Shape();noseShape.moveTo(0,.015);noseShape.lineTo(-.048,-.112);noseShape.quadraticCurveTo(0,-.088,.048,-.112);noseShape.closePath();
  const nose=new T.Mesh(new T.ShapeGeometry(noseShape),mats.dark);nose.position.set(0,-.055,.298);skull.add(nose);
  ellipsoid(skull,mats.bone,0,-.19,.217,.14,.065,.095);
  for(let row=0;row<2;row++)for(let i=0;i<8;i++){
    const x=(i-3.5)*.036,teeth=new T.Mesh(new T.BoxGeometry(.028,.051,.047,1,1,1),mats.ivory);teeth.position.set(x,-.222-row*.061,.289-Math.abs(x)*.19);teeth.rotation.y=x*1.6;skull.add(teeth);
  }
  // A leather strap is visible across the back, beneath a carried scythe.
  pathMesh(body,[[.45,2.39,-.27],[.25,2,-.35],[-.06,1.45,-.35],[-.4,.72,-.4]],.048,mats.leather,44);
  const scythe=new T.Group();scythe.position.set(.48,.45,-.4);scythe.rotation.z=-.21;body.add(scythe);
  pathMesh(scythe,[[0,-.15,0],[.015,.7,.01],[-.025,1.55,0],[0,2.4,.025],[.04,3.35,.025]],.048,mats.leather,42);
  for(const y of [.17,2.62,2.92]){const ring=new T.Mesh(new T.CylinderGeometry(.059,.059,.1,12),mats.gold);ring.position.set(.01,y,.015);scythe.add(ring)}
  const bladeShape=new T.Shape();bladeShape.moveTo(.01,3.11);bladeShape.bezierCurveTo(.53,3.19,1.03,2.93,1.25,2.43);bladeShape.bezierCurveTo(1.2,3.11,.64,3.57,-.04,3.4);bladeShape.lineTo(-.04,3.15);bladeShape.closePath();
  const blade=new T.Mesh(new T.ExtrudeGeometry(bladeShape,{depth:.035,bevelEnabled:true,bevelSegments:2,steps:1,bevelSize:.012,bevelThickness:.01,curveSegments:30}),mats.steel);scythe.add(blade);
  pathMesh(scythe,[[.02,3.12,.047],[.37,3.12,.047],[.76,2.96,.047],[1.08,2.68,.047],[1.25,2.43,.047]],.012,mats.edge,50);
  // Quiet ground shadow and sparse embers, all transparent and local.
  const shadowCanvas=document.createElement('canvas');shadowCanvas.width=shadowCanvas.height=64;const sc=shadowCanvas.getContext('2d'),gradient=sc.createRadialGradient(32,32,0,32,32,32);gradient.addColorStop(0,'rgba(0,0,0,.45)');gradient.addColorStop(1,'rgba(0,0,0,0)');sc.fillStyle=gradient;sc.fillRect(0,0,64,64);
  const shadow=new T.Mesh(new T.PlaneGeometry(2.5,1.4),new T.MeshBasicMaterial({map:new T.CanvasTexture(shadowCanvas),transparent:true,depthWrite:false}));shadow.rotation.x=-Math.PI/2;shadow.position.y=-.08;scene.add(shadow);
  const particles=new T.BufferGeometry(),particleData=new Float32Array(18*3);
  for(let i=0;i<18;i++){particleData[i*3]=Math.sin(i*7.17)*1.3;particleData[i*3+1]=(i/18)*3.7;particleData[i*3+2]=Math.cos(i*4.2)*.7}
  particles.setAttribute('position',new T.BufferAttribute(particleData,3));const sparks=new T.Points(particles,new T.PointsMaterial({color:0x9bbfae,size:.017,transparent:true,opacity:.47,depthWrite:false}));scene.add(sparks);
  function resize(){const w=host.clientWidth,h=host.clientHeight;if(!w||!h)return;renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix()}
  const ro=new ResizeObserver(resize);ro.observe(host);resize();
  let inView=true,raf=0,previous=0,elapsed=0,destroyed=false,first=true;
  const observer=new IntersectionObserver(entries=>{inView=entries[0].isIntersecting;if(inView&&!raf&&!document.hidden)raf=requestAnimationFrame(frame)},{rootMargin:'80px'});observer.observe(host);
  function renderAt(seconds){const pose=keeperPose(seconds,reduced.matches);
    root.position.set(pose.x,.14+pose.y,pose.z);root.rotation.y=pose.yaw;body.rotation.z=Math.sin(seconds*.65)*.012*(reduced.matches?0:1);body.scale.y=1+pose.breath*.003;
    head.rotation.y=pose.head;head.rotation.x=pose.nod;arms[0].rotation.x=pose.arm;arms[1].rotation.x=-pose.arm*.55;
    const p=robe.geometry.attributes.position;
    for(let i=0;i<p.count;i++){const x=robeBase[i*3],y=robeBase[i*3+1],z=robeBase[i*3+2],influence=(1-clamp(y/2.4,0,1))**2;
      p.setXYZ(i,x+Math.sin(seconds*1.05+y*2+z*4)*.027*influence*(reduced.matches?0:1),y,z+Math.sin(seconds*.8+x*5+y)*.045*influence*(reduced.matches?0:1));}
    p.needsUpdate=true;robe.geometry.computeVertexNormals();
    scythe.rotation.x=Math.sin(seconds*.85)*.009*(reduced.matches?0:1);sparks.rotation.y=reduced.matches?0:seconds*.025;
    shadow.position.x=pose.x;shadow.scale.setScalar(1-pose.y*.8);
    host.dataset.reaperAct=String(pose.act);renderer.render(scene,camera);
    if(first){host.dataset.reaperState='ready';first=false;}
  }
  function frame(now){raf=0;if(destroyed||!inView||document.hidden){previous=0;return;}
    // Limit animation to 30 fps and freeze when the page or hero is hidden.
    if(!previous||now-previous>=(renderer.isSoftware?100:32)){if(previous)elapsed+=Math.min(.2,(now-previous)/1000);previous=now;renderAt(elapsed)}
    if(!reduced.matches)raf=requestAnimationFrame(frame);
  }
  const resume=()=>{previous=0;if(!document.hidden&&inView&&!raf)raf=requestAnimationFrame(frame)};
  document.addEventListener('visibilitychange',resume);reduced.addEventListener('change',resume);
  canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();cancelAnimationFrame(raf);raf=0;host.dataset.reaperState='fallback'});
  canvas.addEventListener('webglcontextrestored',()=>{first=true;resume()});
  renderAt(0);if(!reduced.matches&&!raf)raf=requestAnimationFrame(frame);
  window.addEventListener('pagehide',event=>{if(event.persisted)return;destroyed=true;cancelAnimationFrame(raf);ro.disconnect();observer.disconnect();document.removeEventListener('visibilitychange',resume);reduced.removeEventListener('change',resume);const geometries=new Set(),materials=new Set(),textures=new Set();scene.traverse(o=>{if(o.geometry)geometries.add(o.geometry);if(o.material)for(const m of Array.isArray(o.material)?o.material:[o.material])materials.add(m)});materials.forEach(m=>{Object.values(m).forEach(v=>{if(v?.isTexture)textures.add(v)});m.dispose()});textures.forEach(t=>t.dispose());geometries.forEach(g=>g.dispose());renderer.dispose();},{once:true});
}
const start=()=>hosts.forEach(host=>createKeeper(host).catch(()=>{host.dataset.reaperState='fallback'}));
if(document.readyState==='complete')('requestIdleCallback' in window?requestIdleCallback(start,{timeout:1800}):setTimeout(start,150));
else window.addEventListener('load',()=>('requestIdleCallback' in window?requestIdleCallback(start,{timeout:1800}):setTimeout(start,150)),{once:true});

// Real perspective projection of the same meshes when WebGL is unavailable.
// Keeps the character usable on restricted browsers without replacing it by a card.
function createCanvasRenderer(T){
  const canvas=document.createElement('canvas'),ctx=canvas.getContext('2d');let width=1,height=1,dpr=1;
  const ambient=new T.Color(),normal=new T.Vector3(),ab=new T.Vector3(),ac=new T.Vector3();
  const a=new T.Vector3(),b=new T.Vector3(),c=new T.Vector3(),pa=new T.Vector3(),pb=new T.Vector3(),pc=new T.Vector3();
  const key=new T.Vector3(-.45,.7,.6).normalize(),rim=new T.Vector3(.7,.45,-.55).normalize(),view=new T.Vector3();
  return {domElement:canvas,isSoftware:true,setPixelRatio(v){dpr=Math.min(v,1.25)},setClearColor(){},dispose(){},
    setSize(w,h){width=w;height=h;canvas.width=Math.round(w*dpr);canvas.height=Math.round(h*dpr)},
    render(scene,camera){scene.updateMatrixWorld();camera.updateMatrixWorld();const faces=[];camera.getWorldDirection(view);
      scene.traverse(o=>{if(!o.isMesh||!o.visible||o.geometry.type==='PlaneGeometry')return;const geo=o.geometry,pos=geo.attributes.position,ind=geo.index;
        const mat=Array.isArray(o.material)?o.material[0]:o.material;if(!mat?.color)return;
        const count=ind?ind.count:pos.count;
        for(let i=0;i<count;i+=3){a.fromBufferAttribute(pos,ind?ind.getX(i):i).applyMatrix4(o.matrixWorld);b.fromBufferAttribute(pos,ind?ind.getX(i+1):i+1).applyMatrix4(o.matrixWorld);c.fromBufferAttribute(pos,ind?ind.getX(i+2):i+2).applyMatrix4(o.matrixWorld);
          normal.crossVectors(ab.subVectors(b,a),ac.subVectors(c,a)).normalize();const facing=normal.dot(view);
          if(mat.side!==T.DoubleSide&&facing>0)continue;if(facing>0)normal.negate();
          pa.copy(a).project(camera);pb.copy(b).project(camera);pc.copy(c).project(camera);if(pa.z>1||pa.z< -1)continue;
          const coords=[(pa.x+1)*width/2,(1-pa.y)*height/2,(pb.x+1)*width/2,(1-pb.y)*height/2,(pc.x+1)*width/2,(1-pc.y)*height/2];
          const area=Math.abs((coords[2]-coords[0])*(coords[5]-coords[1])-(coords[4]-coords[0])*(coords[3]-coords[1]));if(area<.1)continue;
          const k=Math.max(0,normal.dot(key)),r=Math.max(0,normal.dot(rim));
          ambient.copy(mat.color);const level=.36+k*.92+r*.42;ambient.multiplyScalar(level);
          // Small, cool rim highlights keep the charcoal fabric readable.
          ambient.r+=r*.022;ambient.g+=r*.065;ambient.b+=r*.067;
          const rgb=ambient.getStyle(T.SRGBColorSpace);faces.push({coords,z:(pa.z+pb.z+pc.z)/3,color:rgb,opacity:mat.opacity??1});
        }});
      faces.sort((a,b)=>b.z-a.z);ctx.setTransform(dpr,0,0,dpr,0,0);ctx.clearRect(0,0,width,height);
      // Soft ground contact, without a texture upload.
      const g=ctx.createRadialGradient(width*.5,height*.88,1,width*.5,height*.88,width*.25);g.addColorStop(0,'#0005');g.addColorStop(1,'#0000');ctx.save();ctx.translate(0,height*.76);ctx.scale(1,.15);ctx.fillStyle=g;ctx.fillRect(0,-height*5,width,height*8);ctx.restore();
      for(const f of faces){const q=f.coords;ctx.globalAlpha=f.opacity;ctx.fillStyle=ctx.strokeStyle=f.color;ctx.lineWidth=.45;ctx.lineJoin='round';ctx.beginPath();ctx.moveTo(q[0],q[1]);ctx.lineTo(q[2],q[3]);ctx.lineTo(q[4],q[5]);ctx.closePath();ctx.fill();ctx.stroke();}ctx.globalAlpha=1;
    }};
}
