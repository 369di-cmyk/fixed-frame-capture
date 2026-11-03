using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FixedCapture {
 sealed class WindowRegion {
  public Rectangle Bounds;public string Title;
 }
 static class WindowRegions {
  delegate bool EnumProc(IntPtr window,IntPtr param);
  [StructLayout(LayoutKind.Sequential)]struct Rect{public int L,T,R,B;}
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,IntPtr param);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr window,int index);
  [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect rect);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int length);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder text,int length);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]static extern int FrameBounds(IntPtr window,int attribute,out Rect rect,int size);
  [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]static extern int Cloaked(IntPtr window,int attribute,out int value,int size);
  public static List<WindowRegion> Snapshot(Rectangle desktop){var result=new List<WindowRegion>();uint own=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
   EnumWindows((window,param)=>{try{
    uint process;GetWindowThreadProcessId(window,out process);if(process==own||!IsWindowVisible(window)||IsIconic(window))return true;
    // Ignore click-through and non-activating overlays, including cursor aids.
    if((GetWindowLong(window,-20)&0x08000020)!=0)return true;
    int hidden;if(Cloaked(window,14,out hidden,4)==0&&hidden!=0)return true;
    var className=new StringBuilder(128);GetClassName(window,className,className.Capacity);if(className.ToString()=="Progman"||className.ToString()=="WorkerW"||className.ToString()=="Shell_TrayWnd"||className.ToString()=="Shell_SecondaryTrayWnd")return true;
    var title=new StringBuilder(256);GetWindowText(window,title,title.Capacity);if(title.Length==0)return true;
    Rect native;if(FrameBounds(window,9,out native,16)!=0&&!GetWindowRect(window,out native))return true;
    Rectangle bounds=Rectangle.Intersect(desktop,Rectangle.FromLTRB(native.L,native.T,native.R,native.B));if(bounds.Width>=40&&bounds.Height>=30)result.Add(new WindowRegion{Bounds=bounds,Title=title.ToString()});
   }catch{}return true;},IntPtr.Zero);return result;
  }
 }
 static class SelectionMath {
  public static Rectangle Move(Rectangle start,Point delta,Size desktop){return new Rectangle(Math.Max(0,Math.Min(desktop.Width-start.Width,start.X+delta.X)),Math.Max(0,Math.Min(desktop.Height-start.Height,start.Y+delta.Y)),start.Width,start.Height);}
  public static Rectangle Resize(Rectangle start,Point delta,int handle,Size desktop){int l=start.Left,t=start.Top,r=start.Right,b=start.Bottom;bool left=handle==0||handle==6||handle==7,right=handle==2||handle==3||handle==4,top=handle==0||handle==1||handle==2,bottom=handle==4||handle==5||handle==6;
   if(left)l=Math.Max(0,Math.Min(r-2,l+delta.X));if(right)r=Math.Min(desktop.Width,Math.Max(l+2,r+delta.X));if(top)t=Math.Max(0,Math.Min(b-2,t+delta.Y));if(bottom)b=Math.Min(desktop.Height,Math.Max(t+2,b+delta.Y));return Rectangle.FromLTRB(l,t,r,b);
  }
 }
 class RegionPicker:Form {
  Bitmap desktop;readonly Rectangle screen;readonly bool fixedSize;readonly Size size;readonly List<WindowRegion> windows;
  Rectangle selection,baseline;Point anchor;bool editing,dragging;int resizeHandle=-1;string selectedTitle="";WindowRegion hovered;
  enum DragMode{New,Move,Resize}DragMode dragMode;
  Rectangle confirmButton,resetButton,cancelButton;
  public Rectangle Selected;
  public RegionPicker(bool fixedMode,Size requested){
   Text="定框截图 - 选择区域";fixedSize=fixedMode;size=requested;screen=SystemInformation.VirtualScreen;windows=WindowRegions.Snapshot(screen);
   desktop=new Bitmap(screen.Width,screen.Height);using(var graphics=Graphics.FromImage(desktop))graphics.CopyFromScreen(screen.Location,Point.Empty,screen.Size);
   FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=screen;TopMost=true;ShowInTaskbar=true;DoubleBuffered=true;KeyPreview=true;Cursor=Cursors.Cross;
   MouseMove+=MovePointer;MouseDown+=Press;MouseUp+=Release;
   MouseDoubleClick+=(o,e)=>{if(e.Button==MouseButtons.Left&&editing&&selection.Contains(e.Location))Confirm();};
   KeyDown+=(o,e)=>{if(e.KeyCode==Keys.Escape)Cancel();else if(e.KeyCode==Keys.Enter&&editing)Confirm();else if(e.KeyCode==Keys.R)ResetSelection();else if(editing){int step=e.Shift?10:1;Point delta=Point.Empty;if(e.KeyCode==Keys.Left)delta.X=-step;if(e.KeyCode==Keys.Right)delta.X=step;if(e.KeyCode==Keys.Up)delta.Y=-step;if(e.KeyCode==Keys.Down)delta.Y=step;if(delta!=Point.Empty){selection=SelectionMath.Move(selection,delta,screen.Size);Invalidate();e.Handled=true;}}};
  }
  Point Clamp(Point p){return new Point(Math.Max(0,Math.Min(screen.Width,p.X)),Math.Max(0,Math.Min(screen.Height,p.Y)));}
  WindowRegion WindowAt(Point local){var absolute=new Point(local.X+screen.X,local.Y+screen.Y);foreach(var window in windows)if(window.Bounds.Contains(absolute))return window;return null;}
  Rectangle Local(Rectangle absolute){absolute.Offset(-screen.X,-screen.Y);return absolute;}
  Point[] Handles(){int l=selection.Left,r=selection.Right,t=selection.Top,b=selection.Bottom,cx=(l+r)/2,cy=(t+b)/2;return new[]{new Point(l,t),new Point(cx,t),new Point(r,t),new Point(r,cy),new Point(r,b),new Point(cx,b),new Point(l,b),new Point(l,cy)};}
  int HitHandle(Point p){if(!editing)return -1;var handles=Handles();for(int i=0;i<handles.Length;i++)if(Math.Abs(handles[i].X-p.X)<=8&&Math.Abs(handles[i].Y-p.Y)<=8)return i;return -1;}
  void MovePointer(object sender,MouseEventArgs e){Point p=Clamp(e.Location);
   if(dragging){Point delta=new Point(p.X-anchor.X,p.Y-anchor.Y);if(dragMode==DragMode.Move)selection=SelectionMath.Move(baseline,delta,screen.Size);else if(dragMode==DragMode.Resize)selection=SelectionMath.Resize(baseline,delta,resizeHandle,screen.Size);else if(fixedSize)selection=Position(p);else selection=Rectangle.FromLTRB(Math.Min(anchor.X,p.X),Math.Min(anchor.Y,p.Y),Math.Max(anchor.X,p.X),Math.Max(anchor.Y,p.Y));Invalidate();return;}
   if(!editing){hovered=fixedSize?null:WindowAt(p);selection=fixedSize?Position(p):hovered==null?Rectangle.Empty:Local(hovered.Bounds);Cursor=Cursors.Cross;Invalidate();}
   else{int h=HitHandle(p);Cursor=h==0||h==4?Cursors.SizeNWSE:h==2||h==6?Cursors.SizeNESW:h==1||h==5?Cursors.SizeNS:h==3||h==7?Cursors.SizeWE:selection.Contains(p)?Cursors.SizeAll:Cursors.Cross;}
  }
  Rectangle Position(Point p){return new Rectangle(Math.Max(0,Math.Min(screen.Width-size.Width,p.X)),Math.Max(0,Math.Min(screen.Height-size.Height,p.Y)),size.Width,size.Height);}
  void Press(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Right){Cancel();return;}if(e.Button!=MouseButtons.Left)return;
   if(editing){if(confirmButton.Contains(e.Location)){Confirm();return;}if(resetButton.Contains(e.Location)){ResetSelection();return;}if(cancelButton.Contains(e.Location)){Cancel();return;}}
   anchor=Clamp(e.Location);baseline=selection;resizeHandle=HitHandle(anchor);dragMode=editing&&resizeHandle>=0?DragMode.Resize:editing&&selection.Contains(anchor)?DragMode.Move:DragMode.New;
   if(dragMode==DragMode.New){editing=false;hovered=fixedSize?null:WindowAt(anchor);selectedTitle="";if(!fixedSize)selection=Rectangle.Empty;}
   dragging=true;Capture=true;Invalidate();
  }
  void Release(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left||!dragging)return;MovePointer(sender,e);dragging=false;Capture=false;
   if(dragMode==DragMode.New){Point p=Clamp(e.Location);bool clicked=Math.Abs(p.X-anchor.X)<=5&&Math.Abs(p.Y-anchor.Y)<=5;if(!fixedSize&&clicked&&hovered!=null){selection=Local(hovered.Bounds);selectedTitle=hovered.Title;}else if(fixedSize)selection=Position(p);}
   editing=selection.Width>=2&&selection.Height>=2;Invalidate();
  }
  void ResetSelection(){editing=false;dragging=false;Capture=false;hovered=null;selection=Rectangle.Empty;selectedTitle="";Invalidate();}
  void Confirm(){if(!editing||selection.Width<2||selection.Height<2)return;Selected=new Rectangle(selection.X+screen.X,selection.Y+screen.Y,selection.Width,selection.Height);DialogResult=DialogResult.OK;Close();}
  void Cancel(){DialogResult=DialogResult.Cancel;Close();}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.DrawImageUnscaled(desktop,0,0);using(var dim=new SolidBrush(Color.FromArgb(130,9,16,31)))g.FillRectangle(dim,ClientRectangle);
   if(selection.Width>0&&selection.Height>0){g.DrawImage(desktop,selection,selection,GraphicsUnit.Pixel);using(var pen=new Pen(editing?Color.FromArgb(86,173,255):Color.FromArgb(246,220,130),2))g.DrawRectangle(pen,selection.X,selection.Y,selection.Width-1,selection.Height-1);if(editing)foreach(var p in Handles()){g.FillRectangle(Brushes.White,p.X-4,p.Y-4,8,8);g.DrawRectangle(Pens.DodgerBlue,p.X-4,p.Y-4,8,8);}}
   string description=editing?"拖动边缘调整大小，拖动内部移动 · Enter / 双击确认 · R 重选 · Esc 取消":fixedSize?"移动鼠标定位，单击后可微调 · Enter / 双击确认":"移动识别窗口 · 单击选窗 / 拖动框选 · Click window / Drag a region";
   string name=editing?selectedTitle:hovered==null?"":hovered.Title;if(name.Length>58)name=name.Substring(0,58)+"…";
   string details=selection.Width+" × "+selection.Height+" px  ·  "+(selection.X+screen.X)+", "+(selection.Y+screen.Y)+(name.Length>0?"  ·  "+name:"");
   var header=new Rectangle(18,18,Math.Min(920,screen.Width-36),76);using(var brush=new SolidBrush(Color.FromArgb(238,18,29,48)))g.FillRectangle(brush,header);using(var font=new Font("Microsoft YaHei UI",11))g.DrawString(description+"\n"+details,font,Brushes.White,new RectangleF(header.X+14,header.Y+12,header.Width-28,60));
   if(editing){int width=320,x=Math.Max(10,Math.Min(screen.Width-width-10,selection.Right-width)),y=selection.Bottom+14;if(y+44>screen.Height-8)y=Math.Max(10,selection.Top-56);confirmButton=new Rectangle(x,y,130,42);resetButton=new Rectangle(x+136,y,90,42);cancelButton=new Rectangle(x+232,y,88,42);DrawButton(g,confirmButton,"✓ 确认 / Enter",true);DrawButton(g,resetButton,"重选 / R",false);DrawButton(g,cancelButton,"取消 / Esc",false);}else{confirmButton=resetButton=cancelButton=Rectangle.Empty;}
  }
  void DrawButton(Graphics g,Rectangle rect,string label,bool primary){g.SmoothingMode=SmoothingMode.AntiAlias;using(var shape=Art.Round(rect,9))using(var brush=new SolidBrush(primary?Color.FromArgb(48,101,214):Color.FromArgb(240,24,36,55)))g.FillPath(brush,shape);using(var font=new Font("Microsoft YaHei UI",10))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(label,font,Brushes.White,rect,format);}
  protected override void Dispose(bool disposing){if(disposing&&desktop!=null){desktop.Dispose();desktop=null;}base.Dispose(disposing);}
 }
}
