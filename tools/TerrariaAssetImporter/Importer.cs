// HollowCraft local asset importer. Game content is read from the user's installation only.
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using MonoGame.Framework.Utilities;

public static class TerrariaAssetImporter
{
    static int V(BinaryReader r) { int n=0,s=0,b; do {b=r.ReadByte();n|=(b&127)<<s;s+=7;if(s>35)throw new InvalidDataException();}while((b&128)!=0);return n; }
    static BinaryReader Open(string file) {
        byte[] bytes=File.ReadAllBytes(file);var input=new MemoryStream(bytes);var r=new BinaryReader(input);
        if(r.ReadByte()!=88||r.ReadByte()!=78||r.ReadByte()!=66)throw new InvalidDataException("Not XNB: "+file);
        r.ReadByte();r.ReadByte();int flags=r.ReadByte(),size=r.ReadInt32();
        if(size!=bytes.Length)throw new InvalidDataException("Truncated XNB: "+file);
        if((flags&128)!=0) {int length=r.ReadInt32();var output=new MemoryStream();using(var lzx=new LzxDecoderStream(input,length,size-14))lzx.CopyTo(output);r.Dispose();r=new BinaryReader(new MemoryStream(output.ToArray()));}
        int readers=V(r);for(int i=0;i<readers;i++){r.ReadString();r.ReadInt32();}V(r);V(r);return r;
    }
    public static void Texture(string source,string target) {
        using(var r=Open(source)) {
            int format=r.ReadInt32(),w=r.ReadInt32(),h=r.ReadInt32(),mips=r.ReadInt32(),count=r.ReadInt32();
            if(format!=0||w<1||h<1||w>4096||h>4096||count!=w*h*4)throw new InvalidDataException("Unsupported Terraria texture format: "+source);
            byte[] rgba=r.ReadBytes(count),bgra=new byte[count];if(rgba.Length!=count)throw new EndOfStreamException();
            // XNA stores premultiplied alpha; Unity/Minecraft PNGs use straight alpha.
            for(int p=0;p<count;p+=4){int a=rgba[p+3];bgra[p+3]=(byte)a;for(int c=0;c<3;c++)bgra[p+2-c]=a==0?(byte)0:(byte)Math.Min(255,(rgba[p+c]*255+a/2)/a);}
            using(var image=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {var data=image.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(bgra,0,data.Scan0,count);}finally{image.UnlockBits(data);}image.Save(target,ImageFormat.Png);}
        }
    }
    public static void WingFrames(string source,string folder,int frames) {
        using(var image=new Bitmap(source)) {int h=image.Height/frames;for(int i=0;i<frames;i++)using(var frame=image.Clone(new Rectangle(0,i*h,image.Width,h),PixelFormat.Format32bppArgb))frame.Save(Path.Combine(folder,"stardust_wing_"+i+".png"),ImageFormat.Png);}
    }
    public static void Sound(string source,string target) {
        using(var r=Open(source)) {int size=r.ReadInt32();if(size<16||size>4096)throw new InvalidDataException();byte[] format=r.ReadBytes(size);int n=r.ReadInt32();if(n<0||n>10000000)throw new InvalidDataException();byte[] data=r.ReadBytes(n);if(data.Length!=n)throw new EndOfStreamException();
            using(var w=new BinaryWriter(File.Create(target))) {w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(20+size+n+(size&1)+(n&1));w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(size);w.Write(format);if((size&1)!=0)w.Write((byte)0);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(n);w.Write(data);if((n&1)!=0)w.Write((byte)0);}
        }
    }
}
