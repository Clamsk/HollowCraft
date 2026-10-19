using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

// Static model JSON and local textures only. No Minecraft, Unity or game process.
public sealed class NativeLauncherIcons
{
    private sealed class Model { public Dictionary<string,object> Textures=new Dictionary<string,object>(), Display=new Dictionary<string,object>(); public object[] Elements; }
    private readonly string root;
    private readonly Dictionary<string,Model> models=new Dictionary<string,Model>();
    private readonly Dictionary<string,BitmapSource> images=new Dictionary<string,BitmapSource>();
    public NativeLauncherIcons(string assets) { root=assets; }
    private static Dictionary<string,object> Map(object value) { return value as Dictionary<string,object> ?? new Dictionary<string,object>(); }
    private static object Get(Dictionary<string,object> value,string name) { object item;return value.TryGetValue(name,out item)?item:null; }
    private static double Number(object value) { return value==null?0:Convert.ToDouble(value,System.Globalization.CultureInfo.InvariantCulture); }
    private static Vector3D V(object value,Vector3D fallback) { var a=value as object[];return a!=null&&a.Length>=3?new Vector3D(Number(a[0]),Number(a[1]),Number(a[2])):fallback; }
    private Model Read(string id,int depth) {
        if(depth>20||id.Contains("..")||id.Contains("\\"))return null;if(!id.Contains(":"))id="minecraft:"+id;
        Model model;if(models.TryGetValue(id,out model))return model;string[] parts=id.Split(':');if(parts.Length!=2)return null;
        string path=Path.Combine(root,"models",parts[0],parts[1]+".json");if(!File.Exists(path))return null;
        var json=Map(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)));var parent=Get(json,"parent") as string;var inherited=parent==null?null:Read(parent,depth+1);
        model=new Model();if(inherited!=null){foreach(var p in inherited.Textures)model.Textures[p.Key]=p.Value;foreach(var p in inherited.Display)model.Display[p.Key]=p.Value;model.Elements=inherited.Elements;}
        foreach(var p in Map(Get(json,"textures")))model.Textures[p.Key]=p.Value;foreach(var p in Map(Get(json,"display")))model.Display[p.Key]=p.Value;
        model.Elements=Get(json,"elements") as object[] ?? model.Elements;models[id]=model;return model;
    }
    private BitmapSource Texture(Model model,string key) {
        for(int n=0;n<20&&key!=null&&key.StartsWith("#",StringComparison.Ordinal);n++)key=Get(model.Textures,key.Substring(1)) as string;
        if(String.IsNullOrEmpty(key))return null;string name=key.Substring(key.LastIndexOf('/')+1);int colon=name.IndexOf(':');if(colon>=0)name=name.Substring(colon+1);
        string path=Path.Combine(root,name+".png");BitmapSource result;if(images.TryGetValue(path,out result))return result;if(!File.Exists(path))return null;
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path);bitmap.EndInit();bitmap.Freeze();result=bitmap;
        if(bitmap.PixelHeight>bitmap.PixelWidth*2){var crop=new CroppedBitmap(bitmap,new System.Windows.Int32Rect(0,0,bitmap.PixelWidth,bitmap.PixelWidth));crop.Freeze();result=crop;}
        images[path]=result;return result;
    }
    private static Vector3D Rotate(Vector3D p,Vector3D angles) {
        // Model display uses Rx * Ry * Rz; apply the rightmost rotation first.
        foreach(int axis in new[]{2,1,0}){double angle=(axis==0?angles.X:axis==1?angles.Y:angles.Z)*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);p=axis==0?new Vector3D(p.X,p.Y*c-p.Z*s,p.Y*s+p.Z*c):axis==1?new Vector3D(p.X*c+p.Z*s,p.Y,-p.X*s+p.Z*c):new Vector3D(p.X*c-p.Y*s,p.X*s+p.Y*c,p.Z);}
        return p;
    }
    public BitmapSource Icon(string id) {
        string key="icon/"+id;BitmapSource cached;if(images.TryGetValue(key,out cached))return cached;
        var model=Read(id.Replace(":",":item/"),0);if(model==null)return null;
        if(model.Elements==null||model.Elements.Length==0){cached=Texture(model,Get(model.Textures,"layer0") as string);if(cached!=null)images[key]=cached;return cached;}
        var display=Map(Get(model.Display,"gui"));var angles=V(Get(display,"rotation"),new Vector3D());var scale=V(Get(display,"scale"),new Vector3D(1,1,1));var translation=V(Get(display,"translation"),new Vector3D());
        var pixels=new uint[32*32];var depth=new double[pixels.Length];for(int i=0;i<depth.Length;i++)depth[i]=Double.PositiveInfinity;bool painted=false;
        foreach(object elementValue in model.Elements){var element=Map(elementValue);Vector3D min=V(Get(element,"from"),new Vector3D()),max=V(Get(element,"to"),new Vector3D(16,16,16));var rotation=Map(Get(element,"rotation"));var origin=V(Get(rotation,"origin"),new Vector3D(8,8,8));double angle=Number(Get(rotation,"angle"));string axis=Get(rotation,"axis") as string;var turn=new Vector3D(axis=="x"?angle:0,axis=="y"?angle:0,axis=="z"?angle:0);bool rescale=Object.Equals(Get(rotation,"rescale"),true);
            foreach(var face in Map(Get(element,"faces"))){var data=Map(face.Value);var texture=Texture(model,Get(data,"texture") as string);if(texture==null)continue;Vector3D[] p;
                switch(face.Key){case "north":p=new[]{new Vector3D(max.X,min.Y,min.Z),new Vector3D(min.X,min.Y,min.Z),new Vector3D(min.X,max.Y,min.Z),new Vector3D(max.X,max.Y,min.Z)};break;
                    case "south":p=new[]{new Vector3D(min.X,min.Y,max.Z),new Vector3D(max.X,min.Y,max.Z),new Vector3D(max.X,max.Y,max.Z),new Vector3D(min.X,max.Y,max.Z)};break;
                    case "east":p=new[]{new Vector3D(max.X,min.Y,max.Z),new Vector3D(max.X,min.Y,min.Z),new Vector3D(max.X,max.Y,min.Z),new Vector3D(max.X,max.Y,max.Z)};break;
                    case "west":p=new[]{new Vector3D(min.X,min.Y,min.Z),new Vector3D(min.X,min.Y,max.Z),new Vector3D(min.X,max.Y,max.Z),new Vector3D(min.X,max.Y,min.Z)};break;
                    case "up":p=new[]{new Vector3D(min.X,max.Y,min.Z),new Vector3D(max.X,max.Y,min.Z),new Vector3D(max.X,max.Y,max.Z),new Vector3D(min.X,max.Y,max.Z)};break;
                    default:p=new[]{new Vector3D(min.X,min.Y,max.Z),new Vector3D(max.X,min.Y,max.Z),new Vector3D(max.X,min.Y,min.Z),new Vector3D(min.X,min.Y,min.Z)};break;}
                var uv=Get(data,"uv") as object[] ?? new object[]{0,0,16,16};double[] us={Number(uv[0])/16,Number(uv[2])/16,Number(uv[2])/16,Number(uv[0])/16},vs={Number(uv[3])/16,Number(uv[3])/16,Number(uv[1])/16,Number(uv[1])/16};int offset=(int)(Number(Get(data,"rotation"))/90);
                double shade=face.Key=="up"?1:face.Key=="down"?.5:face.Key=="east"||face.Key=="west"?.6:.8;double red=1,green=1,blue=1;if(Get(data,"tintindex")!=null){red=id.Contains("leaves")?.282:.569;green=id.Contains("leaves")?.71:.741;blue=id.Contains("leaves")?.094:.349;}
                for(int i=0;i<4;i++){Vector3D v=p[i]-origin;if(rescale){double factor=1/Math.Max(.01,Math.Cos(angle*Math.PI/180));v=new Vector3D(v.X*(axis=="x"?1:factor),v.Y*(axis=="y"?1:factor),v.Z*(axis=="z"?1:factor));}v=Rotate(v,turn)+origin-new Vector3D(8,8,8);v=Rotate(new Vector3D(v.X*scale.X,v.Y*scale.Y,v.Z*scale.Z),angles)+translation;p[i]=new Vector3D(16+v.X*2,16-v.Y*2,-v.Z);}
                var raw=new byte[texture.PixelWidth*texture.PixelHeight*4];var bgra=new FormatConvertedBitmap(texture,PixelFormats.Bgra32,null,0);bgra.CopyPixels(raw,texture.PixelWidth*4,0);
                Triangle(p,0,1,2,offset,us,vs,texture.PixelWidth,texture.PixelHeight,raw,shade*red,shade*green,shade*blue,pixels,depth);Triangle(p,0,2,3,offset,us,vs,texture.PixelWidth,texture.PixelHeight,raw,shade*red,shade*green,shade*blue,pixels,depth);painted=true;
            }
        }
        if(!painted)return null;cached=BitmapSource.Create(32,32,96,96,PixelFormats.Bgra32,null,pixels,128);cached.Freeze();images[key]=cached;return cached;
    }
    private static void Triangle(Vector3D[] p,int ai,int bi,int ci,int offset,double[] u,double[] v,int width,int height,byte[] raw,double r,double g,double b,uint[] pixels,double[] depth){
        var a=p[ai];var q=p[bi];var c=p[ci];double area=(q.X-a.X)*(c.Y-a.Y)-(q.Y-a.Y)*(c.X-a.X);if(Math.Abs(area)<1e-8)return;
        for(int y=Math.Max(0,(int)Math.Floor(Math.Min(a.Y,Math.Min(q.Y,c.Y))));y<=Math.Min(31,(int)Math.Ceiling(Math.Max(a.Y,Math.Max(q.Y,c.Y))));y++)for(int x=Math.Max(0,(int)Math.Floor(Math.Min(a.X,Math.Min(q.X,c.X))));x<=Math.Min(31,(int)Math.Ceiling(Math.Max(a.X,Math.Max(q.X,c.X))));x++){
            double px=x+.5-a.X,py=y+.5-a.Y,wb=(px*(c.Y-a.Y)-py*(c.X-a.X))/area,wc=((q.X-a.X)*py-(q.Y-a.Y)*px)/area,wa=1-wb-wc;if(wa<0||wb<0||wc<0)continue;double z=wa*a.Z+wb*q.Z+wc*c.Z;int index=y*32+x;if(z>=depth[index])continue;
            double tx=wa*u[(ai+offset)%4]+wb*u[(bi+offset)%4]+wc*u[(ci+offset)%4],ty=wa*v[(ai+offset)%4]+wb*v[(bi+offset)%4]+wc*v[(ci+offset)%4];int sample=(Math.Max(0,Math.Min(height-1,(int)(ty*height)))*width+Math.Max(0,Math.Min(width-1,(int)(tx*width))))*4;if(raw[sample+3]<25)continue;
            pixels[index]=0xff000000|((uint)(raw[sample+2]*r)<<16)|((uint)(raw[sample+1]*g)<<8)|(uint)(raw[sample]*b);depth[index]=z;
        }
    }
}
