using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace FixedCapture {
 static class PdfExporter {
  [DllImport("shlwapi.dll",CharSet=CharSet.Unicode)] static extern int StrCmpLogicalW(string a,string b);
  public static bool Supported(string path){return new[]{".png",".jpg",".jpeg",".bmp"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
  public static string[] Sorted(IEnumerable<string> files){return files.OrderBy(p=>p,Comparer<string>.Create((a,b)=>{int c=StrCmpLogicalW(Path.GetFileName(a),Path.GetFileName(b));return c==0?StringComparer.OrdinalIgnoreCase.Compare(a,b):c;})).ToArray();}
  static string N(double n){return n.ToString("0.####",CultureInfo.InvariantCulture);}
  static void Write(Stream stream,string text){byte[] data=Encoding.ASCII.GetBytes(text);stream.Write(data,0,data.Length);}
  static void Begin(Stream s,List<long> offsets,int id){while(offsets.Count<=id)offsets.Add(0);offsets[id]=s.Position;Write(s,id+" 0 obj\n");}
  static void Object(Stream s,List<long> offsets,int id,string value){Begin(s,offsets,id);Write(s,value+"\nendobj\n");}
  static void StreamObject(Stream s,List<long> offsets,int id,string dictionary,byte[] data){Begin(s,offsets,id);Write(s,"<< "+dictionary+" /Length "+data.Length+" >>\nstream\n");s.Write(data,0,data.Length);Write(s,"\nendstream\nendobj\n");}
  static Bitmap ReadImage(string path){
   using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var source=Image.FromStream(file,true,true)){
    if((long)source.Width*source.Height>100000000L)throw new Exception("图片超过 1 亿像素，请先缩小："+Path.GetFileName(path));
    if(source.PropertyIdList.Contains(0x112)){int v=source.GetPropertyItem(0x112).Value[0];var rotate=RotateFlipType.RotateNoneFlipNone;switch(v){case 2:rotate=RotateFlipType.RotateNoneFlipX;break;case 3:rotate=RotateFlipType.Rotate180FlipNone;break;case 4:rotate=RotateFlipType.Rotate180FlipX;break;case 5:rotate=RotateFlipType.Rotate90FlipX;break;case 6:rotate=RotateFlipType.Rotate90FlipNone;break;case 7:rotate=RotateFlipType.Rotate270FlipX;break;case 8:rotate=RotateFlipType.Rotate270FlipNone;break;}source.RotateFlip(rotate);}
    var bitmap=new Bitmap(source.Width,source.Height,PixelFormat.Format24bppRgb);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.White);g.DrawImage(source,new Rectangle(0,0,bitmap.Width,bitmap.Height),0,0,source.Width,source.Height,GraphicsUnit.Pixel);}return bitmap;
   }
  }
  static byte[] Compress(Bitmap bitmap,CancellationToken token){
   using(var memory=new MemoryStream()){
    memory.WriteByte(0x78);memory.WriteByte(0x9C);uint a=1,b=0;
    var pixels=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
    try{using(var deflate=new DeflateStream(memory,CompressionLevel.Optimal,true)){var row=new byte[bitmap.Width*3];for(int y=0;y<bitmap.Height;y++){token.ThrowIfCancellationRequested();Marshal.Copy(IntPtr.Add(pixels.Scan0,y*pixels.Stride),row,0,row.Length);for(int x=0;x<row.Length;x+=3){byte t=row[x];row[x]=row[x+2];row[x+2]=t;}for(int x=0;x<row.Length;x++){a=(a+row[x])%65521;b=(b+a)%65521;}deflate.Write(row,0,row.Length);}}}finally{bitmap.UnlockBits(pixels);}
    uint checksum=(b<<16)|a;memory.WriteByte((byte)(checksum>>24));memory.WriteByte((byte)(checksum>>16));memory.WriteByte((byte)(checksum>>8));memory.WriteByte((byte)checksum);return memory.ToArray();
   }
  }
  public static void Export(IList<string> paths,string destination,bool a4,bool overwrite,CancellationToken token,Action<int> progress){
   if(paths.Count==0)throw new Exception("请先添加图片。");if(paths.Count>10000)throw new Exception("一次最多合成 10000 张图片。");
   destination=Path.GetFullPath(destination);if(!String.Equals(Path.GetExtension(destination),".pdf",StringComparison.OrdinalIgnoreCase))throw new Exception("输出文件名必须以 .pdf 结尾。");
   if(paths.Any(p=>String.Equals(Path.GetFullPath(p),destination,StringComparison.OrdinalIgnoreCase)))throw new Exception("输出路径不能与原图片相同。");
   string folder=Path.GetDirectoryName(destination);Directory.CreateDirectory(folder);string temp=Path.Combine(folder,".fixedcapture-"+Guid.NewGuid().ToString("N")+".tmp");
   try{using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
    Write(output,"%PDF-1.4\n%FixedCapture\n");var offsets=new List<long>{0};Object(output,offsets,1,"<< /Type /Catalog /Pages 2 0 R >>");
    string kids=String.Join(" ",Enumerable.Range(0,paths.Count).Select(i=>(3+i*3)+" 0 R"));Object(output,offsets,2,"<< /Type /Pages /Count "+paths.Count+" /Kids [ "+kids+" ] >>");
    for(int i=0;i<paths.Count;i++){
     token.ThrowIfCancellationRequested();string file=paths[i];try{using(var bitmap=ReadImage(file)){
      double pageW=bitmap.Width*.75,pageH=bitmap.Height*.75,x=0,y=0,drawW=pageW,drawH=pageH;
      if(a4){pageW=bitmap.Width>bitmap.Height?841.8898:595.2756;pageH=bitmap.Width>bitmap.Height?595.2756:841.8898;double scale=Math.Min((pageW-48)/bitmap.Width,(pageH-48)/bitmap.Height);drawW=bitmap.Width*scale;drawH=bitmap.Height*scale;x=(pageW-drawW)/2;y=(pageH-drawH)/2;}
      else if(Math.Max(pageW,pageH)>14400){double scale=14400/Math.Max(pageW,pageH);pageW*=scale;pageH*=scale;drawW=pageW;drawH=pageH;}
      int page=3+i*3,img=page+1,content=page+2;
      Object(output,offsets,page,"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 "+N(pageW)+" "+N(pageH)+"] /Resources << /XObject << /Im0 "+img+" 0 R >> >> /Contents "+content+" 0 R >>");
      StreamObject(output,offsets,img,"/Type /XObject /Subtype /Image /Width "+bitmap.Width+" /Height "+bitmap.Height+" /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode",Compress(bitmap,token));
      StreamObject(output,offsets,content,"",Encoding.ASCII.GetBytes("q\n"+N(drawW)+" 0 0 "+N(drawH)+" "+N(x)+" "+N(y)+" cm\n/Im0 Do\nQ\n"));
     }}catch(OperationCanceledException){throw;}catch(Exception ex){throw new Exception("无法处理图片“"+Path.GetFileName(file)+"”："+ex.Message,ex);}if(progress!=null)progress(i+1);
    }
    token.ThrowIfCancellationRequested();long xref=output.Position;Write(output,"xref\n0 "+offsets.Count+"\n0000000000 65535 f \n");for(int i=1;i<offsets.Count;i++)Write(output,offsets[i].ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");Write(output,"trailer\n<< /Size "+offsets.Count+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF\n");output.Flush(true);
   }token.ThrowIfCancellationRequested();if(File.Exists(destination)){if(!overwrite)throw new IOException("PDF 已存在，请选择其他文件名。");File.Replace(temp,destination,null);}else File.Move(temp,destination);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public static void SelfTest(string root){
   Directory.CreateDirectory(root);string input=Path.Combine(root,"input");Directory.CreateDirectory(input);
   int[] names={10,5,6};Color[] colors={Color.FromArgb(70,100,220),Color.FromArgb(220,60,50),Color.FromArgb(40,165,90)};
   for(int i=0;i<3;i++)using(var image=new Bitmap(i==2?160:240,i==1?160:240)){using(var g=Graphics.FromImage(image)){g.Clear(colors[i]);using(var font=new Font("Arial",28,FontStyle.Bold))g.DrawString(names[i].ToString(),font,Brushes.White,15,15);g.FillRectangle(Brushes.Black,0,0,12,12);g.FillRectangle(Brushes.White,image.Width-12,image.Height-12,12,12);}image.Save(Path.Combine(input,names[i]+".png"),ImageFormat.Png);}
   var files=Sorted(Directory.GetFiles(input,"*.png"));if(String.Join(",",files.Select(Path.GetFileName))!="5.png,6.png,10.png")throw new Exception("Natural sort failed");
   Export(files,Path.Combine(root,"original.pdf"),false,true,CancellationToken.None,null);Export(files,Path.Combine(root,"a4.pdf"),true,true,CancellationToken.None,null);
   string existing=Path.Combine(root,"original.pdf");byte[] before=File.ReadAllBytes(existing);var cancel=new CancellationTokenSource();bool cancelled=false;try{Export(files,existing,false,true,cancel.Token,n=>cancel.Cancel());}catch(OperationCanceledException){cancelled=true;}if(!cancelled||!before.SequenceEqual(File.ReadAllBytes(existing)))throw new Exception("Cancel atomicity failed");
   string broken=Path.Combine(root,"broken.png");File.WriteAllText(broken,"invalid image");bool failed=false;try{Export(new[]{files[0],broken},Path.Combine(root,"failed.pdf"),false,false,CancellationToken.None,null);}catch{failed=true;}if(!failed||File.Exists(Path.Combine(root,"failed.pdf"))||Directory.GetFiles(root,".fixedcapture-*.tmp").Length!=0)throw new Exception("Failure cleanup failed");
   File.WriteAllText(Path.Combine(root,"tests.txt"),"PASS: natural sort 5,6,10; original-ratio and A4 export; cancellation preserves existing PDF; corrupt-image failure leaves no partial PDF.");
  }
 }
}
