using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace FixedCapture {
 // Publish fully materialized data in one clipboard transaction. This avoids the
 // OLE delayed-render/Flush race with clipboard synchronization tools.
 static class LocalClipboard {
  [DllImport("user32.dll",SetLastError=true)]static extern bool OpenClipboard(IntPtr window);
  [DllImport("user32.dll")]static extern bool CloseClipboard();
  [DllImport("user32.dll",SetLastError=true)]static extern bool EmptyClipboard();
  [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetClipboardData(uint format,IntPtr memory);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterClipboardFormat(string format);
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr GlobalAlloc(uint flags,UIntPtr size);
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr GlobalLock(IntPtr memory);
  [DllImport("kernel32.dll")]static extern bool GlobalUnlock(IntPtr memory);
  [DllImport("kernel32.dll")]static extern IntPtr GlobalFree(IntPtr memory);
  public static void Image(BitmapSource image){
   byte[] dib,png;using(var stream=new MemoryStream()){var encoder=new BmpBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);byte[] bmp=stream.ToArray();dib=new byte[bmp.Length-14];Buffer.BlockCopy(bmp,14,dib,0,dib.Length);}
   using(var stream=new MemoryStream()){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);png=stream.ToArray();}
   Write(new[]{8u,RegisterClipboardFormat("PNG")},new[]{dib,png});
  }
  public static void Text(string value){Write(new[]{13u},new[]{System.Text.Encoding.Unicode.GetBytes(value+"\0")});}
  static void Write(uint[] formats,byte[][] bytes){
   var buffers=new IntPtr[formats.Length];bool opened=false;
   try{
    for(int i=0;i<buffers.Length;i++){if(formats[i]==0)throw new Exception("无法注册剪贴板格式 / Clipboard format unavailable.");buffers[i]=GlobalAlloc(0x42,(UIntPtr)bytes[i].Length);if(buffers[i]==IntPtr.Zero)throw new OutOfMemoryException();IntPtr target=GlobalLock(buffers[i]);if(target==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();try{Marshal.Copy(bytes[i],0,target,bytes[i].Length);}finally{GlobalUnlock(buffers[i]);}}
    IntPtr owner=new WindowInteropHelper(Application.Current.MainWindow).Handle;
    for(int attempt=0;attempt<12&&!opened;attempt++){opened=OpenClipboard(owner);if(!opened)System.Threading.Thread.Sleep(75);}
    if(!opened)throw new Exception("剪贴板正被其他程序占用，请稍后再点复制 / Clipboard is busy; please retry.");
    if(!EmptyClipboard())throw new System.ComponentModel.Win32Exception();
    for(int i=0;i<buffers.Length;i++){if(SetClipboardData(formats[i],buffers[i])==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();buffers[i]=IntPtr.Zero;}
   }finally{if(opened)CloseClipboard();foreach(IntPtr buffer in buffers)if(buffer!=IntPtr.Zero)GlobalFree(buffer);}
  }
 }
}
