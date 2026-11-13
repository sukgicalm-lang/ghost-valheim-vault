// SPDX-License-Identifier: MIT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace GhostSummonTravel {
 internal static class ShieldPresentation {
  static readonly FieldInfo Instances=AccessTools.Field(typeof(StatusEffect),"m_startEffectInstances");
  static readonly MethodInfo Trigger=AccessTools.Method(typeof(StatusEffect),"TriggerStartEffects");
  static readonly FieldInfo Huds=AccessTools.Field(typeof(EnemyHud),"m_huds");
  static readonly Dictionary<GameObject,Component> Labels=new Dictionary<GameObject,Component>();
  static readonly Dictionary<Type,PropertyInfo> TextProperties=new Dictionary<Type,PropertyInfo>();
  static readonly MethodInfo ShowHud=AccessTools.Method(typeof(EnemyHud),"ShowHud");
  sealed class Row {internal GameObject Root;internal Component Text;internal string Label;internal float Font=-1;}
  sealed class Native {internal GameObject Root;internal bool Active;}
  sealed class Vitals {internal EnemyHud Hud;internal Character Character;internal Component Name;internal List<Native> Hidden=new List<Native>();internal Row Health,Shield,Timer;}
  static readonly Dictionary<GameObject,Vitals> Views=new Dictionary<GameObject,Vitals>();
  sealed class LocalHud {internal EnemyHud Hud;internal Character Character;}
  static readonly Dictionary<GameObject,LocalHud> LocalRoots=new Dictionary<GameObject,LocalHud>();
  sealed class RepairRequest {internal bool Force;internal double Due;}
  static readonly Dictionary<Character,RepairRequest> Pending=new Dictionary<Character,RepairRequest>();
  internal static void RequestRepair(Character c,bool force,double delay){if(!Travel.IsSkeleton(c)||!SummonLifecycle.Usable(c))return;RepairRequest request;if(Pending.TryGetValue(c,out request)){request.Force|=force;request.Due=Math.Min(request.Due,Travel.Now+delay);}else Pending[c]=new RepairRequest{Force=force,Due=Travel.Now+delay};}
  internal static void ProcessRepairs(){Character selected=null;RepairRequest request=null;foreach(var entry in Pending){if(!entry.Key||entry.Value.Due<=Travel.Now){selected=entry.Key;request=entry.Value;break;}}if(request==null)return;Pending.Remove(selected);if(selected)Repair(selected,request.Force);}
  internal static void ValidateApi(){if(Instances==null||Trigger==null||Huds==null||ShowHud==null)throw new MissingMemberException("해골 방어막: 게임의 이펙트·체력 표시 API가 맞지 않습니다.");}
  internal static bool Missing(GameObject[] effects){return effects==null||effects.Length==0||effects.Any(x=>!x||!x.activeInHierarchy);}
  internal static void Repair(Character c,bool force){
   if(!Travel.IsSkeleton(c)||!SummonLifecycle.Usable(c)||!c.GetComponent<ZNetView>().IsOwner())return;
   foreach(var shield in c.GetSEMan().GetStatusEffects().OfType<SE_Shield>()){
    float remaining,total;if(!Travel.ActiveShield(shield,out remaining,out total)||total<=0)continue;
    if((shield.m_ttl>0&&shield.GetDuration()>shield.m_ttl)||!shield.m_startEffects.HasEffects())continue;
    var effects=Instances.GetValue(shield) as GameObject[];
    // Re-run only the native visual lifecycle. Do not call Setup, ResetTime,
    // SetLevel or AddStatusEffect: none of the combat values are refreshed.
    if(force||Missing(effects))Trigger.Invoke(shield,null);
   }
   MoveEffects(c);
  }
  internal static IEnumerable<GameObject> Effects(Character c){
   if(!Travel.IsSkeleton(c))yield break;
   foreach(var s in c.GetSEMan().GetStatusEffects().OfType<SE_Shield>()){
    var a=Instances.GetValue(s) as GameObject[];if(a==null)continue;foreach(var go in a)if(go)yield return go;
   }
  }
  internal static void MoveEffects(Character c){
   if(!Travel.IsSkeleton(c)||!c.GetComponent<ZNetView>().IsOwner())return;
   foreach(var fx in Effects(c)){
    var v=fx.GetComponent<ZNetView>();if(!v||!v.IsValid())continue;
    if(!v.IsOwner())v.ClaimOwnership();
    v.GetZDO().SetPosition(fx.transform.position);v.GetZDO().SetRotation(fx.transform.rotation);
   }
  }
  internal static void PinEffects(Character c,List<ZDO> near){foreach(var fx in Effects(c)){var v=fx.GetComponent<ZNetView>();if(v&&v.IsValid()&&!near.Contains(v.GetZDO()))near.Add(v.GetZDO());}}
  internal static string Format(bool known,float remaining,float capacity){
   if(!known)return "?";
   if(!Travel.Finite(remaining)||!Travel.Finite(capacity)||capacity<=0)return "0";
   return Math.Ceiling(Math.Max(0,remaining)).ToString("0",CultureInfo.InvariantCulture)+" / "+Math.Ceiling(Math.Max(0,capacity)).ToString("0",CultureInfo.InvariantCulture);
  }
  static object Field(object obj,string name){return AccessTools.Field(obj.GetType(),name)?.GetValue(obj);}
  internal static void SetText(object component,string value){
   if(component==null)return;PropertyInfo property;var type=component.GetType();
   if(!TextProperties.TryGetValue(type,out property)){property=type.GetProperty("text",BindingFlags.Instance|BindingFlags.Public);TextProperties[type]=property;}
   if(property!=null&&property.CanWrite)property.SetValue(component,value,null);
  }
  static void SetProperty(object obj,string name,object value){var p=obj.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public);if(p!=null&&p.CanWrite)p.SetValue(obj,value,null);}
  internal static bool Managed(Character c){return c&&(c==Player.m_localPlayer||(Travel.IsSkeleton(c)&&c.IsTamed()));}
  internal static bool CanShowSkeleton(Character c) {
   if (!SummonLifecycle.Usable(c) || c.GetHealth() <= 0f)
    return false;
   var view = c.GetComponent<ZNetView>();
   var zdo = view.GetZDO();
   // Character.CustomFixedUpdate hides the model when HasOwner is false.
   var visual = c.GetVisual();
   var scene = ZNetScene.instance;
   return Travel.IsSkeletonPrefab(zdo) && zdo.HasOwner() && visual && visual.activeInHierarchy &&
    scene && scene.FindInstance(zdo) == view;
  }
  internal static bool Visible(Character c){var p=Player.m_localPlayer;if(!p||!SummonLifecycle.Usable(c))return false;if(c==p){float left,max;return PlayerShieldBar.Active(p,out left,out max);}return Managed(c)&&CanShowSkeleton(c)&&Vector3.Distance(c.transform.position,p.transform.position)<=30f;}
  internal static void PrepareLocal(EnemyHud hud){var p=Player.m_localPlayer;if(!p||!Visible(p))return;var table=Huds.GetValue(hud) as IDictionary;bool existed=table!=null&&table.Contains(p);ShowHud.Invoke(hud,new object[]{p,false});if(!existed&&table!=null&&table.Contains(p)){var gui=Field(table[p],"m_gui") as GameObject;if(gui)LocalRoots[gui]=new LocalHud{Hud=hud,Character=p};}}
  internal static void KeepVisible(EnemyHud hud){var table=Huds.GetValue(hud) as IDictionary;if(table==null)return;foreach(DictionaryEntry entry in table){var c=entry.Key as Character;if(Managed(c)&&Visible(c))AccessTools.Field(entry.Value.GetType(),"m_hoverTimer")?.SetValue(entry.Value,0f);}}
  static float NameSize(Component template){var value=template.GetType().GetProperty("fontSize")?.GetValue(template,null);float size=value is float?(float)value:0;return Travel.Finite(size)&&size>0?size:14;}
  internal static string TimeFormat(float seconds){if(!Travel.Finite(seconds)||seconds<=0)return "";double value=Math.Ceiling(seconds);return Math.Floor(value/60).ToString("0",CultureInfo.InvariantCulture)+":"+(value%60).ToString("00",CultureInfo.InvariantCulture);}
  static Row MakeRow(GameObject gui,Component template,string name,Color color){
   var root=UnityEngine.Object.Instantiate(template.gameObject,gui.transform,false);root.name=name;var row=new Row{Root=root,Text=root.GetComponent(template.GetType())};
   // Clone only the name text. Never clone the Health panel or its bar images.
   foreach(var component in root.GetComponentsInChildren<Component>(true)){SetProperty(component,"raycastTarget",false);if(component.GetType().FullName=="UnityEngine.UI.Image")SetProperty(component,"enabled",false);}
   SetProperty(row.Text,"enableAutoSizing",false);SetProperty(row.Text,"color",color);var align=row.Text.GetType().GetProperty("alignment");if(align!=null&&align.CanWrite&&align.PropertyType.IsEnum)align.SetValue(row.Text,Enum.Parse(align.PropertyType,"Center"),null);root.SetActive(true);return row;
  }
  static void Layout(Row row,Component template,float size,float index,float factor){
   if(row==null)return;float font=size*factor;if(row.Font!=font){SetProperty(row.Text,"fontSize",font);row.Font=font;}var from=template.transform as RectTransform;var to=row.Root.transform as RectTransform;if(!from||!to)return;
   to.anchorMin=from.anchorMin;to.anchorMax=from.anchorMax;to.pivot=from.pivot;to.sizeDelta=from.sizeDelta;to.localScale=from.localScale;to.localRotation=from.localRotation;
   var pos=new Vector2(from.anchoredPosition.x,from.anchoredPosition.y+index*(size*1.15f+2f));if(!to.anchoredPosition.Equals(pos))to.anchoredPosition=pos;
  }
  static void UpdateRow(Row row,string text,bool visible=true){if(row==null||!row.Root)return;if(row.Label!=text){SetText(row.Text,text);row.Label=text;}if(row.Root.activeSelf!=visible)row.Root.SetActive(visible);}
  internal static string HealthFormat(Character c){return Math.Ceiling(Math.Max(0,c.GetHealth())).ToString("0",CultureInfo.InvariantCulture)+" / "+Math.Ceiling(Math.Max(0,c.GetMaxHealth())).ToString("0",CultureInfo.InvariantCulture);}
  static void Remember(Vitals view,GameObject root){if(root)view.Hidden.Add(new Native{Root=root,Active=root.activeSelf});}
  static void DestroyView(Vitals view){foreach(var row in new[]{view.Health,view.Shield,view.Timer})if(row?.Root){row.Root.SetActive(false);UnityEngine.Object.Destroy(row.Root);}foreach(var native in view.Hidden)if(native.Root)native.Root.SetActive(native.Active);}
  internal static void Forget(Character c){
   Pending.Remove(c);
   foreach(var entry in Views)
    if(ReferenceEquals(entry.Value.Character,c)&&entry.Key)entry.Key.SetActive(false);
   var hud = EnemyHud.instance;
   var table = hud ? Huds.GetValue(hud) as IDictionary : null;
   if (table != null && table.Contains(c)) {
    var gui = Field(table[c], "m_gui") as GameObject;
    if (gui)
     gui.SetActive(false);
   }
  }
  static readonly HashSet<Character> ActiveActors=new HashSet<Character>();
  static readonly HashSet<GameObject> ActiveRoots=new HashSet<GameObject>();
  static readonly List<object> RemovedKeys=new List<object>();
  internal static void PruneHud(EnemyHud hud){
   var table=Huds.GetValue(hud) as IDictionary;if(table==null)return;var actors=ActiveActors;actors.Clear();foreach(var actor in Character.GetAllCharacters())actors.Add(actor);var roots=ActiveRoots;roots.Clear();var remove=RemovedKeys;remove.Clear();
   foreach(DictionaryEntry entry in table){
    var actor=entry.Key as Character;
    var gui=Field(entry.Value,"m_gui") as GameObject;
    bool managed=gui&&(Views.ContainsKey(gui)||LocalRoots.ContainsKey(gui))||Managed(actor)||SummonLifecycle.IsRetiring(actor);
    if(!actor||!gui||!actors.Contains(actor)||!ReferenceEquals(Field(entry.Value,"m_character"),actor)
       ||(managed&&!SummonLifecycle.Usable(actor))||(Travel.IsKnownSkeleton(actor)&&!CanShowSkeleton(actor))){
     remove.Add(entry.Key);
     if(!ReferenceEquals(actor,null))Pending.Remove(actor);
     continue;
    }
    roots.Add(gui);
   }
   foreach(var key in remove){
    var gui=Field(table[key],"m_gui") as GameObject;
    table.Remove(key);
    if(gui&&!roots.Contains(gui))DestroyHudRoot(gui);
   }
   RemoveUnboundMobHuds(hud,roots);
   // A still-alive actor or an active GUI is not proof that this is its current
   // HUD. Native removal/rebinding can happen before the cache is revisited.
   foreach(var gui in Views.Keys.ToArray()){
    var view=Views[gui];if(!ReferenceEquals(view.Hud,hud))continue;bool bound=gui&&roots.Contains(gui)&&table.Contains(view.Character)&&ReferenceEquals(Field(table[view.Character],"m_gui"),gui);
    if(bound)continue;DestroyView(view);Views.Remove(gui);Labels.Remove(gui);LocalRoots.Remove(gui);
    if(gui&&!roots.Contains(gui)){gui.SetActive(false);UnityEngine.Object.Destroy(gui);}
   }
  }
  static void DestroyHudRoot(GameObject gui){
   if(Views.TryGetValue(gui,out var view)){DestroyView(view);Views.Remove(gui);}
   Labels.Remove(gui);LocalRoots.Remove(gui);
   gui.SetActive(false);UnityEngine.Object.Destroy(gui);
  }
  static void RemoveUnboundMobHuds(EnemyHud hud,HashSet<GameObject> bound){
   var root=hud.m_hudRoot;
   var template=hud.m_baseHud;
   if(!root||!template)return;
   string cloneName=template.name+"(Clone)";
   // A clone can be left under the HUD canvas before it ever reaches the
   // native table or this mod's cache. Only remove unbound native mob clones.
   for(int i=root.transform.childCount-1;i>=0;i--){
    var child=root.transform.GetChild(i);
    var gui=child.gameObject;
    if(gui&&gui!=template&&!bound.Contains(gui)&&gui.name==cloneName
       &&child.Find("Name")&&child.Find("Health"))DestroyHudRoot(gui);
   }
  }
  internal static void UpdateHud(EnemyHud hud){
   PruneHud(hud);
   var table=Huds.GetValue(hud) as IDictionary;if(table==null)return;
   foreach(var dead in LocalRoots.Keys.Where(x=>!x).ToArray())LocalRoots.Remove(dead);
   foreach(var dead in Views.Keys.Where(x=>!x||!Managed(Views[x].Character)).ToArray()){DestroyView(Views[dead]);Views.Remove(dead);Labels.Remove(dead);}
   foreach(DictionaryEntry entry in table){
    var c=entry.Key as Character;if(!Managed(c)||!SummonLifecycle.Usable(c))continue;var gui=Field(entry.Value,"m_gui") as GameObject;if(!gui||!gui.activeInHierarchy)continue;bool self=c==Player.m_localPlayer;
    Vitals view;if(!Views.TryGetValue(gui,out view)){
     var template=Field(entry.Value,"m_name") as Component;if(!template)continue;view=new Vitals{Hud=hud,Character=c,Name=template};Views[gui]=view;
     if(self){for(int i=0;i<gui.transform.childCount;i++)Remember(view,gui.transform.GetChild(i).gameObject);}else{var health=gui.transform.Find("Health");if(health)Remember(view,health.gameObject);}
     if(!self){view.Health=MakeRow(gui,template,"GhostHealthRow",new Color(1f,0.65f,0.6f,1));view.Shield=MakeRow(gui,template,"GhostShieldRow",new Color(0.4f,0.85f,1,1));Labels[gui]=view.Shield.Text;}
     view.Timer=MakeRow(gui,template,"GhostShieldTime",new Color(0.65f,0.9f,1,1));
    }
    foreach(var native in view.Hidden)if(native.Root&&native.Root.activeSelf)native.Root.SetActive(false);
    float size=NameSize(view.Name);Layout(view.Timer,view.Name,size,1,1);Layout(view.Health,view.Name,size,-1,0.85f);Layout(view.Shield,view.Name,size,-2,0.85f);
    float left,max,seconds;bool known=Travel.ShieldInfo(c,out left,out max,out seconds);string clock=TimeFormat(seconds);UpdateRow(view.Timer,clock,clock.Length>0);if(!self){UpdateRow(view.Health,HealthFormat(c));UpdateRow(view.Shield,Format(known,left,max));}
   }
  }
  internal static void ClearHud(){foreach(var view in Views.Values)DestroyView(view);Views.Clear();foreach(var entry in LocalRoots){var data=entry.Value;var table=data.Hud?Huds.GetValue(data.Hud) as IDictionary:null;if(table!=null&&table.Contains(data.Character)&&Field(table[data.Character],"m_gui") as GameObject==entry.Key)table.Remove(data.Character);if(entry.Key)UnityEngine.Object.Destroy(entry.Key);}LocalRoots.Clear();Labels.Clear();TextProperties.Clear();Pending.Clear();ActiveActors.Clear();ActiveRoots.Clear();RemovedKeys.Clear();}
  static bool hudFailed;
  [HarmonyPatch(typeof(EnemyHud),"LateUpdate")]static class HudPatch{
   [HarmonyPrefix]static void Prefix(EnemyHud __instance){if(hudFailed)return;try{PruneHud(__instance);PrepareLocal(__instance);}catch(Exception e){hudFailed=true;Plugin.LogFault(e);}}
   [HarmonyPostfix,HarmonyPriority(Priority.Last)]static void Postfix(EnemyHud __instance){if(hudFailed)return;try{UpdateHud(__instance);}catch(Exception e){hudFailed=true;Plugin.LogFault(e);}}
  }
  [HarmonyPatch(typeof(EnemyHud),"UpdateHuds")]static class HoverPatch{[HarmonyPrefix]static void Prefix(EnemyHud __instance){try{KeepVisible(__instance);}catch(Exception e){Plugin.LogFault(e);}}}
  [HarmonyPatch(typeof(EnemyHud),"ShowHud")]static class CreatePatch {
   [HarmonyPrefix]
   static bool Prefix(Character c) => !Travel.IsKnownSkeleton(c) || CanShowSkeleton(c);
  }
  [HarmonyPatch(typeof(EnemyHud),"TestShow")]static class VisibilityPatch{
   [HarmonyPostfix]static void Postfix(Character c,ref bool __result){
    if (Travel.IsKnownSkeleton(c) && !CanShowSkeleton(c)) __result=false;
    else if(Managed(c))__result=Visible(c);
   }
  }
 }
}
