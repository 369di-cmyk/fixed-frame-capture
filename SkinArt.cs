using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FixedCapture {
 // Resolution-independent materials and icon tiles, inspired by the user's references.
 static class SkinArt {
  static Color C(string hex){return (Color)ColorConverter.ConvertFromString(hex);}
  static SolidColorBrush B(string hex){return new SolidColorBrush(C(hex));}
  static DrawingBrush Finish(DrawingGroup drawing,double width,double height){drawing.Freeze();var brush=new DrawingBrush(drawing){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,width,height)};brush.Freeze();return brush;}
  static void Stroke(DrawingContext dc,Point a,Point b,Point c,Color color,double width){var geometry=new StreamGeometry();using(var path=geometry.Open()){path.BeginFigure(a,false,false);path.QuadraticBezierTo(b,c,true,false);}geometry.Freeze();dc.DrawGeometry(null,new Pen(new SolidColorBrush(color),width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},geometry);}
  public static Brush Paint(string color,int seed){
   var group=new DrawingGroup();Color baseColor=C(color);var random=new Random(seed);
   using(var dc=group.Open()){
    dc.DrawRectangle(new LinearGradientBrush(baseColor,Color.FromArgb(255,(byte)(baseColor.R*.88),(byte)(baseColor.G*.88),(byte)(baseColor.B*.88)),90),null,new Rect(0,0,240,100));
    for(int i=0;i<180;i++){double x=random.NextDouble()*260-10,y=random.NextDouble()*110-5;bool light=i%3!=0;var shade=light?Color.FromArgb(35,255,248,214):Color.FromArgb(22,12,25,48);Stroke(dc,new Point(x,y),new Point(x+12,y-6),new Point(x+24,y+3),shade,1+random.NextDouble()*2.4);}
   }return Finish(group,240,100);
  }
  public static Brush Paper(){var group=new DrawingGroup();var random=new Random(73);using(var dc=group.Open()){
   dc.DrawRectangle(new LinearGradientBrush(C("#FFF9E9"),C("#EEE4CA"),90),null,new Rect(0,0,354,466));
   for(int i=0;i<600;i++){double x=random.NextDouble()*354,y=random.NextDouble()*466;Stroke(dc,new Point(x,y),new Point(x+4,y-1),new Point(x+8+random.NextDouble()*12,y),Color.FromArgb(10,119,94,52),.6);}
  }return Finish(group,354,466);}
  public static Brush Starry(){
   var group=new DrawingGroup();var random=new Random(37);using(var dc=group.Open()){
    dc.DrawRectangle(new LinearGradientBrush(C("#152B49"),C("#263B59"),35),null,new Rect(0,0,1100,710));
    for(int i=0;i<550;i++){double x=random.NextDouble()*1180-40,y=random.NextDouble()*750-20;Stroke(dc,new Point(x,y),new Point(x+26,y-11),new Point(x+50,y+3),C(i%3==0?"#254A83A2":"#224F6980"),2+random.NextDouble()*3);}
    foreach(var center in new[]{new Point(940,24),new Point(28,560),new Point(560,710)})for(int i=0;i<26;i++){double r=24+i*5;Stroke(dc,new Point(center.X-r,center.Y),new Point(center.X,center.Y-r*1.5),new Point(center.X+r,center.Y+8),Color.FromArgb((byte)(65-i),200,177,88),2.5);}
    foreach(var p in new[]{new Point(490,22),new Point(20,110),new Point(1090,580),new Point(760,698),new Point(1060,90)}){dc.DrawEllipse(B("#32EACB75"),null,p,12,12);dc.DrawEllipse(B("#F8E6A2"),null,p,3,3);}
   }return Finish(group,1100,710);
  }
  public static Brush Crystal(){var group=new DrawingGroup();using(var dc=group.Open()){
   dc.DrawRectangle(B("#F5F6FC"),null,new Rect(0,0,1100,710));
   foreach(var item in new[]{new[]{100.0,30.0,0.0},new[]{1050.0,170.0,1.0},new[]{640.0,730.0,2.0}}){string color=item[2]==0?"#72A5DEFF":item[2]==1?"#65FFBADF":"#60BEB1FF";var fill=new RadialGradientBrush(C(color),Colors.Transparent);dc.DrawEllipse(fill,null,new Point(item[0],item[1]),390,300);}
   dc.DrawRoundedRectangle(null,new Pen(B("#DFFFFFFF"),2),new Rect(9,9,1082,692),24,24);
  }return Finish(group,1100,710);}
  public static Brush GlassButton(string color){return new LinearGradientBrush(new GradientStopCollection{new GradientStop(C("#FEFFFFFF"),0),new GradientStop(C(color),.45),new GradientStop(C("#C7FFFFFF"),1)},new Point(0,0),new Point(1,1));}
  public static ImageSource Icon(string name,bool white,string theme){
   bool paint=theme=="Starry",glass=theme=="Crystal";var group=new DrawingGroup();
   using(var dc=group.Open()){
    if(paint||glass){int index=name=="capture"||name=="region"?0:name=="play"||name=="pause"?1:name=="folder"||name=="save"?2:name=="key"||name=="move"?3:4;string[] colors=paint?new[]{"#357CC3","#C25638","#DEC368","#5DA6AD","#8B72AD"}:new[]{"#58C4FA","#F8A7C6","#FFE49A","#B6ABF7","#9CD9FA"};
     dc.DrawRoundedRectangle(B("#22000000"),null,new Rect(3,6,58,57),14,14);
     dc.PushClip(new RectangleGeometry(new Rect(3,2,58,57),14,14));dc.DrawRectangle(paint?Paint(colors[index],index+11):GlassButton(colors[index]),null,new Rect(3,2,58,57));dc.Pop();
     dc.DrawRoundedRectangle(null,new Pen(B(glass?"#F0FFFFFF":"#60FFEAB2"),1.5),new Rect(4,3,56,55),13,13);
     if(glass){dc.DrawRoundedRectangle(null,new Pen(B("#908596C0"),1),new Rect(2,1,60,59),15,15);Stroke(dc,new Point(11,13),new Point(29,6),new Point(49,11),C("#F0FFFFFF"),3);}
     white=paint&&index!=2;
    }
    using(var bitmap=Art.Symbol(name,64,white?System.Drawing.Color.White:System.Drawing.Color.FromArgb(41,51,72)))using(var stream=new MemoryStream()){bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);stream.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();dc.DrawImage(image,paint||glass?new Rect(14,13,36,36):new Rect(0,0,64,64));}
   }group.Freeze();return new DrawingImage(group);
  }
 }
}
