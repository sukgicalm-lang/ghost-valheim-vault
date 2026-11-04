// SPDX-License-Identifier: MIT
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace GhostSummonTravel {
 internal static class ProtectionRange {
  internal const float Radius=20f;
  internal static bool Apply(Aoe area){
   if(!area)return false;string name=area.gameObject.name;
   if(name!="staff_shield_aoe"&&name!="staff_shield_aoe(Clone)")return false;
   area.m_radius=Radius;
   // Native non-trigger casts query m_radius in world metres. Trigger-based
   // instances also need their collider enlarged, without scaling the VFX or
   // changing which factions, resources or status effects the native cast uses.
   if(!area.m_useTriggers)return true;
   foreach(var component in area.gameObject.GetComponentsInChildren<Component>(true)){
    if(!component||component.GetType().FullName!="UnityEngine.SphereCollider")continue;
    var type=component.GetType();var trigger=type.GetProperty("isTrigger");var radius=type.GetProperty("radius");
    if(trigger==null||radius==null||!radius.CanWrite||!(bool)trigger.GetValue(component,null))continue;
    var scale=component.transform.lossyScale;float largest=Math.Max(Math.Abs(scale.x),Math.Max(Math.Abs(scale.y),Math.Abs(scale.z)));
    if(largest>0.00001f)radius.SetValue(component,Radius/largest,null);
   }
   return true;
  }
  [HarmonyPatch(typeof(Aoe),"Awake")]static class CreatePatch{
   [HarmonyPrefix]static void Prefix(Aoe __instance){try{Apply(__instance);}catch(Exception e){Plugin.LogFault(e);}}
  }
  [HarmonyPatch(typeof(Aoe),"Setup")]static class CastPatch{
   [HarmonyPrefix]static void Prefix(Aoe __instance){try{Apply(__instance);}catch(Exception e){Plugin.LogFault(e);}}
  }
 }
}
