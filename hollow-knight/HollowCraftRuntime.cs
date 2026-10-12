using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class HollowCraftRuntime : MonoBehaviour
    {
        private const float CellSize = 1f;
        private BridgeClient bridge;
        private readonly NativePointerBuffer pointerInput=new NativePointerBuffer();
        private Collider2D[] terrainProbe=new Collider2D[32];
        internal static HollowCraftRuntime Instance;
        internal CursorCombat Combat;
        internal JumpBuilding JumpBuild;
        internal JObject TowerItem=>PlacementItem is JObject item&&(bool?)item["predictSolid"]==true?item:null;
        internal bool BuildRightHeld=>Input.GetMouseButton(1)||pointerInput.Held(1);
        internal string HeldItem => inventory[selected];
        private JObject PlacementItem {
            get {
                var main=LatestSnapshot?["inventory"]?[selected] as JObject;
                if((bool?)main?["block"]==true&&(int?)main["count"]>0)return main;
                var off=Stats?["offhand"] as JObject;
                return (bool?)off?["block"]==true&&(int?)off["count"]>0?off:null;
            }
        }
        internal Texture2D ItemTexture(string id) {
            if(string.IsNullOrEmpty(id) || id=="minecraft:air") return null;
            string[] parts=id.Split(':');
            if(parts.Length!=2 || parts.Any(p=>p.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='/'))) return Texture(id);
            string key="model:"+id;
            if(textures.TryGetValue(key,out Texture2D cached)) return cached;
            string path=Path.Combine(assets,"item_models_rgba",parts[0],parts[1]+".png");
            if(!File.Exists(path)) return Texture(id);
            var icon=new Texture2D(2,2,TextureFormat.RGBA32,false) {filterMode=FilterMode.Point};icon.LoadImage(File.ReadAllBytes(path));textures[key]=icon;return icon;
        }
        private JObject container;
        private readonly Dictionary<string, GameObject> blocks = new Dictionary<string, GameObject>();
        private readonly Dictionary<string,string> blockStates=new Dictionary<string,string>();
        private readonly Dictionary<string,float> pendingPlacements=new Dictionary<string,float>();
        private readonly Dictionary<int,GameObject> drops=new Dictionary<int,GameObject>();
        private readonly Dictionary<int,GameObject> primedTnt=new Dictionary<int,GameObject>();
        private readonly Dictionary<int,GameObject> projectiles=new Dictionary<int,GameObject>();
        private readonly Dictionary<int,object> nativeProjectileSteps=new Dictionary<int,object>();
        private readonly Dictionary<int,object> nativeTntSteps=new Dictionary<int,object>();
        private int lastTntTick=-1;
        private readonly Dictionary<string,float> retiredProjectiles=new Dictionary<string,float>();
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, int> breakingTicks = new Dictionary<string, int>();
        private readonly string[] inventory = new string[36];
        private readonly int[] counts = new int[36];
        private readonly int[] crafting = Enumerable.Repeat(-1, 9).ToArray();
        private string scene = "", status = "Start Minecraft in a separate HollowCraft world";
        private bool building=true, inventoryOpen, creative = false, wasConnected;
        private float nextMineSend, nextAttackRequest;
        private float mineProgress;
        private int selected;
        private int appliedTeleport;
        private float nextPose, miningStarted;
        private float nextTerrain;
        private string campaign="";
        private bool openingSync;
        private bool nativeTransition,sceneReentry,entryRecovery;
        private int sceneVisit;
        private float waterLandingAt=-1000;
        private int waterLandingSerial;
        private int waterLandingPhase,waterLandingHand;
        private MinecraftAudio audio;
        private readonly NativeParticleView particles=new NativeParticleView();
        private readonly NativeParticleView entityView=new NativeParticleView("entity_frames",0x48434554,25,true);
        private readonly NativeParticleView blockView=new NativeParticleView("block_frames",0x4843424c,5,true,525);
        private readonly Dictionary<Renderer,bool> nativeHiddenRenderers=new Dictionary<Renderer,bool>();
        private readonly Dictionary<GameObject,Renderer[]> nativeRendererCache=new Dictionary<GameObject,Renderer[]>();
        private float nextRendererCleanup;
        internal NativeGuiView Gui;
        internal NativeHudView Hud;
        internal int UiRequest;
        private int uiRequests;
        private int lastProjectileTick=-1;
        private float nextVisual;
        private Vector2Int mineCell = new Vector2Int(int.MinValue, int.MinValue);
        private Vector2 crosshair;
        private GameObject placementGhost;
        private bool wasMining;
        private bool usingItem;
        private float nextUseAim;
        private string ghostItem;
        private Vector2Int? queuedTower;
        private float queuedTowerUntil;
        private enum PlacementBlocker {None,Reach,Mode,Occupied,Terrain,Player,Enemy}
        private List<float[]> terrainCells=new List<float[]>();
        internal bool PlacementAllowed(Vector2Int cell,HeroController hero) => PlacementBlockedBy(cell,hero)==PlacementBlocker.None;
        private PlacementBlocker PlacementBlockedBy(Vector2Int cell,HeroController hero,bool foundation=false,float supportY=0) {
            if(!GameCursor.BlockInReach(cell,hero))return PlacementBlocker.Reach;
            if(Spectator||Mode=="adventure")return PlacementBlocker.Mode;
            if(blocks.ContainsKey(Key(cell.x,cell.y)))return PlacementBlocker.Occupied;
            bool floorFoundation=foundation&&supportY>=cell.y-.05f&&supportY<cell.y+1&&Math.Abs(hero.transform.position.x-cell.x-.5f)<=.85f;
            if(!floorFoundation&&terrainCells.Any(p=>p[0]==cell.x&&p[1]==cell.y&&(p.Length<6||p[5]>=.999f)))return PlacementBlocker.Terrain;
            var body=hero.GetComponent<Collider2D>();
            var bounds=new Bounds(new Vector3(cell.x+0.5f,cell.y+0.5f,hero.transform.position.z),new Vector3(0.98f,0.98f,1));
            if(body!=null && body.bounds.Intersects(bounds))return PlacementBlocker.Player;
            return Physics2D.OverlapBoxAll(bounds.center,new Vector2(.98f,.98f),0).Any(c=>!c.isTrigger && c.GetComponentInParent<HealthManager>()!=null)?PlacementBlocker.Enemy:PlacementBlocker.None;
        }
        private Vector2Int PlacementCell(Vector2Int pointed,HeroController hero) {
            if(JumpBuild!=null&&JumpBuild.Preview(hero,new Vector2(pointed.x+.5f,pointed.y+.5f),out Vector2Int fixedCell,out _,out _))return fixedCell;
            if(JumpPlacementCell(pointed,hero,out Vector2Int tower))return tower;
            // Clicking the native ground selects its free upper face, like clicking the top
            // face of a Minecraft terrain block. Empty cursor cells keep their exact target.
            if(blocks.ContainsKey(Key(pointed.x,pointed.y))||terrainCells.Any(p=>p[0]==pointed.x&&p[1]==pointed.y)) {
                // A partial native floor can be filled by a foundation block. Requiring
                // the next integer cell demanded up to TWO blocks of jump clearance.
                if(PlacementAllowed(pointed,hero))return pointed;
                var above=pointed+Vector2Int.up;
                if(Math.Abs(pointed.x+.5f-hero.transform.position.x)<.8f&&pointed.y+1<=hero.GetComponent<Collider2D>().bounds.min.y+.04f)
                    for(int step=0;step<5&&blocks.ContainsKey(Key(above.x,above.y));step++)above+=Vector2Int.up;
                if(PlacementAllowed(above,hero))return above;
            }
            return pointed;
        }
        private bool JumpPlacementCell(Vector2Int pointed,HeroController hero,out Vector2Int cell) {
            cell=pointed;
            if(MinecraftEnvironment.Flying||Spectator||MinecraftEnvironment.Gliding)return false;
            var body=hero.GetComponent<Collider2D>();if(body==null)return false;
            float? origin=MinecraftEnvironment.JumpOriginFeet;
            if(!origin.HasValue&&Input.GetKey(KeyCode.Space)&&MinecraftControls.Grounded(hero))origin=body.bounds.min.y;
            if(!origin.HasValue||Math.Abs(pointed.x+.5f-hero.transform.position.x)>.85f||pointed.y+.5f>body.bounds.min.y+.35f)return false;
            var candidate=new Vector2Int(Cell(hero.transform.position.x),Cell(origin.Value+.001f));
            // A full native voxel is the clicked support; partial floors can instead
            // receive a foundation in their own voxel, avoiding a two-block first jump.
            // Unity's contact skin can leave the feet just below a voxel edge. A
            // placed MC block is also a support, so resolve its upper face before
            // deciding whether this jump's new block can be queued.
            if(blocks.ContainsKey(Key(candidate.x,candidate.y))||terrainCells.Any(p=>p[0]==candidate.x&&p[1]==candidate.y&&(p.Length<6||p[5]>=.999f)))candidate+=Vector2Int.up;
            cell=candidate;
            return !blocks.ContainsKey(Key(cell.x,cell.y));
        }
        private void Preview(Vector2Int cell,HeroController hero) {
            var item=PlacementItem;
            bool visible=!inventoryOpen && !paused && !Dead && item!=null&&(bool?)Stats?["using"]!=true;
            if(!visible) {if(placementGhost!=null) placementGhost.SetActive(false);return;}
            cell=PlacementCell(cell,hero);
            if(placementGhost==null) {placementGhost=new GameObject("HollowCraft.PlacementPreview");DontDestroyOnLoad(placementGhost);placementGhost.AddComponent<SpriteRenderer>().sortingOrder=100;}
            var renderer=placementGhost.GetComponent<SpriteRenderer>();
            string id=(string)item["id"];
            if(ghostItem!=id) {ghostItem=id;var texture=Texture(ghostItem);renderer.sprite=texture!=null ? Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width) : Square(Color.white);}
            bool foundation=false;float support=0;
            if(JumpBuild!=null&&JumpBuild.Preview(hero,new Vector2(cell.x+.5f,cell.y+.5f),out Vector2Int target,out bool native,out float top)&&target==cell) {foundation=native;support=top;}
            renderer.color=PlacementBlockedBy(cell,hero,foundation,support)==PlacementBlocker.None ? new Color(1,1,1,.42f) : new Color(1,.25f,.25f,.42f);
            var original=hero.GetComponent<Renderer>();if(original!=null) renderer.sortingLayerID=original.sortingLayerID;
            placementGhost.transform.position=new Vector3(cell.x+.5f,cell.y+.5f,-.2f);placementGhost.SetActive(true);
        }
        private bool paused;
        private bool connectionFrozen;
        private bool frontendSuspended=true;
        private bool? backendPauseSent;
        private float nextHeldUse;
        private int itemSwingSerial,itemSwingHand;
        private float itemSwingAt=-100;
        private MinecraftUI ui;
        private bool BridgeIntro => (CampaignSession.IntroPending||openingSync)&&cachedManager!=null&&cachedManager.GetSceneNameString()=="Tutorial_01";
        internal bool OpeningTransition=>CampaignSession.IntroPending&&GameManager.instance?.GetSceneNameString()=="Tutorial_01";
        private bool NativeEntranceReady=>cachedHero!=null&&cachedHero.transitionState==GlobalEnums.HeroTransitionState.WAITING_TO_TRANSITION&&!nativeTransition;
        internal bool InGame => !frontendSuspended && cachedHero!=null && cachedHero.gameObject.activeInHierarchy && cachedManager!=null && cachedManager.IsGameplayScene() && ((!cachedManager.IsInSceneTransition&&NativeEntranceReady)||BridgeIntro) && cachedManager.GetSceneNameString()!="Opening_Sequence";
        internal bool Creative => creative;
        internal bool Connected=>bridge?.Connected==true;
        internal string Mode=>(string)Stats?["mode"]??"survival";
        internal bool Spectator=>Mode=="spectator";
        internal JObject LastLanding;
        internal int LandingEvents;
        internal readonly GameModeMenu Modes=new GameModeMenu();
        internal void ChangeMode(string mode) {StopMining();Combat.Cancel();CloseInterface();Send(new {type="mode",scene,mode});}
        internal bool InterfaceBlocked=>frontendSuspended||cachedManager?.IsInSceneTransition==true||SteveAvatar.LandingActive||Modes.Open||inventoryOpen||CatalogOpen||NativeShop.Visible||NativeChoice.Visible||!string.IsNullOrEmpty(CampaignBridge.DialogueText)||CampaignBridge.Sitting||paused||connectionFrozen||Dead;
        internal bool NativeControlLocked=>!BridgeIntro&&(cachedHero?.controlReqlinquished==true||!NativeEntranceReady||cachedManager?.IsInSceneTransition==true);
        internal bool GameplayBlocked=>InterfaceBlocked||NativeControlLocked;
        internal bool SimulationPaused=>frontendSuspended||paused||connectionFrozen||Dead;
        internal bool AudioPaused=>frontendSuspended||paused||connectionFrozen;
        internal bool CatalogOpen;
        internal JArray Catalog;
        internal JArray CatalogGroups;
        internal int SelectedSlot=>selected;
        internal bool Dead => (bool?)LatestSnapshot?["stats"]?["dead"] ?? false;
        internal string Room => scene;
        internal string CampaignId=>campaign;
        internal JObject Stats => LatestSnapshot?["stats"] as JObject;
        internal void Send(object command) => bridge.Send(command);
        private string assets;
        private HeroController cachedHero;
        private GameManager cachedManager;
        private float nextHeroLookup;
        private bool autoStarted;
        private float nextIntroSkip;
        private bool snapshotLogged;
        private int smokeStage;
        private float smokeDeadline;
        private Vector2Int smokeCell;
        private int smokeLogsBefore, smokePlanksBefore;
        private string diagnosticScene;
        private float stableSince;
        private bool Automated => Environment.GetEnvironmentVariable("HOLLOWCRAFT_SMOKE")=="1";
        private float nextDiagnostic;
        private ExtendedSmoke extendedSmoke;
        private PointerSmoke pointerSmoke;
        private AdventureSmoke adventureSmoke;
        private EnvironmentSmoke environmentSmoke;
        private CampaignSmoke campaignSmoke;
        private ShopSmoke shopSmoke;
        private ChoiceSmoke choiceSmoke;
        private ContainersSmoke containersSmoke;
        private BossSmoke bossSmoke;
        private ControlsSmoke controlsSmoke;
        internal JObject LatestSnapshot;
        internal void SmokeSend(object command) => bridge.Send(command);

        private void Awake()
        {
            assets = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "HollowCraftAssets");
            bridge = new BridgeClient();
            Instance=this; Combat=new CursorCombat(bridge.Send);JumpBuild=new JumpBuilding(this);
            audio=gameObject.AddComponent<MinecraftAudio>();audio.Initialize(this,assets);
            Application.runInBackground=true;
            ui=new MinecraftUI(this);
            Gui=new NativeGuiView(this);
            Hud=new NativeHudView(this);
        }
        private void OnDestroy()
        {
            pointerInput.Dispose();
            bridge?.Dispose(); ClearBlocks();
            particles.Dispose();
            entityView.Dispose();
            blockView.Dispose();
            Gui?.Dispose();
            Hud?.Dispose();
            MinecraftControls.Restore(); Instance=null;

            foreach (Texture2D texture in textures.Values) Destroy(texture);
        }
        private static string Key(int x, int y) => x + ":" + y;
        private bool Playable(out HeroController hero)
        {
            if (cachedHero == null && Time.unscaledTime >= nextHeroLookup)
            {
                nextHeroLookup = Time.unscaledTime + 1;
                cachedHero = FindFirstObjectByType<HeroController>();
            }
            if (cachedManager == null) cachedManager = FindFirstObjectByType<GameManager>();
            hero = cachedHero;
            return hero != null && hero.gameObject.activeInHierarchy && cachedManager != null
                && cachedManager.IsGameplayScene() && !cachedManager.isPaused && ((!cachedManager.IsInSceneTransition&&NativeEntranceReady)||BridgeIntro)
                && cachedManager.GetSceneNameString() != "Opening_Sequence"
                && hero.GetComponent<Rigidbody2D>() != null;
        }
        private void SyncBackendPause() {
            if(bridge?.Connected!=true) {backendPauseSent=null;return;}
            bool value=frontendSuspended||paused||connectionFrozen;
            if(backendPauseSent==value)return;
            bridge.Send(new {type="pause",paused=value});backendPauseSent=value;
        }
        internal void BeginNativeTransition() {
            if(nativeTransition)return;
            nativeTransition=true;sceneReentry=true;entryRecovery=false;
            // Invalidate this visit before draining any messages, including return to the same room.
            bridge.SceneVisit=++sceneVisit;
            StopMining();Combat.Cancel();JumpBuild.Cancel();queuedTower=null;
            if(usingItem)Send(new {type="itemUse",scene,cancel=true});usingItem=false;
            CloseInterface();ClearBlocks();frontendSuspended=true;SyncBackendPause();
            MinecraftEnvironment.SuspendNativeMotion();MinecraftControls.Restore();
            Debug.Log("[HollowCraft] Native scene transition begins; visit="+sceneVisit);
        }
        internal void NativeSceneArrived(HeroController hero) {
            nativeTransition=false;entryRecovery=true;
            cachedHero=hero;cachedManager=GameManager.instance;
            // The tutorial bridge is already observing the opening fall. Keep its
            // visit and pending landing-water reply when HK finishes that entrance.
            if(scene=="Tutorial_01"&&(openingSync||CampaignSession.IntroPending)&&scene==cachedManager?.GetSceneNameString()) {openingSync=false;return;}
            sceneReentry=true;
            MinecraftEnvironment.SceneChanged();
            Debug.Log("[HollowCraft] Native entrance complete "+cachedManager?.GetSceneNameString()+" gate="+hero.sceneEntryGate?.name+" position="+hero.transform.position);
        }
        private void Update()
        {
            Gui?.Refresh(assets);
            Hud?.Refresh(assets);
            pointerInput.Context(InGame&&!GameplayBlocked&&!Automated&&Application.isFocused);
            string activeScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeScene!=diagnosticScene)
            {
                diagnosticScene=activeScene;
                Debug.Log("[HollowCraft] Unity active scene: "+activeScene);
            }
            // Only the headless developer launcher opts into a fresh, isolated game save.
            if (!autoStarted && Environment.GetEnvironmentVariable("HOLLOWCRAFT_AUTOSTART") == "1"
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu_Title")
            {
                autoStarted = true;
                var manager = FindFirstObjectByType<GameManager>();
                if (manager != null)
                {
                    manager.profileID = 1;
                    Debug.Log("[HollowCraft] Starting isolated game scene for smoke test");
                    manager.StartNewGame(false,false);
                }
            }
            if (autoStarted && Time.unscaledTime>=nextIntroSkip && UnityEngine.SceneManagement.SceneManager.GetSceneByName("Opening_Sequence").isLoaded)
            {
                var opening = FindFirstObjectByType<OpeningSequence>();
                var ready = typeof(OpeningSequence).GetField("isLevelReady",BindingFlags.Instance|BindingFlags.NonPublic);
                if (opening != null && ready != null && (bool)ready.GetValue(opening))
                {
                    nextIntroSkip=Time.unscaledTime+2;
                    Debug.Log("[HollowCraft] Skipping cinematic in headless smoke test");
                    var skip=typeof(OpeningSequence).GetMethod("Skip",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
                    opening.StartCoroutine((System.Collections.IEnumerator)skip.Invoke(opening,null));
                }
            }
            bool connected = bridge.Connected;
            bool newlyConnected = connected && !wasConnected;
            if (!connected && wasConnected) {connectionFrozen=true;Time.timeScale=0;status = "Minecraft disconnected; reconnecting";}
            if(newlyConnected) {backendPauseSent=null;if(connectionFrozen) {connectionFrozen=false;Time.timeScale=paused?0:1;}}
            wasConnected = connected;
            while (bridge.TryRead(out JObject message))
            {
                if(message["campaign"]!=null&&(string)message["campaign"]!=campaign)continue;
                if(message["scene"]!=null&&((!BridgeIntro&&(nativeTransition||!NativeEntranceReady||cachedManager?.IsInSceneTransition==true))||(int?)message["visit"]!=sceneVisit))continue;
                if((string)message["type"]=="mcSound"&&(string)message["scene"]==scene)audio.Receive(message);
                if((string)message["type"]=="mcSoundStop"&&(string)message["scene"]==scene)audio.StopAll();
                if((string)message["type"]=="projectiles"&&(string)message["scene"]==scene)ApplyProjectiles(message);
                if((string)message["type"]=="primed"&&(string)message["scene"]==scene)ApplyPrimed(message);
                if((string)message["type"]=="nativeImpact"&&(string)message["scene"]==scene&&projectiles.TryGetValue((int)message["entity"],out GameObject impact)&&impact!=null)impact.GetComponent<ProjectileView>().Resolve(message);
                if((string)message["type"]=="projectile"&&(string)message["scene"]==scene) {
                    var value=(JObject)message["value"];
                    if((string)message["phase"]=="impact") {RetireProjectile((int)value["entity"],(string)value["uuid"]);continue;}
                    if(value["uuid"]!=null&&retiredProjectiles.ContainsKey((string)value["uuid"]))continue;
                    Projectile(value).Push(value,scene);
                }
                if((string)message["type"]=="catalog"&&(string)message["scene"]==scene) {Catalog=message["items"] as JArray;CatalogGroups=message["groups"] as JArray;CatalogRevision=(int?)message["revision"]??0;}
                if((string)message["type"]=="notice"&&(string)message["scene"]==scene)ui.ShowNotice((string)message["text"]);
                if((string)message["type"]=="impulse"&&(string)message["scene"]==scene&&cachedHero!=null) PlayerSystem.ApplyImpulse(cachedHero,new Vector2((float)message["x"],(float)message["y"]));
                if((string)message["type"]=="hurt"&&(string)message["scene"]==scene) PlayerSystem.HurtFeedback((int?)message["ticks"]??10);
                if((string)message["type"]=="landing"&&(string)message["scene"]==scene) {
                    LastLanding=message;LandingEvents++;
                    if((string)message["kind"]=="water") {waterLandingAt=Time.unscaledTime;waterLandingSerial++;waterLandingPhase=0;waterLandingHand=(int?)message["hand"]??0;}
                    if((bool?)message["intro"]==true) {IntroPresentation.Landed();CampaignSession.Landed();}
                    if((string)message["kind"]=="hero")SteveAvatar.BeginLanding();
                }
                if((string)message["type"]=="waterCollected"&&(string)message["scene"]==scene&&(bool?)message["recovered"]==true) {
                    waterLandingAt=Time.unscaledTime;waterLandingPhase=1;waterLandingHand=(int?)message["hand"]??0;
                }
                if((string)message["type"]=="itemSwing"&&(string)message["scene"]==scene) {
                    itemSwingSerial=(int?)message["serial"]??0;itemSwingHand=(int?)message["hand"]??0;itemSwingAt=Time.unscaledTime;
                }
                if((string)message["type"]=="guiClosed"&&(string)message["scene"]==scene&&((int?)message["request"]??0)==UiRequest&&((long?)message["gui"]??0)==Gui?.Session) {UiRequest=0;inventoryOpen=CatalogOpen=false;container=null;Gui.Reset();ui.CancelDrag();}
                if ((string)message["type"] == "snapshot" && (string)message["scene"] == scene && (message["campaign"]==null||(string)message["campaign"]==campaign)) ApplySnapshot(message);
                if ((string)message["type"] == "attack" && (string)message["scene"] == scene && building && !inventoryOpen) Combat.Begin(message,scene);
                if((string)message["type"]=="attackRejected"&&(string)message["scene"]==scene)Combat.Reject(message);
                if((string)message["type"]=="towerResult"&&(string)message["scene"]==scene)JumpBuild.Resolve(message);
                if((string)message["type"]=="placeResult"&&(string)message["scene"]==scene&&(bool?)message["accepted"]!=true) {
                    string rejected=Key((int)message["x"],(int)message["y"]);
                    if(pendingPlacements.Remove(rejected)&&blocks.TryGetValue(rejected,out GameObject block)) {block.SetActive(false);Destroy(block);blocks.Remove(rejected);}
                }
                if((string)message["type"]=="explosion" && (string)message["scene"]==scene) StartCoroutine(Explosion(new Vector2((float)message["x"],(float)message["y"])));
                if((string)message["type"]=="teleport" && (string)message["scene"]==scene && cachedHero!=null) {
                    int serial=(int?)message["teleport"]??-1;if(serial<=appliedTeleport)continue;
                    float x=(float)message["x"],y=(float)message["y"];
                    var body=cachedHero.GetComponent<Collider2D>();float feetOffset=cachedHero.transform.position.y-body.bounds.min.y;
                    if(float.IsNaN(x)||float.IsNaN(y)||Math.Abs(x)>400||y<-60||y>240) continue;
                    var landing=NativeLanding.Resolve(cachedHero,new Vector2(x,y),new Vector2((float?)message["normalX"]??0,(float?)message["normalY"]??1));
                    x=landing.x;y=landing.y;
                    appliedTeleport=serial;JumpBuild.Cancel();queuedTower=null;StopMining();Combat.Cancel();
                    cachedHero.transform.position=new Vector3(x,y+feetOffset,cachedHero.transform.position.z);cachedHero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                    MinecraftEnvironment.StopMotion();
                    bridge.Send(new {type="teleportAck",scene,x,y,teleport=serial});
                }
            }
            if (!Playable(out HeroController hero)) {
                if(!frontendSuspended) {StopMining();Combat.Cancel();if(usingItem) {Send(new {type="itemUse",scene,cancel=true});usingItem=false;}CloseInterface();}
                frontendSuspended=true;SyncBackendPause();MinecraftControls.Tick(null,false,false);return;
            }
            frontendSuspended=false;
            string name = cachedManager.GetSceneNameString();
            CampaignSession.Select(cachedManager);
            if (name != scene || newlyConnected || campaign!=CampaignSession.Id || sceneReentry)
            {
                sceneReentry=false;entryRecovery=true;bridge.SceneVisit=++sceneVisit;
                ClearBlocks(); appliedTeleport=0;scene = name; mineCell = new Vector2Int(int.MinValue, int.MinValue);
                container=null; inventoryOpen=false;UiRequest=0;Gui.Reset();waterLandingAt=-1000; Combat.Cancel(); PlayerSystem.EnterScene(hero);
                CatalogOpen=false;usingItem=false;MinecraftEnvironment.SceneChanged();
                snapshotLogged=false; stableSince=Time.unscaledTime;
                campaign=CampaignSession.Id;nextPose=0;nextTerrain=0;
                bridge.Campaign=campaign;LatestSnapshot=null;Combat.Profile=null;Catalog=CatalogGroups=null;creative=false;
                Array.Fill(inventory,null);Array.Clear(counts,0,counts.Length);selected=0;
                openingSync=CampaignSession.IntroPending&&scene=="Tutorial_01"&&cachedManager.IsInSceneTransition;
                Debug.Log("[HollowCraft] Room " + scene + " hero " + hero.transform.position);
                Array.Fill(crafting, -1);
                if (connected) bridge.Send(new { type = "scene", scene, campaign,legacy=CampaignSession.Legacy,intro=CampaignSession.IntroPending,x = Cell(hero.transform.position.x), y = Cell(hero.transform.position.y),exactX=hero.transform.position.x,exactY=hero.GetComponent<Collider2D>().bounds.min.y });
            }
            if (!Automated && Application.isFocused && Input.GetKeyDown(KeyCode.F7) && connected)
                bridge.Send(new { type = "mode", scene, mode = creative ? "survival" : "creative" });
            if(!Automated&&Application.isFocused&&connected) Modes.Tick(this,Input.GetKey(KeyCode.F3),Input.GetKeyDown(KeyCode.F4),Input.GetKeyDown(KeyCode.Escape));
            if (!Automated && Application.isFocused && building && !NativeControlLocked && !NativeShop.Visible && !NativeChoice.Visible && (Input.GetKeyDown(KeyCode.Tab)||Input.GetKeyDown(KeyCode.E)&&!ui.TextInputFocused&&Gui?.Active!=true)) {
                if(inventoryOpen||CatalogOpen)CloseInterface();
                else if(creative)OpenCatalog();
                else {inventoryOpen=true;UiRequest=++uiRequests;bridge.Send(new {type="inventoryOpen",scene,uiRequest=UiRequest});}
            }
            if(!Automated && Application.isFocused && Input.GetKeyDown(KeyCode.Escape)&&!Modes.ConsumedEscape) {if(NativeShop.Visible) NativeShop.Close();else if(NativeChoice.Visible) NativeChoice.Decide(false);else if(CatalogOpen) CloseInterface();else if(inventoryOpen) CloseInterface();else {paused=!paused;Time.timeScale=paused||connectionFrozen ? 0 : 1;}}
            SyncBackendPause();
            if(!Automated&&Application.isFocused&&!NativeShop.Visible&&Input.GetKeyDown(KeyCode.F11)&&creative) {if(CatalogOpen)CloseInterface();else OpenCatalog();}
            if(!Automated && Application.isFocused && !GameplayBlocked && Input.GetKeyDown(KeyCode.F)) SwapHands();
            if(!Automated && Application.isFocused && !GameplayBlocked && Input.GetKeyDown(KeyCode.Q)) bridge.Send(new {type="drop",scene,all=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)});
            PlayerSystem.Tick(hero);
            CampaignBridge.Tick();
            NativeShop.Tick();
            NativeChoice.Tick();
            if(!Automated && Application.isFocused && building && Input.GetKeyDown(KeyCode.F9) && creative) bridge.Send(new {type="kit",scene});
            if(!Automated && Application.isFocused && building && Input.GetKeyDown(KeyCode.F10) && creative) bridge.Send(new {type="adventureKit",scene});
            // Native story FSMs still need key-release events while they own the hero.
            MinecraftControls.Tick(hero,building,InterfaceBlocked||Automated);
            if(entryRecovery&&LatestSnapshot!=null&&!NativeControlLocked) {entryRecovery=false;NativeLanding.SettleSceneEntry(hero);}
            GameCursor.WorldTarget(hero,out _,out crosshair);
            if(connected&&Time.unscaledTime>=nextVisual) {
                nextVisual=Time.unscaledTime+.033f;
                var body=hero.GetComponent<Rigidbody2D>();
                var camera=GameCursor.GameCamera();
                var pointer=GameCursor.ReadScreenPosition();float guiScale=Mathf.Max(1,Mathf.Min(Mathf.Clamp(Mathf.Floor(Screen.height/240f),2,4),Mathf.Floor(Screen.width/320f),Mathf.Floor(Screen.height/240f)));
                float viewYaw=MinecraftControls.FrontView?0:-MinecraftControls.Facing*75;
                if(Combat.Swinging)viewYaw=-Mathf.Sign(Combat.Aim.x)*75;
                bridge.Send(new {type="visual",scene,viewYaw,speed=body.linearVelocity.x,vy=body.linearVelocity.y,
                    viewX=camera!=null?camera.transform.position.x:hero.transform.position.x,viewY=camera!=null?camera.transform.position.y:hero.transform.position.y,
                    viewWidth=camera!=null?camera.orthographicSize*2*camera.aspect:36,viewHeight=camera!=null?camera.orthographicSize*2:24,
                    guiWidth=Mathf.FloorToInt(Screen.width/guiScale),guiHeight=Mathf.FloorToInt(Screen.height/guiScale),guiMouseX=pointer.x/Screen.width,guiMouseY=(Screen.height-pointer.y)/Screen.height,
                    guiMods=NativeGuiView.Modifiers,guiC=Input.GetKey(KeyCode.C),guiX=Input.GetKey(KeyCode.X),
                    crouching=MinecraftControls.Crouching,sprinting=MinecraftControls.Sprinting,grounded=MinecraftControls.Grounded(hero),gliding=MinecraftEnvironment.Gliding,flying=MinecraftEnvironment.Flying,
                    pitch=usingItem&&GameCursor.WorldTarget(hero,out Vector2 aimPoint,out _)?-Mathf.Atan2(aimPoint.y-GameCursor.Eye(hero).y,Mathf.Abs(aimPoint.x-GameCursor.Eye(hero).x))*Mathf.Rad2Deg:0,
                    swinging=Combat.Swinging,attackSerial=Combat.AnimationSerial,animation=(string)Combat.Current?["move"]?["animation"]??"",
                    duration=(float?)Combat.Current?["duration"]??.5f,upswing=(float?)Combat.Current?["upswing"]??.3f,
                    twoHanded=(bool?)Combat.Profile?["weapon"]?["two_handed"]??false,landing=SteveAvatar.LandingElapsed,
                    itemSwing=Time.unscaledTime-itemSwingAt<.6f,itemSwingSerial,itemSwingHand,
                    waterLanding=Time.unscaledTime-waterLandingAt<.6f?Time.unscaledTime-waterLandingAt:-1,waterLandingSerial,waterLandingPhase,waterLandingHand});
            }
            if (connected && Time.unscaledTime >= nextPose)
            {
                nextPose = Time.unscaledTime + .05f;
                SendPose(hero,Time.unscaledTime>=nextTerrain);
            }
            if(building && connected && !GameplayBlocked) Combat.Tick(hero); else Combat.Cancel();
            if (Environment.GetEnvironmentVariable("HOLLOWCRAFT_SMOKE") == "1") SmokeTick(hero);
            if(Automated) return;
            bool hotbarShortcut=HandleHotbarShortcut();
            // Native landing/knockback animation locks must not swallow MC mouse presses.
            pointerInput.Context(building&&connected&&!GameplayBlocked&&Application.isFocused);
            if (!building || !connected || GameplayBlocked) {queuedTower=null;JumpBuild.Cancel();return;}
            if(Spectator) {StopMining();return;}
            if(!hotbarShortcut)for (int i = 0; i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
            float scroll = Input.mouseScrollDelta.y;
            if (Math.Abs(scroll) > 0.01f) Select((selected + (scroll > 0 ? 8 : 1)) % 9);
            if(!GameCursor.WorldTarget(hero,out Vector2 mouse,out crosshair)) return;
            if(!Application.isFocused) {queuedTower=null;JumpBuild.Cancel();StopMining();return;}
            bool nativeLeft=false,nativeRight=false;
            bool leftHeld=Input.GetMouseButton(0)||pointerInput.Held(0),rightHeld=Input.GetMouseButton(1)||pointerInput.Held(1);
            while(pointerInput.Read(out NativePointerBuffer.Press press,out Vector2 screen)) {
                if(!GameCursor.WorldTarget(hero,screen,out Vector2 pressed,out _))continue;
                nativeLeft|=press.Button==0;nativeRight|=press.Button==1;
                HandlePointer(hero,pressed,press.Button==0,false,press.Button==1,rightHeld);
            }
            HandlePointer(hero,mouse,!nativeLeft&&Input.GetMouseButtonDown(0),leftHeld,!nativeRight&&Input.GetMouseButtonDown(1),rightHeld);
            JumpBuild.Tick(hero);
        }
        internal void HandlePointer(HeroController hero,Vector2 mouse,bool leftDown,bool leftHeld,bool rightDown,bool rightHeld) {
            if(rightDown)Combat.Cancel();
            if(rightHeld) {MinecraftControls.Facing=mouse.x<hero.transform.position.x?-1:1;MinecraftControls.frontView=false;}
            Vector2Int cell = new Vector2Int(Cell(mouse.x), Cell(mouse.y));
            bool towerInput=(rightDown||rightHeld)&&JumpBuild?.Pointer(hero,mouse)==true;
            if(towerInput) {
                queuedTower=null;
                if(usingItem) {SendItemUse(hero,mouse,false,true);usingItem=false;}
            }
            Preview(cell,hero);
            if(queuedTower.HasValue&&(Time.unscaledTime>queuedTowerUntil||Math.Abs(queuedTower.Value.x+.5f-hero.transform.position.x)>1||PlacementItem==null))queuedTower=null;
            bool buffered=queuedTower.HasValue;
            if(!towerInput&&(rightDown||rightHeld&&Time.unscaledTime>=nextHeldUse||buffered)) {
                var placement=buffered?queuedTower.Value:PlacementCell(cell,hero);
                var blockedBy=PlacementBlockedBy(placement,hero);
                bool canPlace=blockedBy==PlacementBlocker.None;
                var item=PlacementItem;
                bool tower=buffered||JumpPlacementCell(cell,hero,out _);
                if(blockedBy==PlacementBlocker.Player&&tower&&item!=null) {
                    if(!buffered) {
                        queuedTower=placement;queuedTowerUntil=Time.unscaledTime+.8f;
                        Debug.Log("[HollowCraft] Jump placement buffered cell="+placement+" feet="+hero.GetComponent<Collider2D>().bounds.min.y+" mode="+Mode);
                    }
                    // Recheck clearance each frame while airborne, instead of spending
                    // the 4-tick use delay before the feet clear the new block's top.
                    nextHeldUse=Time.unscaledTime+.02f;
                } else {
                    queuedTower=null;nextHeldUse=Time.unscaledTime+.2f;
                    // Only predict an unambiguous block action; vanilla chooses the actual hand.
                    var main=LatestSnapshot?["inventory"]?[selected] as JObject;
                    bool predict=(bool?)main?["block"]==true||(int?)main?["count"]==0;
                    if(canPlace&&item!=null)SendPose(hero,true);else SendPose(hero,false);
                    if(canPlace&&predict&&item!=null&&(!blocks.TryGetValue(Key(cell.x,cell.y),out GameObject pointed)||MinecraftControls.Crouching||!Interactive(pointed.name)))PredictPlacement(placement,item);
                    var eye=GameCursor.Eye(hero);
                    if(tower)Debug.Log("[HollowCraft] Jump placement sent cell="+placement+" canPlace="+canPlace+" blocker="+blockedBy+" feet="+hero.GetComponent<Collider2D>().bounds.min.y);
                    Send(new {type="interact",scene,x=cell.x,y=cell.y,placeX=placement.x,placeY=placement.y,canPlace,
                        playerX=hero.transform.position.x,playerY=hero.GetComponent<Collider2D>().bounds.min.y,aimX=mouse.x-eye.x,aimY=mouse.y-eye.y});
                    usingItem=true;
                }
            }
            if(usingItem && !rightHeld) {SendItemUse(hero,mouse,false,true);usingItem=false;}
            else if(usingItem && Time.unscaledTime>=nextUseAim) {nextUseAim=Time.unscaledTime+.05f;SendItemUse(hero,mouse,false,false);}
            // A short click always swings, including clicks aimed at a placed block. Holding also mines.
            if(leftDown) Combat.Request(scene,hero,mouse,true);
            if (leftHeld)
            {
                bool enemy=Combat.HasEnemy(hero,mouse);
                if(blocks.ContainsKey(Key(cell.x,cell.y))&&!GameCursor.BlockInReach(cell,hero)) {StopMining();return;}
                if(enemy || !blocks.ContainsKey(Key(cell.x,cell.y)))
                {
                    StopMining();mineCell=new Vector2Int(int.MinValue,int.MinValue);mineProgress=0;
                    if(Time.unscaledTime>=nextAttackRequest) {Combat.Request(scene,hero,mouse);nextAttackRequest=Time.unscaledTime+0.1f;}
                    return;
                }
                if (cell != mineCell) { mineCell = cell; miningStarted = Time.unscaledTime; }
                int held = 1+(int)((Time.unscaledTime - miningStarted) * 20);
                int needed=breakingTicks.TryGetValue(Key(cell.x,cell.y),out int required) ? required : 20;
                mineProgress=creative ? 1 : Mathf.Clamp01((float)held/Math.Max(1,needed));
                if(Time.unscaledTime>=nextMineSend)
                {
                    bridge.Send(new { type = "mining", scene, x = cell.x, y = cell.y, active=true });wasMining=true;
                    nextMineSend=Time.unscaledTime+0.05f;
                }
            }
            else {StopMining();mineCell = new Vector2Int(int.MinValue, int.MinValue);mineProgress=0;}
        }
        private void PredictPlacement(Vector2Int cell,JObject item) {
            if((bool?)item?["predictSolid"]!=true)return;
            string id=(string)item["id"];
            string key=Key(cell.x,cell.y);if(blocks.ContainsKey(key))return;
            var block=new GameObject("HollowCraft.Block."+id);DontDestroyOnLoad(block);
            block.transform.position=new Vector3(cell.x+.5f,cell.y+.5f,.1f);block.layer=Math.Max(0,LayerMask.NameToLayer("Terrain"));
            block.AddComponent<BoxCollider2D>().size=Vector2.one;
            var renderer=block.AddComponent<SpriteRenderer>();var texture=Texture(id);
            renderer.sprite=texture!=null?Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width):Square(Color.white);
            blocks[key]=block;pendingPlacements[key]=Time.unscaledTime+.75f;
        }
        internal bool TowerPlacementAllowed(Vector2Int cell,HeroController hero,bool foundation,float support)=>Math.Abs(hero.transform.position.x-cell.x-.5f)<=.85f&&PlacementBlockedBy(cell,hero,foundation,support)==PlacementBlocker.None;
        internal void PrepareTower(HeroController hero,Vector2Int cell,JObject item) {Combat.Cancel();SendPose(hero,true);PredictPlacement(cell,item);}
        internal void RejectTower(Vector2Int cell) {
            string key=Key(cell.x,cell.y);
            if(pendingPlacements.Remove(key)&&blocks.TryGetValue(key,out GameObject block)) {if(block!=null) {block.SetActive(false);Destroy(block);}blocks.Remove(key);}
        }
        internal void SwapHands() {
            JumpBuild.Cancel();queuedTower=null;StopMining();Combat.Cancel();usingItem=false;
            Send(new {type="itemUse",scene,cancel=true});Send(new {type="offhand",scene});
        }
        private void SendItemUse(HeroController hero,Vector2 target,bool start,bool release) {
            float feet=hero.GetComponent<Collider2D>().bounds.min.y;
            Send(new {type="itemUse",scene,start,release,x=Cell(target.x),y=Cell(target.y),aimX=target.x-hero.transform.position.x,aimY=target.y-feet-((float?)Stats?["eyeHeight"] ?? 1.62f)});
        }
        private void SendPose(HeroController hero,bool sample) {
            int x=Cell(hero.transform.position.x),y=Cell(hero.transform.position.y);
            var pose=new JObject { ["type"]="pose",["scene"]=scene,["x"]=x,["y"]=y,
                ["teleport"]=appliedTeleport,
                ["exactX"]=hero.transform.position.x,["exactY"]=hero.GetComponent<Collider2D>().bounds.min.y,
                ["grounded"]=MinecraftControls.Grounded(hero),["gliding"]=MinecraftEnvironment.Gliding,["flying"]=MinecraftEnvironment.Flying,
                ["sprinting"]=MinecraftControls.Sprinting,["crouching"]=MinecraftControls.Crouching,["scripted"]=NativeControlLocked };
            if(sample) {nextTerrain=Time.unscaledTime+.15f;terrainCells=ProbeTerrain(x,y);pose["terrain"]=JArray.FromObject(terrainCells);pose["enemies"]=JArray.FromObject(EnemySystem.Observe(hero));}
            bridge.Send(pose);
        }
        private void StopMining() {if(wasMining) bridge.Send(new {type="mining",scene,active=false});wasMining=false;}
        private void LateUpdate() {
            FlushNativeSteps();GameCursor.ApplyCursor();
            bool visible=InGame&&bridge?.Connected==true&&building&&!Dead;
            particles.Draw(assets,this,cachedHero,visible);entityView.Draw(assets,this,cachedHero,visible);blockView.Draw(assets,this,cachedHero,visible);
            bool hideWorld=!InGame;
            foreach(var block in blocks.Values) {
                int x=Mathf.FloorToInt(block.transform.position.x),y=Mathf.FloorToInt(block.transform.position.y);
                NativeVisual(block,hideWorld||blockView.Covers((x&65535)<<16|(y&65535)));
            }
            foreach(var pair in drops)NativeVisual(pair.Value,hideWorld||entityView.Covers(pair.Key));
            foreach(var pair in primedTnt)NativeVisual(pair.Value,hideWorld||entityView.Covers(pair.Key));
            foreach(var pair in projectiles)NativeVisual(pair.Value,hideWorld||entityView.Covers(pair.Key));
            if(Time.unscaledTime>=nextRendererCleanup) {
                nextRendererCleanup=Time.unscaledTime+1;
                foreach(var renderer in nativeHiddenRenderers.Keys.Where(key=>key==null).ToArray())nativeHiddenRenderers.Remove(renderer);
                foreach(var item in nativeRendererCache.Keys.Where(key=>key==null).ToArray())nativeRendererCache.Remove(item);
            }
            if(placementGhost!=null && (GameplayBlocked||!building))placementGhost.SetActive(false);
            if(GameplayBlocked||(!Automated&&!Application.isFocused)) {StopMining();if(usingItem) {Send(new {type="itemUse",scene,cancel=true});usingItem=false;}}
        }
        private void NativeVisual(GameObject item,bool hide) {
            if(item==null)return;
            if(!nativeRendererCache.TryGetValue(item,out Renderer[] renderers)) {renderers=item.GetComponentsInChildren<Renderer>(true);nativeRendererCache[item]=renderers;}
            foreach(var renderer in renderers) {
                if(renderer==null)continue;
                if(hide) {if(!nativeHiddenRenderers.ContainsKey(renderer))nativeHiddenRenderers[renderer]=renderer.enabled;renderer.enabled=false;}
                else if(nativeHiddenRenderers.TryGetValue(renderer,out bool previous)) {renderer.enabled=previous;nativeHiddenRenderers.Remove(renderer);}
            }
        }
        internal void CloseInterface() {JumpBuild?.Cancel();queuedTower=null;ui.CancelDrag();UiRequest=0;Gui?.Reset();inventoryOpen=false;CatalogOpen=false;container=null;bridge.Send(new {type="close",scene});}
        private void OpenCatalog() {StopMining();Combat.Cancel();CloseInterface();CatalogOpen=true;UiRequest=++uiRequests;Send(new {type="creativeCatalog",scene,uiRequest=UiRequest});}
        private bool HandleHotbarShortcut() {
            if(!creative||!bridge.Connected||!Application.isFocused||ui.TextInputFocused||Gui?.Active==true||GameplayBlocked&&!CatalogOpen)return false;
            bool load=Input.GetKey(KeyCode.X),save=Input.GetKey(KeyCode.C);if(!load&&!save)return false;
            for(int i=0;i<9;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i)) {StopMining();Combat.Cancel();Send(new {type="creativeHotbar",scene,action=load?"load":"save",slot=i});return true;}
            return false;
        }
        internal int CatalogRevision;
        private static bool Interactive(string name) => name.Contains("crafting_table") || name.Contains("chest") || name.Contains("furnace") || name.Contains("barrel") || name.Contains("shulker_box") || name.Contains("door") || name.Contains("button") || name.Contains("lever") || name.Contains("tnt") || name.Contains("brewing_stand") || name.Contains("anvil") || name.Contains("enchanting_table") || name.Contains("smoker") || name.Contains("hopper") || name.Contains("dropper") || name.Contains("dispenser") || name.Contains("grindstone") || name.Contains("loom") || name.Contains("stonecutter") || name.Contains("smithing_table") || name.Contains("cartography_table");
        private static int Cell(float value) => Mathf.FloorToInt(value / CellSize);
        internal void Select(int value) {JumpBuild.Cancel();queuedTower=null;selected = value;mineCell=new Vector2Int(int.MinValue,int.MinValue);StopMining();bridge.Send(new { type = "select", scene, slot = selected }); }
        private List<float[]> ProbeTerrain(int cx, int cy)
        {
            var result = new List<float[]>();
            int mask = LayerMask.GetMask("Terrain","Default");
            if (mask == 0) return result;
            for (int x = cx - 8; x <= cx + 8; x++) for (int y = cy - 6; y <= cy + 6; y++)
            {
                var filter=new ContactFilter2D();filter.SetLayerMask(mask);filter.useTriggers=false;
                int found=Physics2D.OverlapBox(new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize),
                    // Native platforms can be much thinner than a block and sit at cell edges.
                    // Sampling only the middle half missed their floors, letting server items
                    // fall through platforms that still supported the native player.
                    new Vector2(CellSize * 0.9999f, CellSize * 0.9999f), 0, filter,terrainProbe);
                while(found==terrainProbe.Length&&terrainProbe.Length<256) {Array.Resize(ref terrainProbe,terrainProbe.Length*2);found=Physics2D.OverlapBox(new Vector2(x+.5f,y+.5f),new Vector2(.9999f,.9999f),0,filter,terrainProbe);}
                for(int index=0;index<found;index++) {
                    var c=terrainProbe[index];if(c==null||c.name.StartsWith("HollowCraft.Block")||c.GetComponentInParent<HeroController>()!=null||c.GetComponentInParent<HealthManager>()!=null)continue;
                    var bounds=c.bounds;float x0=Mathf.Max(x,bounds.min.x),x1=Mathf.Min(x+1,bounds.max.x),y0=Mathf.Max(y,bounds.min.y),y1=Mathf.Min(y+1,bounds.max.y);
                    var center=new Vector2(x+.5f,y+.5f);var closest=c.ClosestPoint(center);
                    // Polygon terrain bounds can span a whole room. Use the nearby surface
                    // when the cell center is outside that polygon, preserving thin ledges.
                    if((closest-center).sqrMagnitude>.000001f) {
                        if(closest.y<center.y-.001f)y1=Math.Min(y1,closest.y);
                        if(closest.y>center.y+.001f)y0=Math.Max(y0,closest.y);
                        if(closest.x<center.x-.001f)x1=Math.Min(x1,closest.x);
                        if(closest.x>center.x+.001f)x0=Math.Max(x0,closest.x);
                    }
                    if(x1>x0+.00001f&&y1>y0+.00001f) result.Add(new[]{(float)x,(float)y,x0-x,y0-y,x1-x,y1-y});
                }
            }
            return result;
        }
        private ProjectileView Projectile(JObject value) {
            int id=(int)value["entity"];
            if(!projectiles.TryGetValue(id,out GameObject go)||go==null) {
                go=new GameObject("HollowCraft.Projectile."+(string)value["kind"]);DontDestroyOnLoad(go);
                int layer=cachedHero?.GetComponent<Renderer>()?.sortingLayerID??0;
                bool arrow=((string)value["kind"])?.Contains("arrow")==true;
                bool model=arrow?MinecraftMeshes.Arrow(go.transform,assets,layer,25):MinecraftMeshes.Item(go.transform,(string)value["item"],assets,layer,25);
                if(!model) {
                    var texture=ItemTexture((string)value["item"]);var sprite=go.AddComponent<SpriteRenderer>();
                    if(texture!=null)sprite.sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width);
                    sprite.sortingLayerID=layer;sprite.sortingOrder=25;
                }
                go.AddComponent<ProjectileView>();projectiles[id]=go;
            }
            return go.GetComponent<ProjectileView>();
        }
        private void ApplyProjectiles(JObject message) {
            int tick=(int?)message["tick"]??0;if(tick<=lastProjectileTick)return;lastProjectileTick=tick;
            foreach(string uuid in retiredProjectiles.Keys.Where(key=>retiredProjectiles[key]<=Time.unscaledTime).ToArray())retiredProjectiles.Remove(uuid);
            var ids=new HashSet<int>();
            foreach(JObject value in message["projectiles"] as JArray??new JArray()) {
                if(value["uuid"]!=null&&retiredProjectiles.ContainsKey((string)value["uuid"]))continue;
                ids.Add((int)value["entity"]);Projectile(value).Push(value,scene);
            }
            foreach(int id in projectiles.Keys.Where(id=>!ids.Contains(id)).ToArray()) {
                if(projectiles[id]!=null)projectiles[id].GetComponent<ProjectileView>().Retire();projectiles.Remove(id);
            }
        }
        internal void QueueNativeStep(int entity,string uuid,int age,string room) {
            if(room==scene&&!SimulationPaused)nativeProjectileSteps[entity]=new {entity,uuid,age};
        }
        internal void RevokeNativeStep(int entity) {nativeProjectileSteps.Remove(entity);}
        internal void QueueTntStep(object command,int entity,string room) {if(room==scene&&!SimulationPaused)nativeTntSteps[entity]=command;}
        internal void RetireProjectile(int entity,string uuid) {
            if(!string.IsNullOrEmpty(uuid))retiredProjectiles[uuid]=Time.unscaledTime+2;
            RevokeNativeStep(entity);
            if(projectiles.TryGetValue(entity,out GameObject go)&&go!=null)go.GetComponent<ProjectileView>().Retire();
            projectiles.Remove(entity);
        }
        private void FlushNativeSteps() {
            if(!SimulationPaused) {
                var values=nativeProjectileSteps.Values.ToArray();
                for(int offset=0;offset<values.Length;offset+=64)Send(new {type="projectileStep",scene,steps=values.Skip(offset).Take(64).ToArray()});
                var tnt=nativeTntSteps.Values.ToArray();
                for(int offset=0;offset<tnt.Length;offset+=64)Send(new {type="tntStep",scene,steps=tnt.Skip(offset).Take(64).ToArray()});
            }
            nativeProjectileSteps.Clear();
            nativeTntSteps.Clear();
        }
        private void ApplyPrimed(JObject message) {
            int tick=(int?)message["tick"]??0;if(tick<lastTntTick)return;lastTntTick=tick;
            var tntIds=new HashSet<int>();
            foreach(JObject value in message["primed"] as JArray ?? new JArray()) {
                int id=(int)value["id"];tntIds.Add(id);
                if(!primedTnt.TryGetValue(id,out GameObject tnt)) {
                    tnt=new GameObject("HollowCraft.PrimedTNT");DontDestroyOnLoad(tnt);var renderer=tnt.AddComponent<SpriteRenderer>();var texture=Texture("minecraft:tnt");
                    renderer.sprite=texture!=null ? Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width) : Square(Color.red);
                    var original=cachedHero?.GetComponent<Renderer>();if(original!=null) renderer.sortingLayerID=original.sortingLayerID;renderer.sortingOrder=30;tnt.AddComponent<PrimedTntView>();primedTnt[id]=tnt;
                }
                tnt.GetComponent<PrimedTntView>().Push(value,scene);
            }
            foreach(int id in primedTnt.Keys.Where(id=>!tntIds.Contains(id)).ToArray()) {Destroy(primedTnt[id]);primedTnt.Remove(id);}
        }
        private void ApplySnapshot(JObject message)
        {
            LatestSnapshot=message;
            EnemySystem.Apply(message["enemies"] as JArray,cachedHero);
            ApplyProjectiles(message);ApplyPrimed(message);
            if (!snapshotLogged)
            {
                snapshotLogged=true;
                Debug.Log("[HollowCraft] Received real Minecraft snapshot for " + scene);
            }
            status = (string)message["status"] ?? "Connected";
            creative = (bool?)message["creative"] ?? false;
            selected = (int?)message["selected"] ?? selected;
            Combat.Profile=message["combat"] as JObject;
            var nextContainer=message["container"] as JObject;
            if((long?)message["gui"]?["session"]>0&&(bool?)message["gui"]?["inventory"]==false)inventoryOpen=true;
            if(nextContainer!=null && container==null) inventoryOpen=true;
            container=nextContainer;
            var entries = (JArray)message["inventory"];
            for (int i = 0; i < Math.Min(36, entries.Count); i++)
            {
                inventory[i] = (string)entries[i]["id"]; counts[i] = (int)entries[i]["count"];
            }
            var dropIds=new HashSet<int>();
            foreach(JObject entry in message["drops"] as JArray ?? new JArray()) {
                int id=(int)entry["entity"];dropIds.Add(id);
                if(!drops.TryGetValue(id,out GameObject item)) {
                    item=new GameObject("HollowCraft.Drop");DontDestroyOnLoad(item);var original=cachedHero?.GetComponent<Renderer>();int layer=original?.sortingLayerID??0;
                    string render=(string)entry["render"]??(string)entry["id"];
                    if(!MinecraftMeshes.Item(item.transform,render,assets,layer,20)||item.GetComponentsInChildren<MeshRenderer>().Length==0) {
                        item.SetActive(true);var renderer=item.AddComponent<SpriteRenderer>();var texture=ItemTexture(render);if(texture!=null) renderer.sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width/.4f);renderer.sortingLayerID=layer;renderer.sortingOrder=20;
                    } else item.transform.localScale=Vector3.one*.35f;
                    drops[id]=item;
                    int dropLayer=LayerMask.NameToLayer("Debris");item.layer=dropLayer>=0 ? dropLayer : 0;
                }
                item.transform.position=new Vector3((float)entry["x"],(float)entry["y"]+.2f,-.1f);
                if(item.GetComponent<SpriteRenderer>()==null) item.transform.localRotation=Quaternion.Euler(0,Time.time*40+id%360,0);
                foreach(var renderer in item.GetComponentsInChildren<MeshRenderer>()) MinecraftMeshes.SortFaces(renderer);
            }
            foreach(int id in drops.Keys.Where(id=>!dropIds.Contains(id)).ToArray()) {Destroy(drops[id]);drops.Remove(id);}
            var present = new HashSet<string>();
            foreach (JObject cell in (JArray)message["blocks"])
            {
                int x = (int)cell["x"], y = (int)cell["y"];
                string key = Key(x,y); present.Add(key);
                pendingPlacements.Remove(key);
                string id = (string)cell["id"];
                breakingTicks[key] = (int?)cell["breakTicks"] ?? 20;
                string state=((string)cell["state"] ?? id)+cell["collision"]?.ToString(Newtonsoft.Json.Formatting.None);
                if (blocks.TryGetValue(key, out GameObject existing) && blockStates.TryGetValue(key,out string priorState) && priorState==state) continue;
                if (existing != null) { existing.SetActive(false); Destroy(existing); }
                var block = new GameObject("HollowCraft.Block." + id);
                DontDestroyOnLoad(block);
                block.transform.position = new Vector3((x+0.5f)*CellSize, (y+0.5f)*CellSize, 0.1f);
                var renderer = block.AddComponent<SpriteRenderer>();
                Texture2D texture = Texture(id);
                if((bool?)cell["lit"]==true) texture=Texture(id+"_on") ?? texture;
                if(id.EndsWith("_door")&&!id.EndsWith("_trapdoor")) texture=Texture(id+(state.Contains("half=upper") ? "_top":"_bottom")) ?? texture;
                if(id=="minecraft:redstone_wire") texture=Texture("minecraft:redstone_dust_line0") ?? texture;
                if(id=="minecraft:moving_piston") texture=Texture("minecraft:piston_top") ?? texture;
                renderer.sprite = texture != null ? Sprite.Create(texture, new Rect(0,0,texture.width,texture.height),
                    new Vector2(0.5f,0.5f), texture.width / CellSize) : Square(new Color(0.65f,0.5f,0.8f));
                if(id=="minecraft:water"||id=="minecraft:lava"||id=="minecraft:fire") MinecraftSpriteAnimation.Attach(renderer,id=="minecraft:fire"?"fire_0":id.Substring(10)+"_still");
                int terrain = LayerMask.NameToLayer("Terrain");
                block.layer = terrain >= 0 ? terrain : 0;
                if((bool?)cell["fluid"]==true) {
                    float height=(float?)cell["height"] ?? 1;block.transform.localScale=new Vector3(1,height,1);block.transform.position+=Vector3.down*(1-height)*.5f;
                    if(id=="minecraft:water") renderer.color=new Color(.25f,.5f,1,.7f);
                }
                if(id=="minecraft:redstone_wire") {block.transform.localScale=new Vector3(1,.12f,1);block.transform.position+=Vector3.down*.4f;renderer.color=Color.Lerp(new Color(.25f,0,0),Color.red,((float?)cell["power"] ?? 0)/15f);}
                if ((bool?)cell["solid"] == true) {
                    var collision=cell["collision"] as JArray;
                    if(collision!=null) foreach(JArray box in collision) {
                        var nativeBox=block.AddComponent<BoxCollider2D>();nativeBox.size=new Vector2((float)box[2]-(float)box[0],(float)box[3]-(float)box[1]);nativeBox.offset=new Vector2(((float)box[0]+(float)box[2])*.5f-.5f,((float)box[1]+(float)box[3])*.5f-.5f);
                    } else block.AddComponent<BoxCollider2D>().size = new Vector2(CellSize,CellSize);
                }
                blocks[key] = block;blockStates[key]=state;
            }
            foreach (string key in blocks.Keys.Where(k => !present.Contains(k)).ToArray())
            {
                if(pendingPlacements.TryGetValue(key,out float deadline)&&Time.unscaledTime<deadline)continue;
                pendingPlacements.Remove(key);
                blocks[key].SetActive(false); Destroy(blocks[key]); blocks.Remove(key); blockStates.Remove(key);breakingTicks.Remove(key);
            }
        }
        private System.Collections.IEnumerator Explosion(Vector2 point) {
            Debug.Log("[HollowCraft] TNT exploded at "+point);
            var particles=new List<GameObject>();
            for(int i=0;i<12;i++) {var go=new GameObject("HollowCraft.Explosion");var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=Square(new Color(1,.75f,.3f));var original=cachedHero?.GetComponent<Renderer>();if(original!=null) renderer.sortingLayerID=original.sortingLayerID;renderer.sortingOrder=200;go.transform.position=point;particles.Add(go);}
            float start=Time.time;
            while(Time.time-start<.45f) {float t=(Time.time-start)/.45f;for(int i=0;i<particles.Count;i++) {float angle=i*Mathf.PI*2/particles.Count;particles[i].transform.position=point+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*t*3;particles[i].transform.localScale=Vector3.one*(1-t)*.6f;}yield return null;}
            foreach(var particle in particles) Destroy(particle);
        }
        private void ClearBlocks() { SteveAvatar.ResetLanding();JumpBuild?.Cancel();audio?.StopAll();particles.Reset();entityView.Reset();blockView.Reset();Hud?.Reset();nativeHiddenRenderers.Clear();nativeRendererCache.Clear();nativeProjectileSteps.Clear();nativeTntSteps.Clear();lastTntTick=-1;retiredProjectiles.Clear();foreach (GameObject block in blocks.Values) if(block!=null) { block.SetActive(false); Destroy(block); } blocks.Clear();blockStates.Clear();pendingPlacements.Clear(); breakingTicks.Clear();foreach(var drop in drops.Values) Destroy(drop);drops.Clear();foreach(var tnt in primedTnt.Values)Destroy(tnt);primedTnt.Clear();foreach(var projectile in projectiles.Values)if(projectile!=null)Destroy(projectile);projectiles.Clear();lastProjectileTick=-1;EnemySystem.Clear();if(placementGhost!=null) placementGhost.SetActive(false); }
        private int ItemCount(string id) { int total=0; for(int i=0;i<36;i++) if(inventory[i]==id) total+=counts[i]; return total; }
        private void SmokeTick(HeroController hero)
        {
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_CONTROLS_SMOKE")=="1") {
                if(smokeStage==100)return;
                if(controlsSmoke==null) {if(LatestSnapshot==null||!hero.CanInput()||!hero.cState.onGround)return;controlsSmoke=new ControlsSmoke(this,hero,scene,SmokeResult);}
                try {controlsSmoke.Tick();}catch(Exception error){SmokeResult(false,error.ToString());}return;
            }
            if (!bridge.Connected || !snapshotLogged || smokeStage>=100) return;
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_CHOICE_SMOKE")=="1") {
                if(choiceSmoke==null) {if(!hero.CanInput()||!hero.cState.onGround) return;choiceSmoke=new ChoiceSmoke(this,SmokeResult);}
                try {choiceSmoke.Tick(hero);} catch(Exception error) {SmokeResult(false,error.ToString());}return;
            }
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_SHOP_SMOKE")=="1") {
                if(shopSmoke==null) {if(!hero.CanInput()||!hero.cState.onGround) return;shopSmoke=new ShopSmoke(this,SmokeResult);}
                try {shopSmoke.Tick(hero);} catch(Exception error) {SmokeResult(false,error.ToString());}return;
            }
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_BOSS_SMOKE")=="1") {
                if(bossSmoke==null) {if(!hero.CanInput()||!hero.cState.onGround) return;bossSmoke=new BossSmoke(this,hero,SmokeResult);}
                try {bossSmoke.Tick();} catch(Exception error) {SmokeResult(false,error.ToString());}return;
            }
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_CONTAINER_SMOKE")=="1") {
                if(containersSmoke==null) {if(!hero.CanInput()||!hero.cState.onGround) return;containersSmoke=new ContainersSmoke(this,hero,scene,SmokeResult);}
                try {containersSmoke.Tick();} catch(Exception error) {SmokeResult(false,error.ToString());}return;
            }
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_CAMPAIGN_SMOKE")=="1") {
                if(campaignSmoke==null) {if(!hero.CanInput()||!hero.cState.onGround) return;campaignSmoke=new CampaignSmoke(this,hero,scene,SmokeResult);}
                try {campaignSmoke.Tick();} catch(Exception error) {SmokeResult(false,error.ToString());}return;
            }
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_ENVIRONMENT_SMOKE")=="1") {
                if(environmentSmoke==null) {if(!hero.CanInput()||!hero.cState.onGround) return;environmentSmoke=new EnvironmentSmoke(this,hero,scene,SmokeResult);}
                try {environmentSmoke.Tick();} catch(Exception error) {SmokeResult(false,error.ToString());}return;
            }
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_ADVENTURE_SMOKE")=="1") {
                if(adventureSmoke==null) {if(!hero.CanInput()||!hero.cState.onGround) return;adventureSmoke=new AdventureSmoke(this,hero,scene,SmokeResult);}
                try {adventureSmoke.Tick();} catch(Exception error) {SmokeResult(false,error.ToString());}return;
            }
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_POINTER_ONLY")=="1") {
                if(pointerSmoke==null) {if(!hero.CanInput()||!hero.cState.onGround) return;pointerSmoke=new PointerSmoke(this,hero,scene,SmokeResult);}
                try {pointerSmoke.Tick();} catch(Exception error) {SmokeResult(false,error.ToString());}return;
            }
            if(smokeStage==4) {try {extendedSmoke.Tick(hero);}catch(Exception error) {SmokeResult(false,error.ToString());}return;}
            if (smokeStage>0 && Time.unscaledTime>smokeDeadline) { SmokeResult(false,"Timeout at stage "+smokeStage+": "+status); return; }
            if (smokeStage==0)
            {
                if(Time.unscaledTime>=nextDiagnostic) {nextDiagnostic=Time.unscaledTime+10;Debug.Log("[HollowCraft SMOKE] Waiting hero="+hero.transform.position+" input="+hero.CanInput()+" ground="+hero.cState.onGround+" scale="+Time.timeScale);}
                if(!hero.CanInput() || !hero.cState.onGround) {stableSince=Time.unscaledTime;return;}
                if (Math.Abs(hero.GetComponent<Rigidbody2D>().linearVelocity.y)>0.1f) { stableSince=Time.unscaledTime; return; }
                if(Time.unscaledTime-stableSince<2 || !scene.StartsWith("Tutorial")) return;
                int hx=Cell(hero.transform.position.x), hy=Cell(hero.transform.position.y);
                var occupied=ProbeTerrain(hx,hy);
                bool found=false;
                for(int dx=2;dx<=2 && !found;dx++) for(int dy=0;dy<=1 && !found;dy++)
                {
                    var candidate=new Vector2Int(hx+dx,hy+dy);
                    if(candidate.y < -60 || candidate.y>240 || blocks.ContainsKey(Key(candidate.x,candidate.y))
                        || occupied.Any(p=>p[0]==candidate.x && p[1]==candidate.y)) continue;
                    smokeCell=candidate; found=true;
                }
                if(!found) { SmokeResult(false,"No empty test cell near hero at "+hero.transform.position); return; }
                Debug.Log("[HollowCraft SMOKE] Place at "+smokeCell+" hero "+hero.transform.position);
                bridge.Send(new { type="mode",scene,mode="creative" });
                int dirt=Array.IndexOf(inventory,"minecraft:dirt");
                if(dirt<0) {SmokeResult(false,"Test inventory lacks dirt");return;}
                if(dirt!=0) bridge.Send(new {type="swap",scene,from=dirt,to=0});
                Select(0);
                bridge.Send(new { type="place",scene,x=smokeCell.x,y=smokeCell.y });
                smokeStage=1; smokeDeadline=Time.unscaledTime+30;
            }
            else if(smokeStage==1 && blocks.TryGetValue(Key(smokeCell.x,smokeCell.y),out GameObject block))
            {
                if(!block.name.EndsWith("minecraft:dirt") || block.GetComponent<BoxCollider2D>()==null)
                { SmokeResult(false,"Placed dirt lacks correct Unity collision"); return; }
                Debug.Log("[HollowCraft SMOKE] PASS real Minecraft placement -> Unity sprite and BoxCollider2D");
                bridge.Send(new {type="mine",scene,x=smokeCell.x,y=smokeCell.y,heldTicks=100});
                smokeStage=2; smokeDeadline=Time.unscaledTime+15;
            }
            else if(smokeStage==2 && !blocks.ContainsKey(Key(smokeCell.x,smokeCell.y)))
            {
                Debug.Log("[HollowCraft SMOKE] PASS creative mining -> Unity block removal");
                int logSlot=Array.IndexOf(inventory,"minecraft:oak_log");
                if(logSlot<0) { SmokeResult(false,"Starter oak logs missing"); return; }
                smokeLogsBefore=ItemCount("minecraft:oak_log"); smokePlanksBefore=ItemCount("minecraft:oak_planks");
                int[] recipe=Enumerable.Repeat(-1,9).ToArray(); recipe[0]=logSlot;
                bridge.Send(new {type="craft",scene,slots=recipe}); smokeStage=3; smokeDeadline=Time.unscaledTime+15;
            }
            else if(smokeStage==3 && ItemCount("minecraft:oak_log")==smokeLogsBefore-1 && ItemCount("minecraft:oak_planks")==smokePlanksBefore+4)
            {
                Debug.Log("[HollowCraft SMOKE] PASS vanilla recipe: 1 oak log -> 4 planks; ingredients consumed");
                smokeStage=4;extendedSmoke=new ExtendedSmoke(this,scene,smokeCell,SmokeResult);
            }
        }
        private void SmokeResult(bool success,string description)
        {
            smokeStage=100;
            string text=new JObject { ["success"]=success,["scene"]=scene,["description"]=description,
                ["heroX"]=cachedHero==null?0:cachedHero.transform.position.x,["heroY"]=cachedHero==null?0:cachedHero.transform.position.y,
                ["utc"]=DateTime.UtcNow.ToString("o") }.ToString();
            File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-smoke.json"),text);
            if(!success && LatestSnapshot!=null) File.WriteAllText(Path.Combine(HollowCraftBootstrap.SaveRoot(),"failure-snapshot.json"),LatestSnapshot.ToString());
            Debug.Log("[HollowCraft SMOKE] "+(success ? "PASS " : "FAIL ")+description);
            if(Environment.GetEnvironmentVariable("HOLLOWCRAFT_CAPTURE")=="1")
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-preview.png"));
                if(success) StartCoroutine(CaptureInventory());
            }
        }
        private System.Collections.IEnumerator CaptureInventory()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            inventoryOpen=true;bridge.Send(new {type="inventoryOpen",scene});
            yield return new WaitForSecondsRealtime(0.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-inventory.png"));
            yield return new WaitForSecondsRealtime(0.5f);CloseInterface();
        }
        private Texture2D Texture(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "minecraft:air") return null;
            if (textures.TryGetValue(id, out Texture2D found)) return found;
            bool shield=id=="minecraft:shield";
            bool chest=id=="minecraft:chest";
            string name = shield ? "shield_model" : chest ? "chest_model" : id.Replace("minecraft:", "");
            if (name.Any(c => !(char.IsLetterOrDigit(c) || c == '_'))) return null;
            string path = Path.Combine(assets, name + ".png");
            if (!File.Exists(path)) { textures[id] = null; return null; }
            var texture = new Texture2D(2,2, TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
            texture.LoadImage(File.ReadAllBytes(path));
            if(shield) {
                var face=new Texture2D(12,22,TextureFormat.RGBA32,false) {filterMode=FilterMode.Point};face.SetPixels(texture.GetPixels(1,texture.height-23,12,22));face.Apply();Destroy(texture);texture=face;
            }
            if(chest) {
                var face=new Texture2D(14,14,TextureFormat.RGBA32,false) {filterMode=FilterMode.Point};
                face.SetPixels(0,0,14,10,texture.GetPixels(14,texture.height-43,14,10));
                face.SetPixels(0,10,14,4,texture.GetPixels(14,texture.height-19,14,4));
                face.SetPixels(6,8,2,4,texture.GetPixels(1,texture.height-5,2,4));face.Apply();Destroy(texture);texture=face;
            }
            // Animated block textures are vertical strips: show the first tile for the initial version.
            if (!shield && !chest && !name.StartsWith("gui_") && !name.StartsWith("armor_") && texture.height > texture.width)
            {
                var tile = new Texture2D(texture.width,texture.width, TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
                tile.SetPixels(texture.GetPixels(0, texture.height-texture.width,texture.width,texture.width)); tile.Apply();
                Destroy(texture); texture=tile;
            }
            textures[id]=texture; return texture;
        }
        private Sprite Square(Color color)
        {
            var texture = new Texture2D(1,1); texture.SetPixel(0,0,color); texture.Apply();
            textures["generated:" + textures.Count] = texture;
            return Sprite.Create(texture,new Rect(0,0,1,1),new Vector2(0.5f,0.5f),1/CellSize);
        }
        private void OnGUI()
        {
            if(!InGame) return;
            ui.Draw(LatestSnapshot,container,inventoryOpen||CatalogOpen,selected,creative,Dead,paused,bridge.Connected,crosshair,mineProgress);
            ui.DrawModes();
        }
        internal void Resume() {paused=false;Time.timeScale=connectionFrozen?0:1;SyncBackendPause();}
    }
}
