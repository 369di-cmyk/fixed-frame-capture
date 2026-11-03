using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;

namespace FixedCapture {
 static class EditorImages {
  public static BitmapSource Source(Drawing.Bitmap bitmap){var data=bitmap.LockBits(new Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),Drawing.Imaging.ImageLockMode.ReadOnly,Drawing.Imaging.PixelFormat.Format32bppArgb);try{var image=BitmapSource.Create(bitmap.Width,bitmap.Height,96,96,PixelFormats.Bgra32,null,data.Scan0,Math.Abs(data.Stride)*bitmap.Height,data.Stride);image.Freeze();return image;}finally{bitmap.UnlockBits(data);}}
  public static Button Button(string icon,string text,Action click){var stack=new StackPanel{Orientation=Orientation.Horizontal};using(var bitmap=Art.Symbol(icon,40,Drawing.Color.FromArgb(44,50,65)))stack.Children.Add(new Image{Source=Source(bitmap),Width=18,Height=18,Tag=icon});stack.Children.Add(new TextBlock{Text=text,Margin=new Thickness(7,0,0,0),VerticalAlignment=VerticalAlignment.Center});var b=new Button{Content=stack,Height=46,Padding=new Thickness(10,0,10,0),Margin=new Thickness(3),ToolTip=text};b.Click+=(o,e)=>click();return b;}
  public static void SavePng(Drawing.Bitmap image,string path){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){image.Save(f,Drawing.Imaging.ImageFormat.Png);f.Flush(true);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 }
 sealed class CaptureEditor:Window {
  FrameworkElement view;AnnotationDocument document;Mark pending;MarkTool tool=MarkTool.Pen;Drawing.Color color=Drawing.Color.FromArgb(239,68,68);
  readonly Func<Drawing.Bitmap,string> save;readonly string folder;readonly Drawing.Rectangle region;
  Dictionary<MarkTool,Button> toolButtons=new Dictionary<MarkTool,Button>();readonly double[] toolSizes={4,24,24,14,4,28,4,4};int wheelRemainder;Button undo,redo;bool closed;System.Diagnostics.Stopwatch paintClock=System.Diagnostics.Stopwatch.StartNew();
  public string Outcome="已取消，截图未保存 / Cancelled";
  T C<T>(string name)where T:FrameworkElement{return (T)view.FindName(name);}
  public CaptureEditor(Drawing.Bitmap image,string initial,ResourceDictionary styles,Drawing.Rectangle captureRegion,Func<Drawing.Bitmap,string> saveImage){
   document=new AnnotationDocument(image);save=saveImage;folder=initial;region=captureRegion;
   Title="标注截图 · 定框截图 3.7";Width=Math.Min(1240,SystemParameters.WorkArea.Width-32);Height=Math.Min(900,SystemParameters.WorkArea.Height-32);MinWidth=Math.Min(760,Width);MinHeight=Math.Min(620,Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
   FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=12;Resources=styles;SetResourceReference(BackgroundProperty,"WorkspaceSurface");UseLayoutRounding=true;
   using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("EditorView.xaml"))view=(FrameworkElement)System.Windows.Markup.XamlReader.Load(stream);Content=view;
   string[] symbols={"pen","highlight","eraser","mosaic","arrow","text","ellipse","rectangle"};string[] labels={"画笔\nPen","荧光笔\nHighlight","橡皮擦\nEraser","马赛克\nMosaic","箭头\nArrow","文字\nText","圆框\nEllipse","方框\nRectangle"};
   for(int i=0;i<symbols.Length;i++){MarkTool chosen=(MarkTool)i;var button=EditorImages.Button(symbols[i],labels[i],()=>SelectTool(chosen));button.MinWidth=83;toolButtons.Add(chosen,button);C<WrapPanel>("Tools").Children.Add(button);}
   undo=EditorImages.Button("undo","撤销\nUndo",()=>{CancelStroke();document.Undo();Refresh();});redo=EditorImages.Button("redo","重做\nRedo",()=>{CancelStroke();document.Redo();Refresh();});C<WrapPanel>("Tools").Children.Add(undo);C<WrapPanel>("Tools").Children.Add(redo);
   foreach(string value in new[]{"#EF4444","#F59E0B","#FACC15","#2563EB","#9333EA","#111827","#FFFFFF"}){var shade=(Color)ColorConverter.ConvertFromString(value);var b=new Button{Width=23,Height=23,Margin=new Thickness(2),Padding=new Thickness(0),Background=new SolidColorBrush(shade),ToolTip=value,BorderBrush=Brushes.Gray};b.Click+=(o,e)=>{color=Drawing.Color.FromArgb(shade.R,shade.G,shade.B);UpdateColor();};C<StackPanel>("Colors").Children.Add(b);}
   C<Button>("CustomColor").Click+=(o,e)=>{using(var d=new Forms.ColorDialog{FullOpen=true,Color=color})if(d.ShowDialog()==Forms.DialogResult.OK){color=d.Color;UpdateColor();}};
   C<Slider>("ToolSize").ValueChanged+=(o,e)=>{C<TextBlock>("SizeValue").Text=((int)e.NewValue)+" px";toolSizes[(int)tool]=e.NewValue;};C<TextBlock>("SizeValue").Text="4 px";C<ComboBox>("FontChoice").SelectedIndex=0;
   C<ScrollViewer>("Viewport").PreviewMouseWheel+=WheelSize;C<Slider>("ToolSize").PreviewMouseWheel+=WheelSize;C<Slider>("ToolSize").ToolTip="滚轮调大小；Shift + 滚轮加快 / Wheel: size; Shift: faster";
   C<ComboBox>("Zoom").SelectedIndex=0;C<ComboBox>("Zoom").SelectionChanged+=(o,e)=>UpdateZoom();C<ScrollViewer>("Viewport").SizeChanged+=(o,e)=>{if(C<ComboBox>("Zoom").SelectedIndex==0)UpdateZoom();};
   Action<string,string,Action> action=(icon,label,fn)=>C<WrapPanel>("Actions").Children.Add(EditorImages.Button(icon,label,()=>{try{fn();}catch(Exception ex){Warn(ex.Message);}}));
   action("save","保存\nSave",()=>SaveDefault());action("clipboard","复制图片\nClipboard",()=>Copy());action("folder","保存到文件\nSave as",()=>SaveAs());action("pin","钉在屏幕\nPin",()=>Pin());action("ocr","识别文字\nOCR",()=>OpenOcr());action("clear","清空标注\nClear marks",()=>{document.Add(new Mark{Tool=MarkTool.Clear});Refresh();});action("trash","删除截图\nDiscard",()=>{Outcome="已丢弃本次截图 / Discarded";Close();});action("cancel","取消\nCancel",()=>Close());
   C<Image>("Picture").Width=image.Width;C<Image>("Picture").Height=image.Height;C<Grid>("ImageHost").Width=image.Width;C<Grid>("ImageHost").Height=image.Height;
   C<TextBlock>("ImageSize").Text=image.Width+" × "+image.Height+" px · 保持原始分辨率 / Original resolution";
   var picture=C<Image>("Picture");picture.MouseLeftButtonDown+=Begin;picture.MouseMove+=Move;picture.MouseLeftButtonUp+=End;picture.LostMouseCapture+=(o,e)=>{if(pending!=null){pending=null;Refresh();}};
   PreviewKeyDown+=Keys;Loaded+=(o,e)=>{SelectTool(MarkTool.Pen);Refresh();UpdateZoom();ThemePalette.Decorate(view,Resources["ThemeName"] as string);};Closed+=(o,e)=>{closed=true;document.Dispose();};
  }
  void Warn(string text){MessageBox.Show(this,text,"定框截图",MessageBoxButton.OK,MessageBoxImage.Warning);}
  void SelectTool(MarkTool value){CancelStroke();double desired=toolSizes[(int)value];toolSizes[(int)tool]=C<Slider>("ToolSize").Value;tool=value;wheelRemainder=0;foreach(var pair in toolButtons)pair.Value.Background=pair.Key==tool?new SolidColorBrush(Color.FromRgb(202,220,255)):Brushes.White;
   C<Slider>("ToolSize").Maximum=tool==MarkTool.Text?150:80;C<Slider>("ToolSize").Minimum=tool==MarkTool.Text?8:1;
   C<Slider>("ToolSize").Value=desired;
   C<TextBlock>("SizeLabel").Text=tool==MarkTool.Text?"字号 / Size":tool==MarkTool.Mosaic?"色块 / Block":"粗细 / Size";
   bool hasColor=tool!=MarkTool.Eraser&&tool!=MarkTool.Mosaic;C<StackPanel>("Colors").IsEnabled=hasColor;C<Button>("CustomColor").IsEnabled=hasColor;C<ComboBox>("FontChoice").IsEnabled=tool==MarkTool.Text;UpdateColor();
   C<TextBlock>("EditorStatus").Text=(tool==MarkTool.Mosaic?"拖出矩形打码":tool==MarkTool.Eraser?"擦除标注，保留底图":tool==MarkTool.Text?"点击图片输入文字":"拖动鼠标绘制")+" · 滚轮调大小 / Wheel: size · Shift 加速 · Ctrl+滚轮缩放 / Zoom";
  }
  void AdjustToolSize(int delta,bool faster){wheelRemainder+=delta;int steps=wheelRemainder/120;wheelRemainder%=120;if(steps==0)return;var slider=C<Slider>("ToolSize");slider.Value=Math.Max(slider.Minimum,Math.Min(slider.Maximum,slider.Value+steps*(faster?5:1)));}
  void WheelSize(object sender,MouseWheelEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)!=0){int index=C<ComboBox>("Zoom").SelectedIndex;if(index==0)index=2;C<ComboBox>("Zoom").SelectedIndex=Math.Max(1,Math.Min(4,index+(e.Delta>0?1:-1)));}else AdjustToolSize(e.Delta,(Keyboard.Modifiers&ModifierKeys.Shift)!=0);e.Handled=true;}
  void UpdateColor(){C<Button>("CustomColor").BorderBrush=new SolidColorBrush(Color.FromRgb(color.R,color.G,color.B));C<Button>("CustomColor").Foreground=new SolidColorBrush(Color.FromRgb(color.R,color.G,color.B));}
  Drawing.PointF Point(MouseEventArgs e){var p=e.GetPosition(C<Image>("Picture"));return new Drawing.PointF((float)Math.Max(0,Math.Min(document.Width-1,p.X)),(float)Math.Max(0,Math.Min(document.Height-1,p.Y)));}
  Mark NewMark(Drawing.PointF p){var mark=new Mark{Tool=tool,Color=color,Size=(float)C<Slider>("ToolSize").Value,Font=((ComboBoxItem)C<ComboBox>("FontChoice").SelectedItem).Content.ToString()};mark.Points.Add(p);return mark;}
  void Begin(object sender,MouseButtonEventArgs e){if(e.LeftButton!=MouseButtonState.Pressed)return;Focus();var p=Point(e);if(tool==MarkTool.Text){var entry=new TextEntry{Owner=this};if(entry.ShowDialog()==true&&!string.IsNullOrWhiteSpace(entry.Value)){var mark=NewMark(p);mark.Text=entry.Value;document.Add(mark);Refresh();}return;}pending=NewMark(p);C<Image>("Picture").CaptureMouse();Refresh();e.Handled=true;}
  void Move(object sender,MouseEventArgs e){if(pending==null||e.LeftButton!=MouseButtonState.Pressed)return;UpdatePoint(Point(e));if(paintClock.ElapsedMilliseconds>=32){Refresh();paintClock.Restart();}}
  void UpdatePoint(Drawing.PointF p){if(pending.Tool==MarkTool.Pen||pending.Tool==MarkTool.Highlighter||pending.Tool==MarkTool.Eraser){var last=pending.Points[pending.Points.Count-1];if(Math.Abs(last.X-p.X)+Math.Abs(last.Y-p.Y)>.5)pending.Points.Add(p);}else{if(pending.Points.Count==1)pending.Points.Add(p);else pending.Points[1]=p;}}
  void End(object sender,MouseButtonEventArgs e){if(pending==null)return;UpdatePoint(Point(e));Mark finished=pending;pending=null;C<Image>("Picture").ReleaseMouseCapture();document.Add(finished);Refresh();e.Handled=true;}
  void CancelStroke(){pending=null;C<Image>("Picture").ReleaseMouseCapture();if(document!=null)Refresh();}
  void Refresh(){if(closed)return;using(var bitmap=document.Render(pending))C<Image>("Picture").Source=EditorImages.Source(bitmap);undo.IsEnabled=document.CanUndo;redo.IsEnabled=document.CanRedo;}
  void UpdateZoom(){double factor;int index=C<ComboBox>("Zoom").SelectedIndex;if(index<=0){var v=C<ScrollViewer>("Viewport");factor=Math.Min(1,Math.Min(Math.Max(40,v.ActualWidth-22)/document.Width,Math.Max(40,v.ActualHeight-22)/document.Height));}else factor=new[]{.5,1,1.5,2}[index-1];C<Grid>("ImageHost").LayoutTransform=new ScaleTransform(factor,factor);}
  void SaveDefault(){using(var bitmap=document.Render()){string path=save(bitmap);Outcome="已保存 / "+path;}Close();}
  void SaveAs(){var d=new Microsoft.Win32.SaveFileDialog{Title="保存截图 / Save image",Filter="PNG 图片|*.png",DefaultExt=".png",AddExtension=true,OverwritePrompt=true,FileName="截图_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".png"};if(Directory.Exists(folder))d.InitialDirectory=folder;if(d.ShowDialog(this)==true){using(var bitmap=document.Render())EditorImages.SavePng(bitmap,d.FileName);Outcome="已保存 / "+d.FileName;Close();}}
  void Copy(){using(var bitmap=document.Render())LocalClipboard.Image(EditorImages.Source(bitmap));Outcome="已复制图片到剪贴板 / Copied image";Close();}
  void Pin(){using(var bitmap=document.Render())PinWindow.ShowImage(bitmap,region,Resources);Outcome="已钉在屏幕上 / Pinned";Close();}
  void OpenOcr(){using(var bitmap=document.Render())new OcrWindow(bitmap,Resources){Owner=this}.ShowDialog();}
  void Keys(object sender,KeyEventArgs e){try{bool ctrl=(Keyboard.Modifiers&ModifierKeys.Control)!=0,shift=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;
   if(e.Key==System.Windows.Input.Key.Escape){if(pending!=null)CancelStroke();else Close();e.Handled=true;}
   else if(ctrl&&e.Key==System.Windows.Input.Key.Z){CancelStroke();if(shift)document.Redo();else document.Undo();Refresh();e.Handled=true;}
   else if(ctrl&&e.Key==System.Windows.Input.Key.Y){CancelStroke();document.Redo();Refresh();e.Handled=true;}
   else if(ctrl&&e.Key==System.Windows.Input.Key.S){if(shift)SaveAs();else SaveDefault();e.Handled=true;}
   else if(ctrl&&e.Key==System.Windows.Input.Key.C){Copy();e.Handled=true;}
  }catch(Exception ex){Warn(ex.Message);}}
 }
 sealed class TextEntry:Window {
  TextBox input;public string Value{get{return input.Text;}}
  public TextEntry(){Title="输入文字 / Add text";Width=480;Height=270;WindowStartupLocation=WindowStartupLocation.CenterOwner;FontFamily=new FontFamily("Microsoft YaHei UI");var panel=new DockPanel{Margin=new Thickness(16)};var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(buttons,Dock.Bottom);var cancel=new Button{Content="取消 / Cancel",Padding=new Thickness(14,7,14,7),Margin=new Thickness(8),IsCancel=true};var ok=new Button{Content="添加 / Add",Padding=new Thickness(14,7,14,7),Margin=new Thickness(8)};ok.Click+=(o,e)=>{DialogResult=true;};buttons.Children.Add(cancel);buttons.Children.Add(ok);panel.Children.Add(buttons);input=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontSize=18};panel.Children.Add(input);Content=panel;Loaded+=(o,e)=>input.Focus();}
 }
}
