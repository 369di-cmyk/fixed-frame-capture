using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing=System.Drawing;

namespace FixedCapture {
 sealed class PinWindow:Window {
  static readonly List<PinWindow> pins=new List<PinWindow>();readonly BitmapSource image;bool closed;
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
  public static void ShowImage(Drawing.Bitmap bitmap,Drawing.Rectangle area,ResourceDictionary styles){int w=Math.Min(900,bitmap.Width+8),h=Math.Min(720,bitmap.Height+42);var window=new PinWindow(EditorImages.Source(bitmap),styles);window.Loaded+=(o,e)=>{SetWindowPos(new WindowInteropHelper(window).Handle,IntPtr.Zero,area.X,area.Y,Math.Max(180,w),Math.Max(100,h),0x0014);};window.Show();}
  public static List<PinWindow> HideForCapture(){var visible=new List<PinWindow>();foreach(var pin in pins.ToArray())if(pin.IsVisible){visible.Add(pin);pin.Hide();}return visible;}
  public static void RestoreAfterCapture(List<PinWindow> hidden){if(hidden==null)return;foreach(var pin in hidden)if(!pin.closed){pin.ShowActivated=false;pin.Show();}}
  public static void CloseAll(){foreach(var pin in pins.ToArray())pin.Close();}
  PinWindow(BitmapSource source,ResourceDictionary styles){
   image=source;Title="钉图 · 拖动移动 / Pin";Topmost=true;ShowInTaskbar=false;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResizeWithGrip;Background=Brushes.White;MinWidth=180;MinHeight=100;Width=600;Height=400;Resources=styles;WindowStartupLocation=WindowStartupLocation.CenterScreen;
   var border=new Border{CornerRadius=new CornerRadius(10),BorderBrush=new SolidColorBrush(Color.FromRgb(147,165,200)),BorderThickness=new Thickness(2),Padding=new Thickness(3),Background=Brushes.White};var grid=new DockPanel();border.Child=grid;Content=border;
   var title=new DockPanel{Height=30,Background=new SolidColorBrush(Color.FromRgb(232,238,249))};DockPanel.SetDock(title,Dock.Top);grid.Children.Add(title);var close=new Button{Content="×",Width=32,Height=26,Padding=new Thickness(0),ToolTip="关闭钉图 / Close pin (Esc)"};close.Click+=(o,e)=>Close();DockPanel.SetDock(close,Dock.Right);title.Children.Add(close);title.Children.Add(new TextBlock{Text="  滚轮缩放 · 双击关闭",FontSize=11,VerticalAlignment=VerticalAlignment.Center});
   var picture=new Image{Source=image,Stretch=Stretch.Uniform,Width=double.NaN,Height=double.NaN,ToolTip="滚轮缩放 · 双击关闭 / Wheel to resize · Double-click to close"};grid.Children.Add(picture);title.MouseLeftButtonDown+=(o,e)=>{if(e.OriginalSource is TextBlock||e.OriginalSource==title)DragMove();};picture.MouseLeftButtonDown+=(o,e)=>{if(e.ClickCount>=2){ClosePin();e.Handled=true;}else DragMove();};
   MouseWheel+=(o,e)=>{ResizeByWheel(e.Delta);e.Handled=true;};
   var menu=new ContextMenu();Action<string,Action> item=(text,action)=>{var entry=new MenuItem{Header=text};entry.Click+=(o,e)=>{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"钉图");}};menu.Items.Add(entry);};item("复制图片 / Copy",()=>LocalClipboard.Image(image));item("保存到文件 / Save as",()=>{var d=new Microsoft.Win32.SaveFileDialog{Filter="PNG 图片|*.png",DefaultExt=".png",FileName="钉图.png",OverwritePrompt=true};if(d.ShowDialog(this)==true){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var ms=new MemoryStream()){encoder.Save(ms);ms.Position=0;using(var bitmap=new Drawing.Bitmap(ms))EditorImages.SavePng(bitmap,d.FileName);}}});item("关闭钉图 / Close",()=>Close());ContextMenu=menu;
   KeyDown+=(o,e)=>{if(e.Key==Key.Escape)Close();else if(e.Key==Key.C&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){try{LocalClipboard.Image(image);}catch(Exception ex){MessageBox.Show(this,ex.Message,"钉图");}}};
   pins.Add(this);Closed+=(o,e)=>{closed=true;pins.Remove(this);};
  }
  void ClosePin(){Close();}
  void ResizeByWheel(int delta){double factor=Math.Pow(1.1,delta/120.0);double scale=Math.Max(Math.Max(MinWidth/Width,MinHeight/Height),Math.Min(Math.Min(SystemParameters.WorkArea.Width/Width,SystemParameters.WorkArea.Height/Height),factor));Width*=scale;Height*=scale;}
 }
}
