// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GhostSummonTravel {
 internal static class SavedFollowers {
  const string CastKey="ghost.summontravel.cast.v1";
  const string RecallRpc="GhostRecallSavedSkeletonV1",CastRpc="GhostFinishedSkeletonCastV1";
  static readonly int PlayerPrefab=Travel.StableHash("Player");
  static readonly Dictionary<ZDOID,Sighting> Observed=new Dictionary<ZDOID,Sighting>();
  static readonly Dictionary<long,CastRequest> Casts=new Dictionary<long,CastRequest>();
  static readonly Dictionary<long,double> RecallAfter=new Dictionary<long,double>();
  static ZNetScene currentScene;
  static double nextRecall,nextCleanup;

  sealed class Sighting {internal double Seen,Retry;internal long Leader;internal Vector3 Position;}

  sealed class CastRequest {
   internal ZDOID Player, Witness;
   internal string Token;
   internal int Count;
   internal double Expires, Retry;
   internal Queue<ZDOID> Older;
   internal long SpawnedAfter;
  }

  static void CheckScene() {
   if(ReferenceEquals(currentScene,ZNetScene.instance))return;
   Clear();currentScene=ZNetScene.instance;
  }

  internal static void Observe(Character c,Player p) {
   CheckScene();
   var view=c.GetComponent<ZNetView>();
   if(view.IsOwner())SummonDismissal.RememberSummoner(view,p);
   var id=c.GetZDOID();
   if(!Observed.TryGetValue(id,out var sighting))Observed.Add(id,sighting=new Sighting());
   sighting.Seen=Travel.Now;sighting.Leader=p.GetPlayerID();sighting.Position=c.transform.position;
  }

  internal static void MarkCast(ZNetView view,string token)=>view.GetZDO().Set(CastKey,token);

  internal static void CastEnded(Player player,string token,List<ZDOID> spawned,int limit) {
   if(!player||spawned.Count!=limit||limit<=0||limit>64||!ZNet.instance||player!=Player.m_localPlayer)return;
   CheckScene();
   var net=ZNet.instance;
   if(net.IsServer())QueueCast(player.GetZDOID(),player.GetPlayerID(),spawned[0],token,limit);
   else net.GetServerPeer()?.m_rpc.Invoke(CastRpc,spawned[0],token,limit);
  }

  internal static void Register(ZNet net,ZNetPeer peer) {
   peer.m_rpc.Register<ZDOID,Vector3>(RecallRpc,(connection,id,arrival)=>{
    var player=SummonDismissal.AuthenticatedPlayer(net,peer,connection);
    if(player!=null)Recall(net,player,id,arrival);
   });
   peer.m_rpc.Register<ZDOID,string,int>(CastRpc,(connection,witness,token,count)=>{
    var player=SummonDismissal.AuthenticatedPlayer(net,peer,connection);
    if(player!=null)QueueCast(player.m_uid,player.GetLong(ZDOVars.s_playerID,0),witness,token,count);
   });
  }

  static void QueueCast(ZDOID player,long playerId,ZDOID witness,string token,int count) {
   if(playerId==0||count<=0||count>64||!Guid.TryParseExact(token,"N",out _))return;
   CheckScene();
   Casts[playerId]=new CastRequest{Player=player,Witness=witness,Token=token,Count=count,Expires=Travel.Now+16,Retry=Travel.Now+0.75};
  }

  internal static bool IsOwnedFollower(ZDO zdo,long owner,string name,bool unique) {
   if(owner==0||string.IsNullOrEmpty(name)||!Travel.IsSkeletonPrefab(zdo)||!zdo.IsValid()||
      zdo.GetBool(ZDOVars.s_dead,false)||!zdo.GetBool(ZDOVars.s_tamed,false)||zdo.GetInt(ZDOVars.s_maxInstances,0)<=0||
      zdo.GetString(ZDOVars.s_follow)!=name||SummonLifecycle.WorldRemovalQueued(zdo.m_uid))return false;
   var marked=SummonDismissal.Summoner(zdo);
   return marked==owner||(marked==0&&unique);
  }

  internal static bool ValidArrival(Vector3 origin,Vector3 arrival) {
   var offset=arrival-origin;offset.y=0;
   return Travel.Finite(arrival.x)&&Travel.Finite(arrival.y)&&Travel.Finite(arrival.z)&&
    Mathf.Abs(arrival.y-origin.y)<=2f&&offset.sqrMagnitude>=2.25f&&offset.sqrMagnitude<=81f;
  }

  static void Recall(ZNet net,ZDO player,ZDOID id,Vector3 arrival) {
   var scene=ZNetScene.instance;var manager=ZDOMan.instance;
   if(!scene||manager==null||!net.IsServer()||player.GetBool(ZDOVars.s_dead,false)||!ValidArrival(player.GetPosition(),arrival))return;
   var summon=manager.GetZDO(id);
   var playerId=player.GetLong(ZDOVars.s_playerID,0);var name=player.GetString(ZDOVars.s_playerName);
   if(RecallAfter.TryGetValue(playerId,out var after)&&Travel.Now<after)return;
   if(!IsOwnedFollower(summon,playerId,name,SummonDismissal.UniqueName(net,playerId,name))||
      Vector3.Distance(summon.GetPosition(),player.GetPosition())<Travel.RecallDistance)return;
   // A live instance must be moved by its current owner with the normal travel path.
   if(scene.FindInstance(summon)||(summon.HasOwner()&&summon.GetOwner()!=player.GetOwner()&&!summon.IsOwner()))return;
   summon.SetOwner(ZDOMan.GetSessionID());
   summon.SetPosition(arrival);
   summon.Set(ZDOVars.s_bodyVelocity,Vector3.zero);
   summon.SetOwner(player.GetOwner());
   manager.ForceSendZDO(id);
   RecallAfter[playerId]=Travel.Now+0.35;
  }

  static void RecallLost(Player player) {
   var net=ZNet.instance;var manager=ZDOMan.instance;var now=Travel.Now;
   if(!net||manager==null||now<nextRecall||!player||player.IsDead()||player.IsTeleporting()||!player.IsOnGround())return;
   nextRecall=now+0.75;
   var playerId=player.GetPlayerID();var name=player.GetPlayerName();
   bool unique=SummonDismissal.UniqueName(net,playerId,name);
   foreach(var item in Observed.ToArray()){
    var zdo=manager.GetZDO(item.Key);
    var sighting=item.Value;
    if(sighting.Leader!=playerId||(zdo==null&&(net.IsServer()||now-sighting.Seen>300))||
       (zdo!=null&&!IsOwnedFollower(zdo,playerId,name,unique))){Observed.Remove(item.Key);continue;}
    if(now<sighting.Retry||(zdo!=null&&currentScene.FindInstance(zdo))||
       Vector3.Distance(zdo?.GetPosition()??sighting.Position,player.transform.position)<Travel.RecallDistance)continue;
    if(!Travel.FindSavedLanding(player,(item.Key.GetHashCode()&int.MaxValue)%7,out var arrival))return;
    if(net.IsServer())Recall(net,player.GetComponent<ZNetView>().GetZDO(),item.Key,arrival);
    else net.GetServerPeer()?.m_rpc.Invoke(RecallRpc,item.Key,arrival);
    // Enqueue another skeleton on the next pass even while this one replicates.
    sighting.Retry=now+4;
    return;
   }
  }

  internal static bool CanReplace(long spawned,long newest,string token,string current) =>
   spawned>0&&spawned<newest&&token!=current;

  static void ProcessCast(ZNet net,long playerId,CastRequest request,double now) {
   var manager=ZDOMan.instance;
   var player=manager.GetZDO(request.Player);
   var witness=manager.GetZDO(request.Witness);
   if(player==null||player.GetPrefab()!=PlayerPrefab||player.GetLong(ZDOVars.s_playerID,0)!=playerId)return;
   string name=player.GetString(ZDOVars.s_playerName);
   if(request.Older==null){
    if(!IsOwnedFollower(witness,playerId,name,false)||witness.GetString(CastKey)!=request.Token||witness.GetInt(ZDOVars.s_maxInstances,0)!=request.Count){request.Retry=now+0.75;return;}
    bool unique=SummonDismissal.UniqueName(net,playerId,name);
    var records=SummonDismissal.SavedCandidates().Select(manager.GetZDO)
     .Where(z=>IsOwnedFollower(z,playerId,name,unique)).ToArray();
    var current=records.Where(z=>SummonDismissal.Summoner(z)==playerId&&z.GetString(CastKey)==request.Token&&
     z.GetInt(ZDOVars.s_maxInstances,0)==request.Count).ToArray();
    if(current.Length!=request.Count){request.Retry=now+0.75;return;}
    request.SpawnedAfter=current.Min(z=>z.GetLong(ZDOVars.s_spawnTime,0));
    request.Older=new Queue<ZDOID>(records.Where(z=>CanReplace(z.GetLong(ZDOVars.s_spawnTime,0),request.SpawnedAfter,
     z.GetString(CastKey),request.Token)).Select(z=>z.m_uid));
   }
   bool stillUnique=SummonDismissal.UniqueName(net,playerId,name);
   while(request.Older.Count>0){
    var old=manager.GetZDO(request.Older.Dequeue());
    if(!IsOwnedFollower(old,playerId,name,stillUnique)||
       !CanReplace(old.GetLong(ZDOVars.s_spawnTime,0),request.SpawnedAfter,old.GetString(CastKey),request.Token))continue;
    var view=currentScene.FindInstance(old);
    if(view&&Vector3.Distance(old.GetPosition(),player.GetPosition())<Travel.RecallDistance)continue;
    SummonDismissal.DestroySaved(old,view);
    return;
   }
   Casts.Remove(playerId);
  }

  internal static void Tick() {
   CheckScene();
   if(Travel.Faulted||!currentScene||ZDOMan.instance==null)return;
   var player=Player.m_localPlayer;
   if(player&&Observed.Count>0)RecallLost(player);
   var net=ZNet.instance;var now=Travel.Now;
   if(!net||!net.IsServer()||Casts.Count==0||now<nextCleanup)return;
   nextCleanup=now+0.25;
   foreach(var item in Casts.ToArray()){
    if(now>=item.Value.Expires){Casts.Remove(item.Key);continue;}
    if(now>=item.Value.Retry)ProcessCast(net,item.Key,item.Value,now);
   }
  }

  internal static void Clear(){Observed.Clear();Casts.Clear();RecallAfter.Clear();nextRecall=nextCleanup=0;currentScene=null;}
 }
}
