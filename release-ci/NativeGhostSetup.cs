using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;
[System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLink { }
[System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("000214F9-0000-0000-C000-000000000046"), System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW {
 void GetPath(System.Text.StringBuilder file,int count,IntPtr data,uint flags);
 void GetIDList(out IntPtr list);void SetIDList(IntPtr list);
 void GetDescription(System.Text.StringBuilder text,int count);void SetDescription([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]string text);
 void GetWorkingDirectory(System.Text.StringBuilder text,int count);void SetWorkingDirectory([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]string text);
 void GetArguments(System.Text.StringBuilder text,int count);void SetArguments([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]string text);
 void GetHotkey(out short key);void SetHotkey(short key);void GetShowCmd(out int cmd);void SetShowCmd(int cmd);
 void GetIconLocation(System.Text.StringBuilder file,int count,out int index);void SetIconLocation([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]string file,int index);
 void SetRelativePath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]string file,uint reserved);void Resolve(IntPtr window,uint flags);
 void SetPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]string file);
}
internal sealed class Release {
 public string version { get; set; }
 public string downloadUrl { get; set; }
 public string sha256 { get; set; }
 public long bytes { get; set; }
}
internal sealed class DownloadClient : WebClient {
 protected override WebRequest GetWebRequest(Uri address) { var request=base.GetWebRequest(address);request.Timeout=900000;return request; }
}
internal static class SetupCore {
 internal const string Feed="https://sukgicalm-lang.github.io/ghost-valheim-vault/client-release.json";
 internal const string Releases="https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/";
 internal static void Validate(Release r) {
  if(r==null || !System.Text.RegularExpressions.Regex.IsMatch(r.version??"",@"^\d+\.\d+\.\d+$") || !System.Text.RegularExpressions.Regex.IsMatch(r.sha256??"","^[a-f0-9]{64}$") || r.bytes<=0 || r.bytes>536870912 || r.downloadUrl!=Releases+"client-"+r.version+"/GhostR2-Windows-"+r.version+".zip") throw new InvalidDataException("공식 런처 배포 정보를 확인할 수 없습니다.");
 }
 internal static string Hash(string file) { using(var s=File.OpenRead(file))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant(); }
 internal static string Install(string zip,Release r,string installBase) {
  Validate(r);
  if(new FileInfo(zip).Length!=r.bytes || Hash(zip)!=r.sha256)throw new InvalidDataException("다운로드 파일 검증에 실패했습니다. 다시 시도해 주세요.");
  Directory.CreateDirectory(installBase);
  string staging=Path.Combine(installBase,".staging-"+Guid.NewGuid().ToString("N")),destination=Path.Combine(installBase,r.version),backup=null;
  try {
   using(var a=ZipFile.OpenRead(zip))foreach(var e in a.Entries){string n=e.FullName.Replace('\\','/');if(n.StartsWith("/")||n.Contains(":")||n.Split('/').Contains(".."))throw new InvalidDataException("런처 ZIP에 잘못된 경로가 있습니다.");}
   ZipFile.ExtractToDirectory(zip,staging);
   if(!File.Exists(Path.Combine(staging,"GhostR2.exe"))||!File.Exists(Path.Combine(staging,"resources","app.asar")))throw new InvalidDataException("런처 실행에 필요한 파일이 없습니다.");
   if(Directory.Exists(destination)){backup=destination+".previous-"+Guid.NewGuid().ToString("N");Directory.Move(destination,backup);}
   try{Directory.Move(staging,destination);}catch{if(backup!=null&&!Directory.Exists(destination))Directory.Move(backup,destination);throw;}
   return Path.Combine(destination,"GhostR2.exe");
  }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
 }
 internal static void Links(string exe){
  using(var p=Registry.CurrentUser.CreateSubKey(@"Software\Classes\ghostvault")){p.SetValue("","URL:Ghost Vault install link");p.SetValue("URL Protocol","");using(var c=p.CreateSubKey(@"shell\open\command"))c.SetValue("","\""+exe+"\" \"%1\"");}
  string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolderOption.Create);
  if(string.IsNullOrEmpty(desktop))desktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Desktop");
  Directory.CreateDirectory(desktop);
  var link=(IShellLinkW)new ShellLink();
  try{link.SetPath(exe);link.SetWorkingDirectory(Path.GetDirectoryName(exe));link.SetDescription("Ghost r2modman");((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Save(Path.Combine(desktop,"Ghost r2modman.lnk"),true);}
  finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);}
 }
 internal static void Log(string file,string message){
  try{using(var stream=new FileStream(file,FileMode.Append,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete))using(var writer=new StreamWriter(stream))writer.WriteLine(DateTime.UtcNow.ToString("o")+" "+message);}catch{}
 }
 internal static int ApplyUpdate(string[] args){
  string installBase=Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"),"GhostR2"),record=Path.Combine(installBase,"self-update-result.json");
  int parentId=0;string version=args[5],current=args[6],log=args[9];
  try{
   if(!int.TryParse(args[1],out parentId)||parentId<=0||!File.Exists(current))throw new InvalidDataException("Invalid update parent");
   long length;if(!long.TryParse(args[4],out length))throw new InvalidDataException("Invalid update size");
   var r=new Release{version=version,sha256=args[3],bytes=length,downloadUrl=Releases+"client-"+version+"/GhostR2-Windows-"+version+".zip"};Validate(r);
   if(new FileInfo(args[2]).Length!=r.bytes||Hash(args[2])!=r.sha256)throw new InvalidDataException("Update SHA-256 mismatch");
   Directory.CreateDirectory(installBase);Log(log,"ready pid="+Process.GetCurrentProcess().Id+" parent="+parentId+" base="+installBase);File.WriteAllText(args[7],"ready");
   Process parent=null;try{parent=Process.GetProcessById(parentId);}catch(ArgumentException){}
   if(parent!=null)using(parent){if(!parent.WaitForExit(60000))throw new TimeoutException("Launcher did not exit");}
   Log(log,"parent exited; installing");string exe=Install(args[2],r,installBase);Log(log,"installed; setting links");Links(exe);Log(log,"links ready; starting "+exe);
   var start=new ProcessStartInfo(exe){WorkingDirectory=Path.GetDirectoryName(exe),UseShellExecute=true};
   if(System.Text.RegularExpressions.Regex.IsMatch(args[8],@"^ghostvault://v1/install/(Ghost-GhostValheimSuite|Ghost-GhostSmelterControl)/(latest|\d+\.\d+\.\d+)/?$"))start.Arguments=args[8];
   Process.Start(start);File.WriteAllText(record,new JavaScriptSerializer().Serialize(new{status="success",version=version,time=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}));
   try{File.Delete(args[2]);}catch{}return 0;
  }catch(Exception e){
   Log(log,e.ToString());try{Directory.CreateDirectory(installBase);File.WriteAllText(record,new JavaScriptSerializer().Serialize(new{status="failed",version=version,time=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),message=e.Message,log=log}));}catch{}
   bool alive=false;try{using(var parent=Process.GetProcessById(parentId))alive=!parent.HasExited;}catch{}
   if(!alive&&File.Exists(current))try{Links(current);Process.Start(new ProcessStartInfo(current){WorkingDirectory=Path.GetDirectoryName(current),UseShellExecute=true});}catch{}
   return 1;
  }
 }
 internal static int SelfTest(){
  string root=Path.Combine(Path.GetTempPath(),"Ghost 설치 검사 "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   string source=Path.Combine(root,"source"),zip=Path.Combine(root,"runtime.zip"),target=Path.Combine(root,"install");
   Directory.CreateDirectory(Path.Combine(source,"resources"));File.WriteAllText(Path.Combine(source,"GhostR2.exe"),"test");File.WriteAllText(Path.Combine(source,"resources","app.asar"),"test");ZipFile.CreateFromDirectory(source,zip);
   var r=new Release{version="1.0.12",downloadUrl=Releases+"client-1.0.12/GhostR2-Windows-1.0.12.zip",bytes=new FileInfo(zip).Length,sha256=Hash(zip)};
   string exe=Install(zip,r,target);if(!File.Exists(exe))throw new Exception("Installation missing");
   File.WriteAllText(Path.Combine(target,"keep.cfg"),"preserved");Install(zip,r,target);
   if(File.ReadAllText(Path.Combine(target,"keep.cfg"))!="preserved"||Directory.GetDirectories(target,"*.previous-*").Length!=1)throw new Exception("Replacement failed");
   r.sha256=new string('0',64);bool rejected=false;try{Install(zip,r,target);}catch(InvalidDataException){rejected=true;}if(!rejected||!File.Exists(exe))throw new Exception("Corrupt update accepted");
   string bad=Path.Combine(root,"bad.zip");using(var a=ZipFile.Open(bad,ZipArchiveMode.Create))using(var w=new StreamWriter(a.CreateEntry("../outside.txt").Open()))w.Write("bad");
   r.sha256=Hash(bad);r.bytes=new FileInfo(bad).Length;rejected=false;try{Install(bad,r,target);}catch(InvalidDataException){rejected=true;}if(!rejected||File.Exists(Path.Combine(root,"outside.txt")))throw new Exception("Traversal accepted");
   r.downloadUrl="https://example.com/runtime.zip";rejected=false;try{Validate(r);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Unowned URL accepted");return 0;
  }catch(Exception e){File.WriteAllText(Path.Combine(Path.GetTempPath(),"GhostSetup-self-test-error.txt"),e.ToString());return 1;}finally{Directory.Delete(root,true);}
 }
}
internal sealed class SetupWindow:Form{
 readonly Label status=new Label(),detail=new Label();readonly ProgressBar progress=new ProgressBar();readonly Button retry=new Button();bool busy;
 internal SetupWindow(){
  Text="Ghost r2 설치";ClientSize=new Size(560,280);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
  BackColor=Color.FromArgb(15,27,23);ForeColor=Color.FromArgb(235,230,210);Font=new Font("Malgun Gothic",10);
  var brand=new Label{Text="GHOST R2",Left=28,Top=24,Width=480,Height=28,ForeColor=Color.FromArgb(206,174,91),Font=new Font("Segoe UI",12,FontStyle.Bold)};
  status.SetBounds(28,67,500,55);status.Font=new Font("Malgun Gothic",14,FontStyle.Bold);progress.SetBounds(28,133,500,14);progress.Style=ProgressBarStyle.Marquee;
  detail.SetBounds(28,163,500,58);detail.Text="설치가 끝나면 런처가 열리고 필수 모드 구성을 자동으로 준비합니다.";
  retry.SetBounds(398,233,130,32);retry.Text="다시 시도";retry.Visible=false;retry.Click+=async(s,e)=>await Run();Controls.AddRange(new Control[]{brand,status,progress,detail,retry});
  Shown+=async(s,e)=>await Run();FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};
 }
 async Task Run(){
  busy=true;retry.Visible=false;progress.Style=ProgressBarStyle.Marquee;string temp=Path.Combine(Path.GetTempPath(),"GhostR2-setup-"+Guid.NewGuid().ToString("N"));
  try{
   Directory.CreateDirectory(temp);status.Text="최신 런처를 확인하고 있습니다.";ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;Release r;
   using(var client=new DownloadClient()){
    client.Headers[HttpRequestHeader.UserAgent]="GhostR2-Setup/1.0.12";string json=await client.DownloadStringTaskAsync(new Uri(SetupCore.Feed+"?setup="+Guid.NewGuid().ToString("N")));r=new JavaScriptSerializer().Deserialize<Release>(json);SetupCore.Validate(r);
    status.Text="Ghost r2 "+r.version+" 다운로드 중";progress.Style=ProgressBarStyle.Continuous;
    client.DownloadProgressChanged+=(s,e)=>{progress.Value=Math.Max(0,Math.Min(100,e.ProgressPercentage));detail.Text="다운로드 "+(e.BytesReceived/1048576)+" MB / "+(r.bytes/1048576)+" MB · 설치 후 필수 모드를 자동 준비합니다.";};
    await client.DownloadFileTaskAsync(new Uri(r.downloadUrl),Path.Combine(temp,"runtime.zip"));
   }
   status.Text="파일을 검증하고 있습니다.";progress.Style=ProgressBarStyle.Marquee;string zip=Path.Combine(temp,"runtime.zip");await Task.Run(()=>{if(new FileInfo(zip).Length!=r.bytes||SetupCore.Hash(zip)!=r.sha256)throw new InvalidDataException("다운로드 파일 검증에 실패했습니다.");});
   status.Text="런처를 설치하고 있습니다.";
   foreach(var p in Process.GetProcessesByName("GhostR2"))using(p){try{p.CloseMainWindow();}catch(InvalidOperationException){}}
   var until=DateTime.UtcNow.AddMinutes(3);
   for(;;){var running=Process.GetProcessesByName("GhostR2");bool any=running.Length!=0;foreach(var p in running)p.Dispose();if(!any)break;
    status.Text="열려 있는 Ghost 런처를 종료해 주세요.";detail.Text="런처가 종료되면 설치를 자동으로 이어갑니다. 기존 프로필과 모드 설정은 유지됩니다.";if(DateTime.UtcNow>until)throw new IOException("Ghost 런처를 종료한 뒤 다시 시도를 눌러 주세요.");await Task.Delay(500);
   }
   string installBase=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GhostR2");string exe=await Task.Run(()=>SetupCore.Install(zip,r,installBase));SetupCore.Links(exe);
   status.Text="설치 완료. 런처를 시작합니다.";Process.Start(new ProcessStartInfo(exe){WorkingDirectory=Path.GetDirectoryName(exe),UseShellExecute=true});busy=false;Close();
  }catch(Exception e){
   try{string logs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GhostR2","Logs");Directory.CreateDirectory(logs);File.WriteAllText(Path.Combine(logs,"setup-error.log"),e.ToString());}catch{}
   status.Text="설치를 완료하지 못했습니다.";detail.Text=e.Message;retry.Visible=true;progress.Style=ProgressBarStyle.Continuous;progress.Value=0;
  }finally{busy=false;try{Directory.Delete(temp,true);}catch{}}
 }
}
internal static class Program{
 [STAThread]static int Main(string[] args){if(args.Length==10&&args[0]=="--apply-update")return SetupCore.ApplyUpdate(args);if(args.Length==1&&args[0]=="--self-test")return SetupCore.SelfTest();bool created;using(var mutex=new Mutex(true,"Local\\GhostR2Setup",out created)){if(!created)return 0;Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new SetupWindow());return 0;}}
}
