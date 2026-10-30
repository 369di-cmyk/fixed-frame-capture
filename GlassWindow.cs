using System;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shell;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

namespace FixedCapture {
 class GlassWindow:Window {
  FrameworkElement view; Settings config,activeKeys; bool regionSet,busy,running,exiting,pdfOpen; int generation,count,skipped; string lastFrame; CaptureToast notice; long deadline;
  IntPtr handle; HwndSource source; Forms.NotifyIcon tray; Forms.ToolStripMenuItem trayToggle;
  DispatcherTimer timer=new DispatcherTimer();System.Diagnostics.Stopwatch elapsed=new System.Diagnostics.Stopwatch();
  [StructLayout(LayoutKind.Sequential)] struct Margins{public int L,R,T,B;}
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
  [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
  T C<T>(string name) where T:FrameworkElement {return (T)view.FindName(name);}
  TextBox Box(string name){return C<TextBox>(name);} TextBlock Label(string name){return C<TextBlock>(name);}
  void Click(string name,Action action){C<Button>(name).Click+=(o,e)=>action();}
  void Status(string text){Label("Status").Text=text;}
  public GlassWindow(){
   Title="定框截图 · 3.5";Width=1000;Height=930;MaxHeight=SystemParameters.WorkArea.Height-24;MaxWidth=SystemParameters.WorkArea.Width-24;
   WindowStartupLocation=WindowStartupLocation.CenterScreen;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;Background=Brushes.Transparent;
   FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;UseLayoutRounding=true;SnapsToDevicePixels=true;
   WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,CornerRadius=new CornerRadius(20),GlassFrameThickness=new Thickness(-1),ResizeBorderThickness=new Thickness(0)});
   using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("View.xaml"))view=(FrameworkElement)System.Windows.Markup.XamlReader.Load(stream);
   Content=view;System.Windows.Application.Current.ShutdownMode=ShutdownMode.OnMainWindowClose;
   string loadError=null;try{config=Store.Load();}catch(Exception ex){config=new Settings();loadError=ex.Message;}
   regionSet=config.HasRegion;
   var themePicker=C<ComboBox>("ThemePicker");string[] themes={"Ocean","Lavender","Peach","Classic"};int themeIndex=Array.IndexOf(themes,config.Theme);themePicker.SelectedIndex=themeIndex<0?0:themeIndex;
   themePicker.SelectionChanged+=(o,e)=>{string old=config.Theme;config.Theme=themes[themePicker.SelectedIndex];ApplyTheme();try{Store.Save(config);Status("主题已保存 / Theme saved");}catch(Exception ex){config.Theme=old;Status("主题保存失败 / Could not save theme: "+ex.Message);}};
   C<CheckBox>("SkipDuplicates").IsChecked=config.SkipDuplicates;C<CheckBox>("CaptureSound").IsChecked=config.CaptureSound;C<CheckBox>("CaptureNotice").IsChecked=config.CaptureNotice;
   Set("X",config.X);Set("Y",config.Y);Set("WidthInput",config.Width);Set("HeightInput",config.Height);Set("Interval",Math.Max(1,config.IntervalSeconds));Set("Next",config.Next);Set("Digits",config.Digits);
   Box("Folder").Text=config.Folder;Box("Template").Text=config.Template;
   InitKeys("Shot",config.Modifiers,config.Key);InitKeys("Auto",config.AutoModifiers,config.AutoKey);
   var mode=C<ComboBox>("NameMode");mode.SelectedIndex=config.Template=="{date}_{n}"?0:config.Template=="{n}"?1:2;Box("Template").IsReadOnly=mode.SelectedIndex!=2;
   mode.SelectionChanged+=(o,e)=>{if(mode.SelectedIndex<2)Box("Template").Text=mode.SelectedIndex==0?"{date}_{n}":"{n}";Box("Template").IsReadOnly=mode.SelectedIndex!=2;Preview();};
   foreach(string name in new[]{"Template","Next","Digits"})Box(name).TextChanged+=(o,e)=>Preview();
   foreach(var image in Descendants(view).OfType<Image>()){if(image.Tag!=null){string[] bits=image.Tag.ToString().Split(':');image.Source=Symbol(bits[0],bits.Length>1);}}
   using(var brand=Art.Brand(128)){var img=ToImage(brand);C<Image>("Brand").Source=img;Icon=img;}
   Click("SelectRegion",async()=>await Pick(false));Click("LocateRegion",async()=>await Pick(true));Click("Capture",async()=>await Capture());Click("ToggleAuto",()=>Toggle(false));
   Click("Save",()=>Apply(true));Click("Browse",()=>{using(var dialog=new Forms.FolderBrowserDialog()){if(Directory.Exists(Box("Folder").Text))dialog.SelectedPath=Box("Folder").Text;if(dialog.ShowDialog()==Forms.DialogResult.OK)Box("Folder").Text=dialog.SelectedPath;}});
   Click("Background",()=>{if(running||Apply(false))Hide();});Click("OpenFolder",()=>OpenFolder());Click("Exit",()=>{if(running||Apply(false)){exiting=true;Close();}});
   Click("MergePdf",()=>OpenPdfDialog());
   Click("Minimize",()=>{if(running||Apply(false))WindowState=WindowState.Minimized;});Click("CloseToTray",()=>Close());
   C<Grid>("TitleBar").MouseLeftButtonDown+=(o,e)=>{DependencyObject current=e.OriginalSource as DependencyObject;while(current!=null){if(current is Button||current is ComboBox)return;current=VisualTreeHelper.GetParent(current);}if(e.ClickCount==1)DragMove();};
   tray=new Forms.NotifyIcon{Icon=Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),Text="定框截图",Visible=true};
   var menu=new Forms.ContextMenuStrip();menu.Items.Add("立即截图 / Capture",Art.Symbol("capture",20,Drawing.Color.DimGray),async(o,e)=>await Capture());
   trayToggle=(Forms.ToolStripMenuItem)menu.Items.Add("开始自动截图 / Start",Art.Symbol("play",20,Drawing.Color.DimGray),(o,e)=>Toggle(true));
   menu.Items.Add("打开设置 / Settings",Art.Symbol("gear",20,Drawing.Color.DimGray),(o,e)=>ShowMain());menu.Items.Add("图片合成 PDF / Images to PDF",Art.Symbol("pdf",20,Drawing.Color.DimGray),(o,e)=>OpenPdfDialog());menu.Items.Add("打开保存目录 / Open folder",Art.Symbol("folder",20,Drawing.Color.DimGray),(o,e)=>OpenFolder());menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出 / Exit",Art.Symbol("exit",20,Drawing.Color.DimGray),(o,e)=>{if(running||Apply(false)){exiting=true;Close();}});tray.ContextMenuStrip=menu;tray.DoubleClick+=(o,e)=>ShowMain();
   timer.Interval=TimeSpan.FromMilliseconds(100);timer.Tick+=async(o,e)=>{if(!running)return;UpdateCountdown();if(!busy&&elapsed.ElapsedMilliseconds>=deadline){deadline=elapsed.ElapsedMilliseconds+config.IntervalSeconds*1000L;await Capture(true);}};
   SourceInitialized+=(o,e)=>{handle=new WindowInteropHelper(this).Handle;source=HwndSource.FromHwnd(handle);source.CompositionTarget.BackgroundColor=Colors.Transparent;source.AddHook(Hook);ApplyGlass();};
   Loaded+=(o,e)=>{foreach(var image in Descendants(view).OfType<Image>()){if(image.Tag!=null){string[] bits=image.Tag.ToString().Split(':');image.Source=Symbol(bits[0],bits.Length>1);}}try{RegisterKeys(config);Status("就绪 · 单次 "+KeyText(config.Modifiers,config.Key)+"  /  自动 "+KeyText(config.AutoModifiers,config.AutoKey));}catch(Exception ex){Error(ex.Message);}ApplyTheme();if(loadError!=null)Error("设置读取失败："+loadError);};
   Closing+=(o,e)=>{if(pdfOpen){e.Cancel=true;exiting=false;return;}if(!exiting){e.Cancel=true;if(running||Apply(false)){Hide();tray.ShowBalloonTip(1500,"定框截图","继续在后台运行。"+KeyText(config.AutoModifiers,config.AutoKey)+" 开始 / 暂停自动截图。",Forms.ToolTipIcon.Info);}}else {Stop();if(notice!=null)notice.Close();ShutterSound.Dispose();Native.UnregisterHotKey(handle,1);Native.UnregisterHotKey(handle,2);timer.Stop();tray.Visible=false;tray.Dispose();if(source!=null)source.RemoveHook(Hook);}};
   foreach(string name in new[]{"X","Y","WidthInput","HeightInput","Folder","Template","Next","Digits","Interval"})Box(name).TextChanged+=(o,e)=>{if(!busy)MarkDirty();};
   foreach(string name in new[]{"ShotCtrl","ShotAlt","ShotShift","AutoCtrl","AutoAlt","AutoShift","SkipDuplicates","CaptureSound","CaptureNotice"}){C<CheckBox>(name).Checked+=(o,e)=>MarkDirty();C<CheckBox>(name).Unchecked+=(o,e)=>MarkDirty();}
   foreach(string name in new[]{"ShotKey","AutoKey","NameMode"})C<ComboBox>(name).SelectionChanged+=(o,e)=>MarkDirty();
   if(!File.Exists(Store.FilePath))Label("SaveState").Text="请先设置截图区域 / Set capture area";
   UpdateRegion();Preview();Label("AutoHint").Text=KeyText(config.AutoModifiers,config.AutoKey)+" Start / Pause";
  }
  void MarkDirty(){Label("SaveState").Text="尚未保存 / Unsaved changes";Label("SaveState").Foreground=Brushes.DarkOrange;}
  void ApplyGlass(){ApplyTheme();try{int light=0,round=2,backdrop=3;DwmSetWindowAttribute(handle,20,ref light,4);DwmSetWindowAttribute(handle,33,ref round,4);var margins=new Margins{L=-1,R=-1,T=-1,B=-1};int frame=DwmExtendFrameIntoClientArea(handle,ref margins);int result=DwmSetWindowAttribute(handle,38,ref backdrop,4);if(result==0&&frame==0){Label("MaterialLabel").Text="ACRYLIC / 3.5";return;}}catch{} Label("MaterialLabel").Text="FROST / 3.5";}
  void ApplyTheme(){if(config==null)return;ThemePalette.Apply(view,config.Theme);}
  static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject parent){for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var sub in Descendants(child))yield return sub;}}
  static BitmapSource ToImage(Drawing.Bitmap bitmap){using(var ms=new MemoryStream()){bitmap.Save(ms,ImageFormat.Png);ms.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=ms;image.EndInit();image.Freeze();return image;}}
  static BitmapSource Symbol(string name,bool white){using(var bitmap=Art.Symbol(name,40,white?Drawing.Color.White:Drawing.Color.FromArgb(48,48,55)))return ToImage(bitmap);}
  void Set(string name,long value){Box(name).Text=value.ToString();}
  long Number(string name,long min,long max,string label){long value;if(!long.TryParse(Box(name).Text,out value)||value<min||value>max)throw new Exception(label+"请输入 "+min+"–"+max+" 之间的整数。");return value;}
  void InitKeys(string prefix,int mods,int key){C<CheckBox>(prefix+"Ctrl").IsChecked=(mods&2)!=0;C<CheckBox>(prefix+"Alt").IsChecked=(mods&1)!=0;C<CheckBox>(prefix+"Shift").IsChecked=(mods&4)!=0;var list=C<ComboBox>(prefix+"Key");for(int i=65;i<=90;i++)list.Items.Add(((Forms.Keys)i).ToString());for(int i=0;i<10;i++)list.Items.Add("D"+i);for(int i=1;i<=12;i++)list.Items.Add("F"+i);list.SelectedItem=((Forms.Keys)key).ToString();if(list.SelectedIndex<0)list.SelectedItem=prefix=="Auto"?"F9":"S";}
  int Mods(string p){return (C<CheckBox>(p+"Ctrl").IsChecked==true?2:0)|(C<CheckBox>(p+"Alt").IsChecked==true?1:0)|(C<CheckBox>(p+"Shift").IsChecked==true?4:0);}
  int Key(string p){return (int)(Forms.Keys)Enum.Parse(typeof(Forms.Keys),C<ComboBox>(p+"Key").SelectedItem.ToString());}
  static string KeyText(int mods,int key){return ((mods&2)!=0?"Ctrl + ":"")+((mods&1)!=0?"Alt + ":"")+((mods&4)!=0?"Shift + ":"")+((Forms.Keys)key).ToString();}
  Settings Read(){if(string.IsNullOrWhiteSpace(Box("Folder").Text))throw new Exception("请选择保存目录。");var s=new Settings{Theme=config.Theme,X=(int)Number("X",-100000,100000,"左侧 X"),Y=(int)Number("Y",-100000,100000,"顶部 Y"),Width=(int)Number("WidthInput",1,50000,"宽度"),Height=(int)Number("HeightInput",1,50000,"高度"),Folder=Path.GetFullPath(Environment.ExpandEnvironmentVariables(Box("Folder").Text.Trim())),Template=Box("Template").Text,Next=Number("Next",0,999999999998L,"下一编号"),Digits=(int)Number("Digits",1,12,"编号位数"),IntervalSeconds=(int)Number("Interval",1,86400,"间隔秒数"),Modifiers=Mods("Shot"),Key=Key("Shot"),AutoModifiers=Mods("Auto"),AutoKey=Key("Auto"),SkipDuplicates=C<CheckBox>("SkipDuplicates").IsChecked==true,CaptureSound=C<CheckBox>("CaptureSound").IsChecked==true,CaptureNotice=C<CheckBox>("CaptureNotice").IsChecked==true};s.HasRegion=regionSet||s.Region!=config.Region;return s;}
  static bool SameKeys(Settings a,Settings b){return a!=null&&a.Modifiers==b.Modifiers&&a.Key==b.Key&&a.AutoModifiers==b.AutoModifiers&&a.AutoKey==b.AutoKey;}
  void RegisterKeys(Settings s){
   if(s.Modifiers==0||s.AutoModifiers==0)throw new Exception("两组快捷键都至少需要 Ctrl、Alt、Shift 中的一个。");
   if(s.Modifiers==s.AutoModifiers&&s.Key==s.AutoKey)throw new Exception("单次截图与自动截图不能使用同一组快捷键。");
   if(SameKeys(activeKeys,s))return;
   Settings old=activeKeys;Native.UnregisterHotKey(handle,1);Native.UnregisterHotKey(handle,2);activeKeys=null;
   bool first=Native.RegisterHotKey(handle,1,(uint)(s.Modifiers|0x4000),(uint)s.Key);
   bool second=first&&Native.RegisterHotKey(handle,2,(uint)(s.AutoModifiers|0x4000),(uint)s.AutoKey);
   if(first&&second){activeKeys=s;return;}
   Native.UnregisterHotKey(handle,1);Native.UnregisterHotKey(handle,2);
   bool restored=true;if(old!=null){bool a=Native.RegisterHotKey(handle,1,(uint)(old.Modifiers|0x4000),(uint)old.Key);bool b=Native.RegisterHotKey(handle,2,(uint)(old.AutoModifiers|0x4000),(uint)old.AutoKey);restored=a&&b;if(restored)activeKeys=old;}
   throw new Exception((first?"自动截图":"单次截图")+"快捷键已被占用，请更换组合。"+(old!=null?(restored?" 原快捷键仍然有效。":" 请重新保存两组快捷键。"):""));
  }
  bool Apply(bool notify){try{Settings s=Read();CaptureFiles.Name(s,s.Next,DateTime.Now);Directory.CreateDirectory(s.Folder);Settings old=activeKeys;RegisterKeys(s);try{Store.Save(s);}catch{if(old!=null)RegisterKeys(old);throw;}config=s;regionSet=s.HasRegion;Label("SaveState").Text="设置已保存 / Saved";Label("SaveState").Foreground=Brushes.DimGray;UpdateRegion();Label("AutoHint").Text=KeyText(s.AutoModifiers,s.AutoKey)+" Start / Pause";if(notify)Status("设置已保存 · 自动截图 "+KeyText(s.AutoModifiers,s.AutoKey));return true;}catch(Exception ex){Label("SaveState").Text="保存失败，未开始截图 / Save failed";Label("SaveState").Foreground=Brushes.Firebrick;Error(ex.Message);return false;}}
  bool ValidRegion(){if(!config.HasRegion){Error("请先框选截图区域，或修改坐标和尺寸后保存。");return false;}if(!Forms.SystemInformation.VirtualScreen.Contains(config.Region)){Error("截图区域超出当前屏幕，请重新框选。");return false;}return true;}
  void Toggle(bool feedback){if(pdfOpen)return;if(running){Stop();Status("自动截图已暂停 · 本轮已保存 "+count+" 张");if(feedback)Balloon("已暂停自动截图");return;}if(busy)return;if(!Apply(false)||!ValidRegion())return;running=true;generation++;count=0;skipped=0;lastFrame=null;elapsed.Restart();deadline=config.IntervalSeconds*1000L;LockSettings(true);Label("AutoButtonText").Text="暂停自动截图 / Pause";C<Image>("AutoIcon").Source=Symbol("pause",true);trayToggle.Text="暂停自动截图 / Pause";timer.Start();UpdateCountdown();Status("自动截图已开始 · "+KeyText(config.AutoModifiers,config.AutoKey)+" 再按一次暂停");if(feedback)Balloon("已开始自动截图，每 "+config.IntervalSeconds+" 秒检查一次");}
  void LockSettings(bool locked){foreach(string name in new[]{"RegionControls","FolderControls","NamingControls","KeyControls","FeedbackControls","Interval","Save"})C<FrameworkElement>(name).IsEnabled=!locked;}
  void Stop(){running=false;generation++;timer.Stop();elapsed.Stop();LockSettings(false);Label("AutoButtonText").Text="开始自动截图 / Start";C<Image>("AutoIcon").Source=Symbol("play",true);trayToggle.Text="开始自动截图 / Start";Label("AutoStatus").Text="已暂停 · 本轮已保存 "+count+" 张 · 跳过 "+skipped+" 张";tray.Text="定框截图 · 已暂停自动截图";}
  void UpdateCountdown(){if(!running)return;long seconds=Math.Max(0,(deadline-elapsed.ElapsedMilliseconds+999)/1000);Label("AutoStatus").Text=seconds+" 秒后截图 · 已保存 "+count+" 张 · 跳过 "+skipped+" 张";tray.Text="定框截图 · "+seconds+" 秒后截图 · 已保存 "+count+" 张";}
  void Balloon(string text){tray.ShowBalloonTip(1400,"定框截图",text,Forms.ToolTipIcon.Info);}
  void Preview(){try{Label("Preview").Text="下一个  /  "+CaptureFiles.Name(new Settings{Template=Box("Template").Text,Digits=(int)Number("Digits",1,12,"编号位数")},Number("Next",0,999999999998L,"下一编号"),DateTime.Now);}catch{Label("Preview").Text="模板需包含 {n}，也可使用 {date} 和 {time}";}}
  void UpdateRegion(){Label("RegionInfo").Text=config.HasRegion?"已记住  /  "+config.Width+" × "+config.Height+" px":"尚未设置  /  请先框选或输入位置和尺寸";}
  async Task Pick(bool fixedSize){if(busy||running)return;busy=true;try{var size=new Drawing.Size((int)Number("WidthInput",1,50000,"宽度"),(int)Number("HeightInput",1,50000,"高度"));if(fixedSize&&(size.Width>Forms.SystemInformation.VirtualScreen.Width||size.Height>Forms.SystemInformation.VirtualScreen.Height))throw new Exception("尺寸超出当前桌面。");if(notice!=null)notice.Dismiss();Hide();await Task.Delay(250);using(var picker=new RegionPicker(fixedSize,size)){if(picker.ShowDialog()==Forms.DialogResult.OK){Drawing.Rectangle selected=picker.Selected;Set("X",selected.X);Set("Y",selected.Y);Set("WidthInput",selected.Width);Set("HeightInput",selected.Height);regionSet=true;Apply(true);}}}catch(Exception ex){Error(ex.Message);}finally{busy=false;if(!exiting)ShowMain();}}
  async Task Capture(bool automatic=false,bool fromShortcut=false){if(busy||exiting||pdfOpen)return;if(!running&&!Apply(false))return;if(!ValidRegion())return;busy=true;int token=generation;bool restore=IsVisible&&WindowState!=WindowState.Minimized;try{if(notice!=null)notice.Dismiss();Hide();await Task.Delay(220);if(exiting||(automatic&&(!running||token!=generation)))return;using(var bitmap=new Drawing.Bitmap(config.Width,config.Height)){using(var g=Drawing.Graphics.FromImage(bitmap))g.CopyFromScreen(config.Region.Location,Drawing.Point.Empty,config.Region.Size,Drawing.CopyPixelOperation.SourceCopy);string fingerprint=automatic&&config.SkipDuplicates?FrameFingerprint.Compute(bitmap):null;if(fingerprint!=null&&fingerprint==lastFrame){skipped++;Status("画面相同，已跳过 · 不占用编号");UpdateCountdown();return;}string file=CaptureFiles.Save(bitmap,config);if(automatic)lastFrame=fingerprint;Set("Next",config.Next);try{Store.Save(config);}catch(Exception ex){throw new Exception("图片已保存，编号设置保存失败："+ex.Message);}if(automatic)count++;Status("已保存  /  "+DateTime.Now.ToString("HH:mm:ss")+"  /  "+Path.GetFileName(file));if(running)UpdateCountdown();try{if(config.CaptureSound)ShutterSound.Play();}catch{}try{if(config.CaptureNotice){if(notice==null)notice=new CaptureToast();notice.Present(bitmap,file,config.Region);}}catch{Status("已保存 / "+Path.GetFileName(file)+" · 缩略图提示不可用");}}}catch(Exception ex){Error(ex.Message);}finally{busy=false;if(restore&&!exiting){ShowActivated=!automatic;Show();ShowActivated=true;}}}
  void Error(string message){if(running)Stop();Status(message);if(!IsVisible)ShowMain();MessageBox.Show(this,message,"定框截图",MessageBoxButton.OK,MessageBoxImage.Warning);}
  void ShowMain(){ShowActivated=true;Show();WindowState=WindowState.Normal;Activate();}
  void OpenPdfDialog(){if(pdfOpen||busy)return;try{string folder=Path.GetFullPath(Environment.ExpandEnvironmentVariables(Box("Folder").Text.Trim()));if(running){Stop();Status("自动截图已暂停，正在整理 PDF。");}pdfOpen=true;ShowMain();new PdfWindow(folder,view.Resources){Owner=this,Icon=Icon}.ShowDialog();}catch(Exception ex){Error(ex.Message);}finally{pdfOpen=false;}}
  void OpenFolder(){try{string path=Path.GetFullPath(Environment.ExpandEnvironmentVariables(Box("Folder").Text.Trim()));Directory.CreateDirectory(path);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){Error(ex.Message);}}
  IntPtr Hook(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled){if(msg==Program.ActivateMessage&&msg!=0){Dispatcher.BeginInvoke(new Action(()=>ShowMain()));handled=true;}if(msg==0x0312){int id=wParam.ToInt32();if(id==2){Dispatcher.BeginInvoke(new Action(()=>Toggle(true)));handled=true;}else if(id==1){Dispatcher.BeginInvoke(new Action(async()=>await Capture(false,true)));handled=true;}}if(msg==0x031E)ApplyGlass();return IntPtr.Zero;}
 }
}
