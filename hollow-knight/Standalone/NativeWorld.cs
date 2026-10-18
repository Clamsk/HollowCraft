using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed class NativeRoom
    {
        internal readonly Dictionary<string,JObject> Blocks=new Dictionary<string,JObject>();
        internal readonly Dictionary<string,NativeContainer> Containers=new Dictionary<string,NativeContainer>();
        internal readonly List<JObject> Drops=new List<JObject>();
        internal readonly List<JObject> Creatures=new List<JObject>();
        internal readonly List<JObject> Clouds=new List<JObject>();
        internal readonly List<JObject> Projectiles=new List<JObject>(),Primed=new List<JObject>();
        internal static string Key(int x,int y)=>x+":"+y;
        internal static JObject Cell(int x,int y,string id,bool source=true,int level=0)=>new JObject{["x"]=x,["y"]=y,["id"]=NativeCatalog.Id(id),["source"]=source,["level"]=level};
        internal JArray BlocksJson(int cx,int cy) {
            var result=new JArray();foreach(var cell in Blocks.Values.Where(cell=>Math.Abs((int)cell["x"]-cx)<=14&&Math.Abs((int)cell["y"]-cy)<=12)) {
                var value=(JObject)cell.DeepClone();string id=(string)value["id"];var item=NativeCatalog.Get(id);bool fluid=id=="minecraft:water"||id=="minecraft:lava",open=(bool?)value["open"]==true;
                bool solid=item.Solid&&!fluid&&!open;double height=id.EndsWith("_slab",StringComparison.Ordinal)?.5:1;
                value["solid"]=solid;value["fluid"]=fluid;value["height"]=fluid?Math.Max(.125,1-((int?)value["level"]??0)/8.0):height;value["state"]=id+":"+open+":"+(int?)value["power"]+":"+(bool?)value["lit"];
                value["collision"]=!solid?new JArray():item.Collision!=null?new JArray(item.Collision.Cast<JArray>().Select(b=>new JArray(b[0].DeepClone(),b[1].DeepClone(),b[3].DeepClone(),b[4].DeepClone()))):new JArray(new JArray(0,0,1,height));value["breakTicks"]=item.Hardness<0?int.MaxValue:Math.Max(1,(int)Math.Ceiling(item.Hardness*30));result.Add(value);
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
        private JObject landingWater;private int collectWater;
        private void CaptureTransients(){if(scene.Length==0||!rooms.TryGetValue(scene,out var room))return;room.Projectiles.Clear();room.Projectiles.AddRange(projectiles.Values.Select(v=>(JObject)v.DeepClone()));room.Primed.Clear();room.Primed.AddRange(primed.Values.Select(v=>(JObject)v.DeepClone()));}
        private bool Reach(int bx,int by,double reach)=>Math.Pow(Math.Max(0,Math.Abs(bx+.5-x)-.5),2)+Math.Pow(Math.Max(0,Math.Abs(by+.5-y-1.62)-.5),2)<=reach*reach+.001;
        private void ObserveTerrain(JObject packet) {if(!(packet["terrain"] is JArray values))return;terrain.Clear();foreach(JArray value in values){if(value.Count<2)continue;string key=NativeRoom.Key((int)value[0],(int)value[1]);if(!terrain.TryGetValue(key,out var list))terrain[key]=list=new JArray();list.Add(value.DeepClone());}}
        private bool TerrainFull(int bx,int by)=>terrain.TryGetValue(NativeRoom.Key(bx,by),out var values)&&values.OfType<JArray>().Any(v=>v.Count<6||(double)v[2]<.01&&(double)v[3]<.01&&(double)v[4]>.99&&(double)v[5]>.99);
        private bool Solid(int bx,int by)=>terrain.ContainsKey(NativeRoom.Key(bx,by))||Room.Blocks.TryGetValue(NativeRoom.Key(bx,by),out var cell)&&NativeCatalog.Get((string)cell["id"]).Solid&&(bool?)cell["open"]!=true;
        private void Place(JObject packet,bool tower) {
            string request=(string)packet["request"]??"";if(tower&&towerResults.TryGetValue(request,out var prior)){Emit((JObject)prior.DeepClone());return;}
            int bx=(int)Number(packet,"x"),by=(int)Number(packet,"y");var held=Inventory.Hand.Definition.Block?Inventory.Hand:Inventory.Offhand;
            bool foundation=tower&&(bool?)packet["nativeFoundation"]==true&&Number(packet,"supportY")>=by-.05&&Number(packet,"supportY")<by+1;
            bool body=held.Definition.Solid&&x+.3>bx+.001&&x-.3<bx+1-.001&&y<by+1-.001&&y+1.8>by+.001;
            bool allowed=!Spectator&&mode!="adventure"&&health>0&&!held.Empty&&held.Definition.Block&&Math.Abs(bx)<4096&&by>= -256&&by<=4096&&Reach(bx,by,Creative?5:4.5)&&!body&&!Room.Blocks.ContainsKey(NativeRoom.Key(bx,by))&&(!TerrainFull(bx,by)||foundation)&&Room.Blocks.Count<32768;
            if(allowed){Room.Blocks[NativeRoom.Key(bx,by)]=NativeRoom.Cell(bx,by,held.Id);Sound(SurfaceSound(held.Id,"place"),.8,.9+random.NextDouble()*.2,bx+.5,by+.5);if(!Creative){held.Count--;held.Normalize();}}
            var reply=new JObject{["type"]=tower?"towerResult":"placeResult",["request"]=request,["x"]=bx,["y"]=by,["accepted"]=allowed,["reason"]=allowed?"native placement":"native placement rejected"};
            if(tower){towerResults[request]=(JObject)reply.DeepClone();if(towerResults.Count>128)towerResults.Remove(towerResults.Keys.First());}Emit(reply);
        }
        private void TickMining() {
            if(mining==null)return;
            int bx=(int)Number(mining,"x"),by=(int)Number(mining,"y");string key=NativeRoom.Key(bx,by);
            if(Spectator||mode=="adventure"||health<=0||!Reach(bx,by,Creative?6:4.5)||!Room.Blocks.TryGetValue(key,out var cell)){mining=null;return;}
            var definition=NativeCatalog.Get((string)cell["id"]);if(definition.Hardness<0&&!Creative){mining=null;return;}
            var tool=Inventory.Hand.Definition;bool harvest=NativeCatalog.RegistryLoaded?!definition.RequiresTool||tool.Tool==definition.Tool&&tool.Harvest>=definition.Harvest:definition.Tool!="pickaxe"||tool.Tool=="pickaxe"&&tool.Harvest>=definition.Harvest;
            double speed=definition.Tool==tool.Tool?tool.MiningSpeed:1;int efficiency=NativeData.Enchantment(Inventory.Hand,"efficiency");if(speed>1&&efficiency>0)speed+=efficiency*efficiency+1;if(!grounded)speed/=5;if(InFluid("water")&&NativeData.Enchantment(Inventory.Armor[0],"aqua_affinity")==0)speed/=5;
            if(effects.TryGetValue("haste",out var haste))speed*=1+.2*(haste.Item2+1);
            double delta=Creative||definition.Hardness<=0?1:speed/definition.Hardness/(harvest?30:100);
            mining["progress"]=Number(mining,"progress")+delta;
            if(Number(mining,"progress")<1)return;
            Sound(SurfaceSound(definition.Id,"break"),.8,1,bx+.5,by+.5);Room.Blocks.Remove(key);if(Room.Containers.TryGetValue(key,out var box)){if(containerKey==key)CloseContainer();foreach(var item in box.Slots)DropAt(item,bx+.5,by+.5);Room.Containers.Remove(key);}
            if(!Creative&&harvest){string drop=definition.Id=="minecraft:stone"?"minecraft:cobblestone":definition.Id=="minecraft:iron_ore"?"minecraft:raw_iron":definition.Id=="minecraft:gold_ore"?"minecraft:raw_gold":definition.Id=="minecraft:diamond_ore"?"minecraft:diamond":definition.Id=="minecraft:coal_ore"?"minecraft:coal":definition.Id;DropAt(NativeStack.New(drop),bx+.5,by+.5);}
            Inventory.Wear(Inventory.Hand,tool.Tool=="sword"?2:1,Creative);mining=null;
        }
        private void Drop(NativeStack item,int count) {if(item.Empty||count<=0||scene.Length==0)return;count=Math.Min(count,item.Count);DropAt(item.Copy(count),x,y+.5);item.Count-=count;item.Normalize();}
        private void DropAt(NativeStack item,double px,double py) {if(item.Empty)return;var value=item.Json();value["entity"]=++serial;value["x"]=px;value["y"]=py;value["ready"]=tick+20;Room.Drops.Add(value);}
        private void TickWorld() {
            if(landingWater!=null&&tick>=collectWater){string key=NativeRoom.Key((int)landingWater["x"],(int)landingWater["y"]);if(Room.Blocks.TryGetValue(key,out var value)&&(string)value["id"]=="minecraft:water")Room.Blocks.Remove(key);landingWater=null;Emit(new JObject{["type"]="waterCollected",["recovered"]=true,["hand"]=0});}
            foreach(var drop in Room.Drops.ToArray())if(health>0&&tick>=((int?)drop["ready"]??0)&&Math.Pow(Number(drop,"x")-x,2)+Math.Pow(Number(drop,"y")-y-.8,2)<2.25){var stack=NativeStack.Read(drop);Inventory.Insert(stack);if(stack.Empty)Room.Drops.Remove(drop);else drop["count"]=stack.Count;}
            foreach(var entry in Room.Containers) {
                var box=entry.Value;if(box.Id=="minecraft:brewing_stand"){TickBrew(box);continue;}if(!box.Id.Contains("furnace")&&box.Id!="minecraft:smoker")continue;
                if(box.Burn>0)box.Burn--;var recipe=NativeRecipes.Cook(box.Slots[0].Id,box.Id);string output=recipe?.Output;if(box.CookingItem!=box.Slots[0].Id){box.Cook=0;box.CookingItem=box.Slots[0].Id;}
                bool can=!box.Slots[0].Empty&&output!=null&&(box.Slots[2].Empty||box.Slots[2].Id==NativeCatalog.Id(output)&&box.Slots[2].Count<64);
                if(can&&box.Burn==0&&NativeRecipes.Fuel(box.Slots[1])>0){box.Burn=box.BurnMax=NativeRecipes.Fuel(box.Slots[1])/(box.Id=="minecraft:furnace"?1:2);if(box.Slots[1].Id=="minecraft:lava_bucket")box.Slots[1]=NativeStack.New("bucket");else {box.Slots[1].Count--;box.Slots[1].Normalize();}}
                if(can&&box.Burn>0){box.Cook++;if(box.Cook>=recipe.Ticks){box.Cook=0;box.Slots[0].Count--;box.Slots[0].Normalize();if(box.Slots[2].Empty)box.Slots[2]=NativeStack.New(output);else box.Slots[2].Count++;}}else box.Cook=Math.Max(0,box.Cook-2);
                if(Room.Blocks.TryGetValue(entry.Key,out var cell))cell["lit"]=box.Burn>0;
            }
            TickCreatures();TickClouds();if(tick%4==0)FluidStep();if(tick%2==0)RedstoneStep();
            if(tick%10==0&&InFluid("lava")&&!effects.ContainsKey("fire_resistance"))Damage(4,"lava",x,y);
        }
        private bool InFluid(string id)=>scene.Length>0&&Room.Blocks.TryGetValue(NativeRoom.Key((int)Math.Floor(x),(int)Math.Floor(y+.4)),out var block)&&(string)block["id"]=="minecraft:"+id;
        private void FluidStep() {
            var sources=Room.Blocks.Values.Where(c=>((string)c["id"]=="minecraft:water"||(string)c["id"]=="minecraft:lava")&&(bool?)c["source"]==true).Select(c=>(JObject)c.DeepClone()).ToArray();
            foreach(var key in Room.Blocks.Where(p=>((string)p.Value["id"]=="minecraft:water"||(string)p.Value["id"]=="minecraft:lava")&&(bool?)p.Value["source"]!=true).Select(p=>p.Key).ToArray())Room.Blocks.Remove(key);
            var queue=new Queue<Tuple<int,int,string,int,int>>();foreach(var source in sources)queue.Enqueue(Tuple.Create((int)source["x"],(int)source["y"],(string)source["id"],0,0));
            int budget=2048;var visited=new Dictionary<string,int>();
            while(queue.Count>0&&budget-->0) {
                var cell=queue.Dequeue();int bx=cell.Item1,by=cell.Item2,level=cell.Item4,fall=cell.Item5;string id=cell.Item3,key=NativeRoom.Key(bx,by);
                string seen=id+":"+key;if(visited.TryGetValue(seen,out int prior)&&prior<=level)continue;visited[seen]=level;
                if(by< -60||Math.Abs(bx-x)>32||Math.Abs(by-y)>24||TerrainFull(bx,by))continue;
                if(Room.Blocks.TryGetValue(key,out var old)){string other=(string)old["id"];if((other=="minecraft:water"||other=="minecraft:lava")&&other!=id){Room.Blocks[key]=NativeRoom.Cell(bx,by,(other=="minecraft:lava"&&(bool?)old["source"]==true)?"obsidian":"cobblestone");continue;}if(other!=id)continue;if((bool?)old["source"]!=true&&((int?)old["level"]??8)<=level)continue;}
                bool source=sources.Any(s=>(int)s["x"]==bx&&(int)s["y"]==by);Room.Blocks[key]=NativeRoom.Cell(bx,by,id,source,level);
                if(!Solid(bx,by-1)&&fall<20)queue.Enqueue(Tuple.Create(bx,by-1,id,0,fall+1));
                else if(level<(id=="minecraft:lava"?3:7))foreach(int direction in new[]{-1,1})if(!Solid(bx+direction,by))queue.Enqueue(Tuple.Create(bx+direction,by,id,level+1,fall));
            }
        }
        private void RedstoneStep() {
            var powered=new Dictionary<string,int>();var queue=new Queue<Tuple<string,int>>();
            foreach(var cell in Room.Blocks.Values){string id=(string)cell["id"];if(id=="minecraft:redstone_block"||id=="minecraft:redstone_torch"||id=="minecraft:lever"&&(bool?)cell["powered"]==true||id.EndsWith("_button",StringComparison.Ordinal)&&(bool?)cell["powered"]==true)queue.Enqueue(Tuple.Create(NativeRoom.Key((int)cell["x"],(int)cell["y"]),15));if((int?)cell["until"]>0&&(int)cell["until"]<=tick)cell["powered"]=false;}
            while(queue.Count>0){var current=queue.Dequeue();if(powered.TryGetValue(current.Item1,out int value)&&value>=current.Item2)continue;powered[current.Item1]=current.Item2;var at=Room.Blocks[current.Item1];foreach(var delta in new[]{Tuple.Create(-1,0),Tuple.Create(1,0),Tuple.Create(0,-1),Tuple.Create(0,1)}){string key=NativeRoom.Key((int)at["x"]+delta.Item1,(int)at["y"]+delta.Item2);if(current.Item2>1&&Room.Blocks.TryGetValue(key,out var next)&&((string)next["id"]=="minecraft:redstone"||(string)next["id"]=="minecraft:repeater"))queue.Enqueue(Tuple.Create(key,current.Item2-1));}}
            foreach(var cell in Room.Blocks.Values.ToArray()) {
                string id=(string)cell["id"];int value=0;foreach(var delta in new[]{Tuple.Create(0,0),Tuple.Create(-1,0),Tuple.Create(1,0),Tuple.Create(0,-1),Tuple.Create(0,1)})if(powered.TryGetValue(NativeRoom.Key((int)cell["x"]+delta.Item1,(int)cell["y"]+delta.Item2),out int found))value=Math.Max(value,found);
                if(id=="minecraft:redstone")cell["power"]=value;if(id=="minecraft:redstone_lamp")cell["lit"]=value>0;
                if(id.EndsWith("_door",StringComparison.Ordinal)||id.EndsWith("_trapdoor",StringComparison.Ordinal)||id.EndsWith("_fence_gate",StringComparison.Ordinal)){if((bool?)cell["redstoneOpen"]==true||value>0)cell["open"]=value>0;cell["redstoneOpen"]=value>0;}
                if(id=="minecraft:tnt"&&value>0){Room.Blocks.Remove(NativeRoom.Key((int)cell["x"],(int)cell["y"]));Prime((int)cell["x"]+.5,(int)cell["y"]);}
            }
        }
        private void Land(JObject p) {
            if(p["teleport"]!=null&&(int)p["teleport"]!=teleport)return;
            int sequence=(int?)p["landing"]??lastLanding+1;if(sequence<=lastLanding)return;lastLanding=sequence;
            x=Number(p,"x",x);y=Number(p,"y",y);
            double distance=Number(p,"distance");bool intro=introLanding;
            string kind=Creative&&distance>12?"hero":"normal";
            if(!Creative&&!Spectator&&(distance>=12||intro)&&(Inventory.Hand.Id=="minecraft:water_bucket"||Inventory.Slots.Any(s=>s.Id=="minecraft:water_bucket")||Inventory.Offhand.Id=="minecraft:water_bucket")) {
                int bx=(int)Math.Floor(x),by=(int)Math.Floor(y+.01);string key=NativeRoom.Key(bx,by);if(!Room.Blocks.ContainsKey(key)){landingWater=NativeRoom.Cell(bx,by,"water");Room.Blocks[key]=landingWater;collectWater=tick+30;kind="water";}
            }
            bool wings=Inventory.Armor[1].Id=="hollowcraft_bridge:stardust_wings";
            int safe=3+(effects.TryGetValue("jump_boost",out var boost)?boost.Item2+1:0);if(kind=="normal"&&!wings&&!effects.ContainsKey("slow_falling")&&!effects.ContainsKey("levitation")&&!InFluid("water")&&distance>safe)Damage(Math.Ceiling(distance-safe),"fall",x,y);
            Emit(new JObject{["type"]="landing",["kind"]=kind,["intro"]=intro,["distance"]=distance,["hand"]=0});
            if(intro){introLanding=false;introDone=true;Save();}
        }
    }
}
