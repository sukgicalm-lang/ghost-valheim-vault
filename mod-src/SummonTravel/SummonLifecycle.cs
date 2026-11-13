// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace GhostSummonTravel {
 internal static class SummonLifecycle {
  sealed class Retired {}
  static ConditionalWeakTable<Character,Retired> Retiring=new ConditionalWeakTable<Character,Retired>();
  static readonly HashSet<ZDOID> RetiredIds = new HashSet<ZDOID>();
  static readonly Dictionary<ZDOID,double> PendingDismissals = new Dictionary<ZDOID,double>();
  static readonly List<ZDOID> Due = new List<ZDOID>();
  static ZNetScene scene;
  static double nextCheck;

  static void UpdateScene() {
   var current = ZNetScene.instance;
   if (ReferenceEquals(current, scene))
    return;
   Clear();
   scene = current;
  }

  internal static bool IsRetiring(Character c) {
   if (ReferenceEquals(c, null))
    return false;
   UpdateScene();
   if (Retiring.TryGetValue(c, out _))
    return true;
   if (RetiredIds.Count == 0 || !Travel.IsSkeleton(c))
    return false;
   return RetiredIds.Contains(c.GetZDOID());
  }

  internal static bool Usable(Character c){if(!c||!c.gameObject.activeInHierarchy||c.IsDead()||IsRetiring(c))return false;var view=c.GetComponent<ZNetView>();return view&&view.IsValid();}
  internal static void Retire(Character c){
   UpdateScene();
   if(!Travel.IsSkeleton(c)||Retiring.TryGetValue(c,out _))return;
   Retiring.Add(c,new Retired());
   RetiredIds.Add(c.GetZDOID());
   // Native RPC destruction is deferred. Stop retaining its physics, effects
   // and HUD immediately, while still letting native UnSummon destroy it.
   Travel.Forget(c);ShieldPresentation.Forget(c);
  }
  internal static void TrackDismissal(Character c) {
   PendingDismissals[c.GetZDOID()] = Travel.Now + 2;
   Retire(c);
  }

  internal static void RetryDismissals() {
   UpdateScene();
   if (!scene || PendingDismissals.Count == 0 || ZDOMan.instance == null)
    return;
   double now = Travel.Now;
   if (now < nextCheck)
    return;
   nextCheck = now + 1;
   Due.Clear();
   foreach (var pending in PendingDismissals)
    if (pending.Value <= now)
     Due.Add(pending.Key);

   foreach (var id in Due) {
    var zdo = ZDOMan.instance.GetZDO(id);
    if (zdo == null || !Travel.IsSkeletonPrefab(zdo)) {
     PendingDismissals.Remove(id);
     continue;
    }
    PendingDismissals[id] = now + 5;
    var instance = scene.FindInstance(id);
    if (!instance || !zdo.HasOwner())
     continue;
    var c = instance.GetComponent<Character>();
    if (!Travel.IsSkeleton(c))
     continue;
    Retire(c);
    SummonDismissal.RequestNative(c);
   }
  }

  internal static void Clear() {
   Retiring = new ConditionalWeakTable<Character,Retired>();
   RetiredIds.Clear();
   PendingDismissals.Clear();
   Due.Clear();
   nextCheck = 0;
   scene = null;
  }
  [HarmonyPatch(typeof(Tameable),"UnSummon")]static class RequestPatch{[HarmonyPrefix]static void Prefix(Tameable __instance){try{Retire(__instance.GetComponent<Character>());}catch(Exception e){Plugin.LogFault(e);}}}
  [HarmonyPatch(typeof(Tameable),"RPC_UnSummon")]static class RpcPatch{[HarmonyPrefix]static void Prefix(Tameable __instance){try{Retire(__instance.GetComponent<Character>());}catch(Exception e){Plugin.LogFault(e);}}}
 }
}
