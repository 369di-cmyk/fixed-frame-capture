using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FixedCapture {
 [DataContract] public class Settings {
  [DataMember] public int X=100;
  [DataMember] public int Y=100;
  [DataMember] public int Width=800;
  [DataMember] public int Height=600;
  [DataMember] public bool HasRegion=false;
  [DataMember] public string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"定框截图");
  [DataMember] public string Template="{date}_{n}";
  [DataMember] public long Next=1;
  [DataMember] public int Digits=1;
  [DataMember] public int Modifiers=3;
  [DataMember] public int Key=(int)Keys.S;
  [DataMember] public int IntervalSeconds=10;
  [DataMember] public int AutoModifiers=3;
  [DataMember] public int AutoKey=(int)Keys.F9;
  [DataMember] public string Theme="Ocean";
  [DataMember] public bool SkipDuplicates=true;
  [DataMember] public bool CaptureSound=true;
  [DataMember] public bool CaptureNotice=true;
  [OnDeserializing] void Defaults(StreamingContext context){Theme="Ocean";IntervalSeconds=10;AutoModifiers=3;AutoKey=(int)Keys.F9;SkipDuplicates=true;CaptureSound=true;CaptureNotice=true;}
  public Rectangle Region { get { return new Rectangle(X,Y,Width,Height); } }
 }
 static class Store {
  public static string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FixedCapture");
  public static string FilePath { get { return Path.Combine(Root,"settings.json"); } }
  public static Settings Load() {
   if(!File.Exists(FilePath)) return new Settings();
   using(var f=File.OpenRead(FilePath)) return (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(f);
  }
  public static void Save(Settings s) {
   Directory.CreateDirectory(Root);
   string temp=FilePath+".tmp";
   using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {new DataContractJsonSerializer(typeof(Settings)).WriteObject(f,s); f.Flush(true);}
   if(File.Exists(FilePath)) File.Replace(temp,FilePath,FilePath+".bak"); else File.Move(temp,FilePath);
  }
 }
 static class CaptureFiles {
  public static string Name(Settings s, long n, DateTime now) {
   if(String.IsNullOrWhiteSpace(s.Template)||!s.Template.Contains("{n}")) throw new Exception("命名模板必须包含 {n}（连续编号）。");
   string name=s.Template.Replace("{date}",now.ToString("yyyyMMdd",CultureInfo.InvariantCulture)).Replace("{time}",now.ToString("HHmmss",CultureInfo.InvariantCulture)).Replace("{n}",n.ToString("D"+s.Digits,CultureInfo.InvariantCulture));
   if(name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.Contains("{")||name.Contains("}")||name.EndsWith(".")||name.EndsWith(" ")||name.Length>180) throw new Exception("文件名无效。支持 {date}、{time}、{n}，不能包含路径符号。");
   string stem=name.Split('.')[0].ToUpperInvariant();
   if(System.Text.RegularExpressions.Regex.IsMatch(stem,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$")) throw new Exception("文件名使用了 Windows 保留名称。");
   return name+".png";
  }
  public static string Save(Bitmap bitmap,Settings s) {
   Directory.CreateDirectory(s.Folder);
   DateTime now=DateTime.Now;
   while(s.Next<999999999999L) {
    long current=s.Next; string path=Path.Combine(s.Folder,Name(s,current,now));
    FileStream stream;
    try { stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None); }
    catch(IOException) { if(File.Exists(path)) {s.Next++;continue;} throw; }
    try {using(stream) bitmap.Save(stream,ImageFormat.Png);}
    catch {try {File.Delete(path);} catch {} throw;}
    s.Next=current+1; return path;
   }
   throw new Exception("编号已到上限，请设置新的起始编号。");
  }
 }
 static class Native {
  [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam);
 }
 class RegionPicker:Form {
  Bitmap desktop; Rectangle screen; Rectangle selection; Point anchor; bool dragging; bool fixedSize; Size size;
  public Rectangle Selected;
  public RegionPicker(bool fixedMode,Size requested) {
   Text="定框截图 - 选择区域";fixedSize=fixedMode; size=requested; screen=SystemInformation.VirtualScreen;
   desktop=new Bitmap(screen.Width,screen.Height);
   using(Graphics g=Graphics.FromImage(desktop)) g.CopyFromScreen(screen.Location,Point.Empty,screen.Size);
   FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.Manual; Bounds=screen;
   TopMost=true; ShowInTaskbar=true; DoubleBuffered=true; KeyPreview=true; Cursor=Cursors.Cross;
   KeyDown+=(o,e)=>{if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}};
   MouseDown+=(o,e)=>{if(e.Button==MouseButtons.Right){DialogResult=DialogResult.Cancel;Close();return;} if(e.Button!=MouseButtons.Left)return; anchor=e.Location;dragging=true;Capture=true;UpdateSelection(e.Location);};
   MouseMove+=(o,e)=>{if(fixedSize||dragging)UpdateSelection(e.Location);};
   MouseUp+=(o,e)=>{if(e.Button!=MouseButtons.Left||!dragging)return;UpdateSelection(e.Location);dragging=false;Capture=false;if(selection.Width>0&&selection.Height>0){Selected=new Rectangle(selection.X+screen.X,selection.Y+screen.Y,selection.Width,selection.Height);DialogResult=DialogResult.OK;Close();}};
  }
  void UpdateSelection(Point p) {
   p.X=Math.Max(0,Math.Min(screen.Width,p.X));p.Y=Math.Max(0,Math.Min(screen.Height,p.Y));
   if(fixedSize) selection=new Rectangle(Math.Max(0,Math.Min(screen.Width-size.Width,p.X)),Math.Max(0,Math.Min(screen.Height-size.Height,p.Y)),size.Width,size.Height);
   else selection=Rectangle.FromLTRB(Math.Min(anchor.X,p.X),Math.Min(anchor.Y,p.Y),Math.Max(anchor.X,p.X),Math.Max(anchor.Y,p.Y));
   Invalidate();
  }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.DrawImageUnscaled(desktop,0,0);
   using(var dim=new SolidBrush(Color.FromArgb(135,0,0,0))) e.Graphics.FillRectangle(dim,ClientRectangle);
   if(selection.Width>0&&selection.Height>0) {
    e.Graphics.DrawImage(desktop,selection,selection,GraphicsUnit.Pixel);
    using(var pen=new Pen(Color.FromArgb(235,235,240),2))e.Graphics.DrawRectangle(pen,selection.X,selection.Y,selection.Width-1,selection.Height-1);
   }
   string text=(fixedSize?"移动鼠标定位，单击确认 / Move, click to confirm":"拖动框选 / Drag to select")+"  ·  Esc / Right-click: Cancel\n"+selection.Width+" × "+selection.Height+" px";
   Point mouse=PointToClient(Cursor.Position);
   Rectangle hint=new Rectangle(Math.Max(0,Math.Min(ClientSize.Width-520,mouse.X+18)),Math.Max(0,Math.Min(ClientSize.Height-78,mouse.Y+22)),520,72);
   using(var b=new SolidBrush(Color.FromArgb(235,24,32,47)))e.Graphics.FillRectangle(b,hint);
   using(var f=new Font("Microsoft YaHei UI",12))e.Graphics.DrawString(text,f,Brushes.White,hint.X+12,hint.Y+10);
  }
  protected override void Dispose(bool disposing){if(disposing&&desktop!=null)desktop.Dispose();base.Dispose(disposing);}
 }
 static class Program {
  public static int ActivateMessage;
  [STAThread] static void Main(string[] args){
   try{Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{Native.SetProcessDPIAware();}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   for(int i=0;i+1<args.Length;i++)if(args[i]=="--profile")Store.Root=Path.GetFullPath(args[i+1]);
   if(args.Length==2&&args[0]=="--pdf-test"){PdfExporter.SelfTest(args[1]);return;}
   if(args.Length==2&&args[0]=="--export-icon"){Art.Export(args[1]);return;}
   if(Array.IndexOf(args,"--self-test")>=0){SelfTest();return;}
   bool created;string suffix=System.Text.RegularExpressions.Regex.Replace(Store.Root,@"[^a-zA-Z0-9]","_");ActivateMessage=Native.RegisterWindowMessage("FixedCapture.Show."+suffix);using(var mutex=new Mutex(true,"Local\\FixedCapture_"+suffix,out created)){if(!created){Native.PostMessage(new IntPtr(0xFFFF),ActivateMessage,IntPtr.Zero,IntPtr.Zero);return;}new System.Windows.Application().Run(new GlassWindow());}
  }
  static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
  static void SelfTest(){string report=Path.Combine(Store.Root,"self-test.txt");Directory.CreateDirectory(Store.Root);try{
   var s=new Settings{Folder=Path.Combine(Store.Root,"images"),Template="{n}",Next=5,HasRegion=true,X=-100,Y=20,Width=120,Height=80};
   using(var b=new Bitmap(120,80)){using(var g=Graphics.FromImage(b))g.Clear(Color.Teal);Assert(Path.GetFileName(CaptureFiles.Save(b,s))=="5.png","start 5");Assert(Path.GetFileName(CaptureFiles.Save(b,s))=="6.png","increment 6");s.Next=5;Assert(Path.GetFileName(CaptureFiles.Save(b,s))=="7.png","collision skip");}
   s.Template="{date}_{n}";s.Digits=3;Assert(CaptureFiles.Name(s,8,new DateTime(2026,10,29))=="20261029_008.png","date padding");
   s.Template="实验_{date}_{time}_{n}";Assert(CaptureFiles.Name(s,8,new DateTime(2026,10,29,12,34,56))=="实验_20261029_123456_008.png","custom");
   s.Template="{n}";Store.Save(s);var loaded=Store.Load();Assert(loaded.Next==8&&loaded.X==-100&&loaded.HasRegion&&loaded.IntervalSeconds==10,"persistence");loaded.Next=9;Store.Save(loaded);Assert(Store.Load().Next==9,"atomic replace");
   bool rejected=false;try{CaptureFiles.Name(new Settings{Template="bad/{n}"},1,DateTime.Now);}catch{rejected=true;}Assert(rejected,"invalid names");
   using(var image=Image.FromFile(Path.Combine(s.Folder,"5.png")))Assert(image.Width==120&&image.Height==80,"PNG dimensions");
   using(var a=new Bitmap(17,13))using(var b=new Bitmap(17,13))using(var differentSize=new Bitmap(13,17)){
    using(var g=Graphics.FromImage(a))g.Clear(Color.White);using(var g=Graphics.FromImage(b))g.Clear(Color.White);
    string baseline=FrameFingerprint.Compute(a);Assert(baseline==FrameFingerprint.Compute(b),"identical frame");b.SetPixel(8,6,Color.FromArgb(254,255,255));Assert(baseline!=FrameFingerprint.Compute(b),"single pixel change");Assert(baseline!=FrameFingerprint.Compute(differentSize),"dimensions");Assert(baseline==FrameFingerprint.Compute(a),"stable fingerprint");
   }
   byte[] wave=ShutterSound.Wave();Assert(wave.Length==8864&&System.Text.Encoding.ASCII.GetString(wave,0,4)=="RIFF","shutter PCM");File.WriteAllBytes(Path.Combine(Store.Root,"shutter.wav"),wave);
   using(var json=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}"))){var migrated=(Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(json);Assert(migrated.SkipDuplicates&&migrated.CaptureSound&&migrated.CaptureNotice,"legacy defaults");}
   loaded.SkipDuplicates=false;loaded.CaptureSound=false;loaded.CaptureNotice=false;Store.Save(loaded);var toggles=Store.Load();Assert(!toggles.SkipDuplicates&&!toggles.CaptureSound&&!toggles.CaptureNotice,"feedback persistence");
   File.WriteAllText(report,"PASS: exact duplicate, single pixel change, dimensions, fingerprint stability, PCM WAV, legacy defaults, feedback persistence; start 5, increment 6, collision skip 7, date format, padding, custom template, persistence, atomic replace, invalid names, PNG dimensions.");
  }catch(Exception ex){File.WriteAllText(report,"FAIL: "+ex);Environment.ExitCode=1;}}
 }
}
