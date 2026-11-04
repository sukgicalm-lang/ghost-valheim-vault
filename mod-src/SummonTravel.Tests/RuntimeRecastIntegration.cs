extern alias RuntimeHarmony;
using System;
using System.Reflection;
using Harmony=RuntimeHarmony::HarmonyLib.Harmony;
using HarmonyMethod=RuntimeHarmony::HarmonyLib.HarmonyMethod;
using UnityEngine;
class RuntimeRecastIntegration {
 static int checks;
 static readonly Assembly Production=typeof(Character).Assembly;
 static readonly Type Travel=Production.GetType("GhostSummonTravel.Travel"),Refresh=Production.GetType("GhostSummonTravel.ProtectionRecast");
 static int Hash= (int)Travel.GetField("ProtectionShieldHash",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
 static void Check(bool b,string name){if(!b)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
 static MethodInfo Hook(string nested){return Refresh.GetNestedType(nested,BindingFlags.NonPublic).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);}
 static T Make<T>(int id,string prefab)where T:Character,new(){var go=new GameObject();var c=new T();go.Add(c);int hash=(int)Travel.GetMethod("StableHash",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{prefab});go.Add(new ZNetView{Zdo=new ZDO{m_uid=new ZDOID{Value=id},Prefab=hash}});c.Manager=new SEMan{m_character=c};Character.All.Add(c);return c;}
 static SE_Shield Existing(Character c){var s=new SE_Shield{Hash=Hash,m_character=c,m_totalAbsorbDamage=300,m_damage=245,m_time=45,m_ttl=120};c.Manager.Effects.Clear();c.Manager.Effects.Add(s);return s;}
 // Faithful existing-effect branch of the supplied native Character.RPC_Damage:
 // it calls ResetTime/SetLevel directly and never calls SEMan.AddStatusEffect.
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static void NativeStatusHit(Character c,float skill=20){var existing=c.Manager.GetStatusEffect(Hash);if(existing==null)c.Manager.AddStatusEffect(Hash,false,2,skill);else{existing.ResetTime();existing.SetLevel(2,skill);}}
 static GameObject Gear(string name,Skills.SkillType skill,ItemDrop.ItemData.ItemType type){var go=new GameObject{name=name};go.Add(new ItemDrop{m_itemData=new ItemDrop.ItemData{m_shared=new ItemDrop.SharedData{m_skillType=skill,m_itemType=type}}});return go;}
 static int nextSummon=91000;
 static Humanoid Fresh(GameObject[] weapons,GameObject shield){var h=Make<Humanoid>(nextSummon++,"Skeleton_Friendly");h.gameObject.name="Skeleton_Friendly(Clone)";h.gameObject.Add(new Tameable());h.gameObject.Add(new MonsterAI());h.m_randomWeapon=weapons;h.m_randomShield=new[]{shield};return h;}
 static SpawnAbility Ability(GameObject[] weapons,GameObject shield){var go=new GameObject{name="staff_skeleton_aoe(Clone)"};var a=new SpawnAbility{m_maxSpawned=0,m_spawnPrefab=new[]{new GameObject{name="Skeleton_Friendly"}},Factory=()=>Fresh(weapons,shield)};go.Add(a);a.m_levelUpSettings.Add(new SpawnAbility.LevelUpSettings{m_skill=Skills.SkillType.BloodMagic,m_maxSpawns=4});return a;}
 static string Role(Humanoid h){return(string)Production.GetType("GhostSummonTravel.SpawnRoster").GetMethod("Role",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{h.GetInventory().GetAllItems()});}
 static void Run(SpawnAbility a,Player p){a.Setup(p,Vector3.zero,0,null,new ItemDrop.ItemData(),null);while(a.Enumerator.MoveNext()){} }
 static void Main(){
  var p=Make<Player>(1,"Player");Player.m_localPlayer=p;var npc=Make<Character>(2,"Skeleton_Friendly");npc.gameObject.Add(new Tameable());npc.gameObject.Add(new MonsterAI{Target=p.gameObject});
  var template=new SE_Shield{Hash=Hash,m_absorbDamage=200,m_absorbDamagePerSkillLevel=5,m_ttlPerItemLevel=60};ObjectDB.instance.Templates[Hash]=template;
  var own=Existing(p);var summon=Existing(npc);
  NativeStatusHit(p);NativeStatusHit(npc);Check(own.m_damage==245&&summon.m_damage==245,"reproduced original direct-hit refresh retaining spent absorb for player and summon");
  var harmony=new Harmony("ghost.tests.protection-recast.runtime");
  try{
   harmony.Patch(typeof(StatusEffect).GetMethod("ResetTime"),postfix:new HarmonyMethod(Hook("ResetPatch")));
   harmony.Patch(typeof(SE_Shield).GetMethod("SetLevel"),postfix:new HarmonyMethod(Hook("LevelPatch")));
   NativeStatusHit(p);NativeStatusHit(npc);
   Check(own.m_totalAbsorbDamage==300&&own.m_damage==0,"real detours refill player through native direct-hit refresh branch");
   Check(summon.m_totalAbsorbDamage==300&&summon.m_damage==0,"real detours refill summon through native direct-hit refresh branch");
   Check(own.m_time==0&&own.m_ttl==120&&summon.m_time==0&&summon.m_ttl==120,"native duration and timer survive actual detour execution");
   var infoArgs=new object[]{p,0f,0f,0f};Travel.GetMethod("ShieldInfo",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,infoArgs);Check((float)infoArgs[3]==120,"real shield recast resets own visible countdown to native duration");
   own.m_damage=230;p.Manager.AddStatusEffect(template,true,2,20);Check(own.m_damage==0&&own.m_totalAbsorbDamage==300,"real detours also cover direct StatusEffect overload");
   own.m_damage=230;p.Manager.AddStatusEffect(Hash,true,2,20);Check(own.m_damage==0,"real detours also cover hash and RPC application path");
   own.m_damage=120;NativeStatusHit(p,30);Check(own.m_damage==0&&own.m_totalAbsorbDamage==350,"actual native SetLevel computes new skill-scaled capacity before refill");
   Check(p.Manager.Effects.Count==1&&npc.Manager.Effects.Count==1,"repeated native recasts never stack duplicate shields");
   var args=new object[]{p,0f,0f};bool active=(bool)Production.GetType("GhostSummonTravel.PlayerShieldBar").GetMethod("Active",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);Check(active&&(float)args[1]==350&&(float)args[2]==350,"own HUD reads full real shield immediately after detoured recast");
   own.m_damage=90;own.m_time=17;p.Manager.AddStatusEffect(template,false,2,30);Check(own.m_damage==90&&own.m_time==17,"non-refresh status insertion does not refill");
   infoArgs=new object[]{p,0f,0f,0f};Travel.GetMethod("ShieldInfo",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,infoArgs);Check((float)infoArgs[3]==103&&own.m_time==17&&own.m_damage==90,"countdown reads elapsed time without mutating shield while real recast detours are installed");
   own.SetLevel(2,30);Check(own.m_damage==90,"level-only recalculation cannot refill without native timer reset");
   own.ResetTime();Time.frameCount++;own.SetLevel(2,30);Check(own.m_damage==90,"stale timer reset cannot refill an unrelated later level update");
   own.Hash=777;own.ResetTime();own.SetLevel(2,30);Check(own.m_damage==90,"unrelated SE_Shield effects retain spent damage");own.Hash=Hash;
   p.GetComponent<ZNetView>().Zdo.Owner=2;NativeStatusHit(p);Check(own.m_damage==90,"non-owner does not mutate absorb during refresh");p.GetComponent<ZNetView>().Zdo.Owner=1;
   summon.m_damage=125;summon.m_time=25;Travel.GetMethod("SaveShields",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{npc});summon.m_damage=0;summon.m_time=0;Travel.GetMethod("RestoreShields",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{npc});Check(summon.m_damage==125&&summon.m_time==25,"teleport restore keeps spent shield while real detours are installed");
   infoArgs=new object[]{npc,0f,0f,0f};Travel.GetMethod("ShieldInfo",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,infoArgs);Check((float)infoArgs[3]==95&&summon.m_time==25,"teleport restoration retains remaining shield countdown under real detours");
   npc.Manager.Effects.Clear();NativeStatusHit(npc);var fresh=(SE_Shield)npc.Manager.Effects[0];Check(fresh.m_totalAbsorbDamage==300&&fresh.m_damage==0,"first-time native hit still creates one normal full shield");
   var ai=npc.GetComponent<MonsterAI>();ai.m_tamable=npc.GetComponent<Tameable>();ai.m_tamable.Hungry=true;ai.SimulateRegeneration(3);Check(npc.Health==45,"reproduced native animal hunger blocking summon regeneration");
   var recovery=Production.GetType("GhostSummonTravel.FollowerRecovery");var hooks=recovery.GetNestedType("RegenPatch",BindingFlags.NonPublic);
   harmony.Patch(typeof(BaseAI).GetMethod("UpdateRegeneration",BindingFlags.NonPublic|BindingFlags.Instance),prefix:new HarmonyMethod(hooks.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic)),finalizer:new HarmonyMethod(hooks.GetMethod("Finalizer",BindingFlags.Static|BindingFlags.NonPublic)));
   p.Foods.Add(new Player.Food{m_item=new ItemDrop.ItemData{m_shared=new ItemDrop.SharedData{m_foodRegen=10}}});p.Manager.RegenMultiplier=2;recovery.GetMethod("Tick",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
   npc.Health=45;npc.m_regenAllHPTime=3600;ai.SimulateRegeneration(3);
   Check(npc.Health==51,"real regeneration prefix uses player's two HP per second through native healing");Check(npc.m_regenAllHPTime==3600,"real finalizer restores original summon regeneration setting after success");Check(ai.m_tamable==npc.GetComponent<Tameable>()&&ai.m_tamable.Hungry,"real finalizer restores original hunger reference without changing feeding state");
   ai.ThrowRegen=true;bool thrown=false;try{ai.SimulateRegeneration(3);}catch(InvalidOperationException){thrown=true;}Check(thrown&&npc.m_regenAllHPTime==3600&&ai.m_tamable==npc.GetComponent<Tameable>(),"real finalizer restores original healing and hunger settings while preserving native exceptions");ai.ThrowRegen=false;
   npc.GetComponent<ZNetView>().Zdo.Owner=2;float before=npc.Health;ai.SimulateRegeneration(3);Check(npc.Health==before&&ai.m_tamable==npc.GetComponent<Tameable>(),"real regeneration hook preserves native hunger for non-owned followers");
   npc.GetComponent<ZNetView>().Zdo.Owner=1;npc.Tamed=false;ai.SimulateRegeneration(3);Check(ai.m_tamable==npc.GetComponent<Tameable>()&&npc.m_regenAllHPTime==3600,"wild creatures retain original hunger and regeneration settings");
   var roster=Production.GetType("GhostSummonTravel.SpawnRoster");var setupHook=roster.GetNestedType("SetupPatch",BindingFlags.NonPublic).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);var spawnHook=roster.GetNestedType("SpawnPatch",BindingFlags.NonPublic).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);var awakeHook=roster.GetNestedType("EquipmentPatch",BindingFlags.NonPublic).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);
   harmony.Patch(typeof(SpawnAbility).GetMethod("Setup"),prefix:new HarmonyMethod(setupHook));harmony.Patch(typeof(SpawnAbility).GetMethod("Spawn",BindingFlags.NonPublic|BindingFlags.Instance),postfix:new HarmonyMethod(spawnHook));harmony.Patch(typeof(Humanoid).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance),postfix:new HarmonyMethod(awakeHook));
   var caster=Make<Player>(90000,"Player");caster.PlayerName="RuntimeBatch";Player.m_localPlayer=caster;var sword=Gear("Sword",Skills.SkillType.Swords,ItemDrop.ItemData.ItemType.OneHandedWeapon);var bow=Gear("Bow",Skills.SkillType.Bows,ItemDrop.ItemData.ItemType.Bow);var spear=Gear("Spear",Skills.SkillType.Spears,ItemDrop.ItemData.ItemType.OneHandedWeapon);var shield=Gear("Shield",Skills.SkillType.None,ItemDrop.ItemData.ItemType.Shield);var weapons=new[]{sword,bow,spear};var batch=Ability(weapons,shield);Run(batch,caster);
   Check(batch.NativeSetupCalls==1&&batch.Spawned.Count==4,"real native Setup and iterator detours fill maximum slots with one cast");
   var categories=new System.Collections.Generic.Dictionary<string,int>();foreach(var h in batch.Spawned){var role=Role(h).Replace("+Shield","");categories[role]=categories.ContainsKey(role)?categories[role]+1:1;}Check(categories.Count==3,"real coroutine scope and humanoid Awake detours diversify native weapon categories");
   var survivor=batch.Spawned[0];survivor.Health=19;var survivorShield=Existing(survivor);var victim=batch.Spawned[1];victim.Dead=true;var fill=Ability(weapons,shield);Run(fill,caster);Check(fill.Spawned.Count==1&&survivor.Health==19&&survivor.Manager.Effects[0]==survivorShield&&survivorShield.m_damage==245,"real recast fills only missing summon without replacing, healing or refilling existing skeletons");
   var full=Ability(weapons,shield);Run(full,caster);Check(full.Spawned.Count==0,"real full-roster cast cannot create duplicate summons");
   var native=fill.Spawned[0];int seed=native.GetComponent<ZNetView>().GetZDO().GetInt(ZDOVars.s_seed);var peer=Fresh(weapons,shield);peer.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_seed,seed);peer.SimulateAwake();peer.GiveDefaultItems();Check(Role(peer)==Role(native),"real seed detour persists identical native gear across peer reconstruction");
   var foreign=Ability(weapons,shield);foreign.m_spawnPrefab=new[]{new GameObject{name="Greydwarf"}};foreign.Setup(caster,Vector3.zero,0,null,new ItemDrop.ItemData(),null);Check(foreign.m_minToSpawn==1&&foreign.m_maxToSpawn==1,"real spawn detours leave other creature abilities unchanged");
   var otherCaster=Make<Player>(90001,"Player");otherCaster.PlayerName="RuntimeException";Player.m_localPlayer=otherCaster;var broken=Ability(weapons,shield);broken.Factory=()=>throw new InvalidOperationException("original spawn failed");broken.Setup(otherCaster,Vector3.zero,0,null,new ItemDrop.ItemData(),null);bool failed=false;try{broken.Enumerator.MoveNext();}catch(InvalidOperationException){failed=true;}var retry=Ability(weapons,shield);Run(retry,otherCaster);Check(failed&&retry.Spawned.Count==4,"real iterator failure preserves exception and releases reserved summon slots for retry");
  }finally{harmony.UnpatchAll(harmony.Id);}
  Console.WriteLine("Runtime detour checks: "+checks);
 }
}
