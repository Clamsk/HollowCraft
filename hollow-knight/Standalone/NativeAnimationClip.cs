using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    // Compatible sampling of locally supplied Emotecraft/PlayerAnimator data.
    // Body is the global bone; torso is the chest. Positions are absolute axes.
    internal sealed class NativeAnimationClip
    {
        private struct Key {internal float Tick,Value;internal string Ease;}
        private readonly Dictionary<string,List<Key>> channels=new Dictionary<string,List<Key>>();
        internal readonly float Begin,End,Stop,Degrees;
        internal NativeAnimationClip(JObject source) {
            var clip=source["emote"] as JObject??source;Begin=(float?)clip["beginTick"]??0;End=Math.Max(1,(float?)clip["endTick"]??20);Stop=Math.Max(End,(float?)clip["stopTick"]??End+10);Degrees=(bool?)clip["degrees"]==true?1:180f/(float)Math.PI;
            foreach(JObject move in clip["moves"] as JArray??new JArray())foreach(var bone in move.Properties())if(bone.Value is JObject axes)foreach(var axis in axes.Properties()) {
                if(axis.Value.Type!=JTokenType.Float&&axis.Value.Type!=JTokenType.Integer)continue;
                string name=bone.Name.Replace("_","").ToLowerInvariant()+"/"+axis.Name;if(!channels.TryGetValue(name,out var list))channels[name]=list=new List<Key>();
                list.Add(new Key{Tick=(float?)move["tick"]??0,Value=(float)axis.Value,Ease=(string)move["easing"]??"LINEAR"});
            }
            foreach(var list in channels.Values)list.Sort((a,b)=>a.Tick.CompareTo(b.Tick));
        }
        internal bool Has(string bone,string axis)=>channels.ContainsKey(bone.Replace("_","").ToLowerInvariant()+"/"+axis);
        internal float Sample(string bone,string axis,float tick,float baseline=0) {
            if(!channels.TryGetValue(bone.Replace("_","").ToLowerInvariant()+"/"+axis,out var keys)||keys.Count==0)return baseline;
            if(tick<=0||tick>=Stop)return baseline;
            Key before=new Key{Tick=0,Value=baseline,Ease="LINEAR"},after=keys[0];
            for(int i=0;i<keys.Count;i++) {if(keys[i].Tick<=tick)before=keys[i];else{after=keys[i];return Interpolate(before,after,tick);}}
            if(tick<=End)return before.Value;
            return before.Value+(baseline-before.Value)*Ease("EASEINOUTSINE",(tick-End)/Math.Max(.001f,Stop-End));
        }
        private static float Interpolate(Key a,Key b,float tick){float span=b.Tick-a.Tick,t=span<=0?1:Math.Max(0,Math.Min(1,(tick-a.Tick)/span));return a.Value+(b.Value-a.Value)*Ease(b.Ease,t);}
        internal float Fade(float tick)=>Begin<=0||tick>=Begin?1:Ease("EASEINOUTSINE",tick/Begin);
        internal static float Ease(string name,float value) {
            double t=Math.Max(0,Math.Min(1,value));string key=(name??"LINEAR").ToUpperInvariant().Replace("_","");
            if(key=="CONSTANT"||key=="STEP")return 0;if(key=="LINEAR")return (float)t;
            bool both=key.Contains("INOUT"),output=!both&&key.Contains("OUT");
            string curve=key.Replace("EASEINOUT","").Replace("EASEOUT","").Replace("EASEIN","");
            Func<double,double> input=x=> {
                switch(curve) {
                    case "SINE":return 1-Math.Cos(x*Math.PI/2);case "QUAD":return x*x;case "CUBIC":return x*x*x;case "QUART":return Math.Pow(x,4);case "QUINT":return Math.Pow(x,5);
                    case "EXPO":return x==0?0:Math.Pow(2,10*x-10);case "CIRC":return 1-Math.Sqrt(Math.Max(0,1-x*x));case "BACK":return 2.70158*x*x*x-1.70158*x*x;
                    case "ELASTIC":return x==0?0:x==1?1:-Math.Pow(2,10*x-10)*Math.Sin((x*10-10.75)*(2*Math.PI/3));
                    case "BOUNCE":return 1-Bounce(1-x);default:return x;
                }
            };
            return (float)(both?(t<.5?input(t*2)/2:1-input((1-t)*2)/2):output?1-input(1-t):input(t));
        }
        private static double Bounce(double x){if(x<1/2.75)return 7.5625*x*x;if(x<2/2.75){x-=1.5/2.75;return 7.5625*x*x+.75;}if(x<2.5/2.75){x-=2.25/2.75;return 7.5625*x*x+.9375;}x-=2.625/2.75;return 7.5625*x*x+.984375;}
    }
}
