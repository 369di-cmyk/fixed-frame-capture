using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FixedCapture {
 static class Art {
  public static Color Teal=Color.FromArgb(48,48,55);
  public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  public static Bitmap Symbol(string name,int pixels,Color color){
   var b=new Bitmap(pixels,pixels);using(var g=Graphics.FromImage(b)){
    g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(pixels/24f,pixels/24f);
    using(var pen=new Pen(color,1.8f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
     switch(name){
      case "pdf":g.DrawLines(pen,new PointF[]{new PointF(5,2),new PointF(14,2),new PointF(20,8),new PointF(20,22),new PointF(5,22),new PointF(5,2)});g.DrawLines(pen,new PointF[]{new PointF(14,2),new PointF(14,8),new PointF(20,8)});g.DrawLine(pen,9,13,16,13);g.DrawLine(pen,9,17,16,17);break;
      case "region":g.DrawLines(pen,new PointF[]{new PointF(3,8),new PointF(3,3),new PointF(8,3)});g.DrawLines(pen,new PointF[]{new PointF(16,3),new PointF(21,3),new PointF(21,8)});g.DrawLines(pen,new PointF[]{new PointF(21,16),new PointF(21,21),new PointF(16,21)});g.DrawLines(pen,new PointF[]{new PointF(8,21),new PointF(3,21),new PointF(3,16)});g.DrawRectangle(pen,7,7,10,10);break;
      case "move":g.DrawLine(pen,12,3,12,21);g.DrawLine(pen,3,12,21,12);g.DrawLines(pen,new PointF[]{new PointF(8,7),new PointF(12,3),new PointF(16,7)});g.DrawLines(pen,new PointF[]{new PointF(8,17),new PointF(12,21),new PointF(16,17)});g.DrawLines(pen,new PointF[]{new PointF(7,8),new PointF(3,12),new PointF(7,16)});g.DrawLines(pen,new PointF[]{new PointF(17,8),new PointF(21,12),new PointF(17,16)});break;
      case "folder":g.DrawLines(pen,new PointF[]{new PointF(3,19),new PointF(3,5),new PointF(9,5),new PointF(12,8),new PointF(21,8),new PointF(21,19),new PointF(3,19)});g.DrawLine(pen,3,10,21,10);break;
      case "tag":g.DrawPolygon(pen,new PointF[]{new PointF(3,3),new PointF(12,3),new PointF(22,13),new PointF(13,22),new PointF(3,12)});g.DrawEllipse(pen,7,7,2,2);break;
      case "key":g.DrawRectangle(pen,2,5,20,14);for(int row=0;row<2;row++)for(int col=0;col<5;col++)g.DrawLine(pen,5+col*3.4f,8+row*4,5.5f+col*3.4f,8+row*4);g.DrawLine(pen,7,16,17,16);break;
      case "clock":g.DrawEllipse(pen,3,3,18,18);g.DrawLines(pen,new PointF[]{new PointF(12,6),new PointF(12,12),new PointF(16,14)});break;
      case "play":using(var brush=new SolidBrush(color))g.FillPolygon(brush,new PointF[]{new PointF(7,4),new PointF(20,12),new PointF(7,20)});break;
      case "pause":using(var thick=new Pen(color,4)){g.DrawLine(thick,8,5,8,19);g.DrawLine(thick,16,5,16,19);}break;
      case "save":g.DrawPolygon(pen,new PointF[]{new PointF(4,3),new PointF(17,3),new PointF(21,7),new PointF(21,21),new PointF(4,21)});g.DrawRectangle(pen,8,3,8,6);g.DrawRectangle(pen,8,14,9,7);break;
      case "hide":g.DrawRectangle(pen,3,3,18,13);g.DrawLine(pen,7,21,17,21);g.DrawLine(pen,12,16,12,21);g.DrawLine(pen,8,10,16,10);break;
      case "exit":g.DrawArc(pen,4,4,16,16,-45,270);g.DrawLine(pen,12,2,12,12);break;
      case "check":g.DrawLines(pen,new PointF[]{new PointF(4,12),new PointF(9,17),new PointF(20,6)});break;
      case "gear":g.DrawEllipse(pen,5,5,14,14);g.DrawEllipse(pen,9,9,6,6);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,12+8*(float)Math.Cos(a),12+8*(float)Math.Sin(a),12+10*(float)Math.Cos(a),12+10*(float)Math.Sin(a));}break;
      default:g.DrawLines(pen,new PointF[]{new PointF(3,7),new PointF(7,7),new PointF(9,4),new PointF(15,4),new PointF(17,7),new PointF(21,7),new PointF(21,20),new PointF(3,20),new PointF(3,7)});g.DrawEllipse(pen,8,9,8,8);break;
     }
    }
   }return b;
  }
  public static Bitmap Brand(int pixels){var b=new Bitmap(pixels,pixels);using(var g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(pixels/64f,pixels/64f);using(var path=Round(new RectangleF(1,1,62,62),15))using(var fill=new LinearGradientBrush(new Point(0,0),new Point(64,64),Color.FromArgb(90,90,98),Color.FromArgb(28,28,34)))g.FillPath(fill,path);using(var p=new Pen(Color.White,3.5f)){p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;g.DrawLines(p,new PointF[]{new PointF(15,25),new PointF(15,16),new PointF(24,16)});g.DrawLines(p,new PointF[]{new PointF(40,16),new PointF(49,16),new PointF(49,25)});g.DrawLines(p,new PointF[]{new PointF(49,39),new PointF(49,48),new PointF(40,48)});g.DrawLines(p,new PointF[]{new PointF(24,48),new PointF(15,48),new PointF(15,39)});g.DrawEllipse(p,24,24,16,16);}using(var brush=new SolidBrush(Color.FromArgb(210,210,218)))g.FillEllipse(brush,44,10,10,10);}return b;}
  public static void Export(string path){int[] sizes={16,24,32,48,64,128,256};var data=new List<byte[]>();foreach(int n in sizes)using(var b=Brand(n))using(var ms=new MemoryStream()){b.Save(ms,ImageFormat.Png);data.Add(ms.ToArray());}using(var writer=new BinaryWriter(File.Create(path))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+sizes.Length*16;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(data[i].Length);writer.Write(offset);offset+=data[i].Length;}foreach(var bytes in data)writer.Write(bytes);}}
 }
 class CardPanel:Panel {
  public CardPanel(){DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(18);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Art.Round(new RectangleF(0.5f,0.5f,Width-1,Height-1),12))using(var p=new Pen(Color.FromArgb(222,230,235)))e.Graphics.DrawPath(p,path);}
 }
}
