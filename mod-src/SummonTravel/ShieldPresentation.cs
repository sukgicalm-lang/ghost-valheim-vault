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
  sealed class Row {internal GameObject Root;internal Component Text;internal Component[] Bars;internal Color Color;internal float Value=float.NaN,Max=float.NaN;internal string Label;}
  sealed class Vitals {internal Character Character;internal GameObject NativeHealth;internal bool WasActive;internal Row Health,Shield;}
  static readonly Dictionary<GameObject,Vitals> Views=new Dictionary<GameObject,Vitals>();
  sealed class RepairRequest {internal bool Force;internal double Due;}
  static readonly Dictionary<Character,RepairRequest> Pending=new Dictionary<Character,RepairRequest>();
  internal static void RequestRepair(Character c,bool force,double delay){if(!Travel.IsSkeleton(c))return;RepairRequest request;if(Pending.TryGetValue(c,out request)){request.Force|=force;request.Due=Math.Min(request.Due,Travel.Now+delay);}else Pending[c]=new RepairRequest{Force=force,Due=Travel.Now+delay};}
  internal static void ProcessRepairs(){Character selected=null;RepairRequest request=null;foreach(var entry in Pending){if(!entry.Key||entry.Value.Due<=Travel.Now){selected=entry.Key;request=entry.Value;break;}}if(request==null)return;Pending.Remove(selected);if(selected)Repair(selected,request.Force);}
  internal static void ValidateApi(){if(Instances==null||Trigger==null||Huds==null||ShowHud==null)throw new MissingMemberException("해골 방어막: 게임의 이펙트·체력 표시 API가 맞지 않습니다.");}
  internal static bool Missing(GameObject[] effects){return effects==null||effects.Length==0||effects.Any(x=>!x||!x.activeInHierarchy);}
  internal static void Repair(Character c,bool force){
   if(!Travel.IsSkeleton(c)||c.IsDead()||!c.GetComponent<ZNetView>().IsOwner())return;
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
   if(!known)return "방어막 확인 중";
   if(!Travel.Finite(remaining)||!Travel.Finite(capacity)||capacity<=0)return "방어막 0";
   return "방어막 "+Math.Ceiling(Math.Max(0,remaining)).ToString("0",CultureInfo.InvariantCulture)+" / "+Math.Ceiling(Math.Max(0,capacity)).ToString("0",CultureInfo.InvariantCulture);
  }
  static object Field(object obj,string name){return AccessTools.Field(obj.GetType(),name)?.GetValue(obj);}
  internal static void SetText(object component,string value){
   if(component==null)return;PropertyInfo property;var type=component.GetType();
   if(!TextProperties.TryGetValue(type,out property)){property=type.GetProperty("text",BindingFlags.Instance|BindingFlags.Public);TextProperties[type]=property;}
   if(property!=null&&property.CanWrite)property.SetValue(component,value,null);
  }
  static void SetProperty(object obj,string name,object value){var p=obj.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public);if(p!=null&&p.CanWrite)p.SetValue(obj,value,null);}
  internal static bool Managed(Character c){return c&&(c==Player.m_localPlayer||(Travel.IsSkeleton(c)&&c.IsTamed()));}
  internal static bool Visible(Character c){var p=Player.m_localPlayer;if(!p||!c||c.IsDead())return false;if(c==p){float left,max;return PlayerShieldBar.Active(p,out left,out max);}return Managed(c)&&Vector3.Distance(c.transform.position,p.transform.position)<=30f;}
  internal static void PrepareLocal(EnemyHud hud){var p=Player.m_localPlayer;if(p&&Visible(p))ShowHud.Invoke(hud,new object[]{p,false});}
  internal static void KeepVisible(EnemyHud hud){var table=Huds.GetValue(hud) as IDictionary;if(table==null)return;foreach(DictionaryEntry entry in table){var c=entry.Key as Character;if(Managed(c)&&Visible(c))AccessTools.Field(entry.Value.GetType(),"m_hoverTimer")?.SetValue(entry.Value,0f);}}
  static void Rect(Component text,float y){var r=text.transform as RectTransform;if(!r)return;r.anchorMin=r.anchorMax=new Vector2(0.5f,0.5f);r.pivot=new Vector2(0.5f,0.5f);r.anchoredPosition=new Vector2(0,y);r.sizeDelta=new Vector2(160,18);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;}
  static void Paint(Row row){if(!row.Root)return;foreach(var c in row.Root.GetComponentsInChildren<Component>(true))if(c&&c.GetType().FullName=="UnityEngine.UI.Image"){SetProperty(c,"color",new Color(0.07f,0.07f,0.09f,0.85f));SetProperty(c,"raycastTarget",false);}foreach(var bar in row.Bars)foreach(var c in bar.gameObject.GetComponentsInChildren<Component>(true))if(c&&c.GetType().FullName=="UnityEngine.UI.Image")SetProperty(c,"color",row.Color);}
  static Row MakeRow(GameObject gui,object data,Component template,Transform health,string name,float y,Color color){
   var row=new Row{Color=color};var bars=new List<Component>();
   if(health){row.Root=UnityEngine.Object.Instantiate(health.gameObject,gui.transform,false);row.Root.name=name;var from=health as RectTransform;var to=row.Root.transform as RectTransform;
    if(to){to.anchorMin=to.anchorMax=new Vector2(0.5f,0.5f);to.pivot=new Vector2(0.5f,0.5f);to.anchoredPosition=new Vector2(from?from.anchoredPosition.x:0,(from?from.anchoredPosition.y:0)+y);to.sizeDelta=new Vector2(160,18);to.localScale=Vector3.one;to.localRotation=Quaternion.identity;}
    foreach(var key in new[]{"m_healthFast","m_healthFastFriendly","m_healthSlow"}){var original=Field(data,key) as Component;var copy=from&&to?PlayerShieldBar.CopyReference(original,from,to):null;if(copy){bars.Add(copy);copy.gameObject.SetActive(true);PlayerShieldBar.SetNumber(copy,"SetWidth",160);}}
    foreach(var component in row.Root.GetComponentsInChildren<Component>(true))if(component&&component.GetType().GetProperty("text")!=null)component.gameObject.SetActive(false);
    var label=UnityEngine.Object.Instantiate(template.gameObject,row.Root.transform,false);label.name=name+"Text";row.Text=label.GetComponent(template.GetType());Rect(row.Text,0);
   }else{row.Root=UnityEngine.Object.Instantiate(template.gameObject,gui.transform,false);row.Root.name=name;row.Text=row.Root.GetComponent(template.GetType());Rect(row.Text,y);}
   row.Bars=bars.ToArray();SetProperty(row.Text,"fontSize",12f);SetProperty(row.Text,"enableAutoSizing",true);SetProperty(row.Text,"fontSizeMin",9f);SetProperty(row.Text,"raycastTarget",false);SetProperty(row.Text,"color",new Color(1,1,1,1));
   var align=row.Text.GetType().GetProperty("alignment");if(align!=null&&align.CanWrite&&align.PropertyType.IsEnum)align.SetValue(row.Text,Enum.Parse(align.PropertyType,"Center"),null);
   row.Text.gameObject.SetActive(true);row.Root.SetActive(true);Paint(row);return row;
  }
  static void UpdateRow(Row row,float value,float max,string text){max=Math.Max(1,max);value=Math.Max(0,value);if(row.Max!=max){foreach(var bar in row.Bars)PlayerShieldBar.SetNumber(bar,"SetMaxValue",max);row.Max=max;row.Value=float.NaN;}if(row.Value!=value){foreach(var bar in row.Bars)PlayerShieldBar.SetNumber(bar,"SetValue",value);row.Value=value;}if(row.Label!=text){SetText(row.Text,text);row.Label=text;}}
  internal static string HealthFormat(Character c){return "HP "+Math.Ceiling(Math.Max(0,c.GetHealth())).ToString("0",CultureInfo.InvariantCulture)+" / "+Math.Ceiling(Math.Max(0,c.GetMaxHealth())).ToString("0",CultureInfo.InvariantCulture);}
  static void DestroyView(Vitals view){if(view.Health?.Root)UnityEngine.Object.Destroy(view.Health.Root);if(view.Shield?.Root)UnityEngine.Object.Destroy(view.Shield.Root);if(view.NativeHealth)view.NativeHealth.SetActive(view.WasActive);}
  internal static void UpdateHud(EnemyHud hud){
   var table=Huds.GetValue(hud) as IDictionary;if(table==null)return;
   foreach(var dead in Views.Keys.Where(x=>!x||!Managed(Views[x].Character)).ToArray()){DestroyView(Views[dead]);Views.Remove(dead);Labels.Remove(dead);}
   foreach(DictionaryEntry entry in table){
    var c=entry.Key as Character;if(!Managed(c))continue;var gui=Field(entry.Value,"m_gui") as GameObject;if(!gui||!gui.activeInHierarchy)continue;
    Vitals view;if(!Views.TryGetValue(gui,out view)){
     var template=Field(entry.Value,"m_name") as Component;if(!template)continue;var health=gui.transform.Find("Health");view=new Vitals{Character=c,NativeHealth=health?health.gameObject:null,WasActive=health&&health.gameObject.activeSelf};Views[gui]=view;
     view.Health=MakeRow(gui,entry.Value,template,health,"GhostHealthRow",0,new Color(0.9f,0.15f,0.15f,1));view.Shield=MakeRow(gui,entry.Value,template,health,"GhostShieldRow",-22,new Color(0.15f,0.65f,1,1));Labels[gui]=view.Shield.Text;
    }
    if(view.NativeHealth)view.NativeHealth.SetActive(false);
    UpdateRow(view.Health,c.GetHealth(),c.GetMaxHealth(),HealthFormat(c));float left,max;bool known=Travel.ShieldAmounts(c,out left,out max);UpdateRow(view.Shield,left,max,Format(known,left,max));
   }
  }
  internal static void ClearHud(){foreach(var view in Views.Values)DestroyView(view);Views.Clear();Labels.Clear();TextProperties.Clear();Pending.Clear();}
  static bool hudFailed;
  [HarmonyPatch(typeof(EnemyHud),"LateUpdate")]static class HudPatch{
   [HarmonyPrefix]static void Prefix(EnemyHud __instance){if(hudFailed)return;try{PrepareLocal(__instance);}catch(Exception e){hudFailed=true;Plugin.LogFault(e);}}
   [HarmonyPostfix,HarmonyPriority(Priority.Last)]static void Postfix(EnemyHud __instance){if(hudFailed)return;try{UpdateHud(__instance);}catch(Exception e){hudFailed=true;Plugin.LogFault(e);}}
  }
  [HarmonyPatch(typeof(EnemyHud),"UpdateHuds")]static class HoverPatch{[HarmonyPrefix]static void Prefix(EnemyHud __instance){try{KeepVisible(__instance);}catch(Exception e){Plugin.LogFault(e);}}}
  [HarmonyPatch(typeof(EnemyHud),"TestShow")]static class VisibilityPatch{[HarmonyPostfix]static void Postfix(Character c,ref bool __result){if(Managed(c))__result=Visible(c);}}
 }
}
