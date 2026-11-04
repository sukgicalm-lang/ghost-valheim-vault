// SPDX-License-Identifier: MIT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace GhostSummonTravel {
 internal static class SpawnRoster {
  static readonly FieldInfo Seed=AccessTools.Field(typeof(Humanoid),"m_seed");
  sealed class Cast {internal SpawnAbility Ability;internal Player Owner;internal int Remaining;internal double Started;internal bool Done;}
  sealed class Equipment {internal string Role;}
  static ConditionalWeakTable<SpawnAbility,Cast> Plans=new ConditionalWeakTable<SpawnAbility,Cast>();
  static ConditionalWeakTable<Humanoid,Equipment> EquipmentRoles=new ConditionalWeakTable<Humanoid,Equipment>();
  static readonly List<Cast> Pending=new List<Cast>();static Cast Active;
  internal static void ValidateApi(){if(Seed==null)throw new MissingFieldException("해골 일괄 소환: 게임의 장비 선택 저장값 구조가 맞지 않습니다.");}
  static bool SkeletonPrefab(GameObject go){return go&&(go.name=="Skeleton_Friendly"||go.name=="Skeleton_Friendly(Clone)");}
  internal static int Limit(SpawnAbility ability,Player owner,ItemDrop.ItemData item){
   if(!owner||!owner.GetSkills()||ability.m_levelUpSettings==null)return 0;
   for(int i=ability.m_levelUpSettings.Count-1;i>=0;i--){var setting=ability.m_levelUpSettings[i];if(owner.GetSkillLevel(setting.m_skill)>=setting.m_skillLevel)return Math.Max(0,ability.m_setMaxInstancesFromWeaponLevel?(item==null?0:item.m_quality):setting.m_maxSpawns);}
   return 0;
  }
  static List<Character> Followers(Player owner){return Character.GetAllCharacters().Where(c=>Travel.Follows(c,owner)).ToList();}
  internal static int Prepare(SpawnAbility ability,Character owner,ItemDrop.ItemData item){
   var player=owner as Player;if(!ability||!player||player!=Player.m_localPlayer||player.IsDead()||ability.m_spawnOnAwake||ability.m_spawnPrefab==null||ability.m_spawnPrefab.Length==0||!ability.m_spawnPrefab.All(SkeletonPrefab))return -1;
   var view=player.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner())return -1;int limit=Limit(ability,player,item);if(limit<=0)return -1;
   Pending.RemoveAll(c=>c.Done||!c.Ability||Travel.Now-c.Started>60);int reserved=Pending.Where(c=>c.Owner==player).Sum(c=>c.Remaining);int missing=Math.Max(0,limit-Followers(player).Count-reserved);
   Plans.Remove(ability);var plan=new Cast{Ability=ability,Owner=player,Remaining=missing,Started=Travel.Now};Plans.Add(ability,plan);Pending.Add(plan);
   // Keep the original coroutine, costs, native skill scaling, command RPC,
   // effects and limit enforcement. Change only this cast's batch size.
   ability.m_minToSpawn=ability.m_maxToSpawn=missing;ability.m_spawnDelay=Math.Max(ability.m_spawnDelay,0.06f);ability.m_circleSpawn=true;ability.m_spawnRadius=Math.Max(ability.m_spawnRadius,3f);return missing;
  }
  internal static IEnumerator Wrap(SpawnAbility ability,IEnumerator original){Cast plan;return Plans.TryGetValue(ability,out plan)?Run(plan,original):original;}
  static IEnumerator Run(Cast plan,IEnumerator original){
   try{while(true){bool more;object value=null;var before=Active;try{Active=plan;more=original.MoveNext();if(more)value=original.Current;}finally{Active=before;}if(!more)yield break;yield return value;}}
   finally{plan.Done=true;Pending.Remove(plan);Plans.Remove(plan.Ability);(original as IDisposable)?.Dispose();}
  }
  static ItemDrop.ItemData Data(GameObject prefab){return prefab?prefab.GetComponent<ItemDrop>()?.m_itemData:null;}
  internal static string Role(IEnumerable<ItemDrop.ItemData> items){
   var weapons=new HashSet<string>(StringComparer.Ordinal);bool shield=false,oneHand=false,twoHand=false;
   foreach(var item in items){if(item==null)continue;var type=item.m_shared.m_itemType;if(type==ItemDrop.ItemData.ItemType.Shield)shield=true;if(!item.IsWeapon())continue;weapons.Add(item.m_shared.m_skillType.ToString());oneHand|=type==ItemDrop.ItemData.ItemType.OneHandedWeapon;twoHand|=type==ItemDrop.ItemData.ItemType.Bow||type==ItemDrop.ItemData.ItemType.TwoHandedWeapon||type==ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;}
   return weapons.Count==0?"":string.Join("|",weapons.OrderBy(x=>x,StringComparer.Ordinal).ToArray())+(shield&&oneHand&&!twoHand?"+Shield":"");
  }
  // Match Humanoid.GiveDefaultItems RNG order. Persisting the native seed makes
  // every peer and later reload select the same native weapons and shields.
  static string Sample(Humanoid h,int seed){
   UnityEngine.Random.InitState(seed);var items=new List<ItemDrop.ItemData>();if(h.m_defaultItems!=null)foreach(var go in h.m_defaultItems)items.Add(Data(go));
   if(h.m_randomShield!=null&&h.m_randomShield.Length>0)items.Add(Data(h.m_randomShield[UnityEngine.Random.Range(0,h.m_randomShield.Length)]));
   if(h.m_randomWeapon!=null&&h.m_randomWeapon.Length>0)items.Add(Data(h.m_randomWeapon[UnityEngine.Random.Range(0,h.m_randomWeapon.Length)]));
   if(h.m_randomArmor!=null&&h.m_randomArmor.Length>0)items.Add(Data(h.m_randomArmor[UnityEngine.Random.Range(0,h.m_randomArmor.Length)]));
   if(h.m_randomSets!=null&&h.m_randomSets.Length>0){var set=h.m_randomSets[UnityEngine.Random.Range(0,h.m_randomSets.Length)];if(set?.m_items!=null)foreach(var go in set.m_items)items.Add(Data(go));}
   if(h.m_randomItems!=null){var slots=new HashSet<ItemDrop.ItemData.ItemType>();foreach(var option in h.m_randomItems)if(option.m_prefab&&UnityEngine.Random.value>option.m_chance){var data=Data(option.m_prefab);if(data!=null&&slots.Add(data.m_shared.m_itemType))items.Add(data);}}
   return Role(items);
  }
  internal static string SampleRole(Humanoid h,int seed){var before=UnityEngine.Random.state;try{return Sample(h,seed);}finally{UnityEngine.Random.state=before;}}
  internal static string Category(string role){return role.EndsWith("+Shield",StringComparison.Ordinal)?role.Substring(0,role.Length-7):role;}
  static Dictionary<string,int> Counts(Player owner,Humanoid except){var counts=new Dictionary<string,int>(StringComparer.Ordinal);foreach(var c in Followers(owner)){var h=c as Humanoid;if(!h||h==except)continue;Equipment known;string role=Role(h.GetInventory().GetAllItems());if(role.Length==0&&EquipmentRoles.TryGetValue(h,out known))role=known.Role;if(role.Length==0)continue;int count;counts.TryGetValue(role,out count);counts[role]=count+1;}return counts;}
  internal static int BalancedSeed(Humanoid h,int initial,Dictionary<string,int> counts,out string role){
   int chosen=initial,best=int.MaxValue,bestVariant=int.MaxValue;role="";var before=UnityEngine.Random.state;
   try{for(int i=0;i<128;i++){int seed=unchecked(initial+(int)(0x9E3779B9u*(uint)i));string candidate=Sample(h,seed);if(candidate.Length==0)continue;string category=Category(candidate);int count=counts.Where(x=>Category(x.Key)==category).Sum(x=>x.Value),variant;counts.TryGetValue(candidate,out variant);if(count<best||(count==best&&variant<bestVariant)){best=count;bestVariant=variant;chosen=seed;role=candidate;if(best==0&&bestVariant==0)break;}}return chosen;}finally{UnityEngine.Random.state=before;}
  }
  internal static void Mark(Humanoid h){
   var plan=Active;if(plan==null||plan.Done||!plan.Owner||!SkeletonPrefab(h.gameObject))return;var view=h.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner())return;
   int initial=(int)Seed.GetValue(h);string role;int chosen=BalancedSeed(h,initial,Counts(plan.Owner,h),out role);if(role.Length>0){Seed.SetValue(h,chosen);view.GetZDO().Set(ZDOVars.s_seed,chosen);EquipmentRoles.GetOrCreateValue(h).Role=role;}plan.Remaining=Math.Max(0,plan.Remaining-1);
  }
  internal static void Clear(){Active=null;foreach(var plan in Pending)plan.Done=true;Pending.Clear();Plans=new ConditionalWeakTable<SpawnAbility,Cast>();EquipmentRoles=new ConditionalWeakTable<Humanoid,Equipment>();}
  [HarmonyPatch(typeof(SpawnAbility),"Setup")]static class SetupPatch{[HarmonyPrefix]static void Prefix(SpawnAbility __instance,Character owner,ItemDrop.ItemData item){try{Prepare(__instance,owner,item);}catch(Exception e){Plugin.LogFault(e);}}}
  [HarmonyPatch(typeof(SpawnAbility),"Spawn")]static class SpawnPatch{[HarmonyPostfix]static void Postfix(SpawnAbility __instance,ref IEnumerator __result){__result=Wrap(__instance,__result);}}
  [HarmonyPatch(typeof(Humanoid),"Awake")]static class EquipmentPatch{[HarmonyPostfix]static void Postfix(Humanoid __instance){try{Mark(__instance);}catch(Exception e){Plugin.LogFault(e);}}}
 }
}
