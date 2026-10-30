using System;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace FixedCapture {
 class PdfItem {public string Path{get;set;}public string Label{get;set;}public string Folder{get{return System.IO.Path.GetDirectoryName(Path);}}}
 class PdfWindow:Window {
  FrameworkElement view;ObservableCollection<PdfItem> items=new ObservableCollection<PdfItem>();string initial,lastPdf;bool exporting,closeAfterCancel;CancellationTokenSource cancel;
  T C<T>(string name) where T:FrameworkElement{return (T)view.FindName(name);}
  void Click(string name,Action action){C<Button>(name).Click+=(o,e)=>action();}
  public PdfWindow(string folder,ResourceDictionary styles){
   initial=folder;Title="图片合成 PDF · 定框截图";Width=1100;Height=740;MinWidth=1050;MinHeight=600;MaxHeight=SystemParameters.WorkArea.Height-24;MaxWidth=SystemParameters.WorkArea.Width-24;WindowStartupLocation=WindowStartupLocation.CenterOwner;
   FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;Resources=styles;SetResourceReference(BackgroundProperty,"Surface");
   using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PdfView.xaml"))view=(FrameworkElement)System.Windows.Markup.XamlReader.Load(stream);Content=view;
   Decorate("Add","capture","添加图片\nAdd images");Decorate("CurrentFolder","folder","截图目录\nCapture folder");Decorate("ChooseFolder","folder","选择文件夹\nChoose folder");Decorate("Sort","tag","按编号排序\nSort");Decorate("Remove","exit","移除\nRemove");Decorate("Export","pdf","导出 PDF\nExport PDF",true);Decorate("OpenPdf","pdf","打开 PDF\nOpen PDF");
   C<ListBox>("Files").ItemsSource=items;C<ListBox>("Files").SelectionChanged+=(o,e)=>Preview();
   Click("Add",()=>{var d=new Microsoft.Win32.OpenFileDialog{Title="选择要合成 PDF 的图片",Filter="图片|*.png;*.jpg;*.jpeg;*.bmp",Multiselect=true};if(Directory.Exists(initial))d.InitialDirectory=initial;if(d.ShowDialog(this)==true)Add(d.FileNames);});
   Click("CurrentFolder",()=>LoadFolder(initial));Click("ChooseFolder",()=>{using(var d=new Forms.FolderBrowserDialog()){if(Directory.Exists(initial))d.SelectedPath=initial;if(d.ShowDialog()==Forms.DialogResult.OK)LoadFolder(d.SelectedPath);}});
   Click("Sort",()=>{var sorted=PdfExporter.Sorted(items.Select(p=>p.Path));items.Clear();foreach(string path in sorted)items.Add(new PdfItem{Path=path});Refresh(0);});
   Click("Up",()=>Move(-1));Click("Down",()=>Move(1));Click("Remove",()=>{int i=C<ListBox>("Files").SelectedIndex;if(i>=0){items.RemoveAt(i);Refresh(Math.Min(i,items.Count-1));}});
   Click("Export",async()=>await Export());Click("Cancel",()=>{if(exporting){cancel.Cancel();C<TextBlock>("ProgressText").Text="正在取消，请稍候…";}else Close();});Click("OpenPdf",()=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(lastPdf){UseShellExecute=true});}catch(Exception ex){Warn(ex.Message);}});
   Closing+=(o,e)=>{if(exporting){e.Cancel=true;closeAfterCancel=true;cancel.Cancel();C<TextBlock>("ProgressText").Text="正在取消导出，随后关闭窗口…";}};
   Loaded+=(o,e)=>{if(Directory.Exists(initial))LoadFolder(initial);else Refresh(-1);};
  }
  void Decorate(string name,string symbol,string text,bool white=false){var panel=new StackPanel{Orientation=Orientation.Horizontal};using(var bitmap=Art.Symbol(symbol,36,white?System.Drawing.Color.White:System.Drawing.Color.FromArgb(48,48,55)))using(var memory=new MemoryStream()){bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png);memory.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=memory;image.EndInit();image.Freeze();panel.Children.Add(new Image{Source=image,Width=16,Height=16});}panel.Children.Add(new TextBlock{Text=text,Margin=new Thickness(7,0,0,0),Foreground=white?Brushes.White:new SolidColorBrush(Color.FromRgb(40,40,46))});C<Button>(name).Content=panel;C<Button>(name).Height=48;if(white){C<Button>(name).SetResourceReference(Control.BackgroundProperty,"PdfAccent");C<Button>(name).SetResourceReference(Control.BorderBrushProperty,"PdfAccent");}}
  void Warn(string text){MessageBox.Show(this,text,"图片合成 PDF",MessageBoxButton.OK,MessageBoxImage.Warning);}
  void LoadFolder(string folder){try{if(!Directory.Exists(folder)){Warn("目录不存在，请先选择图片或文件夹。");return;}var paths=Directory.GetFiles(folder).Where(PdfExporter.Supported).ToArray();Add(paths);if(paths.Length==0)C<TextBlock>("ProgressText").Text="这个目录里没有支持的图片。";}catch(Exception ex){Warn(ex.Message);}}
  void Add(string[] paths){var existing=new System.Collections.Generic.HashSet<string>(items.Select(p=>p.Path),StringComparer.OrdinalIgnoreCase);foreach(string path in PdfExporter.Sorted(paths)){string full=Path.GetFullPath(path);if(PdfExporter.Supported(full)&&existing.Add(full))items.Add(new PdfItem{Path=full});}var sorted=PdfExporter.Sorted(items.Select(p=>p.Path));items.Clear();foreach(string path in sorted)items.Add(new PdfItem{Path=path});Refresh(items.Count>0?0:-1);C<TextBlock>("ProgressText").Text="已添加图片。确认顺序后点击“导出 PDF”。";}
  void Refresh(int selected){for(int i=0;i<items.Count;i++)items[i].Label=(i+1).ToString("D3")+"   "+Path.GetFileName(items[i].Path);C<ListBox>("Files").Items.Refresh();C<ListBox>("Files").SelectedIndex=selected;C<TextBlock>("Count").Text=items.Count+" 张图片 / "+items.Count+" 页";C<Button>("Export").IsEnabled=items.Count>0&&!exporting;Preview();}
  void Move(int delta){int i=C<ListBox>("Files").SelectedIndex;if(i<0||i+delta<0||i+delta>=items.Count)return;items.Move(i,i+delta);Refresh(i+delta);C<ListBox>("Files").ScrollIntoView(items[i+delta]);}
  void Preview(){var item=C<ListBox>("Files").SelectedItem as PdfItem;C<Image>("PreviewImage").Source=null;C<TextBlock>("Empty").Visibility=Visibility.Visible;if(item==null){C<TextBlock>("ImageInfo").Text="";return;}try{using(var file=new FileStream(item.Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=900;image.StreamSource=file;image.EndInit();image.Freeze();C<Image>("PreviewImage").Source=image;}C<TextBlock>("Empty").Visibility=Visibility.Collapsed;C<TextBlock>("ImageInfo").Text="第 "+(C<ListBox>("Files").SelectedIndex+1)+" 页 · "+Path.GetFileName(item.Path);}catch{C<TextBlock>("Empty").Text="无法预览此图片，请检查文件是否有效。";}}
  async Task Export(){
   if(exporting||items.Count==0)return;var dialog=new Microsoft.Win32.SaveFileDialog{Title="保存合成的 PDF",Filter="PDF 文件|*.pdf",DefaultExt=".pdf",AddExtension=true,OverwritePrompt=true,FileName="截图合集_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".pdf"};if(Directory.Exists(initial))dialog.InitialDirectory=initial;if(dialog.ShowDialog(this)!=true)return;
   string[] paths=items.Select(p=>p.Path).ToArray();bool a4=C<ComboBox>("PageMode").SelectedIndex==1;exporting=true;cancel=new CancellationTokenSource();C<StackPanel>("Tools").IsEnabled=false;C<ComboBox>("PageMode").IsEnabled=false;C<Button>("Export").IsEnabled=false;C<Button>("OpenPdf").IsEnabled=false;C<Button>("Cancel").Content="取消 / Cancel";C<ProgressBar>("Progress").Visibility=Visibility.Visible;C<ProgressBar>("Progress").Value=0;
   C<TextBlock>("ProgressText").Text="正在合成 0 / "+paths.Length+" 页…";
   try{await Task.Run(()=>PdfExporter.Export(paths,dialog.FileName,a4,true,cancel.Token,n=>Dispatcher.Invoke(new Action(()=>{C<ProgressBar>("Progress").Value=n*100.0/paths.Length;C<TextBlock>("ProgressText").Text="正在合成 "+n+" / "+paths.Length+" 页…";}))));lastPdf=dialog.FileName;C<TextBlock>("ProgressText").Text="完成 · "+paths.Length+" 页 · "+lastPdf;C<TextBlock>("ProgressText").ToolTip=lastPdf;C<Button>("OpenPdf").IsEnabled=true;}
   catch(OperationCanceledException){C<TextBlock>("ProgressText").Text="已取消，原有文件没有改变。";}
   catch(Exception ex){C<TextBlock>("ProgressText").Text="导出失败，原图片和原 PDF 均已保留。";Warn(ex.Message);}
   finally{exporting=false;cancel.Dispose();cancel=null;C<StackPanel>("Tools").IsEnabled=true;C<ComboBox>("PageMode").IsEnabled=true;C<Button>("Export").IsEnabled=items.Count>0;C<Button>("Cancel").Content="关闭 / Close";if(closeAfterCancel)Close();}
  }
 }
}
