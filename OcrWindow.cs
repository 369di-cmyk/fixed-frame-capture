using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Drawing=System.Drawing;

namespace FixedCapture {
 [DataContract] sealed class OcrResult {
  [DataMember]public string Text{get;set;}
  [DataMember]public string Language{get;set;}
  [DataMember]public string[] Languages{get;set;}
  [DataMember]public string Error{get;set;}
 }
 static class LocalOcr {
  // Windows PowerShell 5.1 hosts the built-in WinRT engine; no cloud service or extra package.
  public static async Task<OcrResult> Recognize(Drawing.Bitmap image,string language){
   string root=Path.Combine(Store.Root,"ocr-temp",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   string script=Path.Combine(root,"ocr.ps1"),input=Path.Combine(root,"capture.png"),output=Path.Combine(root,"result.json");
   try{
    using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("OcrWorker.ps1"))using(var file=File.Create(script))stream.CopyTo(file);
    // Large captures are resized only for OCR. The editor and exported image keep full resolution.
    double factor=Math.Min(1,2500.0/Math.Max(image.Width,image.Height));using(var small=new Drawing.Bitmap(Math.Max(1,(int)(image.Width*factor)),Math.Max(1,(int)(image.Height*factor)))){using(var g=Drawing.Graphics.FromImage(small)){g.InterpolationMode=Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(image,0,0,small.Width,small.Height);}small.Save(input,Drawing.Imaging.ImageFormat.Png);}
    return await Task.Run(()=>{
     string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");
     var info=new ProcessStartInfo(exe,"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+script+"\" -ImagePath \""+input+"\" -ResultPath \""+output+"\" -Language \""+language+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
     using(var process=Process.Start(info)){if(!process.WaitForExit(45000)){try{process.Kill();process.WaitForExit(2000);}catch{}throw new Exception("文字识别超时，请重试 / OCR timed out.");}}
     if(!File.Exists(output))throw new Exception("Windows OCR 无法启动。需要 Windows 10 / 11 和对应识别语言包。 / Windows OCR is unavailable.");
     using(var stream=File.OpenRead(output)){var result=(OcrResult)new DataContractJsonSerializer(typeof(OcrResult)).ReadObject(stream);if((result.Language??"").StartsWith("zh",StringComparison.OrdinalIgnoreCase))result.Text=System.Text.RegularExpressions.Regex.Replace(result.Text??"",@"(?<=[\u3400-\u9fff]) +(?=[\u3400-\u9fff])","");return result;}
    });
   }finally{try{Directory.Delete(root,true);}catch{}}
  }
 }
 sealed class OcrWindow:Window {
  Drawing.Bitmap bitmap;TextBox text;TextBlock status;ComboBox language;Button recognize;bool busy;
  public OcrWindow(Drawing.Bitmap image,ResourceDictionary styles){
   bitmap=new Drawing.Bitmap(image);Title="识别文字 / Chinese & English OCR";Width=760;Height=590;MaxWidth=SystemParameters.WorkArea.Width-32;MaxHeight=SystemParameters.WorkArea.Height-32;WindowStartupLocation=WindowStartupLocation.CenterOwner;FontFamily=new FontFamily("Microsoft YaHei UI");Resources=styles;SetResourceReference(BackgroundProperty,"WorkspaceSurface");
   var root=new DockPanel{Margin=new Thickness(20)};Content=root;var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);top.Children.Add(new TextBlock{Text="提取图片文字 / Extract text",FontSize=21,Margin=new Thickness(0,0,0,8)});top.Children.Add(new TextBlock{Text="本机离线识别 · 识别结果可编辑，请核对重要文字 / Local OCR; review the result",FontSize=12,Margin=new Thickness(0,0,0,14)});
   var tools=new StackPanel{Orientation=Orientation.Horizontal};top.Children.Add(tools);language=new ComboBox{Width=210,Margin=new Thickness(0,0,10,10)};language.Items.Add("自动 / Auto (优先中文)");language.Items.Add("简体中文 / Chinese");language.Items.Add("英语 / English");language.Items.Add("繁体中文 / Traditional");language.SelectedIndex=0;tools.Children.Add(language);recognize=EditorImages.Button("ocr","重新识别 / Recognize",async()=>await Run());tools.Children.Add(recognize);
   var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);status=new TextBlock{Text="准备识别…",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,8)};bottom.Children.Add(status);var actions=new WrapPanel();bottom.Children.Add(actions);actions.Children.Add(EditorImages.Button("clipboard","复制文字 / Copy",()=>{try{if(text.Text.Length>0)LocalClipboard.Text(text.Text);}catch(Exception ex){status.Text=ex.Message;}}));actions.Children.Add(EditorImages.Button("save","保存文本 / Save text",()=>{try{var d=new Microsoft.Win32.SaveFileDialog{Filter="文本文件|*.txt",DefaultExt=".txt",FileName="识别文字.txt"};if(d.ShowDialog(this)==true)File.WriteAllText(d.FileName,text.Text,new System.Text.UTF8Encoding(true));}catch(Exception ex){status.Text=ex.Message;}}));actions.Children.Add(EditorImages.Button("cancel","关闭 / Close",()=>Close()));
   // Use the standard TextBox template here so multiline text and scrollbars remain usable.
   text=new TextBox{Style=null,AcceptsReturn=true,AcceptsTab=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontSize=17,Padding=new Thickness(12),Background=Brushes.White};root.Children.Add(text);
   Loaded+=async(o,e)=>await Run();Closing+=(o,e)=>{if(busy){e.Cancel=true;status.Text="正在识别，请稍候再关闭 / Recognition in progress";}};Closed+=(o,e)=>bitmap.Dispose();
  }
  async Task Run(){if(busy)return;busy=true;recognize.IsEnabled=false;language.IsEnabled=false;status.Text="正在本机识别… / Recognizing locally…";try{string code=new[]{"auto","zh-Hans","en","zh-Hant"}[language.SelectedIndex];var result=await LocalOcr.Recognize(bitmap,code);if(!string.IsNullOrEmpty(result.Error))throw new Exception(result.Error);text.Text=result.Text??"";status.Text=(text.Text.Length==0?"未找到文字；可换语言或截取更清晰的区域。 / No text found.":"识别完成 / Done · "+result.Language)+"\n可用语言 / Available: "+string.Join(", ",result.Languages??new string[0]);}catch(Exception ex){status.Text="识别失败：请检查 Windows「时间和语言 → 语言和区域」中的 OCR 语言支持。\n"+ex.Message;}finally{busy=false;recognize.IsEnabled=true;language.IsEnabled=true;}}
 }
}
