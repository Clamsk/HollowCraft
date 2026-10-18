using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace HollowCraft.Standalone
{
    internal sealed class NativeCloudView : MonoBehaviour
    {
        private sealed class Cloud {internal GameObject Root;internal SpriteRenderer[] Points;}
        private readonly Dictionary<int,Cloud> clouds=new Dictionary<int,Cloud>();private Sprite dot;private string room;
        private void LateUpdate() {
            var runtime=HollowCraftRuntime.Instance;if(runtime==null||!runtime.Standalone||!runtime.InGame||runtime.Dead||(string)runtime.LatestSnapshot?["scene"]!=runtime.Room){Clear();return;}
            if(room!=runtime.Room){Clear();room=runtime.Room;}var present=new HashSet<int>();
            foreach(JObject value in runtime.LatestSnapshot?["clouds"] as JArray??new JArray()) {
                int id=(int)value["entity"];present.Add(id);if(!clouds.TryGetValue(id,out var cloud))clouds[id]=cloud=Create();
                float radius=(float)value["radius"],age=Time.time;int color=(int?)value["color"]??0x9966cc;
                var tint=new Color((color>>16&255)/255f,(color>>8&255)/255f,(color&255)/255f,(int?)value["age"]<10?.2f:.65f);
                cloud.Root.transform.position=new Vector3((float)value["x"],(float)value["y"]+.15f,-.02f);
                for(int i=0;i<cloud.Points.Length;i++) {
                    float p=i*2.39996f+age*.35f+id,spread=Mathf.Sqrt((i+.5f)/cloud.Points.Length)*radius;
                    cloud.Points[i].transform.localPosition=new Vector3(Mathf.Cos(p)*spread,.12f+Mathf.Abs(Mathf.Sin(p*1.3f+age))*.6f,0);
                    cloud.Points[i].color=tint;cloud.Points[i].transform.localScale=Vector3.one*(.08f+.04f*Mathf.Sin(age*2+i));
                }
            }
            foreach(int id in clouds.Keys.Where(k=>!present.Contains(k)).ToArray()){Destroy(clouds[id].Root);clouds.Remove(id);}
        }
        private Cloud Create() {
            if(dot==null){var image=new Texture2D(3,3,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var pixels=new Color[9];for(int i=0;i<9;i++)pixels[i]=i%2==0?Color.clear:Color.white;pixels[4]=Color.white;image.SetPixels(pixels);image.Apply();dot=Sprite.Create(image,new Rect(0,0,3,3),new Vector2(.5f,.5f),3);}
            var cloud=new Cloud{Root=new GameObject("Native lingering potion cloud"),Points=new SpriteRenderer[20]};cloud.Root.transform.SetParent(transform,false);
            var actor=HeroController.instance;var hero=actor!=null?actor.GetComponent<Renderer>():null;for(int i=0;i<cloud.Points.Length;i++){var particle=new GameObject("Potion particle");particle.transform.SetParent(cloud.Root.transform,false);var renderer=particle.AddComponent<SpriteRenderer>();renderer.sprite=dot;renderer.sortingLayerID=hero!=null?hero.sortingLayerID:0;renderer.sortingOrder=hero!=null?hero.sortingOrder+2:30;cloud.Points[i]=renderer;}return cloud;
        }
        private void Clear(){foreach(var cloud in clouds.Values)if(cloud.Root!=null)Destroy(cloud.Root);clouds.Clear();}
        private void OnDestroy(){Clear();if(dot!=null){Destroy(dot.texture);Destroy(dot);}}
    }
}
