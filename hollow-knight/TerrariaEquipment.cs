using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace HollowCraft
{
    internal sealed class LocalChat
    {
        private readonly HollowCraftRuntime runtime;
        internal bool Open;private int closedFrame=-1,openedFrame=-1,compositionFrame=-2;private string text="";private bool focus,submitRequested;
        internal bool Captured=>Open||closedFrame==Time.frameCount;
        internal LocalChat(HollowCraftRuntime runtime) {this.runtime=runtime;}
        internal void Close() {if(Open)closedFrame=Time.frameCount;Open=focus=submitRequested=false;GUIUtility.keyboardControl=0;}
        private bool CanSubmit=>string.IsNullOrEmpty(Input.compositionString)&&Time.frameCount>compositionFrame+1;
        private void Submit() {
            // Both physical-key polling and IMGUI use this single, idempotent path.
            if(!Open||!CanSubmit)return;
            if(!string.IsNullOrWhiteSpace(text))runtime.Send(new {type="chat",scene=runtime.Room,text=text.Trim(),assets=File.Exists(Path.Combine(runtime.Assets,"terraria","ready.txt"))});
            Close();
        }
        internal void Tick() {
            if(!string.IsNullOrEmpty(Input.compositionString)) {compositionFrame=Time.frameCount;submitRequested=false;}
            if(Open) {
                if(!runtime.InGame||!runtime.Connected||runtime.Dead||runtime.NativeControlLocked||Input.GetKeyDown(KeyCode.Escape))Close();
                else if(Application.isFocused&&CanSubmit&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)))submitRequested=true;
                return;
            }
            if(!runtime.InGame||!runtime.Connected||runtime.GameplayBlocked||!Application.isFocused)return;
            if(Input.GetKeyDown(KeyCode.T)||Input.GetKeyDown(KeyCode.Slash)) {Open=focus=true;openedFrame=Time.frameCount;text=Input.GetKeyDown(KeyCode.Slash)?"/":"";runtime.Combat.Cancel();}
        }
        internal void Draw() {
            if(!Open)return;
            var e=Event.current;
            if(!string.IsNullOrEmpty(Input.compositionString)) {compositionFrame=Time.frameCount;submitRequested=false;}
            // A focused TextField consumes Return in its own key handler. Handle it
            // before drawing the field, while leaving IME confirmation to the editor.
            if(CanSubmit&&e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)) {Submit();e.Use();return;}
            if(openedFrame==Time.frameCount&&e.type==EventType.KeyDown&&(e.keyCode==KeyCode.T||e.keyCode==KeyCode.Slash))e.Use();
            float scale=Mathf.Clamp(Screen.height/540f,1,3),height=32*scale;
            var box=new Rect(8,Screen.height-height-8,Screen.width-16,height);
            GUI.Box(box,GUIContent.none);var style=new GUIStyle(GUI.skin.textField){fontSize=Mathf.RoundToInt(18*scale)};
            GUI.SetNextControlName("HollowCraft.Chat");text=GUI.TextField(new Rect(box.x+5,box.y+4,box.width-10,box.height-8),text,256,style);
            if(focus){GUI.FocusControl("HollowCraft.Chat");focus=false;}
            // Fallback if another OnGUI listener consumed the key. Repaint follows
            // text/paste events, so submission sees the field's final contents.
            if(submitRequested&&e.type==EventType.Repaint)Submit();
        }
    }

    internal sealed class TerrariaEquipment : MonoBehaviour
    {
        internal const string Zenith="hollowcraft_bridge:zenith",Wings="hollowcraft_bridge:stardust_wings";
        internal static bool WingEquipped=>(string)HollowCraftRuntime.Instance?.Stats?["equipment"]?[1]?["id"]==Wings;
        internal static bool WingActive;internal static float WingPhase,FlightRemaining=3;
        internal static string WingState="none";
        private HollowCraftRuntime runtime;private bool sent;private float nextSend;
        internal int SwingSerial;private float swungAt=-100;
        internal bool Swinging=>runtime.HeldItem==Zenith&&Time.unscaledTime-swungAt<.5f;
        private readonly Dictionary<int,Sword> swords=new Dictionary<int,Sword>();
        private readonly Stack<Sword> pool=new Stack<Sword>();private readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        private readonly List<int> removed=new List<int>();private Material trailMaterial;private AudioClip swing;private AudioSource sound;
        private string context="";private int tick=-1;private float received;
        private readonly List<Spark> sparks=new List<Spark>();private Sprite sparkle;private float nextSpark;
        private static readonly string[] Names={"zenith","copper_shortsword","starfury","terra_blade","enchanted_sword","bee_keeper","horsemans_blade","influx_waver","seedler","meowmere","star_wrath"};
        private static readonly Color[] Colors={new Color(.45f,1,1),new Color(1,.55f,.25f),new Color(1,.5f,.9f),new Color(.2f,1,.5f),new Color(.35f,.55f,1),new Color(1,.85f,.2f),new Color(1,.45f,.05f),new Color(.2f,1,.9f),new Color(.65f,1,.25f),new Color(1,.4f,.8f),new Color(1,.5f,1)};
        internal void Initialize(HollowCraftRuntime owner) {runtime=owner;trailMaterial=new Material(Shader.Find("Sprites/Default"));sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;StartCoroutine(LoadSound());}
        private IEnumerator LoadSound() {
            string file=Path.Combine(runtime.Assets,"terraria","swing.wav");if(!File.Exists(file))yield break;
            using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri,AudioType.WAV)) {yield return request.SendWebRequest();if(request.result==UnityWebRequest.Result.Success)swing=DownloadHandlerAudioClip.GetContent(request);}
        }
        internal void InputAttack(HeroController hero,Vector2 target,bool down,bool held) {
            if(runtime.HeldItem!=Zenith)return;
            bool active=down||held;
            if(active&&(down||!sent||Time.unscaledTime>=nextSend)) {nextSend=Time.unscaledTime+.05f;runtime.Send(new {type="zenithUse",scene=runtime.Room,visit=runtime.SceneVisit,x=target.x,y=target.y,held=true});sent=true;}
            else if(!active)CancelInput();
            if(active){MinecraftControls.Facing=target.x<hero.transform.position.x?-1:1;MinecraftControls.frontView=false;}
        }
        internal void CancelInput() {if(sent&&runtime.Connected)runtime.Send(new {type="zenithUse",scene=runtime.Room,visit=runtime.SceneVisit,held=false});sent=false;}
        internal void Apply(JObject data) {
            if((string)data["scene"]!=runtime.Room||(string)data["campaign"]!=runtime.CampaignId||(int?)data["visit"]!=runtime.SceneVisit)return;
            int next=(int?)data["tick"]??-1;if(next<=tick)return;tick=next;received=Time.unscaledTime;
            var present=new HashSet<int>();
            foreach(JObject value in data["swords"] as JArray??new JArray()) {
                int id=(int)value["id"];present.Add(id);
                if(!swords.TryGetValue(id,out Sword sword)) {
                    sword=pool.Count>0?pool.Pop():Create();swords.Add(id,sword);sword.Root.SetActive(true);
                    sword.Style=Mathf.Clamp((int)value["style"],0,Names.Length-1);sword.Sprite.sprite=Sprite(Names[sword.Style]);
                    if(sword.Style==0){SwingSerial++;swungAt=Time.unscaledTime;}
                    var color=Colors[sword.Style];sword.Trail.startColor=new Color(color.r,color.g,color.b,.75f);sword.Trail.endColor=new Color(color.r,color.g,color.b,0);
                    if(swing!=null&&swords.Count<13&&!runtime.AudioPaused)sound.PlayOneShot(swing,.3f);
                }
                sword.Delta=new Vector2((float)value["dx"],(float)value["dy"]);sword.Age=(float)value["age"];sword.Rate=(float)value["rate"];sword.Spin=(float)value["spin"];
            }
            removed.Clear();foreach(int id in swords.Keys)if(!present.Contains(id))removed.Add(id);foreach(int id in removed){var sword=swords[id];sword.Root.SetActive(false);pool.Push(sword);swords.Remove(id);}
        }
        private Sword Create() {
            var root=new GameObject("HollowCraft.Zenith");root.transform.SetParent(transform);
            var renderer=root.AddComponent<SpriteRenderer>();renderer.sortingOrder=110;
            var child=new GameObject("Prismatic trail");child.transform.SetParent(root.transform);var trail=child.AddComponent<LineRenderer>();trail.sharedMaterial=trailMaterial;trail.useWorldSpace=true;trail.positionCount=24;trail.startWidth=.22f;trail.endWidth=.005f;trail.sortingOrder=109;
            return new Sword{Root=root,Sprite=renderer,Trail=trail};
        }
        private Sprite Sprite(string name) {
            if(sprites.TryGetValue(name,out var value))return value;
            string path=Path.Combine(runtime.Assets,"terraria",name+".png");if(!File.Exists(path)){if(runtime.Standalone){value=Standalone.NativeAssets.SwordSprite();sprites[name]=value;return value;}return null;}
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};texture.LoadImage(File.ReadAllBytes(path));
            value=UnityEngine.Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),1/.0375f);sprites[name]=value;return value;
        }
        private void LateUpdate() {
            if(runtime==null)return;
            string current=runtime.CampaignId+"|"+runtime.Room+"|"+runtime.SceneVisit;
            if(context!=current||!runtime.InGame) {context=current;ResetVisuals();CancelInput();return;}
            if(runtime.GameplayBlocked||runtime.HeldItem!=Zenith||!Application.isFocused)CancelInput();
            var hero=HeroController.instance;if(hero==null)return;
            Vector2 center=new Vector2(hero.transform.position.x,hero.GetComponent<Collider2D>().bounds.min.y+1);
            float elapsed=runtime.SimulationPaused?0:Mathf.Min(.15f,Time.unscaledTime-received);
            int layer=hero.GetComponent<Renderer>()?.sortingLayerID??0;
            WingSparks(hero,layer);
            foreach(var sword in swords.Values) {
                float age=Mathf.Min(119.99f,sword.Age+elapsed*120*sword.Rate);
                float fade=Mathf.Clamp01((120-age)/60);
                Vector2 point=Point(center,sword,age);sword.Root.transform.position=new Vector3(point.x,point.y,-.6f);
                Vector2 tangent=Point(center,sword,age+.25f)-Point(center,sword,Mathf.Max(0,age-.25f));
                if(tangent.sqrMagnitude>.000001f)sword.Root.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg-45);
                sword.Sprite.sortingLayerID=sword.Trail.sortingLayerID=layer;sword.Sprite.color=new Color(1,1,1,fade);
                for(int n=0;n<24;n++){Vector2 previous=Point(center,sword,Mathf.Max(0,age-n*1.8f));sword.Points[n]=new Vector3(previous.x,previous.y,-.59f);}
                sword.Trail.SetPositions(sword.Points);var c=Colors[sword.Style];sword.Trail.startColor=new Color(c.r,c.g,c.b,.8f*fade);
                sword.Root.SetActive(age<120&&!runtime.Dead);
            }
        }
        // Projected FinalFractal equations; keep this identical to the server's hit trajectory.
        private static Vector2 Point(Vector2 center,Sword f,float age) {
            float p=Mathf.Clamp01(age/60),length=f.Delta.magnitude,lerp=Mathf.Clamp01(1-length*2/(900*.0375f));
            float minimum=Mathf.Min(60*.0375f,Mathf.Max(.6f,length));
            float angle=Mathf.PI+Mathf.Sign(f.Delta.x)*p*Mathf.PI*2,radius=Mathf.Max(minimum,length+40*.0375f*Mathf.Clamp01((p-.5f)*2));
            float x=Mathf.Cos(angle)*radius,y=Mathf.Sin(angle)*f.Spin*(2-lerp),rot=Mathf.Atan2(f.Delta.y,f.Delta.x);
            return center+f.Delta+new Vector2(x*Mathf.Cos(rot)-y*Mathf.Sin(rot),x*Mathf.Sin(rot)+y*Mathf.Cos(rot))+(1-Mathf.Clamp01((p-.5f)*2))*new Vector2(-Mathf.Sign(f.Delta.x)*radius*.1f,-f.Spin*.3f*.0375f);
        }
        internal void ResetEffects() {ResetVisuals();sent=false;}
        private void ResetVisuals() {foreach(var sword in swords.Values){sword.Root.SetActive(false);pool.Push(sword);}swords.Clear();foreach(var spark in sparks){spark.Life=0;spark.Root.SetActive(false);}tick=-1;swungAt=-100;}
        private void WingSparks(HeroController hero,int layer) {
            if(WingActive&&Time.time>=nextSpark) {
                nextSpark=Time.time+.06f;Spark target=sparks.Find(value=>value.Life<=0);
                if(target==null&&sparks.Count<16) {
                    if(sparkle==null){var texture=new Texture2D(5,5,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var pixels=new Color[25];for(int i=0;i<5;i++){pixels[2+i*5]=Color.white;pixels[10+i]=Color.white;}texture.SetPixels(pixels);texture.Apply();sparkle=UnityEngine.Sprite.Create(texture,new Rect(0,0,5,5),new Vector2(.5f,.5f),32);}
                    var root=new GameObject("HollowCraft.Stardust");root.transform.SetParent(transform);var renderer=root.AddComponent<SpriteRenderer>();renderer.sprite=sparkle;renderer.sortingOrder=108;target=new Spark{Root=root,Renderer=renderer};sparks.Add(target);
                }
                if(target!=null){target.Life=.65f;target.Root.SetActive(true);target.Root.transform.position=hero.transform.position+new Vector3(UnityEngine.Random.Range(-.7f,.7f),UnityEngine.Random.Range(-.4f,.7f),-.5f);}
            }
            foreach(var value in sparks)if(value.Life>0){value.Life-=Time.deltaTime;value.Root.SetActive(value.Life>0);value.Root.transform.position+=new Vector3(0,-.8f*Time.deltaTime,0);value.Renderer.sortingLayerID=layer;value.Renderer.color=new Color(.2f,.7f,1,Mathf.Clamp01(value.Life/.5f));value.Root.transform.localScale=Vector3.one*(.5f+value.Life);}
        }
        private void OnDestroy() {foreach(var value in sprites.Values){Destroy(value.texture);Destroy(value);}if(sparkle!=null){Destroy(sparkle.texture);Destroy(sparkle);}if(trailMaterial!=null)Destroy(trailMaterial);if(swing!=null)Destroy(swing);WingActive=false;WingPhase=0;FlightRemaining=3;}
        private sealed class Spark {internal GameObject Root;internal SpriteRenderer Renderer;internal float Life;}
        private sealed class Sword {internal GameObject Root;internal SpriteRenderer Sprite;internal LineRenderer Trail;internal int Style;internal Vector2 Delta;internal float Age,Rate,Spin;internal Vector3[] Points=new Vector3[24];}
    }
}
