// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace GhostSummonTravel {
 internal static class PlayerShieldBar {
  static readonly FieldInfo HealthRoot=AccessTools.Field(typeof(Hud),"m_healthBarRoot"),HudRoot=AccessTools.Field(typeof(Hud),"m_rootObject"),
   HealthFast=AccessTools.Field(typeof(Hud),"m_healthBarFast"),HealthSlow=AccessTools.Field(typeof(Hud),"m_healthBarSlow"),HealthText=AccessTools.Field(typeof(Hud),"m_healthText"),GuardianRoot=AccessTools.Field(typeof(Hud),"m_gpRoot");
  sealed class BarApi {internal MethodInfo Max,Value,Width;}
  sealed class Paint {internal Component Graphic;internal PropertyInfo Color;internal Color Tint;}
  static readonly Dictionary<Type,BarApi> Apis=new Dictionary<Type,BarApi>();
  static readonly List<Paint> Paints=new List<Paint>();
  static Hud owner;static RectTransform source,root;static Component fast,slow,text;static bool failed;
  static readonly Vector3[] Corners=new Vector3[4];
  static RectTransform power;static RectTransform[] powerRects;static Vector3 powerBefore,powerApplied;static bool powerMoved;
  internal struct Box {internal float Left,Right,Bottom,Top;}
  static float lastWidth=-1,lastCapacity=-1,lastRemaining=-1;static string lastText;
  internal static void ValidateApi(){if(HealthRoot==null||HudRoot==null||HealthFast==null||HealthSlow==null||HealthText==null||GuardianRoot==null)throw new MissingFieldException("내 방어막: 게임의 HP·보스 버프 표시 구조가 맞지 않습니다.");}
  internal static bool Active(Player p,out float remaining,out float capacity){remaining=capacity=0;return p&&p==Player.m_localPlayer&&!p.IsDead()&&Travel.ShieldAmounts(p,out remaining,out capacity)&&capacity>0&&remaining>0;}
  internal static string Number(float remaining,float capacity){return Math.Ceiling(Math.Max(0,remaining)).ToString("0",CultureInfo.InvariantCulture);}
  internal static Vector3 RightOfHealth(RectTransform health,Transform canvas){
   health.GetWorldCorners(Corners);var right=canvas.right;float low=float.MaxValue,high=float.MinValue;
   foreach(var point in Corners){float x=Vector3.Dot(point,right);low=Math.Min(low,x);high=Math.Max(high,x);}
   var scale=health.lossyScale;float gap=12f*Math.Max(Math.Abs(scale.x),Math.Abs(scale.y));
   return health.position+right*(high-low+gap);
  }
  static Box Bounds(RectTransform rect,Transform canvas){rect.GetWorldCorners(Corners);var result=new Box{Left=float.MaxValue,Right=float.MinValue,Bottom=float.MaxValue,Top=float.MinValue};foreach(var p in Corners){float x=Vector3.Dot(p,canvas.right),y=Vector3.Dot(p,canvas.up);result.Left=Math.Min(result.Left,x);result.Right=Math.Max(result.Right,x);result.Bottom=Math.Min(result.Bottom,y);result.Top=Math.Max(result.Top,y);}return result;}
  static Box Union(Box a,Box b){return new Box{Left=Math.Min(a.Left,b.Left),Right=Math.Max(a.Right,b.Right),Bottom=Math.Min(a.Bottom,b.Bottom),Top=Math.Max(a.Top,b.Top)};}
  static Box Shift(Box box,Vector3 delta,Transform canvas){float x=Vector3.Dot(delta,canvas.right),y=Vector3.Dot(delta,canvas.up);return new Box{Left=box.Left+x,Right=box.Right+x,Bottom=box.Bottom+y,Top=box.Top+y};}
  internal static float Clearance(Box shield,Box guardian,float gap){return guardian.Left<shield.Right+gap&&guardian.Right>shield.Left-gap&&guardian.Bottom<shield.Top+gap&&guardian.Top>shield.Bottom-gap?Math.Max(0,shield.Right+gap-guardian.Left):0;}
  static void RestorePower(){if(powerMoved&&power&&Vector3.Distance(power.localPosition,powerApplied)<0.01f)power.localPosition=powerBefore;powerMoved=false;}
  internal static void ArrangePower(Hud hud,RectTransform shield,Transform canvas){
   var current=GuardianRoot.GetValue(hud) as RectTransform;if(!current||!current.gameObject.activeInHierarchy||!shield){RestorePower();return;}
   if(power!=current){RestorePower();power=current;var set=new HashSet<RectTransform>{power};foreach(var component in power.gameObject.GetComponentsInChildren<Component>(true)){var rect=component.transform as RectTransform;if(rect)set.Add(rect);}powerRects=new RectTransform[set.Count];set.CopyTo(powerRects);}
   var own=Bounds(shield,canvas);var label=text?text.transform as RectTransform:null;if(label&&label.gameObject.activeInHierarchy)own=Union(own,Bounds(label,canvas));
   var other=Bounds(power,canvas);foreach(var rect in powerRects)if(rect&&rect.gameObject.activeInHierarchy)other=Union(other,Bounds(rect,canvas));
   bool applied=powerMoved&&Vector3.Distance(power.localPosition,powerApplied)<0.01f;var originalLocal=applied?powerBefore:power.localPosition;var before=applied?(power.parent?power.parent.TransformPoint(originalLocal):originalLocal):power.position;if(applied)other=Shift(other,before+power.position*(-1),canvas);
   float gap=16f*Math.Max(Math.Abs(shield.lossyScale.x),Math.Abs(shield.lossyScale.y)),shift=Clearance(own,other,gap);
   var target=before+canvas.right*shift;if(Vector3.Distance(power.position,target)>0.01f)power.position=target;powerBefore=originalLocal;powerApplied=power.localPosition;powerMoved=shift>0;
  }
  static MethodInfo NumericMethod(Type type,string name){
   MethodInfo best=null;foreach(var m in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)){
    if(m.Name!=name)continue;var p=m.GetParameters();if(p.Length==0||p[0].ParameterType!=typeof(float))continue;bool valid=true;for(int i=1;i<p.Length;i++)if(!p[i].IsOptional)valid=false;
    if(valid&&(best==null||p.Length<best.GetParameters().Length))best=m;
   }return best;
  }
  static BarApi Api(Component bar){var type=bar.GetType();BarApi a;if(!Apis.TryGetValue(type,out a)){a=new BarApi{Max=NumericMethod(type,"SetMaxValue"),Value=NumericMethod(type,"SetValue"),Width=NumericMethod(type,"SetWidth")};Apis[type]=a;}return a;}
  internal static void SetNumber(Component target,string method,float value){
   if(!target)return;var a=Api(target);var m=method=="SetMaxValue"?a.Max:method=="SetWidth"?a.Width:a.Value;if(m==null)return;
   var parameters=m.GetParameters();var args=new object[parameters.Length];args[0]=value;for(int i=1;i<args.Length;i++)args[i]=parameters[i].DefaultValue;m.Invoke(target,args);
  }
  static string RelativePath(Transform node,Transform ancestor){
   if(node==ancestor)return "";var names=new List<string>();for(var t=node;t&&t!=ancestor;t=t.parent){names.Add(t.name);if(t.parent==ancestor){names.Reverse();return string.Join("/",names.ToArray());}}return null;
  }
  internal static Component CopyReference(Component original,RectTransform from,RectTransform to){
   if(!original)return null;string path=RelativePath(original.transform,from);if(path==null)return null;var node=path.Length==0?to:to.Find(path);return node?node.gameObject.GetComponent(original.GetType()):null;
  }
  internal static void Property(object obj,string name,object value){var p=obj.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);if(p!=null&&p.CanWrite)p.SetValue(obj,value,null);}
  static void AddPaints(Component bar,Color tint){
   foreach(var component in bar.gameObject.GetComponentsInChildren<Component>(true)){
    if(!component||component.GetType().FullName!="UnityEngine.UI.Image")continue;
    var color=component.GetType().GetProperty("color");if(color!=null&&color.CanWrite)Paints.Add(new Paint{Graphic=component,Color=color,Tint=tint});
   }
  }
  internal static bool Ensure(Hud hud){
   if(owner==hud&&root&&fast&&slow)return true;
   Clear();source=HealthRoot.GetValue(hud) as RectTransform;var host=HudRoot.GetValue(hud) as GameObject;var templateFast=HealthFast.GetValue(hud) as Component;var templateSlow=HealthSlow.GetValue(hud) as Component;var templateText=HealthText.GetValue(hud) as Component;
   if(!source||!host||!templateFast||!templateSlow||!templateText)return false;
   owner=hud;var clone=UnityEngine.Object.Instantiate(source.gameObject,host.transform,false);clone.name="GhostPlayerShieldBar";root=clone.transform as RectTransform;
   if(!root){UnityEngine.Object.Destroy(clone);Clear();return false;}
   fast=CopyReference(templateFast,source,root);slow=CopyReference(templateSlow,source,root);text=CopyReference(templateText,source,root);
   if(!fast||!slow){Clear();return false;}
   if(!text){var label=UnityEngine.Object.Instantiate(templateText.gameObject,root,false);label.name="GhostPlayerShieldText";text=label.GetComponent(templateText.GetType());var r=label.transform as RectTransform;if(r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}}
   foreach(var component in clone.GetComponentsInChildren<Component>(true)){
    if(!component)continue;Property(component,"raycastTarget",false);
    if(component.GetType().FullName=="UnityEngine.Animator")Property(component,"enabled",false);
   }
   if(text){Property(text,"enableAutoSizing",true);Property(text,"fontSizeMin",10f);Property(text,"color",new Color(0.9f,0.98f,1f,1f));}
   AddPaints(fast,new Color(0.2f,0.72f,1f,1f));AddPaints(slow,new Color(0.1f,0.38f,0.65f,1f));
   clone.SetActive(false);return true;
  }
  internal static void UpdateHud(Hud hud){
   var p=Player.m_localPlayer;float remaining,capacity;
   if(!Active(p,out remaining,out capacity)||Hud.IsUserHidden()){RestorePower();if(root&&root.gameObject.activeSelf)root.gameObject.SetActive(false);return;}
   if(!Ensure(hud))return;var host=HudRoot.GetValue(hud) as GameObject;if(!host||!host.activeInHierarchy){RestorePower();if(root.gameObject.activeSelf)root.gameObject.SetActive(false);return;}
   float width=source.rect.width,height=source.rect.height;
   if(width<=0||height<=0){RestorePower();return;}
   root.anchorMin=root.anchorMax=new Vector2(0.5f,0.5f);root.pivot=source.pivot;root.sizeDelta=new Vector2(width,height);
   var parentScale=root.parent.lossyScale;var desired=source.lossyScale;
   root.localScale=new Vector3(Math.Abs(parentScale.x)>0.00001f?desired.x/parentScale.x:1f,Math.Abs(parentScale.y)>0.00001f?desired.y/parentScale.y:1f,1f);
   root.rotation=source.rotation;root.position=RightOfHealth(source,host.transform);
   if(lastWidth!=width){SetNumber(fast,"SetWidth",width);SetNumber(slow,"SetWidth",width);lastWidth=width;}
   if(lastCapacity!=capacity){SetNumber(fast,"SetMaxValue",capacity);SetNumber(slow,"SetMaxValue",capacity);lastCapacity=capacity;lastRemaining=-1;}
   if(lastRemaining!=remaining){SetNumber(fast,"SetValue",remaining);SetNumber(slow,"SetValue",remaining);lastRemaining=remaining;}
   string value=Number(remaining,capacity);if(lastText!=value){ShieldPresentation.SetText(text,value);lastText=value;}
   foreach(var paint in Paints)if(paint.Graphic)paint.Color.SetValue(paint.Graphic,paint.Tint,null);
   if(!root.gameObject.activeSelf)root.gameObject.SetActive(true);
   ArrangePower(hud,root,host.transform);
  }
  internal static void Clear(){RestorePower();power=null;powerRects=null;if(root)UnityEngine.Object.Destroy(root.gameObject);root=source=null;owner=null;fast=slow=text=null;Paints.Clear();lastWidth=lastCapacity=lastRemaining=-1;lastText=null;}
  [HarmonyPatch(typeof(Hud),"Awake")]static class CreatePatch{[HarmonyPostfix]static void Postfix(Hud __instance){failed=false;try{Ensure(__instance);}catch(Exception e){failed=true;Plugin.LogFault(e);}}}
  [HarmonyPatch(typeof(Hud),"LateUpdate")]static class UpdatePatch{[HarmonyPostfix,HarmonyPriority(Priority.Last)]static void Postfix(Hud __instance){if(failed)return;try{UpdateHud(__instance);}catch(Exception e){failed=true;Plugin.LogFault(e);}}}
  [HarmonyPatch(typeof(Hud),"OnDestroy")]static class DestroyPatch{[HarmonyPrefix]static void Prefix(Hud __instance){try{if(owner==__instance)Clear();}catch(Exception e){Plugin.LogFault(e);}}}
 }
}
