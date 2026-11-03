using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
namespace FixedCapture {
 static class ThemePalette {
  static Color ColorOf(string value){return (Color)ColorConverter.ConvertFromString(value);}
  static SolidColorBrush Brush(string value){return new SolidColorBrush(ColorOf(value));}
  static void Resource(FrameworkElement view,string key,string value){if(key=="CardTop"||key=="CardBottom")view.Resources[key]=ColorOf(value);else view.Resources[key]=Brush(value);}
  static void ButtonColor(FrameworkElement view,string name,string color,string theme){var button=view.FindName(name) as Button;if(button==null)return;button.Background=theme=="Starry"?SkinArt.Paint(color,name.Length+19):theme=="Crystal"&&name!="Capture"&&name!="ToggleAuto"?SkinArt.GlassButton(color):(System.Windows.Media.Brush)Brush(color);button.BorderBrush=theme=="Starry"?Brush("#60E9D9A4"):theme=="Crystal"?Brush("#B8FFFFFF"):Brush(color);}
  public static void Apply(FrameworkElement view,string theme){
   string surface="#E9F1FC",top="#FAFCFF",bottom="#DDEBFA",accent="#315FC7",auto="#7650B8",pdf="#8052BA";
   string[] soft={"#DCE9FF","#E3E0FA","#DBE9FC","#EEE0FA","#E4E9F3","#FFE8C8","#EDDEFA","#F7DFE5"};
   if(theme=="Lavender"){surface="#F0EAFA";top="#FDFBFF";bottom="#E9DFF7";accent="#7050B1";auto="#B04F78";pdf="#5668B6";soft=new[]{"#E8DDFA","#F6DFEB","#E5DDF7","#DDE5FA","#E8E1F0","#FFE7D0","#DDE5FA","#F6DDE7"};}
   else if(theme=="Peach"){surface="#FFF0E6";top="#FFFCFA";bottom="#FBE3D4";accent="#B84F2E";auto="#9D507F";pdf="#7160AD";soft=new[]{"#FFDFC8","#F3DCEB","#FFE3CD","#E4DEF7","#F0E4DE","#FFE7B9","#E8DFF7","#F6DADD"};}
   else if(theme=="Classic"){surface="#EBEBF0";top="#FCFCFD";bottom="#E7E7EC";accent="#303036";auto=accent;pdf=accent;for(int i=0;i<soft.Length;i++)soft[i]="#ECECF0";}
   else if(theme=="Starry"){surface="#1E3554";top="#FFFCED";bottom="#EAE4CB";accent="#245BA4";auto="#A84732";pdf="#51659B";soft=new[]{"#C3DCE9","#D9D1EC","#EBD17D","#DFCA99","#D8DDDC","#EBCC8E","#D1C4E4","#E5C2AB"};}
   else if(theme=="Crystal"){surface="#F5F6FC";top="#FFFFFF";bottom="#E5EDFF";accent="#405DDB";auto="#A7489E";pdf="#7252C9";soft=new[]{"#C7E7FF","#D8CDFF","#CEE2FF","#D7D1FF","#D4EDF7","#FFE5AF","#E4D5FF","#FFD3E1"};}
   Resource(view,"Surface","#E8"+surface.Substring(1));Resource(view,"CardTop","#E6"+top.Substring(1));Resource(view,"CardBottom","#CC"+bottom.Substring(1));Resource(view,"Accent",accent);Resource(view,"PdfAccent",pdf);
   bool oil=theme=="Starry",glass=theme=="Crystal";
   view.Resources["ThemeName"]=theme;
   view.Resources["WorkspaceSurface"]=Brush(oil?"#F4EDDB":glass?"#F1F5FF":surface);
   view.Resources["HeaderInk"]=Brush(oil?"#FFF3CF":"#24324D");view.Resources["HeaderMuted"]=Brush(oil?"#D1D8DC":"#66738A");
   view.Resources["CardFill"]=oil?SkinArt.Paper():(System.Windows.Media.Brush)new LinearGradientBrush(ColorOf("#F5"+top.Substring(1)),ColorOf("#EA"+bottom.Substring(1)),45);
   view.Resources["CardRim"]=Brush(oil?"#C8D8C99C":"#F5FFFFFF");view.Resources["ButtonRim"]=Brush(glass?"#EFFFFFFF":oil?"#55FFF0C3":"#10FFFFFF");
   view.Resources["ButtonSheen"]=new LinearGradientBrush(ColorOf(glass?"#AFFFFFFF":oil?"#15FFFFFF":"#00FFFFFF"),Colors.Transparent,90);
   view.Resources["IconSize"]=oil||glass?26.0:19.0;
   if(oil)view.Resources["Surface"]=SkinArt.Starry();else if(glass)view.Resources["Surface"]=SkinArt.Crystal();
   ButtonColor(view,"Capture",accent,theme);ButtonColor(view,"ToggleAuto",auto,theme);
   string[] names={"SelectRegion","LocateRegion","Save","Browse","Background","OpenFolder","MergePdf","Exit"};for(int i=0;i<names.Length;i++)ButtonColor(view,names[i],soft[i],theme);
   Decorate(view,theme);
  }
  public static void Decorate(DependencyObject root,string theme){foreach(var item in Children(root)){var image=item as Image;if(image==null||image.Tag==null)continue;string[] bits=image.Tag.ToString().Split(':');image.Source=SkinArt.Icon(bits[0],bits.Length>1,theme);image.Width=image.Height=theme=="Starry"||theme=="Crystal"?26:19;}}
  static System.Collections.Generic.IEnumerable<DependencyObject> Children(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var item in Children(child))yield return item;}}
 }
}
