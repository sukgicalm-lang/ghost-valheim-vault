// SPDX-License-Identifier: MIT
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace GhostSummonTravel {
 internal static class IdleSpacing {
  static readonly MethodInfo MoveTo=AccessTools.Method(typeof(BaseAI),"MoveTo");
  static ConditionalWeakTable<Character,GoalState> Goals=new ConditionalWeakTable<Character,GoalState>();
  sealed class GoalState {internal double NextCheck;internal bool Valid,Retreat;internal Vector3 Point;}
  internal static void ValidateApi(){if(MoveTo==null)throw new MissingMethodException("해골 추적: 게임의 이동 API가 맞지 않습니다.");}
  internal static void Clear(){Goals=new ConditionalWeakTable<Character,GoalState>();}
  internal static float HorizontalGap(Character c,Player p){float x=c.transform.position.x-p.transform.position.x,z=c.transform.position.z-p.transform.position.z;return(float)Math.Sqrt(x*x+z*z);}
  internal static float Minimum(Character c,Player p){return c.GetRadius()+p.GetRadius()+1.25f;}
  internal static bool Crowded(Character c,Player p){return Travel.IsSkeleton(c)&&p&&!c.GetComponent<MonsterAI>().HaveTarget()&&HorizontalGap(c,p)<Minimum(c,p);}
  internal static bool Handle(BaseAI ai,GameObject go,float dt){
   var c=ai.GetComponent<Character>();var p=go?go.GetComponent<Player>():null;var monster=ai as MonsterAI;
   if(!Travel.IsSkeleton(c)||!p||!monster||c.IsDead()||p.IsDead()||!c.IsTamed()||!c.GetComponent<ZNetView>().IsOwner()||monster.GetFollowTarget()!=go||monster.HaveTarget())return true;
   if(Travel.Held(c)){ai.StopMoving();return false;}
   float gap=HorizontalGap(c,p),minimum=Minimum(c,p);
   // Native Follow stops when near but never backs away when the player walks
   // into it. Keep a comfortable band and actively leave the inner band.
   if(gap>=minimum&&gap<=Mathf.Max(4f,minimum+2f)){ai.StopMoving();return false;}
   var state=Goals.GetOrCreateValue(c);
   bool retreat=gap<minimum;
   if(Travel.Now>=state.NextCheck||state.Retreat!=retreat){state.NextCheck=Travel.Now+0.5;state.Retreat=retreat;int slot=(c.GetZDOID().GetHashCode()&int.MaxValue)%7;Vector3 point;state.Valid=Travel.FindLanding(p,c,slot,out point,2.9f,retreat);state.Point=point;}
   if(!state.Valid){ai.StopMoving();return false;}
   MoveTo.Invoke(ai,new object[]{dt,state.Point,0.25f,gap>10f});return false;
  }
  [HarmonyPatch(typeof(BaseAI),"Follow")]
  static class FollowPatch{[HarmonyPrefix]static bool Prefix(BaseAI __instance,GameObject go,float dt){return Handle(__instance,go,dt);}}
 }
}
