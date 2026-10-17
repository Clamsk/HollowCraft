using System;

namespace HollowCraft.Standalone
{
    // Orthographic cutout rasterizer. HK's bundled sprite shader cannot write
    // depth, so depth is resolved here before a single transparent texture upload.
    internal struct NativeRasterVertex
    {
        internal float X,Y,Z,U,V,R,G,B,A;
        internal NativeRasterVertex(float x,float y,float z,float u,float v,uint tint=0xffffffff) {
            X=x;Y=y;Z=z;U=u;V=v;R=(tint&255)/255f;G=((tint>>8)&255)/255f;B=((tint>>16)&255)/255f;A=(tint>>24)/255f;
        }
    }
    internal sealed class NativeRasterTexture
    {
        internal readonly int Width,Height;internal readonly uint[] Pixels;
        internal NativeRasterTexture(int width,int height,uint[] pixels) {
            if(width<1||height<1||pixels==null||pixels.Length!=width*height)throw new ArgumentException("Invalid native raster texture");
            Width=width;Height=height;Pixels=pixels;
        }
        internal uint Sample(float u,float v) {
            int x=Math.Max(0,Math.Min(Width-1,(int)(u*Width))),y=Math.Max(0,Math.Min(Height-1,(int)(v*Height)));
            return Pixels[y*Width+x];
        }
    }
    internal sealed class NativeDepthBuffer
    {
        internal readonly int Width,Height;internal readonly uint[] Pixels;internal readonly float[] Depth;
        internal NativeDepthBuffer(int width,int height) {Width=width;Height=height;Pixels=new uint[width*height];Depth=new float[width*height];Clear();}
        internal void Clear(){Array.Clear(Pixels,0,Pixels.Length);for(int i=0;i<Depth.Length;i++)Depth[i]=float.PositiveInfinity;}
        internal void Triangle(NativeRasterVertex a,NativeRasterVertex b,NativeRasterVertex c,NativeRasterTexture texture) {
            float area=(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);if(Math.Abs(area)<.000001f)return;
            int minX=Math.Max(0,(int)Math.Floor(Math.Min(a.X,Math.Min(b.X,c.X)))),maxX=Math.Min(Width-1,(int)Math.Ceiling(Math.Max(a.X,Math.Max(b.X,c.X))));
            int minY=Math.Max(0,(int)Math.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y)))),maxY=Math.Min(Height-1,(int)Math.Ceiling(Math.Max(a.Y,Math.Max(b.Y,c.Y))));
            float inverse=1/area;
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++) {
                float px=x+.5f-a.X,py=y+.5f-a.Y;
                float wb=(px*(c.Y-a.Y)-py*(c.X-a.X))*inverse,wc=((b.X-a.X)*py-(b.Y-a.Y)*px)*inverse,wa=1-wb-wc;
                if(wa<-.00001f||wb<-.00001f||wc<-.00001f)continue;
                float z=wa*a.Z+wb*b.Z+wc*c.Z;int index=y*Width+x;if(z>=Depth[index]-.000001f)continue;
                uint sample=texture.Sample(wa*a.U+wb*b.U+wc*c.U,wa*a.V+wb*b.V+wc*c.V);
                float alpha=(sample>>24)*(wa*a.A+wb*b.A+wc*c.A);if(alpha<25)continue;
                uint red=(uint)Math.Max(0,Math.Min(255,(sample&255)*(wa*a.R+wb*b.R+wc*c.R))),green=(uint)Math.Max(0,Math.Min(255,((sample>>8)&255)*(wa*a.G+wb*b.G+wc*c.G))),blue=(uint)Math.Max(0,Math.Min(255,((sample>>16)&255)*(wa*a.B+wb*b.B+wc*c.B)));
                Pixels[index]=red|(green<<8)|(blue<<16)|0xff000000;Depth[index]=z;
            }
        }
    }
}
