// SPDX-License-Identifier: MIT
using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace GhostSummonTravel {
 internal static class SummonLifecycle {
  sealed class Retired {}
  static ConditionalWeakTable<Character,Retired> Retiring=new ConditionalWeakTable<Character,Retired>();
  internal static bool IsRetiring(Character c){return !ReferenceEquals(c,null)&&Retiring.TryGetValue(c,out var state);}
  internal static bool Usable(Character c){if(!c||!c.gameObject.activeInHierarchy||c.IsDead()||IsRetiring(c))return false;var view=c.GetComponent<ZNetView>();return view&&view.IsValid();}
  internal static void Retire(Character c){
   if(!Travel.IsSkeleton(c)||IsRetiring(c))return;Retiring.Add(c,new Retired());
   // Native RPC destruction is deferred. Stop retaining its physics, effects
   // and HUD immediately, while still letting native UnSummon destroy it.
   Travel.Forget(c);ShieldPresentation.Forget(c);
  }
  internal static void Clear(){Retiring=new ConditionalWeakTable<Character,Retired>();}
  [HarmonyPatch(typeof(Tameable),"UnSummon")]static class RequestPatch{[HarmonyPrefix]static void Prefix(Tameable __instance){try{Retire(__instance.GetComponent<Character>());}catch(Exception e){Plugin.LogFault(e);}}}
  [HarmonyPatch(typeof(Tameable),"RPC_UnSummon")]static class RpcPatch{[HarmonyPrefix]static void Prefix(Tameable __instance){try{Retire(__instance.GetComponent<Character>());}catch(Exception e){Plugin.LogFault(e);}}}
 }
}
