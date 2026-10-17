using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class SteveAvatar : MonoBehaviour
    {
        private GameObject avatar;
        private readonly NativePlayerView nativeView=new NativePlayerView();
        private readonly Standalone.NativeModelView localView=new Standalone.NativeModelView();
        private Standalone.NativeWingRig nativeWings;
        internal static Texture2D NativeTexture;
        internal static float LandingElapsed=>LandingActive?Time.time-landingStarted:-1;
        private HeroController hero;
        private Renderer knightRenderer;
        private Texture2D skin;
        private Transform head,torso,leftArm,rightArm,leftLeg,rightLeg,held,offhand;
        private GameObject wings;
        private Transform leftWing,rightWing;
        private GameObject playerFlame;
        private readonly Dictionary<string,JObject> animations=new Dictionary<string,JObject>();
        private readonly Dictionary<Transform,Tuple<string,GameObject>> armor=new Dictionary<Transform,Tuple<string,GameObject>>();
        private float nextLookup,walk,nextItem,limbSpeed,limbStep;
        private string heldId,offhandId,assets;
        private int layer,order;
        internal static RenderTexture PortraitTexture;
        private Camera portraitCamera;
        private GameObject portrait;
        private float nextPortrait;
        private string portraitSignature;
        private GameObject whiteEyes;
        private static float landingStarted=-10;
        private bool landingCaptured;
        internal static bool LandingActive=>HollowCraftRuntime.Instance?.Creative==true&&Time.time-landingStarted<1.25f;
        internal static void BeginLanding(){landingStarted=Time.time;}
        internal static void ResetLanding(){landingStarted=-10;}
        private readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
        private void LateUpdate()
        {
            if(hero==null && Time.unscaledTime>=nextLookup) {nextLookup=Time.unscaledTime+1;hero=FindFirstObjectByType<HeroController>();if(hero!=null) CreateAvatar();}
            if(hero==null||avatar==null) {localView.Hide();nativeView.Hide();if(avatar!=null)avatar.SetActive(false);return;}
            WorldCreatures.ConstrainPlayer(hero);
            bool visible=hero.gameObject.activeInHierarchy&&!hero.cState.dead;
            if(visible) foreach(var original in hero.GetComponentsInChildren<Renderer>(true)) original.enabled=false;
            avatar.SetActive(visible);if(!visible) {nativeView.Hide();localView.Hide();return;}
            var collider=hero.GetComponent<Collider2D>();var position=hero.transform.position;var seated=WorldCreatures.PlayerFeet;
            if(seated.HasValue) {position.x=seated.Value.x;position.y=seated.Value.y;}else if(collider!=null)position.y=collider.bounds.min.y;
            position.z-=.1f;avatar.transform.position=position;
            if(!GameplayBackend.Standalone&&nativeView.Draw(assets,position,layer,order)) {NativeTexture=nativeView.Texture;avatar.SetActive(false);return;}
            NativeTexture=null;
            var rb=hero.GetComponent<Rigidbody2D>();float speed=rb!=null?rb.linearVelocity.x:0;
            limbStep+=Time.deltaTime;
            while(limbStep>=.05f) {limbStep-=.05f;limbSpeed+=(Mathf.Min(Math.Abs(speed)*.2f,1)-limbSpeed)*.4f;walk+=limbSpeed;}
            var runtime=HollowCraftRuntime.Instance;var combat=runtime?.Combat;
            whiteEyes.SetActive(runtime?.Creative==true);
            bool burning=(bool?)runtime?.Stats?["onFire"]??false;
            if(burning&&playerFlame==null) {playerFlame=new GameObject("HollowCraft.PlayerFire");playerFlame.transform.SetParent(avatar.transform,false);playerFlame.transform.localPosition=new Vector3(0,.9f,-.22f);playerFlame.transform.localScale=new Vector3(.8f,1.9f,1);var flame=playerFlame.AddComponent<SpriteRenderer>();flame.sortingLayerID=layer;flame.sortingOrder=order+12;flame.color=new Color(1,1,1,.75f);MinecraftSpriteAnimation.Attach(flame,"fire_0");}
            if(playerFlame!=null) playerFlame.SetActive(burning);
            bool swing=combat!=null&&combat.Swinging,crouch=MinecraftControls.Crouching,front=MinecraftControls.FrontView;
            int facing=MinecraftControls.Facing;
            if(swing&&Math.Abs(combat.Aim.x)>.1f&&!front) facing=combat.Aim.x<0?-1:1;
            avatar.transform.localRotation=Quaternion.Euler(0,front?0:-facing*75,MinecraftEnvironment.Gliding?-facing*75:0);
            avatar.transform.localScale=Vector3.one*.9f;
            // Vanilla BipedEntityModel pivots and radians; Better Combat overlays these
            // same shoulder/body pivots rather than rotating cuboids around their centers.
            float phase=(walk-limbSpeed*(1-limbStep/.05f))*.6662f;
            float rightPitch=Mathf.Cos(phase+Mathf.PI)*limbSpeed,leftPitch=Mathf.Cos(phase)*limbSpeed;
            if(runtime?.HeldItem!="minecraft:air")rightPitch=rightPitch*.5f-.31415927f;
            if((string)runtime?.Stats?["offhand"]?["id"]!="minecraft:air")leftPitch=leftPitch*.5f-.31415927f;
            if(crouch) {rightPitch+=.4f;leftPitch+=.4f;}
            torso.localPosition=new Vector3(0,1.5f-(crouch?3.2f/16:0),0);
            torso.localRotation=Quaternion.Euler(crouch?-.5f*Mathf.Rad2Deg:0,0,0);
            head.localPosition=new Vector3(0,1.5f-(crouch?4.2f/16:0),0);
            head.localRotation=Quaternion.identity;
            leftArm.localPosition=new Vector3(-5f/16,1.5f-(crouch?5.2f:2)/16,0);rightArm.localPosition=new Vector3(5f/16,leftArm.localPosition.y,0);
            leftLeg.localPosition=new Vector3(-1.9f/16,1.5f-(crouch?12.2f:12)/16,crouch?.25f:0);rightLeg.localPosition=new Vector3(1.9f/16,leftLeg.localPosition.y,leftLeg.localPosition.z);
            float age=Time.time*20,bob=Mathf.Sin(age*.067f)*.05f,roll=Mathf.Cos(age*.09f)*.05f+.05f;
            leftArm.localRotation=Quaternion.Euler((-leftPitch+bob)*Mathf.Rad2Deg,0,roll*Mathf.Rad2Deg);rightArm.localRotation=Quaternion.Euler((-rightPitch-bob)*Mathf.Rad2Deg,0,-roll*Mathf.Rad2Deg);
            leftLeg.localRotation=Quaternion.Euler(-Mathf.Cos(phase+Mathf.PI)*1.4f*limbSpeed*Mathf.Rad2Deg,0,0);rightLeg.localRotation=Quaternion.Euler(-Mathf.Cos(phase)*1.4f*limbSpeed*Mathf.Rad2Deg,0,0);
            foreach(var renderer in avatar.GetComponentsInChildren<MeshRenderer>()) {
                tint.SetColor("_Color",PlayerSystem.Hurt&&renderer.name=="Model geometry"?new Color(1,.35f,.35f,1):Color.white);renderer.SetPropertyBlock(tint);
            }
            if(Time.unscaledTime>=nextItem) {
                nextItem=Time.unscaledTime+.25f;
                int slot=(int?)runtime?.LatestSnapshot?["selected"] ?? 0;
                string main=(string)runtime?.LatestSnapshot?["inventory"]?[slot]?["render"] ?? runtime?.HeldItem,second=(string)runtime?.Stats?["offhand"]?["render"] ?? (string)runtime?.Stats?["offhand"]?["id"];
                if(runtime?.HeldItem=="minecraft:bow"&&(bool?)runtime.Stats?["using"]==true) main="minecraft:bow_pulling_"+Mathf.Clamp(((int?)runtime.Stats?["useTicks"] ?? 0)/7,0,2);
                if((bool?)runtime?.Stats?["blocking"]==true) {if((string)runtime.Stats["activeHand"]=="offhand"&&second=="minecraft:shield")second="minecraft:shield_blocking";else if(main=="minecraft:shield")main="minecraft:shield_blocking";}
                if(main!=heldId && MinecraftMeshes.Item(held,main,assets,layer,order+8)) heldId=main;
                if(second!=offhandId && MinecraftMeshes.Item(offhand,second,assets,layer,order+7,true)) offhandId=second;
                Armor(head,0,8,8,8,0,0,false);Armor(torso,1,8,12,4,16,16,false);
                Armor(leftArm,1,4,12,4,40,16,true);Armor(rightArm,1,4,12,4,40,16,true);
                Armor(leftLeg,2,4,12,4,0,16,true);Armor(rightLeg,2,4,12,4,0,16,true);
                if(wings!=null) wings.SetActive((string)runtime?.Stats?["equipment"]?[1]?["id"]=="minecraft:elytra");
            }
            if(leftWing!=null) {leftWing.localRotation=Quaternion.Euler(15,-20,MinecraftEnvironment.Gliding?-70:-15);rightWing.localRotation=Quaternion.Euler(15,20,MinecraftEnvironment.Gliding?70:15);}
            if(swing) {
                JObject clip=Animation((string)combat.Current["move"]?["animation"]);
                if(GameplayBackend.Standalone&&clip==null)rightArm.localRotation=Quaternion.Euler(-Mathf.Sin(combat.Progress*Mathf.PI)*110,Mathf.Sin(combat.Progress*Mathf.PI*2)*25,0);
                float aim=Mathf.Atan2(combat.Aim.y,Math.Abs(combat.Aim.x))*Mathf.Rad2Deg;
                float weight=AttackWeight(clip,combat.Progress);
                rightArm.localRotation=Quaternion.Slerp(rightArm.localRotation,Rotation(clip,"rightArm",combat.Progress)*Quaternion.Euler(-aim,0,0),weight);
                leftArm.localRotation=Quaternion.Slerp(leftArm.localRotation,Rotation(clip,"leftArm",combat.Progress),weight);
                torso.localRotation=Quaternion.Slerp(torso.localRotation,Rotation(clip,"body",combat.Progress),weight);
                head.localRotation=Quaternion.Slerp(head.localRotation,Rotation(clip,"head",combat.Progress),weight);
            }
            else if((bool?)runtime?.Stats?["using"]==true) {
                string action=(string)runtime.Stats["useAction"];
                if(action=="BOW"||action=="CROSSBOW") {rightArm.localRotation=Quaternion.Euler(-90,0,0);leftArm.localRotation=Quaternion.Euler(-95,-25,0);}
                else if(action=="BLOCK") {var arm=(string)runtime.Stats["activeHand"]=="offhand"?leftArm:rightArm;arm.localRotation=Quaternion.Euler(-65,0,arm==leftArm?-20:20);}
                else if(action=="SPEAR") rightArm.localRotation=Quaternion.Euler(-145,0,0);
                else if(action=="DRINK"||action=="EAT") rightArm.localRotation=Quaternion.Euler(-115+Mathf.Sin(Time.time*12)*5,0,0);
            }
            ApplyLandingPose();
            nativeWings?.Pose((string)runtime?.Stats?["equipment"]?[1]?["id"]);
            if(!GameplayBackend.Standalone)foreach(var renderer in avatar.GetComponentsInChildren<MeshRenderer>()) MinecraftMeshes.SortFaces(renderer);
            if(Time.unscaledTime>=nextPortrait) {nextPortrait=Time.unscaledTime+.1f;DrawPortrait();}
            if(GameplayBackend.Standalone)localView.Draw(avatar,position,layer,order,(string)runtime?.Stats?["equipment"]?[1]?["id"]==TerrariaEquipment.Wings);
        }
        private void DrawPortrait()
        {
            if(portraitCamera==null) {
                PortraitTexture=new RenderTexture(128,160,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point};PortraitTexture.Create();
                var go=new GameObject("HollowCraft.InventoryPortraitCamera");DontDestroyOnLoad(go);portraitCamera=go.AddComponent<Camera>();portraitCamera.enabled=false;portraitCamera.orthographic=true;portraitCamera.orthographicSize=1.08f;portraitCamera.clearFlags=CameraClearFlags.SolidColor;portraitCamera.backgroundColor=Color.clear;portraitCamera.cullingMask=1<<31;portraitCamera.targetTexture=PortraitTexture;portraitCamera.nearClipPlane=.1f;portraitCamera.farClipPlane=10;
                go.transform.position=new Vector3(10000,.88f,-4);
            }
            string signature=heldId+offhandId+string.Join("|",armor.Values.Select(a=>a.Item1))+((bool?)HollowCraftRuntime.Instance?.Stats?["onFire"]??false)+HollowCraftRuntime.Instance.Creative;
            if(portrait==null||signature!=portraitSignature) {
                if(portrait!=null) Destroy(portrait);portrait=Instantiate(avatar);portrait.name="HollowCraft.InventoryPortrait3D";DontDestroyOnLoad(portrait);portraitSignature=signature;
                foreach(var transform in portrait.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer=31;
                foreach(var renderer in portrait.GetComponentsInChildren<MeshRenderer>(true))renderer.enabled=true;
                portrait.transform.position=new Vector3(10000,0,0);portrait.transform.localRotation=Quaternion.Euler(0,-20,0);
            }
            portrait.SetActive(true);foreach(var renderer in portrait.GetComponentsInChildren<MeshRenderer>()) MinecraftMeshes.SortFaces(renderer,portraitCamera);portraitCamera.Render();
        }
        private void Armor(Transform part,int slot,int w,int h,int d,int u,int v,bool pivot)
        {
            var equipment=HollowCraftRuntime.Instance?.Stats?["equipment"] as JArray;string id=(string)equipment?[slot]?["id"];
            if(slot==1&&(id==TerrariaEquipment.Wings||id=="minecraft:elytra"))id="minecraft:air";
            if(slot==2&&(id==null||id=="minecraft:air")) {slot=3;id=(string)equipment?[slot]?["id"];}
            if(armor.TryGetValue(part,out var prior)&&prior.Item1==id) return;
            if(prior!=null) Destroy(prior.Item2);
            if(string.IsNullOrEmpty(id)||id=="minecraft:air") {armor.Remove(part);return;}
            string material=id.Replace("minecraft:","").Split('_')[0];if(material=="golden") material="gold";
            var texture=HollowCraftRuntime.Instance.ItemTexture("armor_"+material+"_layer_"+(slot==2?"2":"1"));if(texture==null) return;
            var go=new GameObject("Minecraft 3D armor "+id);go.transform.SetParent(part,false);
            go.transform.localPosition=ModelOffset(part);
            MinecraftMeshes.Renderer(go,MinecraftMeshes.Cuboid(w,h,d,u,v,pivot,texture,.018f),texture,layer,order+6);
            armor[part]=Tuple.Create(id,go);
        }
        private JObject Animation(string id)
        {
            if(string.IsNullOrEmpty(id)) return null;string name=id.Split(':').Last();
            if(name.Any(c=>!char.IsLetterOrDigit(c)&&c!='_')) return null;
            if(animations.TryGetValue(name,out JObject cached)) return cached;
            string file=Path.Combine(assets,"bettercombat",name+".json");cached=File.Exists(file)?JObject.Parse(File.ReadAllText(file))["emote"] as JObject:null;
            animations[name]=cached;return cached;
        }
        private static Quaternion Rotation(JObject clip,string bone,float progress)
        {
            if(clip==null) return Quaternion.identity;
            float tick=AttackTick(clip,progress),degrees=(bool?)clip["degrees"]==true?1:Mathf.Rad2Deg;
            // ModelPart rotates Z, then Y, then X. Unity Euler uses a different order.
            return Quaternion.AngleAxis(Channel(clip,bone,"roll",tick)*degrees,Vector3.forward)*Quaternion.AngleAxis(-Channel(clip,bone,"yaw",tick)*degrees,Vector3.up)*Quaternion.AngleAxis(-Channel(clip,bone,"pitch",tick)*degrees,Vector3.right);
        }
        private static float AttackTick(JObject clip,float progress)=>Mathf.Lerp((float?)clip?["beginTick"]??0,(float?)clip?["stopTick"]??30,progress);
        private static float AttackWeight(JObject clip,float progress) {
            if(clip==null)return 0;
            float end=(float?)clip["endTick"]??20,stop=(float?)clip["stopTick"]??30;
            float recovery=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(end,Math.Max(end+.01f,stop),AttackTick(clip,progress)));
            return Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.08f))*recovery;
        }
        private static float Channel(JObject clip,string bone,string channel,float tick)
        {
            var keys=((JArray)clip["moves"]).OfType<JObject>().Where(k=>k[bone]?[channel]!=null).OrderBy(k=>(float)k["tick"]).ToArray();if(keys.Length==0) return 0;
            JObject before=keys[0],after=keys[keys.Length-1];foreach(var key in keys) {if((float)key["tick"]<=tick) before=key;if((float)key["tick"]>=tick) {after=key;break;}}
            float width=(float)after["tick"]-(float)before["tick"],t=width<=0?0:Mathf.Clamp01((tick-(float)before["tick"])/width);
            string easing=(string)after["easing"];if(easing=="EASEINOUTQUAD") t=t<.5f?2*t*t:1-Mathf.Pow(-2*t+2,2)/2;else if(easing=="EASEOUTQUAD") t=1-(1-t)*(1-t);
            return Mathf.Lerp((float)before[bone][channel],(float)after[bone][channel],t);
        }
        private void CreateAvatar()
        {
            if(avatar!=null) Destroy(avatar);armor.Clear();heldId=offhandId=null;playerFlame=null;
            assets=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"HollowCraftAssets");string path=Path.Combine(assets,"steve_skin.png");if(!File.Exists(path)&&!GameplayBackend.Standalone) return;
            if(File.Exists(path)){skin=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};skin.LoadImage(File.ReadAllBytes(path));}else skin=Standalone.NativeAssets.Skin();
            knightRenderer=hero.GetComponent<Renderer>()??hero.GetComponentInChildren<Renderer>();layer=knightRenderer!=null?knightRenderer.sortingLayerID:0;order=knightRenderer!=null?knightRenderer.sortingOrder:0;
            avatar=new GameObject("HollowCraft.Steve3D");DontDestroyOnLoad(avatar);
            head=Part("Head",8,8,8,0,0,false);torso=Part("Torso",8,12,4,16,16,false);
            leftArm=Part("Left arm",4,12,4,32,48,true);rightArm=Part("Right arm",4,12,4,40,16,true);
            leftLeg=Part("Left leg",4,12,4,16,48,true);rightLeg=Part("Right leg",4,12,4,0,16,true);
            whiteEyes=new GameObject("HollowCraft.HerobrineEyes");whiteEyes.transform.SetParent(head,false);
            foreach(float x in new[]{-.125f,.125f}) {
                var eye=new GameObject("Herobrine eye");eye.transform.SetParent(whiteEyes.transform,false);eye.transform.localPosition=new Vector3(x,.21875f,-.252f);
                MinecraftMeshes.Renderer(eye,MinecraftMeshes.Cuboid(2,1,.08f,0,0,false,Texture2D.whiteTexture),Texture2D.whiteTexture,layer,order+3);
            }
            held=new GameObject("HollowCraft.HeldItem3D").transform;held.SetParent(rightArm,false);
            offhand=new GameObject("HollowCraft.Offhand3D").transform;offhand.SetParent(leftArm,false);
            string wingFile=Path.Combine(assets,"elytra_model.png");
            if(GameplayBackend.Standalone)nativeWings=new Standalone.NativeWingRig(torso,layer,order);
            if(File.Exists(wingFile)||GameplayBackend.Standalone) {
                var texture=Standalone.NativeAssets.Texture("minecraft:elytra_model",assets);if(!GameplayBackend.Standalone){texture=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};texture.LoadImage(File.ReadAllBytes(wingFile));}
                wings=new GameObject("HollowCraft.Elytra3D");wings.transform.SetParent(torso,false);
                leftWing=new GameObject("Left elytra wing").transform;leftWing.SetParent(wings.transform,false);leftWing.localPosition=new Vector3(-.08f,-.075f,.17f);
                rightWing=new GameObject("Right elytra wing").transform;rightWing.SetParent(wings.transform,false);rightWing.localPosition=new Vector3(.08f,-.075f,.17f);
                MinecraftMeshes.Renderer(leftWing.gameObject,MinecraftMeshes.Cuboid(10,20,2,0,0,true,texture),texture,layer,order+1);
                MinecraftMeshes.Renderer(rightWing.gameObject,MinecraftMeshes.Cuboid(10,20,2,0,0,true,texture),texture,layer,order+1);wings.SetActive(false);
            }
            Debug.Log("[HollowCraft] Steve uses 3D cuboid meshes; shader="+MinecraftMeshes.Material(skin).shader.name);
        }
        private Transform Part(string name,int w,int h,int d,int u,int v,bool pivot)
        {
            var part=new GameObject("HollowCraft.Steve3D."+name);part.transform.SetParent(avatar.transform,false);
            var geometry=new GameObject("Model geometry");geometry.transform.SetParent(part.transform,false);
            geometry.transform.localPosition=name=="Head"?new Vector3(0,.25f,0):name=="Torso"?new Vector3(0,-.375f,0):name=="Right arm"?new Vector3(.0625f,.125f,0):name=="Left arm"?new Vector3(-.0625f,.125f,0):Vector3.zero;
            MinecraftMeshes.Renderer(geometry,MinecraftMeshes.Cuboid(w,h,d,u,v,pivot,skin),skin,layer,order+2);return part.transform;
        }
        private void ApplyLandingPose() {
            if(!LandingActive){landingCaptured=false;return;}
            float elapsed=Time.time-landingStarted,weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.12f))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1.25f,elapsed)));
            avatar.transform.localRotation=Quaternion.Slerp(avatar.transform.localRotation,Quaternion.Euler(0,-MinecraftControls.Facing*55,0),weight);
            float pitch=1.22f*weight,rightPitch=1.15f*weight,leftPitch=1.35f*weight;
            float foot=Mathf.Max(12*Mathf.Cos(rightPitch)+2*Mathf.Abs(Mathf.Sin(rightPitch)),12*Mathf.Cos(leftPitch)+2*Mathf.Abs(Mathf.Sin(leftPitch)));
            float drop=(24-12*Mathf.Cos(pitch)-foot)/16;
            float rootY=1.5f-drop;
            torso.localPosition=new Vector3(0,rootY,0);torso.localRotation=Quaternion.Euler(-pitch*Mathf.Rad2Deg,0,0);
            head.localPosition=new Vector3(0,rootY,0);head.localRotation=Quaternion.Euler(-.12f*weight*Mathf.Rad2Deg,0,0);
            var shoulder=new Vector3(5f/16,rootY-2f/16*Mathf.Cos(pitch),2f/16*Mathf.Sin(pitch));
            rightArm.localPosition=shoulder;leftArm.localPosition=new Vector3(-shoulder.x,shoulder.y,shoulder.z);
            rightArm.localRotation=Quaternion.Slerp(rightArm.localRotation,Quaternion.Euler(-.2f*Mathf.Rad2Deg,0,0),weight);
            leftArm.localRotation=Quaternion.Slerp(leftArm.localRotation,Quaternion.Euler(1.25f*Mathf.Rad2Deg,0,-.15f*Mathf.Rad2Deg),weight);
            var hip=new Vector3(1.9f/16,rootY-.75f*Mathf.Cos(pitch),.75f*Mathf.Sin(pitch));
            rightLeg.localPosition=hip;leftLeg.localPosition=new Vector3(-hip.x,hip.y,hip.z);
            rightLeg.localRotation=Quaternion.Euler(rightPitch*Mathf.Rad2Deg,0,0);leftLeg.localRotation=Quaternion.Euler(-leftPitch*Mathf.Rad2Deg,0,0);
            if(!landingCaptured&&elapsed>.15f&&Environment.GetEnvironmentVariable("HOLLOWCRAFT_CAPTURE")=="1") {
                landingCaptured=true;ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-hero-landing.png"));
            }
        }
        private Vector3 ModelOffset(Transform part) => part==head?new Vector3(0,.25f,0):part==torso?new Vector3(0,-.375f,0):part==rightArm?new Vector3(.0625f,.125f,0):part==leftArm?new Vector3(-.0625f,.125f,0):Vector3.zero;
        private void OnDestroy() {localView.Dispose();nativeView.Dispose();NativeTexture=null;if(knightRenderer!=null) knightRenderer.enabled=true;if(avatar!=null) Destroy(avatar);if(skin!=null) Destroy(skin);if(portrait!=null) Destroy(portrait);if(portraitCamera!=null) Destroy(portraitCamera.gameObject);if(PortraitTexture!=null) {PortraitTexture.Release();Destroy(PortraitTexture);PortraitTexture=null;}}
    }
}
