// SPDX-License-Identifier: MIT
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace GhostSummonTravel {
 internal static class FollowerRecovery {
  const string Key="ghost.summontravel.foodregen.v1";
  static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
  static readonly FieldInfo Tamable=AccessTools.Field(typeof(BaseAI),"m_tamable");
  sealed class Sample {internal double Next;internal float Rate;}
  static ConditionalWeakTable<Player,Sample> Samples=new ConditionalWeakTable<Player,Sample>();
  static Player published;static double nextSample,nextPublish;
  static Type regeneration;
  static object Setting(Type type,string field){var entry=type.GetField(field,BindingFlags.Public|BindingFlags.Static)?.GetValue(null);return entry?.GetType().GetProperty("Value")?.GetValue(entry,null);}
  internal static float RegenScale(Type type){
   if(type==null)return 1;try{var enabled=Setting(type,"modEnabled");if(!(enabled is bool)||!(bool)enabled)return 1;
    var health=Setting(type,"healthRegenMult");var delay=Setting(type,"healthRegenTimeMult");if(!(health is float)||!(delay is float))return 1;
    float gain=(float)health,time=(float)delay,scale=gain/time;return Travel.Finite(gain)&&gain>=0&&Travel.Finite(time)&&time>0&&Travel.Finite(scale)?scale:1;
   }catch{return 1;}
  }
  static float InstalledScale(){
   if(regeneration==null)foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="Regeneration"){regeneration=assembly.GetType("Regeneration.Regeneration",false);break;}
   return RegenScale(regeneration);
  }
  internal struct State {internal Character Character;internal BaseAI AI;internal Tameable Tamable;internal float Original;internal bool Changed;}
  internal static float FoodRate(Player player){
   if(!player||player.IsDead())return 0;float amount=0;foreach(var food in player.GetFoods())if(food.m_item!=null)amount+=food.m_item.m_shared.m_foodRegen;
   float multiplier=1;player.GetSEMan().ModifyHealthRegen(ref multiplier);float rate=amount*multiplier*InstalledScale()/10f;
   return Travel.Finite(rate)&&rate>0?rate:0;
  }
  internal static void Tick(){
   var p=Player.m_localPlayer;if(!p||p.IsDead())return;if(p!=published){published=p;nextSample=nextPublish=0;}
   if(Travel.Now<nextSample)return;nextSample=Travel.Now+1;var sample=Samples.GetOrCreateValue(p);float rate=FoodRate(p);bool changed=sample.Rate!=rate;sample.Rate=rate;sample.Next=Travel.Now+2;
   var v=p.GetComponent<ZNetView>();if(v&&v.IsValid()&&v.IsOwner()&&(changed||Travel.Now>=nextPublish)){nextPublish=Travel.Now+5;v.GetZDO().Set(Key,rate.ToString("R",Inv)+";"+Travel.Now.ToString("R",Inv));}
  }
  static bool Rate(Player p,out float rate){
   rate=0;if(!p||p.IsDead())return false;Sample sample;
   if(Samples.TryGetValue(p,out sample)&&Travel.Now<sample.Next){rate=sample.Rate;return true;}
   if(p==Player.m_localPlayer){Tick();if(Samples.TryGetValue(p,out sample)){rate=sample.Rate;return true;}return false;}
   var v=p.GetComponent<ZNetView>();if(!v||!v.IsValid())return false;var fields=v.GetZDO().GetString(Key).Split(';');float value;double stamp;
   if(fields.Length!=2||!float.TryParse(fields[0],NumberStyles.Float,Inv,out value)||!double.TryParse(fields[1],NumberStyles.Float,Inv,out stamp)||!Travel.Finite(value)||value<0||double.IsNaN(stamp)||double.IsInfinity(stamp)||Travel.Now-stamp>15||Travel.Now-stamp< -2)return false;
   sample=Samples.GetOrCreateValue(p);sample.Rate=rate=value;sample.Next=Travel.Now+1;return true;
  }
  internal static bool Begin(BaseAI ai,out State state){
   state=default(State);var c=ai.GetComponent<Character>();if(!Travel.IsSkeleton(c)||!c.IsTamed()||!SummonLifecycle.Usable(c))return true;var v=c.GetComponent<ZNetView>();if(!v||!v.IsValid()||!v.IsOwner())return true;
   if(Travel.Held(c))return false;var monster=ai as MonsterAI;var go=monster?monster.GetFollowTarget():null;var p=go?go.GetComponent<Player>():Player.m_localPlayer;float rate;
   if(!Rate(p,out rate)||rate<=0||!Travel.Finite(c.m_regenAllHPTime)||c.m_regenAllHPTime<=0)return true;
   float native=c.GetMaxHealth()/c.m_regenAllHPTime;
   // Raised skeletons cannot eat. Bypass the animal hunger gate only for this
   // owned summon regeneration call, without changing feeding ZDO or pet rules.
   state=new State{Character=c,AI=ai,Tamable=Tamable.GetValue(ai) as Tameable,Original=c.m_regenAllHPTime,Changed=rate>native};
   if(state.Tamable)Tamable.SetValue(ai,null);if(state.Changed)c.m_regenAllHPTime=c.GetMaxHealth()/rate;return true;
  }
  internal static void End(State state){if(state.Changed&&state.Character)state.Character.m_regenAllHPTime=state.Original;if(state.AI&&state.Tamable)Tamable.SetValue(state.AI,state.Tamable);}
  internal static void Clear(){Samples=new ConditionalWeakTable<Player,Sample>();published=null;nextSample=nextPublish=0;}
  [HarmonyPatch(typeof(BaseAI),"UpdateRegeneration")]static class RegenPatch{
   [HarmonyPrefix]static bool Prefix(BaseAI __instance,out State __state){__state=default(State);try{return Begin(__instance,out __state);}catch(Exception e){Plugin.LogFault(e);return true;}}
   [HarmonyFinalizer]static Exception Finalizer(State __state,Exception __exception){try{End(__state);}catch(Exception e){Plugin.LogFault(e);}return __exception;}
  }
 }
}
