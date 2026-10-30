using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace FixedCapture {
 static class FrameFingerprint {
  public static string Compute(Bitmap image) {
   var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
   try {using(var hash=SHA256.Create()) {
    byte[] size=System.Text.Encoding.ASCII.GetBytes(image.Width+"x"+image.Height+":");hash.TransformBlock(size,0,size.Length,size,0);
    byte[] row=new byte[image.Width*4];
    for(int y=0;y<image.Height;y++){Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);for(int x=3;x<row.Length;x+=4)row[x]=255;hash.TransformBlock(row,0,row.Length,row,0);}
    hash.TransformFinalBlock(new byte[0],0,0);return Convert.ToBase64String(hash.Hash);
   }} finally {image.UnlockBits(data);}
  }
 }
 static class ShutterSound {
  static MemoryStream audio;static System.Media.SoundPlayer player;
  public static byte[] Wave(){using(var stream=new MemoryStream()){using(var w=new BinaryWriter(stream)){
   const int rate=22050,n=4410;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+n*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(n*2);
   var random=new Random(17);for(int i=0;i<n;i++){double t=(double)i/rate;double envelope=Math.Exp(-t*90)+(t>=0.075?0.65*Math.Exp(-(t-0.075)*110):0);double noise=random.NextDouble()*2-1;w.Write((short)(7000*envelope*(0.8*noise+0.2*Math.Sin(2*Math.PI*1400*t))));}w.Flush();return stream.ToArray();
  }}}
  public static void Play(){if(player==null){audio=new MemoryStream(Wave());player=new System.Media.SoundPlayer(audio);player.Load();}player.Play();}
  public static void Dispose(){if(player!=null)player.Dispose();if(audio!=null)audio.Dispose();}
 }
 class CaptureToast:Window {
  DispatcherTimer timer=new DispatcherTimer();System.Windows.Controls.Image preview=new System.Windows.Controls.Image();TextBlock name=new TextBlock();string path;
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
  protected override void OnSourceInitialized(EventArgs e){base.OnSourceInitialized(e);var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;SetWindowLong(hwnd,-20,GetWindowLong(hwnd,-20)|0x08000000|0x80);}
  public CaptureToast(){Title="截图已保存";Width=252;Height=208;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=System.Windows.Media.Brushes.Transparent;ShowInTaskbar=false;Topmost=true;ShowActivated=false;
   var panel=new StackPanel();var header=new DockPanel{Margin=new Thickness(0,0,0,8)};var close=new Button{Content="×",Width=24,Height=24,Background=System.Windows.Media.Brushes.Transparent,BorderThickness=new Thickness(0)};close.Click+=(s,e)=>Dismiss();DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);header.Children.Add(new TextBlock{Text="✓  已保存 / Saved",FontSize=13,VerticalAlignment=VerticalAlignment.Center});panel.Children.Add(header);
   preview.Height=116;preview.Stretch=Stretch.Uniform;preview.Cursor=System.Windows.Input.Cursors.Hand;preview.ToolTip="点击打开 / Open image";preview.MouseLeftButtonUp+=(s,e)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});}catch{}Dismiss();};panel.Children.Add(preview);
   name.FontSize=11;name.Foreground=System.Windows.Media.Brushes.DimGray;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new Thickness(0,9,0,0);panel.Children.Add(name);
   Content=new Border{CornerRadius=new CornerRadius(18),Background=new SolidColorBrush(System.Windows.Media.Color.FromArgb(245,248,248,250)),BorderBrush=System.Windows.Media.Brushes.White,BorderThickness=new Thickness(2),Padding=new Thickness(12),Margin=new Thickness(4),Child=panel};timer.Interval=TimeSpan.FromSeconds(3);timer.Tick+=(s,e)=>Dismiss();Closed+=(s,e)=>timer.Stop();
  }
  public void Dismiss(){timer.Stop();Hide();}
  public void Present(Bitmap bitmap,string file,Rectangle region){path=file;using(var ms=new MemoryStream()){bitmap.Save(ms,ImageFormat.Png);ms.Position=0;var src=new BitmapImage();src.BeginInit();src.CacheOption=BitmapCacheOption.OnLoad;src.StreamSource=ms;src.EndInit();src.Freeze();preview.Source=src;}name.Text=Path.GetFileName(file);Show();
   var work=System.Windows.Forms.Screen.FromRectangle(region).WorkingArea;var source=System.Windows.PresentationSource.FromVisual(this);var transform=source.CompositionTarget.TransformFromDevice;var point=transform.Transform(new System.Windows.Point(work.Left+20,work.Bottom-20));Left=point.X;Top=point.Y-Height;BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));timer.Stop();timer.Start();
  }
 }
}


