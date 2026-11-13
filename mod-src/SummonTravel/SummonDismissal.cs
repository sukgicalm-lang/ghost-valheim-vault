// SPDX-License-Identifier: MIT
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace GhostSummonTravel {
 internal static class SummonDismissal {
  const string SummonerKey = "ghost.summontravel.summoner.v1";
  static readonly MethodInfo Unsummon = AccessTools.Method(typeof(Tameable), "UnSummon");

  internal static void RememberSummoner(ZNetView view, Player player) {
   var id = player.GetPlayerID();
   if (id != 0 && view.IsOwner() && view.GetZDO().GetLong(SummonerKey, 0) == 0)
    view.GetZDO().Set(SummonerKey, id);
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

   // Older summons have only the game's follow target and saved character name.
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

  internal static int Dismiss(Player player) {
   // Native RPC handling can change the live character list during this loop.
   var summons = Character.GetAllCharacters().Where(character => CanDismiss(character, player)).ToArray();
   int requested = 0;
   foreach (var summon in summons) {
    if (!CanDismiss(summon, player))
     continue;
    Unsummon.Invoke(summon.GetComponent<Tameable>(), null);
    requested++;
   }
   return requested;
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
