// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace GhostSummonTravel {
 internal static class SummonDismissal {
  const string SummonerKey = "ghost.summontravel.summoner.v1";
  const string DismissRpc = "GhostDismissSummonsV1";
  static readonly int SummonerHash = Travel.StableHash(SummonerKey);
  static readonly MethodInfo Unsummon = AccessTools.Method(typeof(Tameable), "UnSummon");

  internal static void RememberSummoner(ZNetView view, Player player) {
   var id = player.GetPlayerID();
   if (id != 0 && view.IsOwner() && view.GetZDO().GetLong(SummonerKey, 0) == 0)
    view.GetZDO().Set(SummonerKey, id);
  }

  internal static long Summoner(ZDO zdo) => zdo.GetLong(SummonerKey,0);

  internal static bool UniqueName(ZNet net,long playerId,string name) => !string.IsNullOrEmpty(name) &&
   !net.GetAllCharacterZDOS().Any(other => other != null && other.GetLong(ZDOVars.s_playerID,0) != playerId && other.GetString(ZDOVars.s_playerName) == name);

  internal static bool CanIdentifyFollower(ZDO zdo,Player player) {
   var owner=Summoner(zdo);
   if(owner!=0)return owner==player.GetPlayerID();
   var name=player.GetPlayerName();
   return zdo.GetInt(ZDOVars.s_maxInstances,0)>0 && !string.IsNullOrEmpty(name) &&
    zdo.GetString(ZDOVars.s_follow)==name && !Player.GetAllPlayers().Any(other=>other&&other!=player&&other.GetPlayerName()==name);
  }

  internal static HashSet<ZDOID> SavedCandidates() {
   var candidates=new HashSet<ZDOID>(ZDOExtraData.GetAllZDOIDsWithHash(ZDOExtraData.Type.Long,SummonerHash));
   candidates.UnionWith(ZDOExtraData.GetAllZDOIDsWithHash(ZDOExtraData.Type.String,ZDOVars.s_follow));
   return candidates;
  }

  internal static bool CanDismiss(Character character, Player player) {
   if (!player || player != Player.m_localPlayer || player.IsDead() || player.IsTeleporting())
    return false;
   if (!Travel.IsSkeleton(character) || !SummonLifecycle.Usable(character) || !character.IsTamed())
    return false;

   var zdo = character.GetComponent<ZNetView>().GetZDO();
   var summoner = zdo.GetLong(SummonerKey, 0);
   if (summoner != 0)
    return summoner == player.GetPlayerID();
   if (zdo.GetInt(ZDOVars.s_maxInstances, 0) <= 0)
    return false;
   var target = character.GetComponent<MonsterAI>().GetFollowTarget();
   if (target)
    return target == player.gameObject;
   var name = player.GetPlayerName();
   return !string.IsNullOrEmpty(name) && zdo.GetString(ZDOVars.s_follow) == name &&
    !Player.GetAllPlayers().Any(other => other && other != player && other.GetPlayerName() == name);
  }

  internal static bool HasSummons(Player player) =>
   Character.GetAllCharacters().Any(character => CanDismiss(character, player));

  // A host can clear saved summons even when every relevant region is unloaded.
  internal static bool CanClearWorld(Player player) =>
   player && !player.IsDead() && !player.IsTeleporting() && ZNet.instance &&
    (ZNet.instance.IsServer() || ZNet.instance.GetServerPeer() != null);

  internal static int Dismiss(Player player) => Dismiss(player, out _);

  internal static int Dismiss(Player player, out int cancelled) {
   cancelled = 0;
   if (!player || player != Player.m_localPlayer || player.IsDead() || player.IsTeleporting())
    return 0;
   cancelled = SpawnRoster.CancelPending(player);
   var net = ZNet.instance;
   if (net && net.IsServer()) {
    var playerView = player.GetComponent<ZNetView>();
    if (playerView && playerView.IsValid() && playerView.IsOwner())
     return DestroyWorldSummons(net, player.GetPlayerID(), player.GetPlayerName(), player.gameObject);
   }
   var server = net ? net.GetServerPeer() : null;
   // Keep the native route for loaded summons, including on older servers.
   var summons = Character.GetAllCharacters().Where(character => CanDismiss(character, player)).ToArray();
   int requested = 0;
   foreach (var summon in summons) {
    if (!CanDismiss(summon, player))
     continue;
    SummonLifecycle.TrackDismissal(summon);
    RequestNative(summon);
    requested++;
   }
   server?.m_rpc.Invoke(DismissRpc);
   return requested;
  }

  internal static void RequestNative(Character summon) =>
   Unsummon.Invoke(summon.GetComponent<Tameable>(), null);

  static void OnDismissRequest(ZNet net, ZNetPeer peer, ZRpc connection) {
   var playerZdo=AuthenticatedPlayer(net,peer,connection);
   if(playerZdo==null)return;
   var player=ZNetScene.instance ? ZNetScene.instance.FindInstance(peer.m_characterID) : null;
   DestroyWorldSummons(net,playerZdo.GetLong(ZDOVars.s_playerID,0),playerZdo.GetString(ZDOVars.s_playerName),player);
  }

  internal static ZDO AuthenticatedPlayer(ZNet net,ZNetPeer peer,ZRpc connection) {
   if (!net || !net.IsServer() || ZDOMan.instance == null || peer.m_uid == 0 ||
       !ReferenceEquals(peer.m_rpc, connection) || !ReferenceEquals(net.GetPeer(peer.m_uid), peer))
    return null;
   var playerZdo = ZDOMan.instance.GetZDO(peer.m_characterID);
   if (playerZdo == null || playerZdo.GetOwner() != peer.m_uid ||
       playerZdo.GetPrefab() != Travel.StableHash("Player"))
    return null;
   return playerZdo;
  }

  internal static int DestroyWorldSummons(ZNet net, long playerId, string name, UnityEngine.GameObject player) {
   var manager = ZDOMan.instance;
   var scene = ZNetScene.instance;
   if (!net || !net.IsServer() || playerId == 0 || manager == null || !scene)
    return 0;

   // This runs on a dismissal request, so records outside the loaded scene
   // are included without scanning the world's objects every frame.
   var candidates = SavedCandidates();
   foreach (var character in Character.GetAllCharacters())
    if (Travel.IsSkeleton(character))
     candidates.Add(character.GetZDOID());
   bool uniqueName = UniqueName(net,playerId,name);
   int removed = 0;
   foreach (var id in candidates) {
    var zdo = manager.GetZDO(id);
    if (!Travel.IsSkeletonPrefab(zdo) || !zdo.IsValid())
     continue;
    var view = scene.FindInstance(zdo);
    var character = view ? view.GetComponent<Character>() : null;
    var summoner = zdo.GetLong(SummonerKey, 0);
    if (summoner == 0) {
     if (zdo.GetInt(ZDOVars.s_maxInstances, 0) <= 0 ||
         !(character ? character.IsTamed() : zdo.GetBool(ZDOVars.s_tamed, false)))
      continue;
     var target = character ? character.GetComponent<MonsterAI>()?.GetFollowTarget() : null;
     if (target ? target != player : !uniqueName || zdo.GetString(ZDOVars.s_follow) != name)
      continue;
    } else if (summoner != playerId) {
     continue;
    }

    if(DestroySaved(zdo,view))removed++;
   }
   if (removed > 0)
    Plugin.LogDismissal(removed);
   return removed;
  }

  internal static bool DestroySaved(ZDO zdo,ZNetView view) {
   if(SummonLifecycle.WorldRemovalQueued(zdo.m_uid))return false;
   var id=zdo.m_uid;
   if(view)SummonLifecycle.Retire(view.GetComponent<Character>());
   zdo.SetOwner(ZDOMan.GetSessionID());
   if(view)ZNetScene.instance.Destroy(view.gameObject);
   else ZDOMan.instance.DestroyZDO(zdo);
   SummonLifecycle.MarkWorldRemoval(id);
   return true;
  }

  [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
  static class RegisterDismissRequest {
   [HarmonyPostfix]
   static void Postfix(ZNet __instance, ZNetPeer peer) {
    if (__instance.IsServer()) {
     peer.m_rpc.Register(DismissRpc, connection => OnDismissRequest(__instance, peer, connection));
     SavedFollowers.Register(__instance,peer);
    }
   }
  }

  [HarmonyPatch(typeof(Tameable), "RPC_Command")]
  static class RememberBeforeStay {
   [HarmonyPrefix]
   static void Prefix(Tameable __instance) {
    var character = __instance.GetComponent<Character>();
    if (!Travel.IsSkeleton(character) || !SummonLifecycle.Usable(character) || !character.IsTamed())
     return;
    var view = character.GetComponent<ZNetView>();
    if (!view.IsOwner() || view.GetZDO().GetInt(ZDOVars.s_maxInstances, 0) <= 0)
     return;
    var target = character.GetComponent<MonsterAI>().GetFollowTarget();
    var player = target ? target.GetComponent<Player>() : null;
    if (player)
     RememberSummoner(view, player);
   }
  }
 }
}
