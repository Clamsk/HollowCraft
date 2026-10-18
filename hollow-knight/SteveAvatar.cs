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
        private readonly Dictionary<string,Standalone.NativeAnimationClip> nativeAnimations=new Dictionary<string,Standalone.NativeAnimationClip>();
        private readonly Vector3[] transitionPositions=new Vector3[7];private readonly Quaternion[] transitionRotations=new Quaternion[7];private int transitionSerial=-1;private bool transitionValid,renderedPose;
        private readonly Vector3[] nativeAngles=new Vector3[7],transitionAngles=new Vector3[7];private bool nativePoseValid,nativeFrameRecorded;
        private Transform animationRoot;
        private readonly Dictionary<Transform,Tuple<string,GameObject>> armor=new Dictionary<Transform,Tuple<string,GameObject>>();
        private readonly Dictionary<string,Tuple<string,GameObject>> nativeArmor=new Dictionary<string,Tuple<string,GameObject>>();
        private float nextLookup,walk,nextItem,limbSpeed,limbStep;
        private string heldId,offhandId,assets;
        private int layer,order;
        internal static RenderTexture PortraitTexture;
        internal static Texture2D NativePortraitTexture;
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
            // InventoryScreen's camera flips X relative to the old cuboid adapter.
            // Reflect the whole rig once: bones, skin, armor and hand matrices agree.
            avatar.transform.localScale=GameplayBackend.Standalone?new Vector3(-.9f,.9f,.9f):Vector3.one*.9f;
            if(GameplayBackend.Standalone&&combat?.Current!=null&&transitionSerial!=combat.AnimationSerial){transitionSerial=combat.AnimationSerial;transitionValid=renderedPose;var bones=new[]{head,torso,rightArm,leftArm,rightLeg,leftLeg,animationRoot};for(int i=0;i<bones.Length;i++){transitionPositions[i]=bones[i].localPosition;transitionRotations[i]=bones[i].localRotation;transitionAngles[i]=nativePoseValid?nativeAngles[i]:ModelAngles(bones[i].localRotation,i==6);}}
            nativeFrameRecorded=false;
            if(animationRoot!=null){animationRoot.localPosition=Vector3.zero;animationRoot.localRotation=Quaternion.identity;}
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
                tint.SetColor("_Color",PlayerSystem.Hurt&&(renderer.name=="Model geometry"||renderer.name=="Skin outer layer")?new Color(1,.35f,.35f,1):Color.white);renderer.SetPropertyBlock(tint);
            }
            if(GameplayBackend.Standalone||Time.unscaledTime>=nextItem) {
                nextItem=Time.unscaledTime+.25f;
                int slot=(int?)runtime?.LatestSnapshot?["selected"] ?? 0;
                string main=(string)runtime?.LatestSnapshot?["inventory"]?[slot]?["render"] ?? runtime?.HeldItem,second=(string)runtime?.Stats?["offhand"]?["render"] ?? (string)runtime?.Stats?["offhand"]?["id"];
                if(runtime?.HeldItem=="minecraft:bow"&&(bool?)runtime.Stats?["using"]==true) main="minecraft:bow_pulling_"+Mathf.Clamp(((int?)runtime.Stats?["useTicks"] ?? 0)/7,0,2);
                if(GameplayBackend.Standalone){bool active=(bool?)runtime?.Stats?["using"]==true,off=(string)runtime?.Stats?["activeHand"]=="offhand";int ticks=(int?)runtime?.Stats?["useTicks"]??0;
                    main=NativeHandModel(main,runtime?.LatestSnapshot?["inventory"]?[slot],active&&!off,ticks);second=NativeHandModel(second,runtime?.Stats?["offhand"],active&&off,ticks);}
                if((bool?)runtime?.Stats?["blocking"]==true) {if((string)runtime.Stats["activeHand"]=="offhand"&&second=="minecraft:shield")second="minecraft:shield_blocking";else if(main=="minecraft:shield")main="minecraft:shield_blocking";}
                if(main!=heldId && MinecraftMeshes.Item(held,main,assets,layer,order+8)) heldId=main;
                if(second!=offhandId && MinecraftMeshes.Item(offhand,second,assets,layer,order+7,true)) offhandId=second;
                Armor(head,0,8,8,8,0,0,false);Armor(torso,1,8,12,4,16,16,false);
                Armor(leftArm,1,4,12,4,40,16,true);Armor(rightArm,1,4,12,4,40,16,true);
                Armor(leftLeg,2,4,12,4,0,16,true);Armor(rightLeg,2,4,12,4,0,16,true);
                if(wings!=null) wings.SetActive((string)runtime?.Stats?["equipment"]?[1]?["id"]=="minecraft:elytra");
            }
            if(leftWing!=null) {leftWing.localRotation=Quaternion.Euler(15,-20,MinecraftEnvironment.Gliding?-70:-15);rightWing.localRotation=Quaternion.Euler(15,20,MinecraftEnvironment.Gliding?70:15);}
            if(GameplayBackend.Standalone){int slot=(int?)runtime?.LatestSnapshot?["selected"]??0;TintHand(held,runtime?.LatestSnapshot?["inventory"]?[slot]);TintHand(offhand,runtime?.Stats?["offhand"]);}
            if(GameplayBackend.Standalone&&combat?.Current!=null&&Standalone.NativeCombatTimeline.Recovery(combat.AnimationAge,(float)combat.Current["duration"])>0&&(bool?)runtime?.Stats?["using"]!=true) {
                ApplyNativeAttack(combat);
            }
            else if(swing) {
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
                float aim=GameCursor.WorldTarget(hero,out Vector2 target,out _)?Mathf.Atan2(target.y-GameCursor.Eye(hero).y,Math.Abs(target.x-GameCursor.Eye(hero).x))*Mathf.Rad2Deg:0;
                if(action=="BOW"||action=="CROSSBOW") {bool off=(string)runtime.Stats["activeHand"]=="offhand";rightArm.localRotation=Quaternion.Euler(90+aim,off?28.6f:5.7f,0);leftArm.localRotation=Quaternion.Euler(90+aim,off?-5.7f:-28.6f,0);}
                else if(action=="BLOCK") {var arm=(string)runtime.Stats["activeHand"]=="offhand"?leftArm:rightArm;arm.localRotation=Quaternion.Euler(60,arm==leftArm?-30:30,0);}
                else if(action=="SPEAR") ((string)runtime.Stats["activeHand"]=="offhand"?leftArm:rightArm).localRotation=Quaternion.Euler(162+aim,0,0);
                else if(action=="DRINK"||action=="EAT") {var arm=(string)runtime.Stats["activeHand"]=="offhand"?leftArm:rightArm;arm.localRotation=Quaternion.Euler(75+Mathf.Sin(Time.time*12)*5,arm==leftArm?25:-25,0);}
            }
            ApplyLandingPose();
            if(GameplayBackend.Standalone){if(!nativeFrameRecorded||LandingActive){var bones=new[]{head,torso,rightArm,leftArm,rightLeg,leftLeg,animationRoot};for(int i=0;i<bones.Length;i++)nativeAngles[i]=ModelAngles(bones[i].localRotation,i==6);}nativePoseValid=true;}
            nativeWings?.Pose((string)runtime?.Stats?["equipment"]?[1]?["id"]);
            if(!GameplayBackend.Standalone)foreach(var renderer in avatar.GetComponentsInChildren<MeshRenderer>()) MinecraftMeshes.SortFaces(renderer);
            if(Time.unscaledTime>=nextPortrait&&(!GameplayBackend.Standalone||NativePortraitTexture==null||runtime?.InventoryVisible==true)) {nextPortrait=Time.unscaledTime+.1f;DrawPortrait();}
            if(GameplayBackend.Standalone)localView.Draw(avatar,position,layer,order,(string)runtime?.Stats?["equipment"]?[1]?["id"]==TerrariaEquipment.Wings);
            renderedPose=true;
        }
        private void DrawPortrait()
        {
            if(GameplayBackend.Standalone){localView.DrawPortrait(avatar);NativePortraitTexture=localView.Portrait;return;}
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
        private void TintHand(Transform hand,JToken item){if(hand==null||item==null)return;foreach(var renderer in hand.GetComponentsInChildren<MeshRenderer>()){renderer.GetPropertyBlock(tint);tint.SetColor("_Color",Standalone.NativeItemColors.Tint(item,renderer.sharedMaterial.mainTexture?.name??""));renderer.SetPropertyBlock(tint);}}
        private static string NativeHandModel(string id,JToken item,bool active,int ticks) {
            string original=(string)item?["id"]??id;if(original=="minecraft:bow")return active?"minecraft:bow_pulling_"+Mathf.Clamp(ticks/7,0,2):original;
            if(original=="minecraft:crossbow"){if((bool?)item?["nbt"]?["Charged"]==true)return "minecraft:crossbow_arrow";if(active)return "minecraft:crossbow_pulling_"+Mathf.Clamp(ticks/8,0,2);}return id;
        }
        private void Armor(Transform part,int slot,int w,int h,int d,int u,int v,bool pivot)
        {
            if(GameplayBackend.Standalone){NativeArmor(part,slot,w,h,d,u,v,pivot);if(part==torso)NativeArmor(part,2,w,h,d,u,v,pivot);if(part==leftLeg||part==rightLeg)NativeArmor(part,3,w,h,d,u,v,pivot);return;}
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
        private void NativeArmor(Transform part,int slot,int w,int h,int d,int u,int v,bool pivot) {
            string id=(string)HollowCraftRuntime.Instance?.Stats?["equipment"]?[slot]?["id"]??"minecraft:air",key=part.name+"/"+slot;
            if(slot==1&&(id==TerrariaEquipment.Wings||id=="minecraft:elytra"))id="minecraft:air";
            if(nativeArmor.TryGetValue(key,out var previous)&&previous.Item1==id)return;
            if(previous!=null){previous.Item2.SetActive(false);Destroy(previous.Item2);nativeArmor.Remove(key);}if(id=="minecraft:air")return;
            string name=id.Replace("minecraft:","").Split('_')[0];if(name=="golden")name="gold";
            var texture=Standalone.NativeAssets.Texture("armor_"+name+"_layer_"+(slot==2?2:1),assets);if(texture==null)return;
            var go=new GameObject("Native joint armor "+id);go.transform.SetParent(part,false);go.transform.localPosition=ModelOffset(part);
            MinecraftMeshes.Renderer(go,MinecraftMeshes.Cuboid(w,h,d,u,v,pivot,texture,slot==2?.03125f:.0625f),texture,layer,order+6);nativeArmor[key]=Tuple.Create(id,go);
        }
        private Standalone.NativeAnimationClip NativeAnimation(string id) {
            if(string.IsNullOrEmpty(id))return null;string name=id.Split(':').Last();if(name.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'))return null;
            if(nativeAnimations.TryGetValue(name,out var cached))return cached;string path=Path.Combine(assets,"bettercombat",name+".json");cached=File.Exists(path)?new Standalone.NativeAnimationClip(JObject.Parse(File.ReadAllText(path))):null;nativeAnimations[name]=cached;return cached;
        }
        private static Vector3 ModelAngles(Quaternion rotation,bool global=false) {
            // Unity.eulerAngles uses ZXY; ModelPart uses ZYX. Decomposing with
            // Unity's order and recomposing with Minecraft's order twisted joints.
            var value=Standalone.NativeRigSpace.Angles(rotation.x,rotation.y,rotation.z,rotation.w,global);return new Vector3(value.X,value.Y,value.Z);
        }
        private static Quaternion ModelRotation(Vector3 angles,bool global=false) {
            // Global body transforms precede LivingEntityRenderer's (-1,-1,1)
            // model scale. Only individual ModelPart rotations need that conversion.
            var q=Standalone.NativeRigSpace.Rotation(angles.x,angles.y,angles.z,global);return new Quaternion(q.X,q.Y,q.Z,q.W);
        }
        private Quaternion NativeRotation(Standalone.NativeAnimationClip clip,string bone,float tick,Quaternion baseline,int index,float fade,float recovery,bool global=false) {
            var initial=ModelAngles(baseline,global);var angles=initial;
            // Better Combat deliberately leaves head pitch to the player's aim.
            if(bone!="head")angles.x=clip.Sample(bone,"pitch",tick,angles.x);
            angles.y=clip.Sample(bone,"yaw",tick,angles.y);angles.z=clip.Sample(bone,"roll",tick,angles.z);
            if(bone=="torso"){if(!clip.Has(bone,"pitch"))angles.x=0;if(!clip.Has(bone,"yaw"))angles.y=0;if(!clip.Has(bone,"roll"))angles.z=0;}
            // Preserve the sampled Euler branch across combos. Recovering angles
            // from a quaternion loses turns and changes branch beyond 90 degrees.
            var previous=transitionValid?transitionAngles[index]:initial;
            // FadeModifier interpolates Euler axes, not a shortest-path quaternion.
            nativeAngles[index]=Vector3.Lerp(initial,Vector3.Lerp(previous,angles,fade),recovery);return ModelRotation(nativeAngles[index],global);
        }
        private void NativeBone(Standalone.NativeAnimationClip clip,string name,Transform bone,int index,float tick,float fade,float recovery) {
            var current=bone.localPosition;
            var pivot=Standalone.NativeRigSpace.Pivot(clip.Sample(name,"x",tick,-current.x*16),clip.Sample(name,"y",tick,(1.5f-current.y)*16),clip.Sample(name,"z",tick,current.z*16));var position=new Vector3(pivot.X,pivot.Y,pivot.Z);
            if(name=="torso"){if(!clip.Has(name,"x"))position.x=0;if(!clip.Has(name,"y"))position.y=1.5f;if(!clip.Has(name,"z"))position.z=0;}
            var rotation=NativeRotation(clip,name,tick,bone.localRotation,index,fade,recovery);position=Vector3.Lerp(transitionValid?transitionPositions[index]:current,position,fade);
            bone.localPosition=Vector3.Lerp(current,position,recovery);bone.localRotation=rotation;
        }
        private void ApplyNativeAttack(CursorCombat combat) {
            var clip=NativeAnimation((string)combat.Current["move"]?["animation"]);
            if(clip==null){if(combat.AnimationAge<1)rightArm.localRotation=Quaternion.Euler(-Mathf.Sin(combat.AnimationAge*Mathf.PI)*110,Mathf.Sin(combat.AnimationAge*Mathf.PI*2)*25,0);return;}
            float tick=Standalone.NativeCombatTimeline.Tick(combat.AnimationAge,clip.End,(float?)combat.Current["upswing"]??.5f);float fade=clip.Fade(tick),recovery=Standalone.NativeCombatTimeline.Recovery(combat.AnimationAge,(float)combat.Current["duration"]);
            nativeAngles[4]=ModelAngles(rightLeg.localRotation);nativeAngles[5]=ModelAngles(leftLeg.localRotation);
            NativeBone(clip,"torso",torso,1,tick,fade,recovery);NativeBone(clip,"head",head,0,tick,fade,recovery);NativeBone(clip,"rightArm",rightArm,2,tick,fade,recovery);NativeBone(clip,"leftArm",leftArm,3,tick,fade,recovery);
            if(!WorldCreatures.Riding){NativeBone(clip,"rightLeg",rightLeg,4,tick,fade,recovery);NativeBone(clip,"leftLeg",leftLeg,5,tick,fade,recovery);}
            var rotation=NativeRotation(clip,"body",tick,Quaternion.identity,6,fade,recovery,true);
            var offset=new Vector3(clip.Sample("body","x",tick),clip.Sample("body","y",tick),clip.Sample("body","z",tick));
            var pivot=new Vector3(0,.7f,0);
            // Root translation is measured in blocks, ModelPart pivots in sixteenths.
            var previous=transitionValid?transitionPositions[6]-pivot+transitionRotations[6]*pivot:Vector3.zero;
            var translated=Vector3.Lerp(previous,offset,fade)*recovery;var root=Standalone.NativeRigSpace.BodyOffset(translated.x,translated.y,translated.z,new Standalone.NativeRigRotation(rotation.x,rotation.y,rotation.z,rotation.w));
            animationRoot.localRotation=rotation;animationRoot.localPosition=new Vector3(root.X,root.Y,root.Z);
            nativeFrameRecorded=true;
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
            if(GameplayBackend.Standalone)localView.ResetSources();
            nativeWings?.Dispose();nativeWings=null;
            renderedPose=transitionValid=nativePoseValid=nativeFrameRecorded=false;transitionSerial=-1;
            if(avatar!=null) Destroy(avatar);armor.Clear();nativeArmor.Clear();heldId=offhandId=null;playerFlame=null;
            assets=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"HollowCraftAssets");string path=Path.Combine(assets,"steve_skin.png");if(!File.Exists(path)&&!GameplayBackend.Standalone) return;
            if(File.Exists(path)){skin=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};skin.LoadImage(File.ReadAllBytes(path));}else skin=Standalone.NativeAssets.Skin();
            knightRenderer=hero.GetComponent<Renderer>()??hero.GetComponentInChildren<Renderer>();layer=knightRenderer!=null?knightRenderer.sortingLayerID:0;order=knightRenderer!=null?knightRenderer.sortingOrder:0;
            avatar=new GameObject("HollowCraft.Steve3D");DontDestroyOnLoad(avatar);
            animationRoot=new GameObject("PlayerAnimator body root").transform;animationRoot.SetParent(avatar.transform,false);
            head=Part("Head",8,8,8,0,0,false);torso=Part("Torso",8,12,4,16,16,false);
            leftArm=Part("Left arm",4,12,4,32,48,true);rightArm=Part("Right arm",4,12,4,40,16,true);
            leftLeg=Part("Left leg",4,12,4,16,48,true);rightLeg=Part("Right leg",4,12,4,0,16,true);
            whiteEyes=new GameObject("HollowCraft.HerobrineEyes");whiteEyes.transform.SetParent(head,false);
            foreach(float x in new[]{-.125f,.125f}) {
                var eye=new GameObject("Herobrine eye");eye.transform.SetParent(whiteEyes.transform,false);eye.transform.localPosition=new Vector3(x,.21875f,GameplayBackend.Standalone?-.284f:-.252f);
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
            var part=new GameObject("HollowCraft.Steve3D."+name);part.transform.SetParent(animationRoot??avatar.transform,false);
            var geometry=new GameObject("Model geometry");geometry.transform.SetParent(part.transform,false);
            geometry.transform.localPosition=name=="Head"?new Vector3(0,.25f,0):name=="Torso"?new Vector3(0,-.375f,0):name=="Right arm"?new Vector3(.0625f,.125f,0):name=="Left arm"?new Vector3(-.0625f,.125f,0):Vector3.zero;
            MinecraftMeshes.Renderer(geometry,MinecraftMeshes.Cuboid(w,h,d,u,v,pivot,skin),skin,layer,order+2);
            if(GameplayBackend.Standalone&&skin.height>=64){int outerU=name=="Head"?32:name=="Torso"?16:name=="Left arm"?48:name=="Right arm"?40:0,outerV=name=="Head"?0:name=="Left arm"||name=="Left leg"?48:32;var outer=new GameObject("Skin outer layer");outer.transform.SetParent(part.transform,false);outer.transform.localPosition=geometry.transform.localPosition;MinecraftMeshes.Renderer(outer,MinecraftMeshes.Cuboid(w,h,d,outerU,outerV,pivot,skin,name=="Head"?.03125f:.015625f),skin,layer,order+3);}
            return part.transform;
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
        private void OnDestroy() {localView.Dispose();nativeView.Dispose();nativeWings?.Dispose();NativeTexture=null;NativePortraitTexture=null;if(knightRenderer!=null) knightRenderer.enabled=true;if(avatar!=null) Destroy(avatar);if(skin!=null) Destroy(skin);if(portrait!=null) Destroy(portrait);if(portraitCamera!=null) Destroy(portraitCamera.gameObject);if(PortraitTexture!=null) {PortraitTexture.Release();Destroy(PortraitTexture);PortraitTexture=null;}}
    }
}
