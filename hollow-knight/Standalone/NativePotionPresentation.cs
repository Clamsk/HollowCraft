using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativePresentation
    {
        private sealed class SplashParticle {internal SpriteRenderer Renderer;internal Vector2 Velocity;internal Color Color;internal float Age,Life,Size;internal bool Falling;internal Sprite[] Frames;}
        private readonly List<SplashParticle> splash=new List<SplashParticle>();
        private readonly Stack<SplashParticle> splashPool=new Stack<SplashParticle>();
        private readonly Dictionary<string,Sprite> splashFrames=new Dictionary<string,Sprite>();
        private readonly Dictionary<string,Sprite[]> splashAnimations=new Dictionary<string,Sprite[]>();
        private string splashRoom;private int splashVisit;
        private Sprite SplashFrame(string name) {
            if(splashFrames.TryGetValue(name,out var frame))return frame;
            var root=HollowCraftRuntime.Instance.Assets;Texture2D texture=File.Exists(Path.Combine(root,"particle_"+name+".png"))?NativeAssets.Texture("minecraft:particle_"+name):Texture2D.whiteTexture;
            frame=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width);splashFrames[name]=frame;return frame;
        }
        private Sprite[] SplashAnimation(string name,int count){if(splashAnimations.TryGetValue(name,out var frames))return frames;frames=new Sprite[count];for(int i=0;i<count;i++)frames[i]=SplashFrame(name+"_"+i);splashAnimations[name]=frames;return frames;}
        internal void PotionSplash(JObject message) {
            var runtime=HollowCraftRuntime.Instance;if(runtime==null||!runtime.InGame)return;
            if(splashRoom!=runtime.Room||splashVisit!=runtime.SceneVisit)ClearSplash();splashRoom=runtime.Room;splashVisit=runtime.SceneVisit;
            int rgb=(int?)message["color"]??3694022;var tint=new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);
            bool water=(bool?)message["water"]==true,instant=(bool?)message["instant"]==true;
            var origin=new Vector3((float)message["x"],(float)message["y"],-.25f);
            for(int i=0;i<48&&splash.Count<288;i++) {
                var particle=splashPool.Count>0?splashPool.Pop():new SplashParticle();
                if(particle.Renderer==null){var root=new GameObject("HollowCraft.PotionSplash");root.transform.SetParent(transform,false);particle.Renderer=root.AddComponent<SpriteRenderer>();particle.Renderer.sortingOrder=130;}
                bool glass=i>=40,droplet=i<16||water;float angle=Random.value*Mathf.PI*2,speed=Random.Range(.9f,4.2f);
                particle.Frames=SplashAnimation(glass?"glass":droplet?"splash":instant?"spell":"effect",glass?1:droplet?4:8);particle.Falling=glass||droplet;
                particle.Age=0;particle.Life=glass?Random.Range(.2f,.45f):Random.Range(.55f,1.15f);particle.Size=glass?Random.Range(.025f,.06f):Random.Range(.12f,.25f);
                particle.Color=glass?new Color(.85f,.95f,1,.9f):tint*Random.Range(.7f,1f);particle.Color.a=glass?.9f:.85f;
                particle.Velocity=new Vector2(Mathf.Cos(angle)*speed,Mathf.Sin(angle)*speed*.65f+1.3f);
                particle.Renderer.transform.position=origin;particle.Renderer.transform.localScale=Vector3.one*particle.Size;particle.Renderer.color=particle.Color;particle.Renderer.sprite=particle.Frames[0];particle.Renderer.gameObject.SetActive(true);splash.Add(particle);
            }
        }
        private void ClearSplash(){foreach(var particle in splash){if(particle.Renderer!=null)particle.Renderer.gameObject.SetActive(false);splashPool.Push(particle);}splash.Clear();}
        private void Update() {
            var runtime=HollowCraftRuntime.Instance;
            if(runtime==null||!runtime.InGame||runtime.Room!=splashRoom||runtime.SceneVisit!=splashVisit){if(splash.Count>0)ClearSplash();return;}
            if(runtime.SimulationPaused)return;float elapsed=Mathf.Min(.05f,Time.deltaTime);
            for(int i=splash.Count-1;i>=0;i--){var p=splash[i];p.Age+=elapsed;if(p.Age>=p.Life){p.Renderer.gameObject.SetActive(false);splash.RemoveAt(i);splashPool.Push(p);continue;}
                float progress=p.Age/p.Life;p.Velocity+=new Vector2(0,p.Falling?-7f:1.2f)*elapsed;p.Velocity*=Mathf.Pow(p.Falling?.96f:.88f,elapsed*20);
                p.Renderer.transform.position+=(Vector3)(p.Velocity*elapsed);var color=p.Color;color.a*=1-progress;p.Renderer.color=color;p.Renderer.sprite=p.Frames[Mathf.Min(p.Frames.Length-1,(int)(progress*p.Frames.Length))];p.Renderer.transform.localScale=Vector3.one*p.Size*(1-progress*.4f);
            }
        }
        private void OnDestroy(){foreach(var frame in splashFrames.Values)if(frame!=null)Destroy(frame);splashFrames.Clear();}
    }
}
