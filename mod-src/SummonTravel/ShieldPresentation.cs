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
  internal static void ValidateApi(){if(Instances==null||Trigger==null||Huds==null)throw new MissingMemberException("해골 방어막: 게임의 이펙트·체력 표시 API가 맞지 않습니다.");}
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
    if(Travel.Held(c))ZDOMan.instance.ForceSendZDO(v.GetZDO().m_uid);
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
  internal static void UpdateHud(EnemyHud hud){
   var table=Huds.GetValue(hud) as IDictionary;if(table==null)return;
   foreach(var dead in Labels.Keys.Where(x=>!x).ToArray())Labels.Remove(dead);
   foreach(DictionaryEntry entry in table){
    var c=entry.Key as Character;if(!Travel.IsSkeleton(c))continue;
    var gui=Field(entry.Value,"m_gui") as GameObject;if(!gui||!gui.activeInHierarchy)continue;
    Component label;if(!Labels.TryGetValue(gui,out label)||!label){
     var template=Field(entry.Value,"m_name") as Component;if(!template)continue;
     var parent=gui.transform.Find("Health");if(!parent)parent=gui.transform;
     var clone=UnityEngine.Object.Instantiate(template.gameObject,parent,false);clone.name="GhostShieldRemaining";
     label=clone.GetComponent(template.GetType());if(!label){UnityEngine.Object.Destroy(clone);continue;}
     var rect=clone.transform as RectTransform;
     if(rect){rect.anchorMin=rect.anchorMax=new Vector2(1,0.5f);rect.pivot=new Vector2(0,0.5f);rect.anchoredPosition=new Vector2(6,0);rect.sizeDelta=new Vector2(190,26);rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;}
     SetProperty(label,"fontSize",14f);SetProperty(label,"enableAutoSizing",false);
     var align=label.GetType().GetProperty("alignment");if(align!=null&&align.CanWrite&&align.PropertyType.IsEnum)align.SetValue(label,Enum.Parse(align.PropertyType,"Left"),null);
     SetProperty(label,"raycastTarget",false);clone.SetActive(true);Labels[gui]=label;
    }
    float remaining,capacity;bool known=Travel.ShieldAmounts(c,out remaining,out capacity);
    SetText(label,Format(known,remaining,capacity));SetProperty(label,"color",known&&capacity>0?new Color(0.45f,0.85f,1f,1f):new Color(0.75f,0.8f,0.85f,1f));
   }
  }
  internal static void ClearHud(){foreach(var label in Labels.Values)if(label)UnityEngine.Object.Destroy(label.gameObject);Labels.Clear();TextProperties.Clear();}
  [HarmonyPatch(typeof(EnemyHud),"LateUpdate")]static class HudPatch{[HarmonyPostfix,HarmonyPriority(Priority.Last)]static void Postfix(EnemyHud __instance){UpdateHud(__instance);}}
 }
}
