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
        private readonly bool easingBefore,loop;
        private readonly float returnTick;
        internal readonly float Begin,End,Stop,Degrees;
        internal NativeAnimationClip(JObject source) {
            var clip=source["emote"] as JObject??source;Begin=(float?)clip["beginTick"]??0;End=Math.Max(1,(float?)clip["endTick"]??20);Stop=Math.Max(End,(float?)clip["stopTick"]??End);
            // PlayerAnimator converts degrees on import. All public rotation samples are radians.
            Degrees=180f/(float)Math.PI;bool degrees=(bool?)clip["degrees"]??true;int version=(int?)source["version"]??1;
            easingBefore=(bool?)clip["easeBeforeKeyframe"]??false;loop=string.Equals((string)clip["isLoop"],"true",StringComparison.OrdinalIgnoreCase);returnTick=(float?)clip["returnTick"]??0;
            foreach(JObject move in clip["moves"] as JArray??new JArray())foreach(var bone in move.Properties())if(bone.Value is JObject axes)foreach(var axis in axes.Properties()) {
                if(axis.Value.Type!=JTokenType.Float&&axis.Value.Type!=JTokenType.Integer)continue;
                string part=Normalize(bone.Name);if(version<3&&part=="torso")part="body";
                string name=part+"/"+axis.Name;if(!channels.TryGetValue(name,out var list))channels[name]=list=new List<Key>();
                bool angle=Angle(axis.Name);float value=(float)axis.Value;if(angle&&degrees)value*=(float)Math.PI/180;
                list.Add(new Key{Tick=(float?)move["tick"]??0,Value=value,Ease=(string)move["easing"]??"LINEAR"});
                int turn=(int?)move["turn"]??0;if(angle&&turn!=0)list.Add(new Key{Tick=(float?)move["tick"]??0,Value=value+turn*2*(float)Math.PI,Ease=(string)move["easing"]??"LINEAR"});
            }
            foreach(var list in channels.Values) {var sorted=System.Linq.Enumerable.OrderBy(list,k=>k.Tick);var copy=new List<Key>(sorted);list.Clear();list.AddRange(copy);}
        }
        private static string Normalize(string bone)=>bone.Replace("_","").ToLowerInvariant();
        private static bool Angle(string axis)=>axis=="pitch"||axis=="yaw"||axis=="roll";
        private static float ClampAngle(float value){float pi=(float)Math.PI,tau=2*pi;return value-(float)Math.Floor((value+pi)/tau)*tau;}
        private static float Default(string bone,string axis) {
            bone=Normalize(bone);if(axis=="x")return bone=="rightarm"?-5:bone=="leftarm"?5:bone=="rightleg"?-1.9f:bone=="leftleg"?1.9f:0;
            if(axis=="y")return bone.EndsWith("arm")?2:bone.EndsWith("leg")?12:0;
            return axis=="z"&&bone.EndsWith("leg")?.1f:0;
        }
        internal bool Has(string bone,string axis)=>channels.ContainsKey(Normalize(bone)+"/"+axis);
        internal float Sample(string bone,string axis,float tick,float baseline=0) {
            if(!channels.TryGetValue(Normalize(bone)+"/"+axis,out var keys)||keys.Count==0)return baseline;
            bool angle=Angle(axis);if(angle)baseline=ClampAngle(baseline);
            // Integer frame lookup plus fractional tick is how the original player samples.
            float frame=(float)Math.Floor(Math.Max(0,tick)),partial=tick-frame;
            if(loop&&frame>returnTick)frame=returnTick+(frame-returnTick)%(End-returnTick+1);
            tick=frame+partial;int index=-1;for(int i=0;i<keys.Count&&keys[i].Tick<=frame;i++)index=i;
            Key before=index<0?new Key{Tick=frame<Begin?0:frame<End?Begin:End,Value=frame<Begin?baseline:Default(bone,axis),Ease="EASEINOUTSINE"}:keys[index];
            if(!loop&&frame>=End&&before.Tick<End)before=new Key{Tick=End,Value=before.Value,Ease=before.Ease};
            Key after;
            if(index+1<keys.Count)after=keys[index+1];
            else if(loop)after=new Key{Tick=End+1,Value=Default(bone,axis),Ease="EASEINOUTSINE"};
            else if(frame<End&&keys.Count>0)after=new Key{Tick=End,Value=keys[keys.Count-1].Value,Ease=keys[keys.Count-1].Ease};
            else after=new Key{Tick=frame>=End?Stop:frame>=Begin?End:Begin,Value=frame>=End?baseline:Default(bone,axis),Ease="EASEINOUTSINE"};
            if(loop&&after.Tick>End){int next=0;while(next<keys.Count&&keys[next].Tick<returnTick)next++;after=next<keys.Count?keys[next]:new Key{Tick=End+1,Value=Default(bone,axis),Ease="EASEINOUTSINE"};}
            float result=Interpolate(before,after,tick);return angle?ClampAngle(result):result;
        }
        private float Interpolate(Key a,Key b,float tick){float span=b.Tick-a.Tick;if(span==0)return a.Value;float t=(tick-a.Tick)/span;return a.Value+(b.Value-a.Value)*Ease(easingBefore?b.Ease:a.Ease,t);}
        internal float Fade(float tick)=>Begin<=0||tick>=Begin?1:Ease("EASEINOUTSINE",tick/Begin);
        internal static float Ease(string name,float value) {
            double t=value;string key=(name??"LINEAR").ToUpperInvariant().Replace("_","");
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
