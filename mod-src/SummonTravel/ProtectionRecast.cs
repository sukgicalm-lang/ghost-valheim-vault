// SPDX-License-Identifier: MIT
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace GhostSummonTravel {
 internal static class ProtectionRecast {
  sealed class Refresh {internal int Frame;}
  static ConditionalWeakTable<SE_Shield,Refresh> Pending=new ConditionalWeakTable<SE_Shield,Refresh>();
  static readonly FieldInfo CharacterField=AccessTools.Field(typeof(StatusEffect),"m_character");
  internal static void Clear(){Pending=new ConditionalWeakTable<SE_Shield,Refresh>();}
  internal static bool Mark(StatusEffect effect){
   var shield=effect as SE_Shield;if(!shield||shield.NameHash()!=Travel.ProtectionShieldHash)return false;
   Pending.GetOrCreateValue(shield).Frame=Time.frameCount;return true;
  }
  internal static bool Finish(SE_Shield shield){
   Refresh refresh;if(!shield||!Pending.TryGetValue(shield,out refresh))return false;Pending.Remove(shield);
   if(refresh.Frame!=Time.frameCount)return false;
   var c=CharacterField.GetValue(shield) as Character;if(!c||c.GetSEMan().GetStatusEffect(shield.NameHash())!=shield)return false;
   if(!Travel.RefillProtection(c.GetSEMan(),shield.NameHash(),true))return false;
   if(Travel.IsSkeleton(c)){ShieldPresentation.RequestRepair(c,true,0.05);Travel.SaveShields(c);ZDOMan.instance.ForceSendZDO(c.GetZDOID());}
   Plugin.LogShieldRecast(c);return true;
  }
  // Both SEMan's hash/RPC path and its direct StatusEffect overload call these
  // virtual methods in this order. Complete only after native capacity/TTL were
  // computed, not on HUD reads, SetLevel alone, or teleport restoration.
  [HarmonyPatch(typeof(StatusEffect),"ResetTime")]static class ResetPatch{
   [HarmonyPostfix]static void Postfix(StatusEffect __instance){try{Mark(__instance);}catch(Exception e){Plugin.LogFault(e);}}
  }
  [HarmonyPatch(typeof(SE_Shield),"SetLevel")]static class LevelPatch{
   [HarmonyPostfix]static void Postfix(SE_Shield __instance){try{Finish(__instance);}catch(Exception e){Plugin.LogFault(e);}}
  }
 }
}
