using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    // Static vanilla font providers, never indexed by assumed Unicode atlas slots.
    internal sealed class NativeFont
    {
        internal sealed class Glyph {internal string Texture;internal float X,Y,Width,Height,RenderHeight,Ascent,Advance;}
        private readonly Dictionary<int,Glyph> glyphs=new Dictionary<int,Glyph>();
        internal NativeFont(JObject data) {
            foreach(var space in data["spaces"] as JObject??new JObject())glyphs[char.ConvertToUtf32(space.Key,0)]=new Glyph{Advance=(float)space.Value};
            foreach(JObject row in data["glyphs"] as JArray??new JArray()) {
                int code=(int)row["code"];if(glyphs.ContainsKey(code))continue;
                glyphs[code]=new Glyph{Texture=(string)row["texture"],X=(float)row["x"],Y=(float)row["y"],Width=(float)row["width"],Height=(float)row["height"],RenderHeight=(float)row["renderHeight"],Ascent=(float)row["ascent"],Advance=(float)row["advance"]};
            }
        }
        internal bool TryGet(int code,out Glyph glyph)=>glyphs.TryGetValue(code,out glyph);
        internal static int Code(string value,ref int index){int code=char.ConvertToUtf32(value,index);if(code>65535)index++;return code;}
        internal bool Supports(string value) {
            for(int i=0;i<value.Length;i++){int code=Code(value,ref i);if(code==167&&i+1<value.Length){i++;continue;}if(code!=10&&code!=13&&code!=9&&!glyphs.ContainsKey(code))return false;}return true;
        }
        internal float Measure(string value) {
            float width=0,max=0;value=value??"";
            for(int i=0;i<value.Length;i++) {
                int code=Code(value,ref i);if(code==167&&i+1<value.Length){i++;continue;}
                if(code==10){max=Math.Max(max,width);width=0;continue;}if(code==13)continue;
                if(code==9){width+=16;continue;}if(TryGet(code,out var glyph))width+=glyph.Advance;else width+=6;
            }
            return Math.Max(max,width);
        }
    }
}
