// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace GhostSummonTravel {
 internal static class GhostFollow {
  sealed class Phase {
   internal Player Leader;
   internal Rigidbody Body;
   internal Vector3 Start, Destination;
   internal bool Kinematic, Gravity, Collisions, VisualWasVisible;
   internal GameObject Visual;
   internal float Deadline;
  }

  readonly struct Pair {
   internal readonly Collider Skeleton, Player;
   internal Pair(Collider skeleton, Collider player) { Skeleton=skeleton; Player=player; }
  }

  static readonly Dictionary<Character,Phase> Phases=new Dictionary<Character,Phase>();
  static readonly Dictionary<Character,Pair> Pairs=new Dictionary<Character,Pair>();
  static readonly List<Character> Expired=new List<Character>();
  static float nextPairs;
  static int solidMask;

  internal static bool IsPhasing(Character c)=>c&&Phases.Count!=0&&Phases.ContainsKey(c);

  internal static bool StartPhase(Character c,Player p,Vector3 destination) {
   if(Phases.Count!=0||!CanPhase(c,p))return false;
   var body=Travel.GetBody(c);
   if(!body)return false;
   var visual=c.GetVisual();
   var phase=new Phase{Leader=p,Body=body,Start=body.position,Destination=destination,
    Kinematic=body.isKinematic,Gravity=body.useGravity,Collisions=body.detectCollisions,
    Deadline=Time.unscaledTime+2.25f,Visual=visual,VisualWasVisible=visual&&visual.activeSelf};
   Phases.Add(c,phase);
   if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
   body.useGravity=false;
   body.isKinematic=true;
   body.detectCollisions=false;
   if(phase.VisualWasVisible)visual.SetActive(false);
   c.SetMoveDir(Vector3.zero);
   return true;
  }

  static bool CanPhase(Character c,Player p)=>p&&!p.IsDead()&&!p.IsTeleporting()&&Travel.FollowsOwner(c,p)&&
   c.GetComponent<ZNetView>().IsOwner()&&!c.GetComponent<MonsterAI>().HaveTarget()&&!Travel.Held(c);

  internal static Vector3 Step(Vector3 from,Vector3 to,float dt)=>Vector3.MoveTowards(from,to,42f*Mathf.Clamp(dt,0,0.05f));

  internal static void Advance(Character c,float dt) {
   if(!IsPhasing(c)||!Phases.TryGetValue(c,out var phase))return;
   if(!phase.Body||!CanPhase(c,phase.Leader)||Time.unscaledTime>=phase.Deadline||
      Vector3.Distance(phase.Destination,phase.Leader.transform.position)>11f){CancelPhase(c);return;}
   var next=Step(phase.Body.position,phase.Destination,dt);
   if((next-phase.Destination).sqrMagnitude>0.01f){phase.Body.MovePosition(next);return;}
   if(phase.Leader.IsOnGround()&&ZoneSystem.instance&&ZoneSystem.instance.IsZoneLoaded(phase.Destination)&&
      ZoneSystem.instance.FindFloor(phase.Destination,out var floor)&&Mathf.Abs(floor-phase.Destination.y)<0.5f&&IsClear(c,phase.Destination)){
    phase.Body.position=phase.Destination;
    Travel.ResetFall(c,phase.Destination.y);
    EndPhase(c,phase);
   }else CancelPhase(c);
  }

  static bool IsClear(Character c,Vector3 position) {
   var radius=Mathf.Max(0.3f,c.GetRadius());
   var lower=position+Vector3.up*(radius+0.1f);
   var upper=position+Vector3.up*Mathf.Max(radius+0.1f,c.GetHeight()-radius);
   if(solidMask==0)solidMask=LayerMask.GetMask("Default","static_solid","Default_small","piece","terrain","vehicle");
   return !Physics.CheckCapsule(lower,upper,radius,solidMask,QueryTriggerInteraction.Ignore);
  }

  internal static void CancelPhase(Character c) {
   if(!ReferenceEquals(c,null)&&Phases.TryGetValue(c,out var phase)){
    if(c&&phase.Body&&c.GetComponent<ZNetView>().IsOwner()&&!IsClear(c,phase.Body.position)&&
       (phase.Body.position-phase.Start).sqrMagnitude>0.01f){
     phase.Body.position=phase.Start;
     Travel.ResetFall(c,phase.Start.y);
    }
    EndPhase(c,phase);
   }
  }

  static void EndPhase(Character c,Phase phase) {
   Phases.Remove(c);
   if(phase.Visual&&phase.VisualWasVisible)phase.Visual.SetActive(true);
   if(!phase.Body)return;
   phase.Body.detectCollisions=phase.Collisions;
   phase.Body.useGravity=phase.Gravity;
   phase.Body.isKinematic=phase.Kinematic;
   if(Pairs.TryGetValue(c,out var pair)&&pair.Skeleton&&pair.Player)
    Physics.IgnoreCollision(pair.Skeleton,pair.Player);
  }

  static void RestorePair(Character c) {
   if(!Pairs.TryGetValue(c,out var pair))return;
   if(pair.Skeleton&&pair.Player)Physics.IgnoreCollision(pair.Skeleton,pair.Player,false);
   Pairs.Remove(c);
  }

  static void IgnorePlayer(Character c,Player p) {
   var skeleton=c.GetCollider();var player=p.GetCollider();
   if(!skeleton||!player||Pairs.ContainsKey(c)||Physics.GetIgnoreCollision(skeleton,player))return;
   Physics.IgnoreCollision(skeleton,player);
   Pairs.Add(c,new Pair(skeleton,player));
  }

  internal static void Tick() {
   if(Time.unscaledTime<nextPairs)return;
   nextPairs=Time.unscaledTime+0.25f;
   var p=Player.m_localPlayer;
   Expired.Clear();
   foreach(var pair in Pairs)
    if(!p||p.IsDead()||!SummonDismissal.CanDismiss(pair.Key,p)||!pair.Value.Player||pair.Value.Player!=p.GetCollider())Expired.Add(pair.Key);
   foreach(var c in Expired)RestorePair(c);
   if(!p||p.IsDead()||p.IsTeleporting())return;
   foreach(var c in Character.GetAllCharacters())
    if(Travel.IsSkeleton(c)&&SummonDismissal.CanDismiss(c,p))IgnorePlayer(c,p);
  }

  internal static void Forget(Character c){CancelPhase(c);RestorePair(c);}

  internal static void Clear() {
   foreach(var c in new List<Character>(Phases.Keys))CancelPhase(c);
   foreach(var c in new List<Character>(Pairs.Keys))RestorePair(c);
   nextPairs=0;
  }

  // Leave model and hitboxes alone in normal play. The owner's view hides the
  // skeleton mesh during the short passage without changing network visibility.
  [HarmonyPatch(typeof(Character),"SetVisible")]
  static class PhaseVisual {
   [HarmonyPrefix]static void Prefix(Character __instance,ref bool __0) {
    if(IsPhasing(__instance))__0=false;
   }
  }
 }
}
