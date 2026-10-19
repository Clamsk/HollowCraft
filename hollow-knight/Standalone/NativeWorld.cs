using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed class NativeRoom
    {
        internal readonly Dictionary<string,JObject> Blocks=new Dictionary<string,JObject>();
        internal readonly Dictionary<string,Tuple<string,JObject,string>> FluidVisuals=new Dictionary<string,Tuple<string,JObject,string>>();
        internal readonly Dictionary<string,NativeContainer> Containers=new Dictionary<string,NativeContainer>();
        internal readonly List<JObject> Drops=new List<JObject>();
        internal readonly List<JObject> Creatures=new List<JObject>();
        internal readonly List<JObject> Clouds=new List<JObject>();
        internal readonly List<JObject> Projectiles=new List<JObject>(),Primed=new List<JObject>();
        internal static string Key(int x,int y)=>x+":"+y;
        internal static JObject Cell(int x,int y,string id,bool source=true,int level=0)=>new JObject{["x"]=x,["y"]=y,["id"]=NativeCatalog.Id(id),["source"]=source,["level"]=level};
        internal static JArray Shapes(JObject cell) {
            string id=(string)cell["id"];var item=NativeCatalog.Get(id);if(!item.Solid||LiquidId(id)||(bool?)cell["open"]==true||(bool?)cell["crop"]==true)return new JArray();
            if(cell["pistonHead"]!=null){int dx=(int?)cell["dx"]??1,dy=(int?)cell["dy"]??0;return new JArray{new JArray(dx<0?0:dx>0?.75:0,dy<0?0:dy>0?.75:0,dx<0?.25:1,dy<0?.25:1)};}
            if(id.EndsWith("_slab",StringComparison.Ordinal))return (bool?)cell["double"]==true?new JArray{new JArray(0,0,1,1)}:(bool?)cell["top"]==true?new JArray{new JArray(0,.5,1,1)}:new JArray{new JArray(0,0,1,.5)};
            return item.Collision!=null?new JArray(item.Collision.Cast<JArray>().Select(b=>new JArray(b[0].DeepClone(),b[1].DeepClone(),b[3].DeepClone(),b[4].DeepClone()))):new JArray{new JArray(0,0,1,1)};
        }
        private static bool LiquidId(string id)=>id=="minecraft:water"||id=="minecraft:lava";
        internal JArray BlocksJson(int cx,int cy,Dictionary<string,JArray> terrain=null) {
            var result=new JArray();foreach(var cell in Blocks.Values.Where(cell=>Math.Abs((int)cell["x"]-cx)<=14&&Math.Abs((int)cell["y"]-cy)<=12)) {
                var value=(JObject)cell.DeepClone();string id=(string)value["id"];var item=NativeCatalog.Get(id);bool fluid=id=="minecraft:water"||id=="minecraft:lava",open=(bool?)value["open"]==true;
                bool solid=item.Solid&&!fluid&&!open&&(bool?)cell["crop"]!=true;double height=id.EndsWith("_slab",StringComparison.Ordinal)?.5:1;
                value["solid"]=solid;value["fluid"]=fluid;value["height"]=height;value["state"]=id+":"+open+":"+(int?)value["power"]+":"+(bool?)value["lit"];
                value["collision"]=Shapes(cell);value["breakTicks"]=item.Hardness<0?int.MaxValue:Math.Max(1,(int)Math.Ceiling(item.Hardness*30));result.Add(value);
                if((bool?)cell["crop"]==true){int age=(int?)cell["age"]??0;string crop=id.Split(':').Last();int stage=crop=="carrots"||crop=="potatoes"?age==7?3:age/2:age;value["render"]=crop+"_stage"+stage;}
                if(id=="minecraft:redstone")value["render"]="redstone_dust_line0";
                if(id=="minecraft:piston"||id=="minecraft:sticky_piston")value["render"]=cell["pistonHead"]!=null?"piston_top":id=="minecraft:sticky_piston"?"piston_top_sticky":"piston_side";
                value["state"]=(string)value["state"]+"/"+value["render"]+"/"+cell["waterlogged"]+"/"+cell["extended"]+"/"+cell["dx"]+"/"+cell["dy"];
                if(NativeFluidGeometry.IsLiquid(cell))NativeFluidGeometry.Describe(this,terrain,cell,value);
            }return result;
        }
        internal JObject Save() {var boxes=new JObject();foreach(var pair in Containers)boxes[pair.Key]=pair.Value.Save();return new JObject{["blocks"]=new JArray(Blocks.Values.Select(v=>v.DeepClone())),["containers"]=boxes,["drops"]=new JArray(Drops.Select(v=>v.DeepClone())),["creatures"]=new JArray(Creatures.Select(v=>v.DeepClone())),["clouds"]=new JArray(Clouds.Select(v=>v.DeepClone())),["projectiles"]=new JArray(Projectiles.Select(v=>v.DeepClone())),["primed"]=new JArray(Primed.Select(v=>v.DeepClone()))};}
        internal static NativeRoom Read(JObject data) {
            var room=new NativeRoom();if((data["blocks"] as JArray)?.Count>32768)throw new InvalidOperationException("Native room exceeds block limit");
            foreach(JObject value in data["blocks"] as JArray??new JArray())room.Blocks[Key((int)value["x"],(int)value["y"])]=(JObject)value.DeepClone();
            foreach(var box in data["containers"] as JObject??new JObject())room.Containers[box.Key]=NativeContainer.Read((JObject)box.Value);
            foreach(JObject drop in data["drops"] as JArray??new JArray())room.Drops.Add((JObject)drop.DeepClone());
            foreach(JObject mob in data["creatures"] as JArray??new JArray()){var value=(JObject)mob.DeepClone();value.Remove("vehicle");if(value["maxHealth"]==null)value["maxHealth"]=NativeData.Entities.TryGetValue((string)value["kind"]??"",out var definition)?definition["health"]?.DeepClone()??value["health"]?.DeepClone():value["health"]?.DeepClone();room.Creatures.Add(value);}
            foreach(JObject cloud in data["clouds"] as JArray??new JArray())room.Clouds.Add((JObject)cloud.DeepClone());
            foreach(JObject value in data["projectiles"] as JArray??new JArray())room.Projectiles.Add((JObject)value.DeepClone());
            foreach(JObject value in data["primed"] as JArray??new JArray())room.Primed.Add((JObject)value.DeepClone());return room;
        }
    }
    internal sealed partial class NativeGame
    {
        private readonly Dictionary<string,JArray> terrain=new Dictionary<string,JArray>();
        private readonly HashSet<string> terrainKnown=new HashSet<string>();
        private bool terrainSurveyRequired;
        private JObject landingWater;private int collectWater;
        private void CaptureTransients(){if(scene.Length==0||!rooms.TryGetValue(scene,out var room))return;room.Projectiles.Clear();room.Projectiles.AddRange(projectiles.Values.Select(v=>(JObject)v.DeepClone()));room.Primed.Clear();room.Primed.AddRange(primed.Values.Select(v=>(JObject)v.DeepClone()));}
        private bool Reach(int bx,int by,double reach)=>Math.Pow(Math.Max(0,Math.Abs(bx+.5-x)-.5),2)+Math.Pow(Math.Max(0,Math.Abs(by+.5-y-1.62)-.5),2)<=reach*reach+.001;
        private void ObserveTerrain(JObject packet) {
            if(!(packet["terrain"] is JArray values))return;
            if(packet["terrainKnown"] is JArray known){foreach(JArray at in known){if(at.Count<2)continue;string key=NativeRoom.Key((int)at[0],(int)at[1]);terrainKnown.Add(key);terrain.Remove(key);}}else {terrain.Clear();terrainKnown.Clear();}
            foreach(JArray value in values){if(value.Count<2)continue;string key=NativeRoom.Key((int)value[0],(int)value[1]);if(!terrain.TryGetValue(key,out var list))terrain[key]=list=new JArray();list.Add(value.DeepClone());}
        }
        private bool TerrainFull(int bx,int by)=>terrain.TryGetValue(NativeRoom.Key(bx,by),out var values)&&values.OfType<JArray>().Any(v=>v.Count<6||(double)v[2]<.01&&(double)v[3]<.01&&(double)v[4]>.99&&(double)v[5]>.99);
        private bool Solid(int bx,int by)=>terrain.ContainsKey(NativeRoom.Key(bx,by))||Room.Blocks.TryGetValue(NativeRoom.Key(bx,by),out var cell)&&NativeCatalog.Get((string)cell["id"]).Solid&&(bool?)cell["open"]!=true;
        private void Place(JObject packet,bool tower) {
            string request=(string)packet["request"]??"";if(tower&&towerResults.TryGetValue(request,out var prior)){Emit((JObject)prior.DeepClone());return;}
            int bx=(int)Number(packet,"x"),by=(int)Number(packet,"y");var held=Inventory.Hand.Definition.Block?Inventory.Hand:Inventory.Offhand;
            bool foundation=tower&&(bool?)packet["nativeFoundation"]==true&&Number(packet,"supportY")>=by-.05&&Number(packet,"supportY")<by+1;
            bool body=held.Definition.Solid&&x+.3>bx+.001&&x-.3<bx+1-.001&&y<by+1-.001&&y+1.8>by+.001;
            bool allowed=!Spectator&&mode!="adventure"&&health>0&&!held.Empty&&held.Definition.Block&&Math.Abs(bx)<4096&&by>= -256&&by<=4096&&Reach(bx,by,Creative?5:4.5)&&!body&&!Room.Blocks.ContainsKey(NativeRoom.Key(bx,by))&&(!TerrainFull(bx,by)||foundation)&&Room.Blocks.Count<32768;
            if(allowed){Room.Blocks[NativeRoom.Key(bx,by)]=NativeRoom.Cell(bx,by,held.Id);InitializeBlockState(Room.Blocks[NativeRoom.Key(bx,by)],packet);RestorePlacedItem(held,Room.Blocks[NativeRoom.Key(bx,by)]);Sound(SurfaceSound(held.Id,"place"),.8,.9+random.NextDouble()*.2,bx+.5,by+.5);if(!Creative){held.Count--;held.Normalize();}}
            var reply=new JObject{["type"]=tower?"towerResult":"placeResult",["request"]=request,["x"]=bx,["y"]=by,["accepted"]=allowed,["reason"]=allowed?"native placement":"native placement rejected"};
            if(tower){towerResults[request]=(JObject)reply.DeepClone();if(towerResults.Count>128)towerResults.Remove(towerResults.Keys.First());}Emit(reply);
        }
        private void TickMining() {
            if(mining==null)return;
            int bx=(int)Number(mining,"x"),by=(int)Number(mining,"y");string key=NativeRoom.Key(bx,by);
            if(Spectator||mode=="adventure"||health<=0||!Reach(bx,by,Creative?6:4.5)||!Room.Blocks.TryGetValue(key,out var cell)){mining=null;return;}
            if(cell["pistonHead"]!=null){key=(string)cell["pistonHead"];if(!Room.Blocks.TryGetValue(key,out cell)){mining=null;return;}bx=(int)cell["x"];by=(int)cell["y"];mining["x"]=bx;mining["y"]=by;}
            var definition=NativeCatalog.Get((string)cell["id"]);if(definition.Hardness<0&&!Creative){mining=null;return;}
            var tool=Inventory.Hand.Definition;bool harvest=NativeCatalog.RegistryLoaded?!definition.RequiresTool||tool.Tool==definition.Tool&&tool.Harvest>=definition.Harvest:definition.Tool!="pickaxe"||tool.Tool=="pickaxe"&&tool.Harvest>=definition.Harvest;
            double speed=definition.Tool==tool.Tool?tool.MiningSpeed:1;int efficiency=NativeData.Enchantment(Inventory.Hand,"efficiency");if(speed>1&&efficiency>0)speed+=efficiency*efficiency+1;if(!grounded)speed/=5;if(InFluid("water")&&NativeData.Enchantment(Inventory.Armor[0],"aqua_affinity")==0)speed/=5;
            if(effects.TryGetValue("haste",out var haste))speed*=1+.2*(haste.Item2+1);if(effects.TryGetValue("mining_fatigue",out var fatigue))speed*=Math.Pow(.3,Math.Min(4,fatigue.Item2+1));
            double delta=Creative||definition.Hardness<=0||(bool?)cell["crop"]==true?1:speed/definition.Hardness/(harvest?30:100);
            mining["progress"]=Number(mining,"progress")+delta;
            if(Number(mining,"progress")<1)return;
            Sound(SurfaceSound(definition.Id,"break"),.8,1,bx+.5,by+.5);Room.Blocks.Remove(key);Room.Containers.TryGetValue(key,out var box);if(containerKey==key)CloseContainer();DropMinedBlock(cell,box,harvest);Room.Containers.Remove(key);
            if((bool?)cell["extended"]==true){var facing=Facing(cell);Room.Blocks.Remove(NativeRoom.Key(bx+facing[0],by+facing[1]));}
            Inventory.Wear(Inventory.Hand,tool.Tool=="sword"?2:1,Creative);mining=null;
        }
        private void Drop(NativeStack item,int count) {if(item.Empty||count<=0||scene.Length==0)return;count=Math.Min(count,item.Count);DropAt(item.Copy(count),x,y+.5);item.Count-=count;item.Normalize();}
        private void DropAt(NativeStack item,double px,double py) {if(item.Empty)return;var value=item.Json();value["entity"]=++serial;value["x"]=px;value["y"]=py;value["ready"]=tick+20;Room.Drops.Add(value);}
        private void TickWorld() {
            navigationBudget=384;
            if(container!=null){if(tradingMob!=null&&(Number(tradingMob,"health")<=0||Math.Abs(Number(tradingMob,"x")-x)>6||Math.Abs(Number(tradingMob,"y")-y)>6))CloseContainer();else if(containerKey!=null&&(!Room.Blocks.TryGetValue(containerKey,out var openBlock)||Math.Abs(Number(openBlock,"x")-x)>8||Math.Abs(Number(openBlock,"y")-y)>8))CloseContainer();}
            CollectLandingWater();
            foreach(var drop in Room.Drops.ToArray())if(health>0&&tick>=((int?)drop["ready"]??0)&&Math.Pow(Number(drop,"x")-x,2)+Math.Pow(Number(drop,"y")-y-.8,2)<2.25){var stack=NativeStack.Read(drop);Inventory.Insert(stack);if(stack.Empty)Room.Drops.Remove(drop);else drop["count"]=stack.Count;}
            foreach(var entry in Room.Containers) {
                var box=entry.Value;if(box.Id=="minecraft:brewing_stand"){TickBrew(box);continue;}if(!box.Id.Contains("furnace")&&box.Id!="minecraft:smoker")continue;
                if(box.Burn>0)box.Burn--;var recipe=NativeRecipes.Cook(box.Slots[0].Id,box.Id);string output=recipe?.Output;if(box.CookingItem!=box.Slots[0].Id){box.Cook=0;box.CookingItem=box.Slots[0].Id;}
                bool can=!box.Slots[0].Empty&&output!=null&&(box.Slots[2].Empty||box.Slots[2].Id==NativeCatalog.Id(output)&&box.Slots[2].Count<64);
                if(can&&box.Burn==0&&NativeRecipes.Fuel(box.Slots[1])>0){box.Burn=box.BurnMax=NativeRecipes.Fuel(box.Slots[1])/(box.Id=="minecraft:furnace"?1:2);if(box.Slots[1].Id=="minecraft:lava_bucket")box.Slots[1]=NativeStack.New("bucket");else {box.Slots[1].Count--;box.Slots[1].Normalize();}}
                if(can&&box.Burn>0){box.Cook++;if(box.Cook>=recipe.Ticks){box.Cook=0;box.Slots[0].Count--;box.Slots[0].Normalize();if(box.Slots[2].Empty)box.Slots[2]=NativeStack.New(output);else box.Slots[2].Count++;}}else box.Cook=Math.Max(0,box.Cook-2);
                if(Room.Blocks.TryGetValue(entry.Key,out var cell))cell["lit"]=box.Burn>0;
            }
            TickCreatures();TickClouds();TickPlants();FluidStep();RedstoneStep();
            if(tick%10==0&&InFluid("lava")&&!effects.ContainsKey("fire_resistance"))Damage(4,"lava",x,y);
        }
        private bool InFluid(string id)=>scene.Length>0&&FluidDepth(id)>0;
        private void Land(JObject p) {
            if(p["teleport"]!=null&&(int)p["teleport"]!=teleport)return;
            int sequence=(int?)p["landing"]??lastLanding+1;if(sequence<=lastLanding)return;lastLanding=sequence;
            x=Number(p,"x",x);y=Number(p,"y",y);
            double distance=Number(p,"distance");bool intro=introLanding;
            string kind=Creative&&distance>12?"hero":"normal";
            if(mode=="survival"&&settings.AutomaticWater&&(distance>=12||intro)&&Inventory.Slots.Concat(new[]{Inventory.Hand,Inventory.Offhand}).Any(s=>s.Id=="minecraft:water_bucket")&&DeployLandingWater())kind="water";
            bool wings=Inventory.Armor[1].Id=="hollowcraft_bridge:stardust_wings";
            int safe=3+(effects.TryGetValue("jump_boost",out var boost)?boost.Item2+1:0);if(kind=="normal"&&!wings&&!effects.ContainsKey("slow_falling")&&!effects.ContainsKey("levitation")&&!InFluid("water")&&distance>safe)Damage(Math.Ceiling(distance-safe),"fall",x,y);
            Emit(new JObject{["type"]="landing",["kind"]=kind,["intro"]=intro,["distance"]=distance,["hand"]=0});
            if(intro){introLanding=false;introDone=true;Save();}
        }
    }
}
