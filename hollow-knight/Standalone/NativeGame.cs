using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    // Unity-independent rules and storage; the adapter pumps this on the HK thread.
    internal sealed partial class NativeGame : IDisposable
    {
        private readonly string root;
        private readonly Queue<JObject> commands=new Queue<JObject>(),events=new Queue<JObject>();
        private readonly HashSet<string> warned=new HashSet<string>();
        private JObject snapshot;
        internal NativeInventory Inventory=new NativeInventory();
        private string campaign="",scene="",mode="survival",savePath,status="Native C# rules";
        private int visit,tick,attackSerial,serial,syncId,teleport,usingTicks,hurtTicks,regenTicks,lastSwing=-100;
        private double accumulator,x,y,health=20,food=20,saturation=5,exhaustion,absorption,lastDamage;
        private bool paused,grounded,usingItem,usingOffhand,closed,saveBlocked;
        private readonly Dictionary<string,NativeRoom> rooms=new Dictionary<string,NativeRoom>();
        private NativeRoom Room=>rooms[scene];
        private readonly Dictionary<int,JObject> enemies=new Dictionary<int,JObject>();
        private readonly Dictionary<int,JObject> attackReplies=new Dictionary<int,JObject>();
        private readonly HashSet<int> spentAttacks=new HashSet<int>();
        private bool introLanding,introDone;
        private int lastLanding=-1;
        private int air=300,fireTicks;
        private readonly Dictionary<int,int> enemyHurt=new Dictionary<int,int>();
        private readonly Dictionary<string,Tuple<int,int>> effects=new Dictionary<string,Tuple<int,int>>();
        private NativeContainer container;private string containerKey;
        private JObject mining;
        private readonly Dictionary<string,JObject> towerResults=new Dictionary<string,JObject>();
        private NativeStack Active=>usingOffhand?Inventory.Offhand:Inventory.Hand;
        private bool Creative=>mode=="creative";
        private bool Spectator=>mode=="spectator";
        private readonly NativeCombatProfiles combatProfiles;private int combo,lastComboTick=-100;private string comboItem="";
        internal NativeGame(string path,string resourceRoot=null) {NativeData.Load(resourceRoot);combatProfiles=new NativeCombatProfiles(resourceRoot);root=Path.GetFullPath(path);Directory.CreateDirectory(root);}
        internal void Enqueue(JObject packet) {if(closed)return;if(commands.Count>=4096)throw new InvalidOperationException("Native command queue overflow");commands.Enqueue((JObject)packet.DeepClone());}
        internal bool TryRead(out JObject packet) {if(events.Count>0){packet=events.Dequeue();return true;}packet=snapshot;snapshot=null;return packet!=null;}
        internal void Pump(double elapsed) {
            if(closed)return;
            for(int i=0;i<512&&commands.Count>0;i++)Execute(commands.Dequeue());
            if(scene.Length==0||paused)return;
            accumulator+=Math.Max(0,Math.Min(.25,elapsed));
            for(int budget=0;accumulator>=.05&&budget<5;budget++) {accumulator-=.05;tick++;TickPlayer();TickWorld();TickMining();TickWeapons();if(tick%200==0)Save();Publish();}
        }
        private void Emit(JObject value) {value["scene"]=scene;value["campaign"]=campaign;value["visit"]=visit;events.Enqueue(value);}
        private void Notice(string text) {status=text;Emit(new JObject{["type"]="notice",["text"]=text});}
        private static double Number(JObject value,string name,double fallback=0) {double result=(double?)value[name]??fallback;return double.IsNaN(result)||double.IsInfinity(result)?fallback:result;}
        private void Execute(JObject p) {
            string type=(string)p["type"]??"";
            if(type=="scene") {Enter(p);return;}
            if(type=="pause") {paused=(bool?)p["paused"]??false;return;}
            if(type=="visual"){lastVisual=packetVisual(p);return;}
            if(type=="audioFile"||type=="guiInput")return;
            if(scene.Length==0||(string)p["campaign"]!=campaign||(string)p["scene"]!=scene||(int?)p["visit"]!=visit)return;
            switch(type) {
                case "pose":
                    if(p["teleport"]!=null&&(int)p["teleport"]<teleport){Observe(p);ObserveTerrain(p);break;}
                    double nextX=Number(p,"exactX",x),nextY=Number(p,"exactY",y);if(Math.Abs(nextX)>4096||nextY< -256||nextY>4096)break;
                    if((bool?)p["sprinting"]==true)exhaustion+=Math.Min(4,Math.Abs(nextX-x))*.1;
                    bool nextGround=(bool?)p["grounded"]??false;if(grounded&&!nextGround&&nextY>y+.01)exhaustion+=(bool?)p["sprinting"]==true?.2:.05;
                    if((bool?)p["dismount"]==true)Dismount();x=nextX;y=nextY;grounded=nextGround;Observe(p);ObserveTerrain(p);break;
                case "select":usingItem=false;Inventory.Selected=Math.Max(0,Math.Min(8,(int?)p["slot"]??0));mining=null;break;
                case "mode":
                    string requested=(string)p["mode"];if(new[]{"survival","creative","adventure","spectator"}.Contains(requested)){CloseContainer();mode=requested;usingItem=false;mining=null;if(Creative&&!Inventory.Slots.Any(s=>s.Id=="minecraft:trident")) {var trident=NativeStack.New("trident");trident.Data["attack"]=100;trident.Data["loyalty"]=3;Inventory.Insert(trident);}}break;
                case "offhand":usingItem=false;var swap=Inventory.Hand;Inventory.Slots[Inventory.Selected]=Inventory.Offhand;Inventory.Offhand=swap;break;
                case "inventoryOpen":usingItem=false;CloseContainer();if(Creative)Catalog();break;
                case "creativeCatalog":if(Creative)Catalog();break;
                case "creativeItem":CreativeItem(p);break;
                case "creativeDiscard":if(Creative)Inventory.Carried=new NativeStack();break;
                case "slotClick":SlotClick(p);break;
                case "close":CloseContainer();break;
                case "swap":int from=(int?)p["from"]??-1,to=(int?)p["to"]??-1;if(from>=0&&from<36&&to>=0&&to<36){var old=Inventory.Slots[from];Inventory.Slots[from]=Inventory.Slots[to];Inventory.Slots[to]=old;}break;
                case "place":Place(p,false);break;
                case "towerPlace":Place(p,true);break;
                case "mining":if((bool?)p["active"]!=true)mining=null;else if(mining==null||Number(mining,"x")!=Number(p,"x")||Number(mining,"y")!=Number(p,"y")){mining=(JObject)p.DeepClone();mining["progress"]=0;mining["started"]=tick;}break;
                case "mine":mining=(JObject)p.DeepClone();mining["progress"]=0;mining["started"]=tick;break;
                case "swing":Swing(p);break;
                case "cancelAttack":break;
                case "hit":Melee(p);break;
                case "damage":Observe(p);Damage(Number(p,"amount"), (string)p["kind"]??"generic",Number(p,"attackerX",x-1),Number(p,"attackerY",y),(int?)p["enemy"]??0);break;
                case "nativeDeath":break;
                case "flight":break; // Movement is owned by MinecraftEnvironment inside HK.
                case "itemUse":ItemUse(p);break;
                case "eat":if(Inventory.Hand.Definition.Food>0){usingItem=true;usingOffhand=false;usingTicks=0;}break;
                case "use":case "interact":Interact(p);break;
                case "drop":Drop(Inventory.Hand,(bool?)p["all"]==true?Inventory.Hand.Count:1);break;
                case "nativeLanding":Land(p);break;
                case "teleportAck":if((int?)p["teleport"]==teleport){x=Number(p,"x",x);y=Number(p,"y",y);}break;
                case "rest":health=20;food=20;saturation=5;Save();break;
                case "checkpoint":Save();break;
                case "respawn":if(health<=0){health=20;food=20;saturation=5;hurtTicks=fireTicks=0;air=300;effects.Clear();absorption=0;Publish();}break;
                case "projectileStep":ProjectileSteps(p);break;
                case "nativeProjectileHit":ProjectileHit(p);break;
                case "tntStep":TntSteps(p);break;
                case "zenithUse":ZenithInput(p);break;
                case "chat":if(((string)p["text"]??"").Trim().TrimStart('/').Equals("Clamsk",StringComparison.OrdinalIgnoreCase)){if(Inventory.Slots.Count(s=>s.Empty)<2)Notice("Free two inventory slots");else {Inventory.Insert(NativeStack.New("hollowcraft_bridge:zenith"));Inventory.Insert(NativeStack.New("hollowcraft_bridge:stardust_wings"));Notice("Clamsk: Zenith + Stardust Wings");}}break;
                case "kit":case "adventureKit":if(Creative)Inventory.Starter();break;
                default:if(warned.Add(type))Notice("Native migration: "+type+" is not available yet");break;
            }
        }
        private void Enter(JObject packet) {
            string nextCampaign=(string)packet["campaign"]??"",nextScene=(string)packet["scene"]??"";if(nextCampaign.Length==0||nextScene.Length==0||nextCampaign.Length>512||nextScene.Length>512)return;
            CaptureTransients();
            if(campaign!=nextCampaign){CloseContainer();Save();campaign=nextCampaign;Load();}
            else CloseContainer();
            scene=nextScene;visit=(int?)packet["visit"]??0;
            if(!rooms.ContainsKey(scene))rooms[scene]=new NativeRoom();
            x=Number(packet,"exactX",Number(packet,"x"));y=Number(packet,"exactY",Number(packet,"y"));
            enemies.Clear();enemyHurt.Clear();attackReplies.Clear();spentAttacks.Clear();towerResults.Clear();flights.Clear();projectiles.Clear();primed.Clear();terrain.Clear();rider=null;mining=null;usingItem=false;usingTicks=0;accumulator=0;teleport=0;lastLanding=-1;landingWater=null;lastVisual=null;
            foreach(var value in Room.Projectiles)projectiles[(int)value["entity"]]=(JObject)value.DeepClone();
            foreach(var value in Room.Primed)primed[(int)value["id"]]=(JObject)value.DeepClone();
            introLanding=!introDone&&scene=="Tutorial_01"&&(bool?)packet["intro"]==true;
            Emit(new JObject{["type"]="hello",["protocol"]=1,["backend"]="native-csharp"});Publish();
        }
        private void Observe(JObject p) {
            if(!(p["enemies"] is JArray values))return;var present=new HashSet<int>();
            foreach(JObject data in values) {
                int id=(int)data["id"];present.Add(id);int generation=(int?)data["generation"]??0;
                if(!enemies.TryGetValue(id,out var target)||(int?)target["generation"]!=generation) {target=(JObject)data.DeepClone();target["blockSerial"]=target["contactSerial"]=0;enemies[id]=target;enemyHurt.Remove(id);}
                foreach(string field in new[]{"x","y","width","height","phaseFloor","nativeHurtTicks","nativeContact","nailBlock","spellBlock","kind","name","actor"})if(data[field]!=null)target[field]=data[field].DeepClone();
            }
            foreach(int id in enemies.Keys.Where(id=>!present.Contains(id)).ToArray()){enemies.Remove(id);enemyHurt.Remove(id);}
        }
        private void Swing(JObject p) {
            int request=(int?)p["request"]??0;if(attackReplies.TryGetValue(request,out var repeated)){Emit((JObject)repeated.DeepClone());return;}
            if(health<=0||Spectator)return;
            var profile=Profile();var moves=(JArray)profile["weapon"]["attacks"];if(comboItem!=Inventory.Hand.Id||tick-lastComboTick>30)combo=0;var move=(JObject)moves[combo++%moves.Count];comboItem=Inventory.Hand.Id;lastComboTick=tick;
            var reply=new JObject{["type"]="attack",["request"]=request,["attack"]=++attackSerial,["duration"]=1/Math.Max(.2,Inventory.Hand.Definition.Speed),["upswing"]=Number(move,"upswing",.5),["range"]=Creative?6:3,["damage"]=Number(profile,"damage")*Number(move,"damage_multiplier",1),["speed"]=Inventory.Hand.Definition.Speed,["item"]=Inventory.Hand.Id,["aimX"]=Number(p,"aimX",1),["aimY"]=0,["move"]=move.DeepClone()};
            attackReplies[request]=reply;while(attackReplies.Count>32)attackReplies.Remove(attackReplies.Keys.Min());Emit((JObject)reply.DeepClone());
        }
        private JObject Profile() {
            var hand=Inventory.Hand;var weapon=combatProfiles.Weapon(hand);weapon["attack_range"]=Creative?6:3;return new JObject{["item"]=hand.Empty?"minecraft:air":hand.Id,["damage"]=Number(hand.Data,"attack",hand.Definition.Attack),["speed"]=hand.Definition.Speed,["source"]="HollowCraft native C# / static Better Combat profiles",["weapon"]=weapon};
        }
        private void Melee(JObject p) {
            Observe(p);var attack=attackReplies.Values.FirstOrDefault(a=>(int?)a["attack"]==(int?)p["attack"]);if(health<=0||Spectator||attack==null)return;
            if(!spentAttacks.Add((int)p["attack"]))return;
            if(spentAttacks.Count>64)spentAttacks.Remove(spentAttacks.Min());
            double cooldown=Math.Max(0,Math.Min(1,(tick-lastSwing)/Math.Max(1,20/Number(attack,"speed",4))));lastSwing=tick;
            double amount=Number(attack,"damage")*(.2+.8*cooldown*cooldown);int sharp=NativeData.Enchantment(Inventory.Hand,"sharpness");if(sharp>0)amount+=(.5*sharp+.5)*cooldown;if(effects.TryGetValue("strength",out var strength))amount+=3*(strength.Item2+1);if(effects.TryGetValue("weakness",out var weakness))amount=Math.Max(0,amount-4*(weakness.Item2+1));if((bool?)p["falling"]==true&&cooldown>.9)amount*=1.5;
            bool hit=false;foreach(var value in p["targets"] as JArray??new JArray())if(enemies.TryGetValue((int)value,out var enemy)) {
                double closestX=Math.Max(Number(enemy,"x")-Number(enemy,"width",.6)/2,Math.Min(x,Number(enemy,"x")+Number(enemy,"width",.6)/2)),closestY=Math.Max(Number(enemy,"y"),Math.Min(y+1.62,Number(enemy,"y")+Number(enemy,"height",1.8)));
                if((closestX-x)*(closestX-x)+(closestY-y-1.62)*(closestY-y-1.62)>Math.Pow(Creative?6:3,2)+.01)continue;
                hit|=HitEnemy(enemy,amount,"player",Number(enemy,"x")>=x?0:180);
            }
            if(hit){Inventory.Wear(Inventory.Hand,1,Creative);exhaustion+=.1;}
            foreach(JObject creature in p["creatures"] as JArray??new JArray())HitCreature(creature,amount);
        }
        private bool HitEnemy(JObject enemy,double amount,string kind,double angle) {
            double current=Number(enemy,"health");if(current<=0||(string)enemy["kind"]=="npc")return false;
            if(kind=="player"&&(bool?)enemy["nativeContact"]==true){enemy["contactSerial"]=((int?)enemy["contactSerial"]??0)+1;enemy["contactAngle"]=angle;}
            double floor=Number(enemy,"phaseFloor");if(current<=floor+.0001||enemyHurt.TryGetValue((int)enemy["id"],out int until)&&tick<until)return false;
            int direction=(((int)Math.Round(angle/90))%4+4)%4,mask=(int?)enemy[kind=="player"?"nailBlock":"spellBlock"]??0;
            if((mask&(1<<direction))!=0){enemy["blockSerial"]=((int?)enemy["blockSerial"]??0)+1;enemy["blockedAngle"]=angle;enemy["blockedSpell"]=kind!="player";return false;}
            double next=Math.Max(Math.Min(current,floor),current-Math.Max(0,amount));enemy["health"]=next<.0001?0:next;enemy["damageKind"]=kind;enemy["damageAngle"]=angle;enemy["moveFactor"]=1;
            enemyHurt[(int)enemy["id"]]=tick+Math.Max(0,(int?)enemy["nativeHurtTicks"]??0);return true;
        }
        private void TickPlayer() {
            TickEnemyEffects();
            if(hurtTicks>0)hurtTicks--;
            foreach(string id in effects.Keys.ToArray()){var value=effects[id];if(value.Item1<=1){effects.Remove(id);if(id=="absorption")absorption=0;}else effects[id]=Tuple.Create(value.Item1-1,value.Item2);}
            if(effects.TryGetValue("regeneration",out var regen)&&tick%Math.Max(1,50>>regen.Item2)==0&&health>0)health=Math.Min(20,health+1);
            if(health<=0)return;
            if(InFluid("lava"))fireTicks=300;else if(InFluid("water"))fireTicks=0;else if(fireTicks>0)fireTicks--;
            if(fireTicks>0&&tick%20==0&&!effects.ContainsKey("fire_resistance"))Damage(1,"onFire",x,y);
            if(Room.Blocks.TryGetValue(NativeRoom.Key((int)Math.Floor(x),(int)Math.Floor(y+1.62)),out var head)&&(string)head["id"]=="minecraft:water"&&!effects.ContainsKey("water_breathing")&&!Creative&&!Spectator){if(--air<=-20){air=0;Damage(2,"drown",x,y);}}else air=Math.Min(300,air+4);
            if(effects.TryGetValue("poison",out var poison)&&health>1&&tick%Math.Max(1,25>>poison.Item2)==0)Damage(Math.Min(1,health-1),"magic",x,y);
            if(effects.TryGetValue("wither",out var wither)&&tick%Math.Max(1,40>>wither.Item2)==0)Damage(1,"wither",x,y);
            if(usingItem){usingTicks++;if(Active.Definition.Food>0&&usingTicks>=(Active.Definition.FastFood?16:32))FinishFood();else if(Active.Id=="minecraft:milk_bucket"&&usingTicks>=32){effects.Clear();absorption=0;Active.Id="minecraft:bucket";usingItem=false;}else if(Active.Id=="minecraft:potion"&&usingTicks>=32)FinishPotion();}
            while(exhaustion>=4){exhaustion-=4;if(saturation>0)saturation=Math.Max(0,saturation-1);else if(!Creative&&!Spectator)food=Math.Max(0,food-1);}
            if(!Creative&&!Spectator) {
                if(food>=20&&saturation>0&&health<20){if(++regenTicks>=10){double heal=Math.Min(6,saturation)/6;health=Math.Min(20,health+heal);exhaustion+=heal*6;regenTicks=0;}}
                else if(food>=18&&health<20){if(++regenTicks>=80){health=Math.Min(20,health+1);exhaustion+=6;regenTicks=0;}}
                else if(food<=0){if(++regenTicks>=80){if(health>1)Damage(1,"starve",x,y);regenTicks=0;}}else regenTicks=0;
            }
        }
        private void Damage(double amount,string kind,double attackerX,double attackerY,int enemyId=0) {
            if(Creative||Spectator||health<=0||amount<=0)return;
            if(effects.ContainsKey("fire_resistance")&&(kind=="lava"||kind=="onFire"||kind=="inFire"))return;
            if(usingItem&&Active.Id=="minecraft:shield"&&usingTicks>=5&&kind!="fall"&&kind!="hazard"&&kind!="starve"&&kind!="pearl"&&kind!="onFire"&&kind!="lava"&&kind!="drown"&&kind!="magic"&&kind!="wither") {int facing=(int?)lastVisual?["viewYaw"]>0?-1:1;if((attackerX-x)*facing>=0){Inventory.Wear(Active,(int)Math.Ceiling(amount)+1,Creative);if(enemyId!=0)Emit(new JObject{["type"]="shieldRecoil",["id"]=enemyId,["x"]=facing*.4,["y"]=.2});return;}}
            if(hurtTicks>10){if(amount<=lastDamage)return;double incoming=amount;amount-=lastDamage;lastDamage=incoming;}else {lastDamage=amount;hurtTicks=20;}
            if(kind!="fall"&&kind!="pearl"&&kind!="starve") {int armor=Inventory.Armor.Sum(s=>s.Empty?0:s.Definition.Armor);double toughness=Inventory.Armor.Sum(s=>s.Id.StartsWith("minecraft:diamond_",StringComparison.Ordinal)?2:s.Id.StartsWith("minecraft:netherite_",StringComparison.Ordinal)?3:0);amount*=1-Math.Min(20,Math.Max(armor/5.0,armor-amount/(2+toughness/4)))/25;foreach(var piece in Inventory.Armor)Inventory.Wear(piece,Math.Max(1,(int)(lastDamage/4)),Creative);}
            if(effects.TryGetValue("resistance",out var resist)&&kind!="starve")amount*=Math.Max(0,1-.2*(resist.Item2+1));
            if(kind!="starve"){int protection=Inventory.Armor.Sum(s=>NativeData.Enchantment(s,"protection")+(kind=="fall"||kind=="pearl"?3*NativeData.Enchantment(s,"feather_falling"):0)+(kind=="explosion"?2*NativeData.Enchantment(s,"blast_protection"):0)+(kind=="onFire"||kind=="lava"?2*NativeData.Enchantment(s,"fire_protection"):0)+(kind=="arrow"||kind=="trident"?2*NativeData.Enchantment(s,"projectile_protection"):0));amount*=1-Math.Min(20,protection)/25.0;}
            double absorbed=Math.Min(absorption,amount);absorption-=absorbed;health=Math.Max(0,health-(amount-absorbed));exhaustion+=.1;
            if(health<=0){var totem=Inventory.Hand.Id=="minecraft:totem_of_undying"?Inventory.Hand:Inventory.Offhand.Id=="minecraft:totem_of_undying"?Inventory.Offhand:null;if(totem!=null){totem.Count--;totem.Normalize();health=1;effects.Clear();absorption=0;fireTicks=0;Effect("regeneration",900,1);Effect("absorption",100,1);Effect("fire_resistance",800,0);Emit(new JObject{["type"]="totem"});Notice("Totem of Undying");}}
            Emit(new JObject{["type"]="hurt",["ticks"]=10});
            if(kind!="fall"&&kind!="pearl"&&kind!="starve")Emit(new JObject{["type"]="impulse",["x"]=attackerX<x?.4:-.4,["y"]=.4});
        }
        private void Effect(string id,int duration,int level) {if(effects.TryGetValue(id,out var previous)&&(previous.Item2>level||previous.Item2==level&&previous.Item1>=duration))return;effects[id]=Tuple.Create(duration,level);if(id=="absorption")absorption=Math.Max(absorption,4*(level+1));}
        private void FinishFood() {
            var item=Active;if(item.Empty){usingItem=false;return;}food=Math.Min(20,food+item.Definition.Food);saturation=Math.Min(food,saturation+item.Definition.Saturation);
            foreach(JObject effect in item.Definition.FoodEffects??new JArray())if(random.NextDouble()<Number(effect,"chance",1))Effect(((string)effect["id"]).Split(':').Last(),(int)effect["ticks"],(int)effect["amplifier"]);
            if(item.Id.EndsWith("golden_apple",StringComparison.Ordinal)){bool enchanted=item.Id=="minecraft:enchanted_golden_apple";Effect("regeneration",enchanted?400:100,1);Effect("absorption",2400,enchanted?3:0);if(enchanted){Effect("resistance",6000,0);Effect("fire_resistance",6000,0);}}
            if(!Creative){string remainder=item.Definition.Remainder;item.Count--;item.Normalize();if(remainder!=null){var left=NativeStack.New(remainder);if(item.Empty){item.Id=left.Id;item.Count=1;}else if(!Inventory.Insert(left))DropAt(left,x,y+.5);}}usingItem=false;
        }
        private void FinishPotion() {ApplyPotion(Active);if(!Creative){Active.Count--;Active.Normalize();var bottle=NativeStack.New("glass_bottle");if(Active.Empty){Active.Id=bottle.Id;Active.Count=1;}else if(!Inventory.Insert(bottle))DropAt(bottle,x,y+.5);}usingItem=false;}
        private JObject lastVisual;
        private JObject packetVisual(JObject packet)=>packet["scene"]==null||(string)packet["scene"]==scene?(JObject)packet.DeepClone():lastVisual;
        private void Publish() {
            if(scene.Length==0)return;
            var armor=new JArray(Inventory.Armor.Select(s=>s.Json()));
            var stats=new JObject{["health"]=health,["maxHealth"]=20,["food"]=(int)food,["saturation"]=saturation,["armor"]=Inventory.Armor.Sum(s=>s.Empty?0:s.Definition.Armor),["absorption"]=absorption,["dead"]=health<=0,["mode"]=mode,["offhand"]=Inventory.Offhand.Json(),["equipment"]=armor,["using"]=usingItem,["useTicks"]=usingTicks,["activeHand"]=usingOffhand?"offhand":"mainhand",["useAction"]=usingItem?Active.Definition.Food>0?"EAT":Active.Id=="minecraft:shield"?"BLOCK":Active.Id=="minecraft:bow"?"BOW":Active.Id=="minecraft:trident"?"SPEAR":"DRINK":"NONE",["blocking"]=usingItem&&usingTicks>=5&&Active.Id=="minecraft:shield",["speed"]=effects.ContainsKey("speed")?.12:.1,["jumpVelocity"]=.42,["velocityMultiplier"]=1,["slipperiness"]=.6,["eyeHeight"]=1.62,["allowFlying"]=Creative||Spectator,["flySpeed"]=.05,["flying"]=(bool?)lastVisual?["flying"]??false,["water"]=InFluid("water"),["lava"]=InFluid("lava"),["air"]=300,["effects"]=new JArray(effects.Select(e=>new JObject{["id"]="minecraft:"+e.Key,["ticks"]=e.Value.Item1,["amplifier"]=e.Value.Item2})),["level"]=0,["xp"]=0};
            var craft=new JArray{NativeRecipes.Result(Inventory.Craft,2).Json()};foreach(var item in Inventory.Craft)craft.Add(item.Json());
            stats["air"]=air;stats["onFire"]=fireTicks>0;stats["rocket"]=effects.ContainsKey("rocket");stats["hurtTicks"]=hurtTicks;stats["swimHeight"]=.4;stats["waterHeight"]=InFluid("water")?1:0;stats["lavaHeight"]=InFluid("lava")?1:0;if(usingItem&&Active.Id=="minecraft:crossbow")stats["useAction"]="CROSSBOW";
            stats["speed"]=.1*(effects.TryGetValue("speed",out var speedEffect)?1+.2*(speedEffect.Item2+1):1)*(effects.TryGetValue("slowness",out var slowEffect)?Math.Max(0,1-.15*(slowEffect.Item2+1)):1);
            stats["jumpVelocity"]=.42+(effects.TryGetValue("jump_boost",out var jumpEffect)?.1*(jumpEffect.Item2+1):0);
            snapshot=new JObject{["type"]="snapshot",["backend"]="native-csharp",["scene"]=scene,["campaign"]=campaign,["visit"]=visit,["tick"]=tick,["status"]=status,["creative"]=Creative,["selected"]=Inventory.Selected,["inventory"]=new JArray(Inventory.Slots.Select(s=>s.Json())),["stats"]=stats,["carried"]=Inventory.Carried.Json(),["playerCraft"]=craft,["playerSyncId"]=0,["insertSlots"]=new JArray(Enumerable.Range(1,45)),["combat"]=Profile(),["blocks"]=Room.BlocksJson((int)Math.Floor(x),(int)Math.Floor(y)),["drops"]=new JArray(Room.Drops.Select(d=>d.DeepClone())),["enemies"]=new JArray(enemies.Values.Select(e=>e.DeepClone())),["projectiles"]=ProjectileJson(),["primed"]=PrimedJson(),["creatures"]=new JArray()};
            snapshot["playerX"]=x;snapshot["playerY"]=y;snapshot["creatures"]=new JArray(Room.Creatures.Select(v=>v.DeepClone()));if(rider!=null)snapshot["rider"]=rider.DeepClone();
            if(mining!=null)snapshot["mining"]=mining.DeepClone();if(container!=null)snapshot["container"]=ContainerJson();
        }
        private static string Hash(string value) {using(var hash=SHA256.Create())return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b=>b.ToString("x2")));}
        private void Load() {
            Inventory=new NativeInventory();Inventory.Starter();rooms.Clear();effects.Clear();mode="survival";health=food=20;saturation=5;exhaustion=absorption=0;tick=serial=0;introDone=false;
            lastSwing=-100;combo=0;comboItem="";lastComboTick=-100;hurtTicks=regenTicks=fireTicks=0;air=300;lastDamage=0;zenithHeld=false;zenithBurst=zenithClock=0;zenithInput=-100;
            savePath=Path.Combine(root,Hash(campaign)+".json");saveBlocked=File.Exists(savePath);if(!saveBlocked)return;
            var data=JObject.Parse(File.ReadAllText(savePath));if((int?)data["schema"]!=1)throw new InvalidDataException("Unsupported native save schema");
            tick=Math.Max(0,(int?)data["tick"]??0);introDone=(bool?)data["introDone"]??false;
            Inventory.Load((JObject)data["inventory"]);mode=(string)data["mode"]??"survival";health=Math.Max(0,Math.Min(20,Number(data,"health",20)));food=Math.Max(0,Math.Min(20,Number(data,"food",20)));saturation=Math.Max(0,Math.Min(food,Number(data,"saturation",5)));absorption=Math.Max(0,Number(data,"absorption"));
            foreach(var property in (JObject)data["rooms"])rooms[property.Key]=NativeRoom.Read((JObject)property.Value);
            foreach(JObject value in data["effects"] as JArray??new JArray())effects[(string)value["id"]]=Tuple.Create((int)value["ticks"],(int)value["amplifier"]);
            serial=rooms.Values.SelectMany(r=>r.Drops.Concat(r.Creatures).Concat(r.Projectiles).Concat(r.Primed)).Select(v=>(int?)v["entity"]??(int?)v["id"]??0).DefaultIfEmpty(0).Max();saveBlocked=false;
        }
        private void Save() {
            if(string.IsNullOrEmpty(savePath)||saveBlocked)return;
            CaptureTransients();
            var world=new JObject();foreach(var room in rooms)world[room.Key]=room.Value.Save();
            var data=new JObject{["schema"]=1,["campaign"]=campaign,["inventory"]=Inventory.Save(),["mode"]=mode,["health"]=health,["food"]=food,["saturation"]=saturation,["absorption"]=absorption,["rooms"]=world,["effects"]=new JArray(effects.Select(e=>new JObject{["id"]=e.Key,["ticks"]=e.Value.Item1,["amplifier"]=e.Value.Item2}))};
            data["tick"]=tick;data["introDone"]=introDone;
            string temporary=savePath+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temporary,data.ToString(Newtonsoft.Json.Formatting.None),new UTF8Encoding(false));if(File.Exists(savePath))File.Replace(temporary,savePath,savePath+".bak");else File.Move(temporary,savePath);
        }
        public void Dispose() {if(closed)return;while(commands.Count>0)Execute(commands.Dequeue());CloseContainer();Save();closed=true;events.Clear();snapshot=null;}
    }
}
