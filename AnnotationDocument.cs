using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FixedCapture {
 enum MarkTool { Pen, Highlighter, Eraser, Mosaic, Arrow, Text, Ellipse, Rectangle, Clear }
 sealed class Mark {
  public MarkTool Tool;
  public Color Color=Color.Red;
  public float Size=4;
  public string Text="",Font="Microsoft YaHei UI";
  public readonly List<PointF> Points=new List<PointF>();
  public Mark Clone(){var m=new Mark{Tool=Tool,Color=Color,Size=Size,Text=Text,Font=Font};m.Points.AddRange(Points);return m;}
 }
 // Keep annotations separate from the source. Erasing never destroys screenshot pixels.
 sealed class AnnotationDocument:IDisposable {
  readonly Bitmap original; readonly List<Mark> history=new List<Mark>();
  Bitmap overlay; int cursor;
  public int Width{get{return original.Width;}} public int Height{get{return original.Height;}}
  public bool CanUndo{get{return cursor>0;}} public bool CanRedo{get{return cursor<history.Count;}}
  public AnnotationDocument(Bitmap image){original=new Bitmap(image);Rebuild();}
  public void Add(Mark mark){if(cursor<history.Count)history.RemoveRange(cursor,history.Count-cursor);history.Add(mark.Clone());cursor++;Rebuild();}
  public void Undo(){if(CanUndo){cursor--;Rebuild();}}
  public void Redo(){if(CanRedo){cursor++;Rebuild();}}
  void Rebuild(){if(overlay!=null)overlay.Dispose();overlay=new Bitmap(Width,Height,PixelFormat.Format32bppArgb);for(int i=0;i<cursor;i++)Paint(overlay,history[i]);}
  public Bitmap Render(Mark preview=null){using(var layer=new Bitmap(overlay)){if(preview!=null)Paint(layer,preview);var result=new Bitmap(original);using(var g=Graphics.FromImage(result))g.DrawImageUnscaled(layer,0,0);return result;}}
  void Paint(Bitmap layer,Mark mark){
   if(mark.Tool==MarkTool.Clear){using(var g=Graphics.FromImage(layer))g.Clear(Color.Transparent);return;}
   if(mark.Points.Count==0)return;
   PointF a=mark.Points[0],b=mark.Points[mark.Points.Count-1];
   var rect=RectangleF.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y));
   float width=Math.Max(1,mark.Size);
   if(mark.Tool==MarkTool.Mosaic){Mosaic(layer,rect,Math.Max(3,(int)width));return;}
   using(var g=Graphics.FromImage(layer)){
    g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
    Color color=mark.Tool==MarkTool.Highlighter?Color.FromArgb(85,mark.Color):mark.Color;
    if(mark.Tool==MarkTool.Eraser){g.CompositingMode=CompositingMode.SourceCopy;g.SmoothingMode=SmoothingMode.None;color=Color.Transparent;}
    using(var pen=new Pen(color,width))using(var brush=new SolidBrush(color)){
     pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
     switch(mark.Tool){
      case MarkTool.Pen:case MarkTool.Highlighter:case MarkTool.Eraser:
       if(mark.Points.Count==1)g.FillEllipse(brush,a.X-width/2,a.Y-width/2,width,width);
       else {using(var path=new GraphicsPath()){path.AddLines(mark.Points.ToArray());g.DrawPath(pen,path);}}break;
      case MarkTool.Arrow:
       if(Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)<1)break;
       g.DrawLine(pen,a,b);double angle=Math.Atan2(b.Y-a.Y,b.X-a.X);float head=Math.Max(10,width*3.5f);
       PointF l=new PointF(b.X-head*(float)Math.Cos(angle-.48),b.Y-head*(float)Math.Sin(angle-.48)),r=new PointF(b.X-head*(float)Math.Cos(angle+.48),b.Y-head*(float)Math.Sin(angle+.48));
       g.FillPolygon(brush,new[]{b,l,r});break;
      case MarkTool.Ellipse:if(rect.Width>0&&rect.Height>0)g.DrawEllipse(pen,rect);break;
      case MarkTool.Rectangle:if(rect.Width>0&&rect.Height>0)g.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);break;
      case MarkTool.Text:
       using(var font=new Font(mark.Font,Math.Max(8,width),FontStyle.Regular,GraphicsUnit.Pixel)){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;g.DrawString(mark.Text,font,brush,a,StringFormat.GenericTypographic);}break;
     }
    }
   }
  }
  void Mosaic(Bitmap layer,RectangleF area,int block){
   Rectangle rect=Rectangle.Intersect(new Rectangle(0,0,Width,Height),Rectangle.Ceiling(area));if(rect.Width<1||rect.Height<1)return;
   using(var composite=new Bitmap(original)){
    using(var g=Graphics.FromImage(composite))g.DrawImageUnscaled(layer,0,0);
    var data=composite.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);byte[] pixels=new byte[Math.Abs(data.Stride)*Height];int stride=data.Stride;
    try{System.Runtime.InteropServices.Marshal.Copy(data.Scan0,pixels,0,pixels.Length);}finally{composite.UnlockBits(data);}
    // Sample the visible composite once, then flatten full, opaque blocks into the output.
    using(var g=Graphics.FromImage(layer)){g.CompositingMode=CompositingMode.SourceCopy;g.SmoothingMode=SmoothingMode.None;
     for(int y=rect.Top;y<rect.Bottom;y+=block)for(int x=rect.Left;x<rect.Right;x+=block){int w=Math.Min(block,rect.Right-x),h=Math.Min(block,rect.Bottom-y);long rr=0,gg=0,bb=0,n=0;int step=Math.Max(1,block/5);
      for(int sy=y;sy<y+h;sy+=step)for(int sx=x;sx<x+w;sx+=step){int offset=sy*stride+sx*4;bb+=pixels[offset];gg+=pixels[offset+1];rr+=pixels[offset+2];n++;}
      using(var brush=new SolidBrush(Color.FromArgb((int)(rr/n),(int)(gg/n),(int)(bb/n))))g.FillRectangle(brush,x,y,w,h);
     }
    }
   }
  }
  public void Dispose(){original.Dispose();if(overlay!=null)overlay.Dispose();}
 }
}
