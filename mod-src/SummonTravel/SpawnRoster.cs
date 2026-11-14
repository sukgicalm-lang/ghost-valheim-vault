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
  const string LoadoutKey="ghost.summontravel.loadout.v1";
  sealed class Loadout {internal GameObject Weapon,Shield;internal bool Applied;}
  static ConditionalWeakTable<Humanoid,Loadout> Loadouts=new ConditionalWeakTable<Humanoid,Loadout>();
  sealed class Cast {internal SpawnAbility Ability;internal Player Owner;internal double Started;internal bool Done,Cancelled;internal int Limit;internal string Id;internal readonly List<ZDOID> Spawned=new List<ZDOID>();internal readonly Dictionary<string,int> Roles=new Dictionary<string,int>(StringComparer.Ordinal);}
  static ConditionalWeakTable<SpawnAbility,Cast> Plans=new ConditionalWeakTable<SpawnAbility,Cast>();
  static readonly List<Cast> Pending=new List<Cast>();static Cast Active;
  internal static void ValidateApi(){if(AccessTools.Method(typeof(Humanoid),"GiveDefaultItems")==null)throw new MissingFieldException("해골 일괄 소환: 게임의 장비 선택 저장값 구조가 맞지 않습니다.");}
  static bool SkeletonPrefab(GameObject go){return go&&(go.name=="Skeleton_Friendly"||go.name=="Skeleton_Friendly(Clone)");}
  internal static int Limit(SpawnAbility ability,Player owner,ItemDrop.ItemData item){
   if(!owner||!owner.GetSkills()||ability.m_levelUpSettings==null)return 0;
   for(int i=ability.m_levelUpSettings.Count-1;i>=0;i--){var setting=ability.m_levelUpSettings[i];if(owner.GetSkillLevel(setting.m_skill)>=setting.m_skillLevel)return Math.Max(0,ability.m_setMaxInstancesFromWeaponLevel?(item==null?0:item.m_quality):setting.m_maxSpawns);}
   return 0;
  }
  internal static int Prepare(SpawnAbility ability,Character owner,ItemDrop.ItemData item){
   var player=owner as Player;if(!ability||!player||player!=Player.m_localPlayer||player.IsDead()||ability.m_spawnOnAwake||ability.m_spawnPrefab==null||ability.m_spawnPrefab.Length==0||!ability.m_spawnPrefab.All(SkeletonPrefab))return -1;
   var view=player.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner())return -1;int limit=Limit(ability,player,item);if(limit<=0)return -1;
   Pending.RemoveAll(c=>c.Done||!c.Ability||Travel.Now-c.Started>60);
   Plans.Remove(ability);var plan=new Cast{Ability=ability,Owner=player,Started=Travel.Now,Limit=limit,Id=System.Guid.NewGuid().ToString("N")};Plans.Add(ability,plan);Pending.Add(plan);
   // Keep the original coroutine, costs, native skill scaling, command RPC,
   // effects and limit enforcement. Every cast creates a full new roster;
   // native Tameable.Command -> UnsummonMaxInstances retires older summons.
   // Both native waits run per creature. Keep only the shared initial cast
   // delay, so valid spawn positions produce the whole roster in one frame.
   ability.m_minToSpawn=ability.m_maxToSpawn=limit;ability.m_spawnDelay=0f;ability.m_preSpawnDelay=0f;ability.m_circleSpawn=true;ability.m_spawnRadius=Math.Max(ability.m_spawnRadius,3f);return limit;
  }
  internal static IEnumerator Wrap(SpawnAbility ability,IEnumerator original){Cast plan;return Plans.TryGetValue(ability,out plan)?Run(plan,original):original;}
  internal static int CancelPending(Player player) {
   int count = 0;
   foreach (var plan in Pending)
    if (plan.Owner == player && !plan.Cancelled) {
     plan.Cancelled = true;
     count++;
    }
   return count;
  }
  static IEnumerator Run(Cast plan,IEnumerator original){
   try{while(!plan.Cancelled){bool more;object value=null;var before=Active;try{Active=plan;more=original.MoveNext();if(more)value=original.Current;}finally{Active=before;}if(!more)yield break;yield return value;}}
   finally{
    plan.Done=true;Pending.Remove(plan);
    if(Plans.TryGetValue(plan.Ability,out var current)&&ReferenceEquals(current,plan))Plans.Remove(plan.Ability);
    (original as IDisposable)?.Dispose();
    if(!plan.Cancelled)SavedFollowers.CastEnded(plan.Owner,plan.Id,plan.Spawned,plan.Limit);
   }
  }
  static ItemDrop.ItemData Data(GameObject prefab){return prefab?prefab.GetComponent<ItemDrop>()?.m_itemData:null;}
  static bool Weapon(GameObject prefab){var item=Data(prefab);return item!=null&&item.IsWeapon();}
  static string Kind(ItemDrop.ItemData item,string name){
   if(item.m_shared.m_itemType==ItemDrop.ItemData.ItemType.Bow)return "Bows";
   name=name??"";if(name.IndexOf("spear",StringComparison.OrdinalIgnoreCase)>=0)return "Spears";if(name.IndexOf("sword",StringComparison.OrdinalIgnoreCase)>=0)return "Swords";if(name.IndexOf("mace",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("club",StringComparison.OrdinalIgnoreCase)>=0)return "Clubs";
   return item.m_shared.m_skillType!=Skills.SkillType.None?item.m_shared.m_skillType.ToString():"Weapon/"+name;
  }
  internal static string Role(IEnumerable<ItemDrop.ItemData> items){
   var weapons=new HashSet<string>(StringComparer.Ordinal);bool shield=false,oneHand=false,twoHand=false;
   foreach(var item in items){if(item==null)continue;var type=item.m_shared.m_itemType;if(type==ItemDrop.ItemData.ItemType.Shield)shield=true;if(!item.IsWeapon())continue;weapons.Add(Kind(item,item.m_dropPrefab?item.m_dropPrefab.name:item.m_shared.m_name));oneHand|=type==ItemDrop.ItemData.ItemType.OneHandedWeapon;twoHand|=type==ItemDrop.ItemData.ItemType.Bow||type==ItemDrop.ItemData.ItemType.TwoHandedWeapon||type==ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;}
   return weapons.Count==0?"":string.Join("|",weapons.OrderBy(x=>x,StringComparer.Ordinal).ToArray())+(shield&&oneHand&&!twoHand?"+Shield":"");
  }
  internal static string Category(string role){return role.EndsWith("+Shield",StringComparison.Ordinal)?role.Substring(0,role.Length-7):role;}
  static IEnumerable<GameObject> Prefabs(Humanoid h){
   foreach(var array in new[]{h.m_defaultItems,h.m_randomWeapon,h.m_randomShield,h.m_randomArmor})if(array!=null)foreach(var go in array)if(go)yield return go;
   if(h.m_randomSets!=null)foreach(var set in h.m_randomSets)if(set?.m_items!=null)foreach(var go in set.m_items)if(go)yield return go;
   if(h.m_randomItems!=null)foreach(var item in h.m_randomItems)if(item?.m_prefab)yield return item.m_prefab;
  }
  static string SelectedRole(GameObject weapon,GameObject shield){var item=Data(weapon);return Kind(item,weapon.name)+(shield&&item.m_shared.m_itemType==ItemDrop.ItemData.ItemType.OneHandedWeapon?"+Shield":"");}
  internal static void Mark(Humanoid h){
   var plan=Active;if(plan==null||plan.Done||!plan.Owner||!SkeletonPrefab(h.gameObject))return;var view=h.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner()||Loadouts.TryGetValue(h,out var prior))return;
   SummonDismissal.RememberSummoner(view,plan.Owner);
   SavedFollowers.MarkCast(view,plan.Id);
   plan.Spawned.Add(view.GetZDO().m_uid);
   var weapons=Prefabs(h).Where(Weapon).GroupBy(x=>x.name,StringComparer.Ordinal).Select(x=>x.First()).ToArray();if(weapons.Length==0)return;
   var shields=h.m_randomShield!=null&&h.m_randomShield.Length>0?h.m_randomShield:new GameObject[]{null};Loadout chosen=null;string role="";int best=int.MaxValue,bestVariant=int.MaxValue;
   foreach(var weapon in weapons)foreach(var shield in shields){string candidate=SelectedRole(weapon,shield),category=Category(candidate);int count=plan.Roles.Where(x=>Category(x.Key)==category).Sum(x=>x.Value),variant;plan.Roles.TryGetValue(candidate,out variant);if(count<best||count==best&&variant<bestVariant){best=count;bestVariant=variant;role=candidate;chosen=new Loadout{Weapon=weapon,Shield=shield};}}
   if(chosen==null)return;Loadouts.Add(h,chosen);int used;plan.Roles.TryGetValue(role,out used);plan.Roles[role]=used+1;
   // Persist actual native prefab identities, not a prediction of Unity RNG.
   // Item creation can itself advance RNG between native random selections.
   view.GetZDO().Set(LoadoutKey,"1|"+chosen.Weapon.name+"|"+(chosen.Shield?chosen.Shield.name:""));
  }
  static GameObject[] WithoutWeapons(GameObject[] source){return source==null?Array.Empty<GameObject>():source.Where(x=>!Weapon(x)).ToArray();}
  internal static void Apply(Humanoid h){
   if(!h||!SkeletonPrefab(h.gameObject))return;var view=h.GetComponent<ZNetView>();if(!view||!view.IsValid())return;Loadout loadout;
   if(!Loadouts.TryGetValue(h,out loadout)){
    var saved=view.GetZDO().GetString(LoadoutKey).Split('|');if(saved.Length!=3||saved[0]!="1")return;var native=Prefabs(h).ToArray();var weapon=native.FirstOrDefault(x=>x.name==saved[1]&&Weapon(x));if(!weapon)return;
    var shield=saved[2].Length==0?null:(h.m_randomShield??Array.Empty<GameObject>()).FirstOrDefault(x=>x&&x.name==saved[2]);if(saved[2].Length>0&&!shield)return;loadout=new Loadout{Weapon=weapon,Shield=shield};Loadouts.Add(h,loadout);
   }
   if(loadout.Applied)return;
   // Per-instance arrays only. Keep native armor/cosmetics and inventory code,
   // but give exactly the assigned weapon, even when NPC skills are identical.
   h.m_defaultItems=WithoutWeapons(h.m_defaultItems).Concat(new[]{loadout.Weapon}).ToArray();h.m_randomWeapon=WithoutWeapons(h.m_randomWeapon);h.m_randomArmor=WithoutWeapons(h.m_randomArmor);
   h.m_randomShield=loadout.Shield?new[]{loadout.Shield}:Array.Empty<GameObject>();
   if(h.m_randomSets!=null)h.m_randomSets=h.m_randomSets.Select(set=>set==null?null:new Humanoid.ItemSet{m_name=set.m_name,m_items=WithoutWeapons(set.m_items)}).ToArray();
   if(h.m_randomItems!=null)h.m_randomItems=h.m_randomItems.Where(item=>item==null||!Weapon(item.m_prefab)).ToArray();loadout.Applied=true;
  }
  internal static void Clear(){Active=null;foreach(var plan in Pending)plan.Done=true;Pending.Clear();Plans=new ConditionalWeakTable<SpawnAbility,Cast>();Loadouts=new ConditionalWeakTable<Humanoid,Loadout>();}
  [HarmonyPatch(typeof(SpawnAbility),"Setup")]static class SetupPatch{[HarmonyPrefix]static void Prefix(SpawnAbility __instance,Character owner,ItemDrop.ItemData item){try{Prepare(__instance,owner,item);}catch(Exception e){Plugin.LogFault(e);}}}
  [HarmonyPatch(typeof(SpawnAbility),"Spawn")]static class SpawnPatch{[HarmonyPostfix]static void Postfix(SpawnAbility __instance,ref IEnumerator __result){__result=Wrap(__instance,__result);}}
  [HarmonyPatch(typeof(Humanoid),"GiveDefaultItems")]static class ApplyEquipmentPatch{[HarmonyPrefix]static void Prefix(Humanoid __instance){try{Apply(__instance);}catch(Exception e){Plugin.LogFault(e);}}}
  [HarmonyPatch(typeof(Humanoid),"Awake")]static class EquipmentPatch{[HarmonyPostfix]static void Postfix(Humanoid __instance){try{Mark(__instance);}catch(Exception e){Plugin.LogFault(e);}}}
 }
}
