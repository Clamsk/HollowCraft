using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft {
    internal sealed class MinecraftSpriteAnimation : MonoBehaviour {
        private sealed class Clip {internal Sprite[] Frames;internal int[] Order;internal float[] Duration;internal float Total;}
        private static readonly Dictionary<string,Clip> clips=new Dictionary<string,Clip>();
        private SpriteRenderer renderer;private Clip clip;private int frame=-1;
        internal static void Attach(SpriteRenderer value,string name) {
            if(!clips.TryGetValue(name,out Clip data)) {
                string path=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"HollowCraftAssets",name+".png");if(!File.Exists(path)) return;
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};texture.LoadImage(File.ReadAllBytes(path));int count=texture.height/texture.width;if(count<1) {Destroy(texture);return;}
                data=new Clip{Frames=new Sprite[count]};for(int i=0;i<count;i++) data.Frames[i]=Sprite.Create(texture,new Rect(0,texture.height-(i+1)*texture.width,texture.width,texture.width),new Vector2(.5f,.5f),texture.width);
                var meta=File.Exists(path+".mcmeta")?JObject.Parse(File.ReadAllText(path+".mcmeta"))["animation"]:null;int duration=Math.Max(1,(int?)meta?["frametime"]??1);var frames=meta?["frames"] as JArray;
                data.Order=frames==null?Enumerable.Range(0,count).ToArray():frames.Select(f=>Mathf.Clamp(f is JObject?(int)f["index"]:(int)f,0,count-1)).ToArray();
                data.Duration=frames==null?Enumerable.Repeat(duration*.05f,count).ToArray():frames.Select(f=>Math.Max(1,f is JObject?(int?)f["time"]??duration:duration)*.05f).ToArray();data.Total=data.Duration.Sum();clips[name]=data;
            }
            var animation=value.GetComponent<MinecraftSpriteAnimation>()??value.gameObject.AddComponent<MinecraftSpriteAnimation>();animation.renderer=value;animation.clip=data;animation.frame=-1;
        }
        private void LateUpdate() {if(clip==null||renderer==null||clip.Total<=0) return;float time=Time.time%clip.Total;int index=0;while(index<clip.Duration.Length-1&&time>=clip.Duration[index]) time-=clip.Duration[index++];int next=clip.Order[index];if(next!=frame) {frame=next;renderer.sprite=clip.Frames[frame];}}
    }
}
