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
[assembly: AssemblyVersion("1.0.3.0")]
[assembly: AssemblyFileVersion("1.0.3.0")]
namespace GhostSummonTravel {
 [BepInPlugin(Guid,"Ghost 해골 동행·방어막 표시","1.0.3")]
 public sealed class Plugin:BaseUnityPlugin {
  public const string Guid="ghost.valheim.summontravel";Harmony harmony;internal static Plugin Instance;
  void Awake(){Instance=this;Travel.Faulted=false;Travel.ValidateApi();ShieldPresentation.ValidateApi();IdleSpacing.ValidateApi();PlayerShieldBar.ValidateApi();harmony=new Harmony(Guid);try{harmony.PatchAll(typeof(Plugin).Assembly);}catch{harmony.UnpatchSelf();throw;}Logger.LogInfo("해골 동행 1.0.3: 검은 로딩 화면 충돌 수정·순간이동 반복 작업 제한·내 HP 옆 방어막 막대");}
  internal static void LogFault(Exception e){if(Instance)Instance.Logger.LogError("해골 자동 동행 오류로 해당 기능을 중단하고 기본 게임 이동을 유지합니다. 게임 재시작 전 로그를 보관하세요. "+e);}
  void Update(){try{Travel.Tick();}catch(Exception e){Travel.Abort(e);}}void OnDestroy(){Travel.Clear();if(harmony!=null)harmony.UnpatchSelf();Instance=null;}
 }
 internal static class Travel {
  internal static bool Faulted;
  internal static void Abort(Exception error){if(Faulted)return;Faulted=true;try{Clear();}catch{Moving.Clear();local=null;}Plugin.LogFault(error);}
  internal const string BeginRpc="GhostSummonTravelBeginV1",FinishRpc="GhostSummonTravelFinishV1";
  const string ShieldKey="ghost.summontravel.shields.v1";
  static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
  static readonly Dictionary<ZDOID,Transit> Moving=new Dictionary<ZDOID,Transit>();
  static readonly Dictionary<ZDOID,Progress> Followers=new Dictionary<ZDOID,Progress>();
  static ConditionalWeakTable<Character,ShieldState> Shields=new ConditionalWeakTable<Character,ShieldState>();
  static LocalTrip local;static double nextCheck;static int lastTickFrame=-1,lastArrivalFrame=-1,searchFrame=-1,searchProbes;
  static readonly List<ZDOID> Finished=new List<ZDOID>();
  static ConditionalWeakTable<Character,CharacterKind> Kinds=new ConditionalWeakTable<Character,CharacterKind>();
  static ConditionalWeakTable<Character,SearchState[]> Searches=new ConditionalWeakTable<Character,SearchState[]>();
  sealed class CharacterKind {internal bool Skeleton;internal ZNetView View;}
  sealed class SearchState {internal int Cursor;internal Vector3 Anchor;internal bool Started;}
  static readonly float[] LandingAngles={180,150,210,120,240,90,270},EscapeAngles={0,30,-30,60,-60,90,-90};
  static readonly int SkeletonPrefab=StableHash("Skeleton_Friendly");
  static readonly FieldInfo Total=AccessTools.Field(typeof(SE_Shield),"m_totalAbsorbDamage"),Damage=AccessTools.Field(typeof(SE_Shield),"m_damage"),
   EffectTime=AccessTools.Field(typeof(StatusEffect),"m_time"),EffectCharacter=AccessTools.Field(typeof(StatusEffect),"m_character"),
   Body=AccessTools.Field(typeof(Character),"m_body"),Altitude=AccessTools.Field(typeof(Character),"m_maxAirAltitude"),ManagerCharacter=AccessTools.Field(typeof(SEMan),"m_character");
  static PropertyInfo BodyPosition,BodyRotation,BodyVelocity,BodyAngular,BodyKinematic,BodyGravity;
  static MethodInfo CheckCapsule;static object IgnoreTriggers;
  internal sealed class Transit {internal Character C;internal ZNetView V;internal ZDOID Leader;internal long Sender;internal string Token;internal double Started,Finished;internal bool Done,Kinematic,Gravity;internal Vector3 Target;internal Quaternion Rotation;}
  sealed class LocalTrip {internal Player Player;internal string Token;internal List<ZNetView> Views;internal double NextLandingAttempt;}
  sealed class ShieldState {internal bool WasOwner;internal double SavedAt=-1;}
  sealed class Progress {internal Vector3 Position;internal double StationarySince;internal bool Tracking;}
  internal static double Now {get{return ZNet.instance?ZNet.instance.GetTimeSeconds():0;}}
  internal static int StableHash(string s){unchecked{int a=5381,b=a;for(int i=0;i<s.Length&&s[i]!='\0';i+=2){a=((a<<5)+a)^s[i];if(i==s.Length-1||s[i+1]=='\0')break;b=((b<<5)+b)^s[i+1];}return a+b*1566083941;}}
  internal static bool Finite(float n){return !float.IsNaN(n)&&!float.IsInfinity(n);}
  internal static bool IsSkeleton(Character c){if(!c||c is Player)return false;CharacterKind kind;if(Kinds.TryGetValue(c,out kind))return kind.Skeleton&&kind.View&&kind.View.IsValid();var v=c.GetComponent<ZNetView>();if(!v||!v.IsValid()||v.GetZDO().GetPrefab()==0)return false;kind=new CharacterKind{View=v,Skeleton=v.GetZDO().GetPrefab()==SkeletonPrefab&&c.GetComponent<Tameable>()&&c.GetComponent<MonsterAI>()};Kinds.Add(c,kind);return kind.Skeleton;}
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
   if(Faulted||p!=Player.m_localPlayer||!p.IsTeleporting()||local!=null)return;
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
   var trip=local;if(trip==null||trip.Player!=p||p.IsTeleporting()||Now<trip.NextLandingAttempt||lastArrivalFrame==Time.frameCount)return;trip.NextLandingAttempt=Now+0.1;int i=0;
   foreach(var v in trip.Views.ToArray()){if(!v||!v.IsValid()){trip.Views.Remove(v);continue;}var c=v.GetComponent<Character>();if(!c||c.IsDead()){trip.Views.Remove(v);continue;}Vector3 pos;
    if(!FindLanding(p,c,i++,out pos))continue;
    SendFinish(c,p,trip.Token,pos);trip.Views.Remove(v);lastArrivalFrame=Time.frameCount;trip.NextLandingAttempt=Now;break;
   }if(trip.Views.Count==0)local=null;
  }
  static void SendFinish(Character c,Player p,string token,Vector3 pos){var v=c.GetComponent<ZNetView>();v.InvokeRPC(ZNetView.Everybody,FinishRpc,p.GetZDOID(),token,pos,p.transform.rotation);Finish(c,ZDOMan.GetSessionID(),p.GetZDOID(),token,pos,p.transform.rotation);}
  internal static void Finish(Character c,long sender,ZDOID leader,string token,Vector3 pos,Quaternion rot){
   Transit t;if(!IsSkeleton(c)||!Moving.TryGetValue(c.GetZDOID(),out t)||t.Done||c.IsDead()||t.Sender!=sender||t.Leader!=leader||t.Token!=token)return;
   if(!Finite(pos.x)||!Finite(pos.y)||!Finite(pos.z)||!Finite(rot.x)||!Finite(rot.y)||!Finite(rot.z)||!Finite(rot.w))return;
   t.Done=true;t.Finished=Now;t.Target=pos;t.Rotation=rot;
   if(t.V.IsOwner()){SaveShields(c);Move(t);ShieldPresentation.RequestRepair(c,false,0.15);t.V.GetZDO().SetOwner(sender);ZDOMan.instance.ForceSendZDO(t.V.GetZDO().m_uid);}else MoveVisual(t);
  }
  static void MoveVisual(Transit t){var body=Body.GetValue(t.C);t.C.transform.position=t.Target;t.C.transform.rotation=t.Rotation;BodyPosition.SetValue(body,t.Target,null);BodyRotation.SetValue(body,t.Rotation,null);Altitude.SetValue(t.C,t.Target.y);Freeze(t);}
  static void Move(Transit t){MoveVisual(t);var z=t.V.GetZDO();z.SetPosition(t.Target);z.SetRotation(t.Rotation);z.Set(ZDOVars.s_bodyVelocity,Vector3.zero);ShieldPresentation.MoveEffects(t.C);}
  static void Freeze(Transit t){if(!t.C)return;var b=Body.GetValue(t.C);if(!(bool)BodyKinematic.GetValue(b,null)){BodyVelocity.SetValue(b,Vector3.zero,null);BodyAngular.SetValue(b,Vector3.zero,null);}BodyGravity.SetValue(b,false,null);BodyKinematic.SetValue(b,true,null);}
  static void Release(Transit t){if(!t.C)return;var b=Body.GetValue(t.C);BodyGravity.SetValue(b,t.Gravity,null);BodyKinematic.SetValue(b,t.Kinematic,null);}
  internal static bool ProtectFollowDistance(Character c){if(Faulted||!IsSkeleton(c)||c.IsDead()||!c.IsTamed())return false;var go=c.GetComponent<MonsterAI>().GetFollowTarget();var p=go?go.GetComponent<Player>():null;return p&&!p.IsDead();}
  internal static bool Held(Character c){return Moving.Count>0&&c&&Moving.ContainsKey(c.GetZDOID());}
  internal static void Tick(){
   if(Faulted||lastTickFrame==Time.frameCount)return;lastTickFrame=Time.frameCount;IdleSpacing.RefreshSpeeds();
   if(local!=null){if(!local.Player||local.Player.IsDead()){Clear();return;}if(!local.Player.IsTeleporting())CompleteLocal(local.Player);}
   Finished.Clear();foreach(var item in Moving){var t=item.Value;bool bad=!t.C||!t.V||!t.V.IsValid()||t.C.IsDead();bool expired=Now-t.Started>120;
    bool ready=t.Done&&Now-t.Finished>1&&(!t.V.IsOwner()||(ZNetScene.instance&&ZNetScene.instance.IsAreaReady(t.Target)));
    if(bad||expired||ready){Release(t);Finished.Add(item.Key);continue;}if(t.Done&&Vector3.Distance(t.C.transform.position,t.Target)>0.05f){if(t.V.IsOwner())Move(t);else MoveVisual(t);}
   }foreach(var id in Finished)Moving.Remove(id);
   if(Now>=nextCheck){nextCheck=Now+0.5;RescueFollowers();foreach(var c in Character.GetAllCharacters())if(IsSkeleton(c))ShieldPresentation.RequestRepair(c,false,0);}
   ShieldPresentation.ProcessRepairs();
  }
  internal static bool ShouldRecall(float gap,double stuckSeconds){return gap>=100f||stuckSeconds>=10;}
  static void RescueFollowers(){
   var p=Player.m_localPlayer;if(!p||p.IsDead()||p.IsTeleporting()||!p.IsOnGround()||local!=null)return;
   var active=new HashSet<ZDOID>();int index=0;
   foreach(var c in Character.GetAllCharacters()){
    if(!Follows(c,p)||Held(c))continue;var id=c.GetZDOID();active.Add(id);float gap=Vector3.Distance(c.transform.position,p.transform.position);Progress state;
    if(!Followers.TryGetValue(id,out state))Followers[id]=state=new Progress{Position=c.transform.position,StationarySince=Now};
    float heightGap=Mathf.Abs(c.transform.position.y-p.transform.position.y);
    bool wantsMove=!c.GetComponent<MonsterAI>().HaveTarget()&&(IdleSpacing.Crowded(c,p)||IdleSpacing.HorizontalGap(c,p)>Mathf.Max(4f,IdleSpacing.Minimum(c,p)+2f)||(heightGap>=1.5f&&gap>=2.5f));
    if(!wantsMove||!state.Tracking||Vector3.Distance(c.transform.position,state.Position)>=0.5f){state.Position=c.transform.position;state.StationarySince=Now;}
    state.Tracking=wantsMove;double stuck=wantsMove?Now-state.StationarySince:0;
    if(!ShouldRecall(gap,stuck))continue;
    if(lastArrivalFrame==Time.frameCount)break;
    Vector3 pos;if(!FindLanding(p,c,index++,out pos))continue;
    string token=System.Guid.NewGuid().ToString("N");Begin(c,ZDOMan.GetSessionID(),p.GetZDOID(),token);c.GetComponent<ZNetView>().InvokeRPC(ZNetView.Everybody,BeginRpc,p.GetZDOID(),token);SendFinish(c,p,token,pos);
    state.Position=c.transform.position;state.StationarySince=Now;state.Tracking=false;lastArrivalFrame=Time.frameCount;
   }
   foreach(var id in Followers.Keys.Where(id=>!active.Contains(id)&&!Moving.ContainsKey(id)).ToArray())Followers.Remove(id);
  }
  internal static bool FindLanding(Player p,Character c,int index,out Vector3 result,float bodyClearance=1f,bool retreat=false){
   result=Vector3.zero;if(!ZoneSystem.instance||!ZNetScene.instance||!ZNetScene.instance.IsAreaReady(p.transform.position))return false;
   int mask=LayerMask.GetMask("Default","static_solid","Default_small","piece","terrain","vehicle","character","character_net");
   float radius=Mathf.Max(0.3f,c.GetRadius());float height=Mathf.Max(radius*2+0.2f,c.GetHeight());
   // Rear half only. Radius leaves at least one metre between the two bodies.
   float first=Mathf.Max(1.8f,p.GetRadius()+radius+Mathf.Max(1f,bodyClearance));
   float dx=c.transform.position.x-p.transform.position.x,dz=c.transform.position.z-p.transform.position.z;
   if(Math.Abs(dx)+Math.Abs(dz)<0.01f){var back=p.transform.rotation*new Vector3(0,0,-1);dx=back.x;dz=back.z;}
   if(searchFrame!=Time.frameCount){searchFrame=Time.frameCount;searchProbes=0;}
   SearchState[] states;if(!Searches.TryGetValue(c,out states)){states=new SearchState[28];Searches.Add(c,states);}
   int slot=(retreat?14:0)+(bodyClearance>1.1f?7:0)+((index%7)+7)%7;var state=states[slot];if(state==null){state=new SearchState();states[slot]=state;}
   if(!state.Started||Vector3.Distance(state.Anchor,p.transform.position)>0.5f){state.Cursor=0;state.Anchor=p.transform.position;state.Started=true;}
   int attempted=0;
   while(searchProbes<4&&attempted<35){int i=state.Cursor;state.Cursor=(state.Cursor+1)%35;searchProbes++;attempted++;float a=LandingAngles[(i+index)%LandingAngles.Length]*Mathf.Deg2Rad;float d=first+(i/LandingAngles.Length)*0.8f;
    Vector3 offset;
    if(retreat){a=(float)Math.Atan2(dx,dz)+EscapeAngles[i%EscapeAngles.Length]*Mathf.Deg2Rad;offset=new Vector3(Mathf.Sin(a)*d,0,Mathf.Cos(a)*d);}
    else offset=p.transform.rotation*new Vector3(Mathf.Sin(a)*d,0,Mathf.Cos(a)*d);
    var pos=p.transform.position+offset+Vector3.up*1.5f;float floor;
    if(!ZoneSystem.instance.FindFloor(pos,out floor)||Mathf.Abs(floor-p.transform.position.y)>1.5f)continue;pos.y=floor+0.15f;
    var bottom=pos+Vector3.up*(radius+0.1f);var top=pos+Vector3.up*Mathf.Max(radius+0.1f,height-radius);
    if((bool)CheckCapsule.Invoke(null,new object[]{bottom,top,radius,mask,IgnoreTriggers}))continue;
    state.Cursor=0;result=pos;return true;
   }return false;
  }
  internal static void Pin(List<ZDO> near){if(Faulted)return;foreach(var t in Moving.Values)if(t.V&&t.V.IsValid()){if(!near.Contains(t.V.GetZDO()))near.Add(t.V.GetZDO());ShieldPresentation.PinEffects(t.C,near);}}
  internal static void BeforeUnload(ZNetView view){if(!view||!view.IsValid()||!view.IsOwner()||view.GetZDO().GetPrefab()!=SkeletonPrefab)return;var c=view.GetComponent<Character>();if(IsSkeleton(c))SaveShields(c);}
  internal static void Clear(){foreach(var t in Moving.Values)try{Release(t);}catch{}Moving.Clear();Followers.Clear();Finished.Clear();local=null;nextCheck=0;lastTickFrame=lastArrivalFrame=searchFrame=-1;searchProbes=0;Shields=new ConditionalWeakTable<Character,ShieldState>();Kinds=new ConditionalWeakTable<Character,CharacterKind>();Searches=new ConditionalWeakTable<Character,SearchState[]>();ShieldPresentation.ClearHud();PlayerShieldBar.Clear();IdleSpacing.Clear();}
  internal static void RestoreIfNewOwner(Character c){if(!IsSkeleton(c))return;var v=c.GetComponent<ZNetView>();var state=Shields.GetOrCreateValue(c);bool owner=v.IsOwner();if(owner&&!state.WasOwner)RestoreShields(c);state.WasOwner=owner;}
  internal static void SaveShields(Character c){
   if(!IsSkeleton(c)||!c.GetComponent<ZNetView>().IsOwner())return;var rows=new List<string>{Now.ToString("R",Inv)};
   foreach(var s in c.GetSEMan().GetStatusEffects().OfType<SE_Shield>())rows.Add(string.Join(",",s.NameHash().ToString(Inv),s.m_ttl.ToString("R",Inv),s.GetDuration().ToString("R",Inv),((float)Total.GetValue(s)).ToString("R",Inv),((float)Damage.GetValue(s)).ToString("R",Inv)));
   c.GetComponent<ZNetView>().GetZDO().Set(ShieldKey,string.Join(";",rows.ToArray()));Shields.GetOrCreateValue(c).SavedAt=Now;
  }
  internal static bool ReadShieldRecords(Character c,out List<ShieldRecord> records){
   records=new List<ShieldRecord>();if(!IsSkeleton(c))return false;
   string text=c.GetComponent<ZNetView>().GetZDO().GetString(ShieldKey);if(string.IsNullOrEmpty(text)||text.Length>4096)return false;
   var rows=text.Split(';');double saved;if(!double.TryParse(rows[0],NumberStyles.Float,Inv,out saved)||double.IsNaN(saved)||double.IsInfinity(saved))return false;
   foreach(var row in rows.Skip(1)){ShieldRecord r;if(!ShieldRecord.TryParse(row,Now-saved,out r))return false;records.Add(r);}return true;
  }
  internal static bool ShieldAmounts(Character c,out float remaining,out float capacity){
   remaining=0;capacity=0;if(!c)return false;bool self=c is Player&&c==Player.m_localPlayer;if(!self&&!IsSkeleton(c))return false;
   if(self||c.GetComponent<ZNetView>().IsOwner()){
    foreach(var s in c.GetSEMan().GetStatusEffects().OfType<SE_Shield>()){
     float value,total;if(!ActiveShield(s,out value,out total))continue;
     capacity+=total;remaining+=value;
    }return true;
   }
   List<ShieldRecord> records;if(!ReadShieldRecords(c,out records))return false;
   foreach(var r in records.Where(r=>r.Alive)){capacity+=r.Total;remaining+=Math.Max(0,r.Total-r.Damage);}return true;
  }
  internal static bool ActiveShield(SE_Shield s,out float remaining,out float capacity){
   capacity=(float)Total.GetValue(s);float damage=(float)Damage.GetValue(s);remaining=0;
   if(!Finite(capacity)||!Finite(damage)||capacity<0||damage<0||(s.m_ttl>0&&s.GetDuration()>s.m_ttl)||damage>capacity)return false;
   remaining=Math.Max(0,capacity-damage);return true;
  }
  internal static void RestoreShields(Character c){
   List<ShieldRecord> records;if(!ReadShieldRecords(c,out records))return;
   var sm=c.GetSEMan();foreach(var old in sm.GetStatusEffects().OfType<SE_Shield>().ToArray())if(!records.Any(r=>r.Hash==old.NameHash()&&r.Alive))sm.RemoveStatusEffect(old,true);
   foreach(var r in records.Where(r=>r.Alive)){var s=sm.GetStatusEffect(r.Hash) as SE_Shield;if(!s){var template=ObjectDB.instance?ObjectDB.instance.GetStatusEffect(r.Hash) as SE_Shield:null;if(!template)continue;s=sm.AddStatusEffect(template,false) as SE_Shield;}if(!s)continue;s.m_ttl=r.Ttl;EffectTime.SetValue(s,r.Elapsed);Total.SetValue(s,r.Total);Damage.SetValue(s,r.Damage);}
   ShieldPresentation.RequestRepair(c,false,0.1);
  }
  internal sealed class ShieldRecord {
   internal int Hash;internal float Ttl,Elapsed,Total,Damage;internal bool Alive{get{return(Ttl<=0||Elapsed<=Ttl)&&Damage<=Total;}}
   internal static bool TryParse(string row,double age,out ShieldRecord r){r=null;var a=row.Split(',');int h;float t,e,n,d;
    if(a.Length!=5||!int.TryParse(a[0],NumberStyles.Integer,Inv,out h)||h==0||!float.TryParse(a[1],NumberStyles.Float,Inv,out t)||!float.TryParse(a[2],NumberStyles.Float,Inv,out e)||!float.TryParse(a[3],NumberStyles.Float,Inv,out n)||!float.TryParse(a[4],NumberStyles.Float,Inv,out d)||!Finite(t)||!Finite(e)||!Finite(n)||!Finite(d)||t<0||e<0||n<0||d<0||double.IsNaN(age)||double.IsInfinity(age)||age < -2||age>float.MaxValue)return false;
    r=new ShieldRecord{Hash=h,Ttl=t,Elapsed=e+(float)Math.Max(0,age),Total=n,Damage=d};return Finite(r.Elapsed);
   }
  }
  [HarmonyPatch(typeof(Character),"Awake")]static class RegisterPatch{[HarmonyPostfix]static void Postfix(Character __instance){try{Register(__instance);}catch(Exception e){Abort(e);}}}
  [HarmonyPatch(typeof(Player),"TeleportTo")]static class StartPatch{[HarmonyPostfix]static void Postfix(Player __instance,bool __result){try{if(__result)Start(__instance);}catch(Exception e){Abort(e);}}}
  [HarmonyPatch(typeof(Player),"UpdateTeleport")]static class FinishPatch{[HarmonyPostfix,HarmonyPriority(Priority.Last)]static void Postfix(Player __instance){try{if(!Faulted)CompleteLocal(__instance);}catch(Exception e){Abort(e);}}}
  [HarmonyPatch(typeof(ZNetScene),"RemoveObjects")]static class PinPatch{[HarmonyPrefix]static void Prefix(List<ZDO> currentNearObjects){try{Pin(currentNearObjects);}catch(Exception e){Abort(e);}}}
  [HarmonyPatch(typeof(ZNetView),"ResetZDO")]static class UnloadPatch{[HarmonyPrefix]static void Prefix(ZNetView __instance){try{BeforeUnload(__instance);}catch(Exception e){Abort(e);}}}
  [HarmonyPatch(typeof(Character),"CustomFixedUpdate")]static class OwnerPatch{[HarmonyPrefix]static void Prefix(Character __instance){try{if(!Faulted)RestoreIfNewOwner(__instance);}catch(Exception e){Abort(e);}}[HarmonyPostfix]static void Postfix(Character __instance){try{Transit t;if(Moving.Count>0&&__instance&&Moving.TryGetValue(__instance.GetZDOID(),out t))Freeze(t);}catch(Exception e){Abort(e);}}}
  [HarmonyPatch(typeof(Tameable),"UpdateSummon")]static class DistancePatch{[HarmonyPrefix]static bool Prefix(Tameable __instance){try{var c=__instance.GetComponent<Character>();return !Held(c)&&!ProtectFollowDistance(c);}catch(Exception e){Abort(e);return true;}}}
  [HarmonyPatch(typeof(Tameable),"UpdateSavedFollowTarget")]static class LogoutPatch{[HarmonyPrefix]static bool Prefix(Tameable __instance){return !Held(__instance.GetComponent<Character>());}}
  [HarmonyPatch(typeof(MonsterAI),"UpdateAI")]static class AiPatch{[HarmonyPrefix]static bool Prefix(MonsterAI __instance){try{IdleSpacing.UpdateSpeed(__instance);return !Held(__instance.GetComponent<Character>());}catch(Exception e){Abort(e);return true;}}}
  [HarmonyPatch(typeof(Character),"UpdateMotion")]static class MotionPatch{[HarmonyPrefix]static bool Prefix(Character __instance){return !Held(__instance);}}
  [HarmonyPatch(typeof(Character),"UnderWorldCheck")]static class FloorPatch{[HarmonyPrefix]static bool Prefix(Character __instance){return !Held(__instance);}}
  [HarmonyPatch(typeof(SEMan),"Update")]static class SavePatch{[HarmonyPostfix]static void Postfix(SEMan __instance){try{var c=ManagerCharacter.GetValue(__instance) as Character;if(IsSkeleton(c)&&Now-Shields.GetOrCreateValue(c).SavedAt>=0.1)SaveShields(c);}catch(Exception e){Abort(e);}}}
  [HarmonyPatch(typeof(SE_Shield),"OnDamaged")]static class DamagePatch{[HarmonyPostfix]static void Postfix(SE_Shield __instance){try{var c=EffectCharacter.GetValue(__instance) as Character;SaveShields(c);if(IsSkeleton(c)&&c.GetComponent<ZNetView>().IsOwner())ZDOMan.instance.ForceSendZDO(c.GetZDOID());}catch(Exception e){Abort(e);}}}
  [HarmonyPatch(typeof(SEMan),"Internal_AddStatusEffect")]static class RecastPatch{[HarmonyPostfix]static void Postfix(SEMan __instance,int nameHash){try{var c=ManagerCharacter.GetValue(__instance) as Character;if(!IsSkeleton(c)||!c.GetComponent<ZNetView>().IsOwner()||!(__instance.GetStatusEffect(nameHash) is SE_Shield))return;ShieldPresentation.RequestRepair(c,true,0.05);SaveShields(c);ZDOMan.instance.ForceSendZDO(c.GetZDOID());}catch(Exception e){Abort(e);}}}
 }
}
