// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;
using UnityEngine;
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
namespace GhostSummonTravel {
 [BepInPlugin(Guid,"Ghost 해골 동행·길막힘 복귀","1.0.0")]
 public sealed class Plugin:BaseUnityPlugin {
  public const string Guid="ghost.valheim.summontravel";Harmony harmony;
  void Awake(){Travel.ValidateApi();harmony=new Harmony(Guid);try{harmony.PatchAll(typeof(Plugin).Assembly);}catch{harmony.UnpatchSelf();throw;}Logger.LogInfo("해골 동행 1.0.0: 순간이동·방어막 유지·지형에 막힌 해골의 안전한 합류");}
  void Update(){Travel.Tick();}void OnDestroy(){Travel.Clear();if(harmony!=null)harmony.UnpatchSelf();}
 }
 internal static class Travel {
  internal const string BeginRpc="GhostSummonTravelBeginV1",FinishRpc="GhostSummonTravelFinishV1";
  const string ShieldKey="ghost.summontravel.shields.v1";
  static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
  static readonly Dictionary<ZDOID,Transit> Moving=new Dictionary<ZDOID,Transit>();
  static readonly Dictionary<ZDOID,Progress> Followers=new Dictionary<ZDOID,Progress>();
  static ConditionalWeakTable<Character,ShieldState> Shields=new ConditionalWeakTable<Character,ShieldState>();
  static LocalTrip local;static double nextCheck;
  static readonly FieldInfo Total=AccessTools.Field(typeof(SE_Shield),"m_totalAbsorbDamage"),Damage=AccessTools.Field(typeof(SE_Shield),"m_damage"),
   EffectTime=AccessTools.Field(typeof(StatusEffect),"m_time"),EffectCharacter=AccessTools.Field(typeof(StatusEffect),"m_character"),
   Body=AccessTools.Field(typeof(Character),"m_body"),Altitude=AccessTools.Field(typeof(Character),"m_maxAirAltitude"),ManagerCharacter=AccessTools.Field(typeof(SEMan),"m_character");
  static PropertyInfo BodyPosition,BodyRotation,BodyVelocity,BodyAngular,BodyKinematic,BodyGravity;
  static MethodInfo CheckCapsule;static object IgnoreTriggers;
  internal sealed class Transit {internal Character C;internal ZNetView V;internal ZDOID Leader;internal long Sender;internal string Token;internal double Started,Finished;internal bool Done,Kinematic,Gravity;internal Vector3 Target;internal Quaternion Rotation;}
  sealed class LocalTrip {internal Player Player;internal string Token;internal List<ZNetView> Views;}
  sealed class ShieldState {internal bool WasOwner;internal double SavedAt=-1;}
  sealed class Progress {internal float Best;internal double LastProgress,LastRecall;}
  internal static double Now {get{return ZNet.instance?ZNet.instance.GetTimeSeconds():0;}}
  internal static int StableHash(string s){unchecked{int a=5381,b=a;for(int i=0;i<s.Length&&s[i]!='\0';i+=2){a=((a<<5)+a)^s[i];if(i==s.Length-1||s[i+1]=='\0')break;b=((b<<5)+b)^s[i+1];}return a+b*1566083941;}}
  internal static bool Finite(float n){return !float.IsNaN(n)&&!float.IsInfinity(n);}
  internal static bool IsSkeleton(Character c){if(!c||c is Player)return false;var v=c.GetComponent<ZNetView>();return v&&v.IsValid()&&v.GetZDO().GetPrefab()==StableHash("Skeleton_Friendly")&&c.GetComponent<Tameable>()&&c.GetComponent<MonsterAI>();}
  static bool Follows(Character c,Player p){if(!IsSkeleton(c)||!p||!c.IsTamed()||c.IsDead())return false;var target=c.GetComponent<MonsterAI>().GetFollowTarget();return target?target==p.gameObject:c.GetComponent<ZNetView>().GetZDO().GetString(ZDOVars.s_follow)==p.GetPlayerName();}
  internal static void ValidateApi(){
   if(new[]{Total,Damage,EffectTime,EffectCharacter,Body,Altitude,ManagerCharacter}.Any(x=>x==null))throw new MissingFieldException("해골 동행: 게임 필드가 달라 시작을 중단합니다.");
   var t=Body.FieldType;BodyPosition=t.GetProperty("position");BodyRotation=t.GetProperty("rotation");BodyVelocity=t.GetProperty("linearVelocity");BodyAngular=t.GetProperty("angularVelocity");BodyKinematic=t.GetProperty("isKinematic");BodyGravity=t.GetProperty("useGravity");
   if(new[]{BodyPosition,BodyRotation,BodyVelocity,BodyAngular,BodyKinematic,BodyGravity}.Any(x=>x==null||!x.CanWrite))throw new MissingMemberException("해골 동행: 물리 API가 맞지 않습니다.");
   var physics=t.Assembly.GetType("UnityEngine.Physics");var trigger=t.Assembly.GetType("UnityEngine.QueryTriggerInteraction");
   if(physics==null||trigger==null)throw new MissingMemberException("해골 동행: 안전한 합류 위치를 확인할 수 없습니다.");
   CheckCapsule=physics.GetMethod("CheckCapsule",new[]{typeof(Vector3),typeof(Vector3),typeof(float),typeof(int),trigger});IgnoreTriggers=Enum.ToObject(trigger,1);
   if(CheckCapsule==null)throw new MissingMemberException("해골 동행: 충돌 확인 API가 맞지 않습니다.");
  }
  internal static void Register(Character c){if(!IsSkeleton(c))return;var v=c.GetComponent<ZNetView>();v.Register<ZDOID,string>(BeginRpc,(s,l,t)=>Begin(c,s,l,t));v.Register<ZDOID,string,Vector3,Quaternion>(FinishRpc,(s,l,t,p,r)=>Finish(c,s,l,t,p,r));}
  internal static void Start(Player p){
   if(p!=Player.m_localPlayer||!p.IsTeleporting()||local!=null)return;
   var views=Character.GetAllCharacters().Where(c=>Follows(c,p)).Select(c=>c.GetComponent<ZNetView>()).ToList();if(views.Count==0)return;
   local=new LocalTrip{Player=p,Token=System.Guid.NewGuid().ToString("N"),Views=views};
   foreach(var v in views){Begin(v.GetComponent<Character>(),ZDOMan.GetSessionID(),p.GetZDOID(),local.Token);v.InvokeRPC(ZNetView.Everybody,BeginRpc,p.GetZDOID(),local.Token);}
  }
  internal static void Begin(Character c,long sender,ZDOID leader,string token){
   if(!IsSkeleton(c)||token==null||token.Length!=32)return;var obj=ZNetScene.instance.FindInstance(leader);var p=obj?obj.GetComponent<Player>():null;
   if(!p||p.GetOwner()!=sender||!Follows(c,p))return;var v=c.GetComponent<ZNetView>();var id=v.GetZDO().m_uid;if(Moving.ContainsKey(id))return;
   var body=Body.GetValue(c);var t=new Transit{C=c,V=v,Leader=leader,Sender=sender,Token=token,Started=Now,Kinematic=(bool)BodyKinematic.GetValue(body,null),Gravity=(bool)BodyGravity.GetValue(body,null)};
   Moving.Add(id,t);if(v.IsOwner()){RestoreIfNewOwner(c);SaveShields(c);}Freeze(t);
  }
  internal static void CompleteLocal(Player p){
   var trip=local;if(trip==null||trip.Player!=p||p.IsTeleporting())return;local=null;int i=0;
   foreach(var v in trip.Views){if(!v||!v.IsValid())continue;var c=v.GetComponent<Character>();if(!c||c.IsDead())continue;Vector3 pos;
    if(!FindLanding(p,c,i++,out pos))pos=p.transform.position+Vector3.up*0.2f;
    SendFinish(c,p,trip.Token,pos);
   }
  }
  static void SendFinish(Character c,Player p,string token,Vector3 pos){var v=c.GetComponent<ZNetView>();v.InvokeRPC(ZNetView.Everybody,FinishRpc,p.GetZDOID(),token,pos,p.transform.rotation);Finish(c,ZDOMan.GetSessionID(),p.GetZDOID(),token,pos,p.transform.rotation);}
  internal static void Finish(Character c,long sender,ZDOID leader,string token,Vector3 pos,Quaternion rot){
   Transit t;if(!IsSkeleton(c)||!Moving.TryGetValue(c.GetZDOID(),out t)||t.Done||c.IsDead()||t.Sender!=sender||t.Leader!=leader||t.Token!=token)return;
   if(!Finite(pos.x)||!Finite(pos.y)||!Finite(pos.z)||!Finite(rot.x)||!Finite(rot.y)||!Finite(rot.z)||!Finite(rot.w))return;
   t.Done=true;t.Finished=Now;t.Target=pos;t.Rotation=rot;
   if(t.V.IsOwner()){SaveShields(c);Move(t);t.V.GetZDO().SetOwner(sender);ZDOMan.instance.ForceSendZDO(t.V.GetZDO().m_uid);}else MoveVisual(t);
  }
  static void MoveVisual(Transit t){var body=Body.GetValue(t.C);t.C.transform.position=t.Target;t.C.transform.rotation=t.Rotation;BodyPosition.SetValue(body,t.Target,null);BodyRotation.SetValue(body,t.Rotation,null);Altitude.SetValue(t.C,t.Target.y);Freeze(t);}
  static void Move(Transit t){MoveVisual(t);var z=t.V.GetZDO();z.SetPosition(t.Target);z.SetRotation(t.Rotation);z.Set(ZDOVars.s_bodyVelocity,Vector3.zero);}
  static void Freeze(Transit t){if(!t.C)return;var b=Body.GetValue(t.C);if(!(bool)BodyKinematic.GetValue(b,null)){BodyVelocity.SetValue(b,Vector3.zero,null);BodyAngular.SetValue(b,Vector3.zero,null);}BodyGravity.SetValue(b,false,null);BodyKinematic.SetValue(b,true,null);}
  static void Release(Transit t){if(!t.C)return;var b=Body.GetValue(t.C);BodyGravity.SetValue(b,t.Gravity,null);BodyKinematic.SetValue(b,t.Kinematic,null);}
  internal static bool Held(Character c){return c&&Moving.ContainsKey(c.GetZDOID());}
  internal static void Tick(){
   if(local!=null){if(!local.Player||local.Player.IsDead()){Clear();return;}if(!local.Player.IsTeleporting())CompleteLocal(local.Player);}
   foreach(var item in Moving.ToArray()){var t=item.Value;bool bad=!t.C||!t.V||!t.V.IsValid()||t.C.IsDead();bool expired=Now-t.Started>120;
    bool ready=t.Done&&Now-t.Finished>1&&(!t.V.IsOwner()||(ZNetScene.instance&&ZNetScene.instance.IsAreaReady(t.Target)));
    if(bad||expired||ready){Release(t);Moving.Remove(item.Key);continue;}if(t.Done){if(t.V.IsOwner())Move(t);else MoveVisual(t);}else Freeze(t);
   }
   if(Now>=nextCheck){nextCheck=Now+0.5;RescueFollowers();}
  }
  internal static bool ShouldRecall(float gap,float height,double stalled){return stalled>=4&&gap>=8&&(height>=1.5f||gap>=12);}
  static void RescueFollowers(){
   var p=Player.m_localPlayer;if(!p||p.IsDead()||p.IsTeleporting()||!p.IsOnGround()||local!=null)return;
   var active=new HashSet<ZDOID>();int index=0;
   foreach(var c in Character.GetAllCharacters()){
    if(!Follows(c,p)||Held(c))continue;var id=c.GetZDOID();active.Add(id);float gap=Vector3.Distance(c.transform.position,p.transform.position);Progress state;
    if(!Followers.TryGetValue(id,out state)){Followers[id]=new Progress{Best=gap,LastProgress=Now,LastRecall=-99};continue;}
    if(gap<5||gap<state.Best-0.75f){state.Best=gap;state.LastProgress=Now;}
    // When the player moves farther away, measure progress from the new gap.
    if(gap>state.Best+3){state.Best=gap;}
    if(Now-state.LastRecall<8||!ShouldRecall(gap,Mathf.Abs(c.transform.position.y-p.transform.position.y),Now-state.LastProgress))continue;
    Vector3 pos;if(!FindLanding(p,c,index++,out pos))continue;
    string token=System.Guid.NewGuid().ToString("N");Begin(c,ZDOMan.GetSessionID(),p.GetZDOID(),token);c.GetComponent<ZNetView>().InvokeRPC(ZNetView.Everybody,BeginRpc,p.GetZDOID(),token);SendFinish(c,p,token,pos);
    state.LastRecall=Now;state.LastProgress=Now;state.Best=0;
   }
   foreach(var id in Followers.Keys.Where(id=>!active.Contains(id)&&!Moving.ContainsKey(id)).ToArray())Followers.Remove(id);
  }
  internal static bool FindLanding(Player p,Character c,int index,out Vector3 result){
   result=Vector3.zero;if(!ZoneSystem.instance||!ZNetScene.instance||!ZNetScene.instance.IsAreaReady(p.transform.position))return false;
   int mask=LayerMask.GetMask("Default","static_solid","Default_small","piece","terrain","vehicle");
   float radius=Mathf.Max(0.3f,c.GetRadius());float height=Mathf.Max(radius*2+0.2f,c.GetHeight());
   for(int i=0;i<12;i++){float a=(index*137.5f+180f+i*30f)*Mathf.Deg2Rad;float d=1.6f+(i/6)*0.8f;
    var pos=p.transform.position+new Vector3(Mathf.Sin(a)*d,1.5f,Mathf.Cos(a)*d);float floor;
    if(!ZoneSystem.instance.FindFloor(pos,out floor)||Mathf.Abs(floor-p.transform.position.y)>1.5f)continue;pos.y=floor+0.15f;
    var bottom=pos+Vector3.up*(radius+0.1f);var top=pos+Vector3.up*Mathf.Max(radius+0.1f,height-radius);
    if((bool)CheckCapsule.Invoke(null,new object[]{bottom,top,radius,mask,IgnoreTriggers}))continue;
    result=pos;return true;
   }return false;
  }
  internal static void Pin(List<ZDO> near){Tick();foreach(var t in Moving.Values)if(t.V&&t.V.IsValid()&&!near.Contains(t.V.GetZDO()))near.Add(t.V.GetZDO());foreach(var c in Character.GetAllCharacters())if(IsSkeleton(c)&&c.GetComponent<ZNetView>().IsOwner())SaveShields(c);}
  internal static void Clear(){foreach(var t in Moving.Values)Release(t);Moving.Clear();Followers.Clear();local=null;nextCheck=0;Shields=new ConditionalWeakTable<Character,ShieldState>();}
  internal static void RestoreIfNewOwner(Character c){if(!IsSkeleton(c))return;var v=c.GetComponent<ZNetView>();var state=Shields.GetOrCreateValue(c);bool owner=v.IsOwner();if(owner&&!state.WasOwner)RestoreShields(c);state.WasOwner=owner;}
  internal static void SaveShields(Character c){
   if(!IsSkeleton(c)||!c.GetComponent<ZNetView>().IsOwner())return;var rows=new List<string>{Now.ToString("R",Inv)};
   foreach(var s in c.GetSEMan().GetStatusEffects().OfType<SE_Shield>())rows.Add(string.Join(",",s.NameHash().ToString(Inv),s.m_ttl.ToString("R",Inv),s.GetDuration().ToString("R",Inv),((float)Total.GetValue(s)).ToString("R",Inv),((float)Damage.GetValue(s)).ToString("R",Inv)));
   c.GetComponent<ZNetView>().GetZDO().Set(ShieldKey,string.Join(";",rows.ToArray()));Shields.GetOrCreateValue(c).SavedAt=Now;
  }
  internal static void RestoreShields(Character c){
   string text=c.GetComponent<ZNetView>().GetZDO().GetString(ShieldKey);if(string.IsNullOrEmpty(text)||text.Length>4096)return;var rows=text.Split(';');double saved;
   if(!double.TryParse(rows[0],NumberStyles.Float,Inv,out saved)||double.IsNaN(saved)||double.IsInfinity(saved))return;var records=new List<ShieldRecord>();
   foreach(var row in rows.Skip(1)){ShieldRecord r;if(!ShieldRecord.TryParse(row,Now-saved,out r))return;records.Add(r);}
   var sm=c.GetSEMan();foreach(var old in sm.GetStatusEffects().OfType<SE_Shield>().ToArray())if(!records.Any(r=>r.Hash==old.NameHash()&&r.Alive))sm.RemoveStatusEffect(old,true);
   foreach(var r in records.Where(r=>r.Alive)){var s=sm.GetStatusEffect(r.Hash) as SE_Shield;if(!s){var template=ObjectDB.instance?ObjectDB.instance.GetStatusEffect(r.Hash) as SE_Shield:null;if(!template)continue;s=sm.AddStatusEffect(template,false) as SE_Shield;}if(!s)continue;s.m_ttl=r.Ttl;EffectTime.SetValue(s,r.Elapsed);Total.SetValue(s,r.Total);Damage.SetValue(s,r.Damage);}
  }
  internal sealed class ShieldRecord {
   internal int Hash;internal float Ttl,Elapsed,Total,Damage;internal bool Alive{get{return(Ttl<=0||Elapsed<=Ttl)&&Damage<=Total;}}
   internal static bool TryParse(string row,double age,out ShieldRecord r){r=null;var a=row.Split(',');int h;float t,e,n,d;
    if(a.Length!=5||!int.TryParse(a[0],NumberStyles.Integer,Inv,out h)||h==0||!float.TryParse(a[1],NumberStyles.Float,Inv,out t)||!float.TryParse(a[2],NumberStyles.Float,Inv,out e)||!float.TryParse(a[3],NumberStyles.Float,Inv,out n)||!float.TryParse(a[4],NumberStyles.Float,Inv,out d)||!Finite(t)||!Finite(e)||!Finite(n)||!Finite(d)||t<0||e<0||n<0||d<0||double.IsNaN(age)||double.IsInfinity(age)||age < -2||age>float.MaxValue)return false;
    r=new ShieldRecord{Hash=h,Ttl=t,Elapsed=e+(float)Math.Max(0,age),Total=n,Damage=d};return Finite(r.Elapsed);
   }
  }
  [HarmonyPatch(typeof(Character),"Awake")]static class RegisterPatch{[HarmonyPostfix]static void Postfix(Character __instance){Register(__instance);}}
  [HarmonyPatch(typeof(Player),"TeleportTo")]static class StartPatch{[HarmonyPostfix]static void Postfix(Player __instance,bool __result){if(__result)Start(__instance);}}
  [HarmonyPatch(typeof(Player),"UpdateTeleport")]static class FinishPatch{[HarmonyPostfix,HarmonyPriority(Priority.Last)]static void Postfix(Player __instance){CompleteLocal(__instance);}}
  [HarmonyPatch(typeof(ZNetScene),"RemoveObjects")]static class PinPatch{[HarmonyPrefix]static void Prefix(List<ZDO> currentNearObjects){Pin(currentNearObjects);}}
  [HarmonyPatch(typeof(Character),"CustomFixedUpdate")]static class OwnerPatch{[HarmonyPrefix]static void Prefix(Character __instance){RestoreIfNewOwner(__instance);}[HarmonyPostfix]static void Postfix(Character __instance){Transit t;if(__instance&&Moving.TryGetValue(__instance.GetZDOID(),out t))Freeze(t);}}
  [HarmonyPatch(typeof(Tameable),"UpdateSummon")]static class DistancePatch{[HarmonyPrefix]static bool Prefix(Tameable __instance){return !Held(__instance.GetComponent<Character>());}}
  [HarmonyPatch(typeof(Tameable),"UpdateSavedFollowTarget")]static class LogoutPatch{[HarmonyPrefix]static bool Prefix(Tameable __instance){return !Held(__instance.GetComponent<Character>());}}
  [HarmonyPatch(typeof(MonsterAI),"UpdateAI")]static class AiPatch{[HarmonyPrefix]static bool Prefix(MonsterAI __instance){return !Held(__instance.GetComponent<Character>());}}
  [HarmonyPatch(typeof(Character),"UpdateMotion")]static class MotionPatch{[HarmonyPrefix]static bool Prefix(Character __instance){return !Held(__instance);}}
  [HarmonyPatch(typeof(Character),"UnderWorldCheck")]static class FloorPatch{[HarmonyPrefix]static bool Prefix(Character __instance){return !Held(__instance);}}
  [HarmonyPatch(typeof(SEMan),"Update")]static class SavePatch{[HarmonyPostfix]static void Postfix(SEMan __instance){var c=ManagerCharacter.GetValue(__instance) as Character;if(IsSkeleton(c)&&Now-Shields.GetOrCreateValue(c).SavedAt>=0.25)SaveShields(c);}}
  [HarmonyPatch(typeof(SE_Shield),"OnDamaged")]static class DamagePatch{[HarmonyPostfix]static void Postfix(SE_Shield __instance){SaveShields(EffectCharacter.GetValue(__instance) as Character);}}
 }
}
