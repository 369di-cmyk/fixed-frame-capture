using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FixedCapture {
 static class ThemePalette {
  static Color ColorOf(string value){return (Color)ColorConverter.ConvertFromString(value);}
  static SolidColorBrush Brush(string value){return new SolidColorBrush(ColorOf(value));}
  static void Resource(FrameworkElement view,string key,string value){if(key=="CardTop"||key=="CardBottom")view.Resources[key]=ColorOf(value);else view.Resources[key]=Brush(value);}
  static void ButtonColor(FrameworkElement view,string name,string color){var button=view.FindName(name) as Button;if(button==null)return;button.Background=Brush(color);button.BorderBrush=Brush(color);}
  public static void Apply(FrameworkElement view,string theme){
   string surface="#E9F1FC",top="#FAFCFF",bottom="#DDEBFA",accent="#315FC7",auto="#7650B8",pdf="#8052BA";
   string[] soft={"#DCE9FF","#E3E0FA","#DBE9FC","#EEE0FA","#E4E9F3","#FFE8C8","#EDDEFA","#F7DFE5"};
   if(theme=="Lavender"){surface="#F0EAFA";top="#FDFBFF";bottom="#E9DFF7";accent="#7050B1";auto="#B04F78";pdf="#5668B6";soft=new[]{"#E8DDFA","#F6DFEB","#E5DDF7","#DDE5FA","#E8E1F0","#FFE7D0","#DDE5FA","#F6DDE7"};}
   else if(theme=="Peach"){surface="#FFF0E6";top="#FFFCFA";bottom="#FBE3D4";accent="#B84F2E";auto="#9D507F";pdf="#7160AD";soft=new[]{"#FFDFC8","#F3DCEB","#FFE3CD","#E4DEF7","#F0E4DE","#FFE7B9","#E8DFF7","#F6DADD"};}
   else if(theme=="Classic"){surface="#EBEBF0";top="#FCFCFD";bottom="#E7E7EC";accent="#303036";auto=accent;pdf=accent;for(int i=0;i<soft.Length;i++)soft[i]="#ECECF0";}
   Resource(view,"Surface","#E8"+surface.Substring(1));Resource(view,"CardTop","#E6"+top.Substring(1));Resource(view,"CardBottom","#CC"+bottom.Substring(1));Resource(view,"Accent",accent);Resource(view,"PdfAccent",pdf);
   ButtonColor(view,"Capture",accent);ButtonColor(view,"ToggleAuto",auto);
   string[] names={"SelectRegion","LocateRegion","Save","Browse","Background","OpenFolder","MergePdf","Exit"};for(int i=0;i<names.Length;i++)ButtonColor(view,names[i],soft[i]);
  }
 }
}
