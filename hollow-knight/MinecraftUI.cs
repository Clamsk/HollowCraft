using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class MinecraftUI
    {
        private readonly HollowCraftRuntime runtime;
        private float scale,left,top;
        private Vector2 pointer;
        private bool containerOpen;
        private readonly List<Tuple<int,Rect,JToken>> drawnSlots=new List<Tuple<int,Rect,JToken>>();
        private readonly HashSet<int> dragSlots=new HashSet<int>();
        private int dragButton=-1,pressedSlot=-1,dragSyncId;
        private bool dragPending,dragActive;
        private string dragHandler;
        private JToken dragItem;
        private Font dialogueFont;
        private string anvilName="";
        private JToken hovered;
        private string catalogSearch="";private int catalogPage,shopPage,catalogTab;
        private bool textInputFocused;
        internal bool TextInputFocused => runtime.Gui?.Active==true?runtime.Gui.TextFocused:textInputFocused;
        private string anvilSource="";
        private string notice="";private float noticeUntil;
        private string choiceKey="";private int choiceRow;
        internal void ShowNotice(string value) {notice=value??"";noticeUntil=Time.unscaledTime+3;}
        internal MinecraftUI(HollowCraftRuntime value) {runtime=value;}
        internal void DrawModes()
        {
            var menu=runtime.Modes;if(!menu.Open)return;
            float size=Mathf.Clamp(Mathf.Floor(Screen.height/240f),2,4),width=125*size,height=75*size;
            float x=Screen.width/2-62*size,y=Screen.height/2-40*size;
            GUI.depth=-200;GUI.color=Color.white;Region("gui_gamemode_switcher",new Rect(x,y,width,height),0,0,125,75);
            for(int i=0;i<4;i++) {
                var rect=new Rect(Screen.width/2+(-59+i*31)*size,Screen.height/2-31*size,26*size,26*size);
                if(rect.Contains(pointer))menu.Hover(i);
                Region("gui_gamemode_switcher",rect,0,75,26,26);
                if(i==menu.Selected)Region("gui_gamemode_switcher",rect,26,75,26,26);
                var texture=runtime.ItemTexture(GameModeMenu.Icons[i]);if(texture!=null)GUI.DrawTexture(new Rect(rect.x+5*size,rect.y+5*size,16*size,16*size),texture);
            }
            Text(GameModeMenu.Labels[menu.Selected],Screen.width/2-GameModeMenu.Labels[menu.Selected].Length*3*size,Screen.height/2+5*size,size);
            Text("[F4]",Screen.width/2-27*size,Screen.height/2+25*size,size,Color.yellow);Text("Next",Screen.width/2+3*size,Screen.height/2+25*size,size,new Color(.67f,.67f,.67f));
        }
        private Texture2D Texture(string name) => runtime.ItemTexture(name);
        private void Region(string name,Rect target,float x,float y,float w,float h)
        {
            var image=Texture(name);if(image==null) return;
            GUI.DrawTextureWithTexCoords(target,image,new Rect(x/image.width,1-(y+h)/image.height,w/image.width,h/image.height));
        }
        private void Tile(string name,float x,float y,float w,float h,float sx,float sy,float sw,float sh) => Region(name,new Rect(left+x*scale,top+y*scale,w*scale,h*scale),sx,sy,sw,sh);
        private void Text(string value,float x,float y,float size=2,Color? color=null)
        {
            var font=Texture("gui_font");if(font==null) return;
            Color previous=GUI.color;
            for(int pass=0;pass<2;pass++) {
                GUI.color=pass==0 ? new Color(0.12f,0.12f,0.12f) : color ?? Color.white;
                float advance=x+(pass==0 ? size : 0),py=y+(pass==0 ? size : 0);
                foreach(char ch in value ?? "") {
                    int code=ch<256 ? ch : '?';
                    Region("gui_font",new Rect(advance,py,8*size,8*size),(code%16)*8,(code/16)*8,8,8);
                    advance+=6*size;
                }
            }
            GUI.color=previous;
        }
        private void Item(JToken entry,Rect bounds)
        {
            if(entry==null || ((int?)entry["count"] ?? 0)<=0) return;
            var texture=runtime.ItemTexture((string)entry["render"] ?? (string)entry["id"])??runtime.ItemTexture((string)entry["id"]);
            if(texture!=null) GUI.DrawTexture(bounds,texture,ScaleMode.ScaleToFit);
            int count=(int)entry["count"];
            if(count>1) Text(count.ToString(),bounds.xMax-count.ToString().Length*6*scale,bounds.yMax-8*scale,scale);
            int damage=(int?)entry["damage"] ?? 0,max=(int?)entry["maxDamage"] ?? 0;
            if(max>0 && damage>0) {
                float remaining=Mathf.Clamp01(1-(float)damage/max);
                GUI.color=Color.black;GUI.DrawTexture(new Rect(bounds.x+2*scale,bounds.yMax-3*scale,13*scale,2*scale),Texture2D.whiteTexture);
                GUI.color=Color.HSVToRGB(remaining/3,1,1);GUI.DrawTexture(new Rect(bounds.x+2*scale,bounds.yMax-3*scale,Mathf.RoundToInt(13*remaining)*scale,scale),Texture2D.whiteTexture);GUI.color=Color.white;
            }
        }
        private void Slot(JToken entry,int serverSlot,float x,float y)
        {
            var rect=new Rect(left+x*scale,top+y*scale,16*scale,16*scale);
            drawnSlots.Add(Tuple.Create(serverSlot,rect,entry));
            JToken display=entry;
            if(dragActive&&dragSlots.Contains(serverSlot)&&dragItem!=null) {
                int before=(int?)entry?["count"]??0,max=(int?)dragItem["maxCount"]??64;
                int add=dragButton==1?1:dragButton==2?max:((int)dragItem["count"])/Math.Max(1,dragSlots.Count);
                display=dragItem.DeepClone();display["count"]=Math.Min(max,before+add);
            }
            Item(display,rect);
            if(!rect.Contains(pointer)) return;
            hovered=entry;
            GUI.color=new Color(1,1,1,0.4f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;
        }
        internal void CancelDrag() {
            if(dragActive)Click(-999,(dragButton<<2)|2,"QUICK_CRAFT",dragHandler,dragSyncId);
            dragButton=-1;pressedSlot=-1;dragPending=dragActive=false;dragItem=null;dragSlots.Clear();
        }
        private void Click(int slot,int button,string action,string handler=null,int? sync=null) {
            runtime.Send(new {type="slotClick",scene=runtime.Room,handler=handler??(containerOpen?"container":"player"),slot,button,action,syncId=sync??CurrentSync()});
        }
        private int CurrentSync()=>(int?)(containerOpen?runtime.LatestSnapshot?["container"]?["syncId"]:runtime.LatestSnapshot?["playerSyncId"])??0;
        private void AddDragSlot(int slot) {
            var entry=drawnSlots.FirstOrDefault(s=>s.Item1==slot)?.Item3;
            int count=(int?)entry?["count"]??0;
            if(slot<0||dragSlots.Contains(slot)||dragItem==null||count>=((int?)dragItem["maxCount"]??64))return;
            var allowed=runtime.LatestSnapshot?["insertSlots"] as JArray;
            if(allowed!=null&&!allowed.Any(s=>(int)s==slot))return;
            if(count>0&&((string)entry["id"]!=(string)dragItem["id"]||(string)entry["potion"]!=(string)dragItem["potion"]))return;
            if(dragButton!=2&&dragSlots.Count>=((int?)dragItem["count"]??0))return;
            // Result slots cannot accept an ingredient. Other restrictions stay server-owned.
            string id=(string)runtime.LatestSnapshot?["container"]?["id"]??"inventory";
            if(slot==0&&(id=="inventory"||id.Contains("crafting_table")))return;
            if(id.Contains("furnace")&&slot==2)return;
            dragSlots.Add(slot);Click(slot,(dragButton<<2)|1,"QUICK_CRAFT",dragHandler,dragSyncId);
        }
        private void InventoryInput(JObject snapshot,Rect bounds) {
            var e=Event.current;
            if(e.type==EventType.KeyDown&&TextInputFocused)return;
            if(e.type==EventType.KeyDown&&runtime.Creative&&e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9&&(Input.GetKey(KeyCode.C)||Input.GetKey(KeyCode.X))) {e.Use();return;}
            var pointed=drawnSlots.FirstOrDefault(s=>s.Item2.Contains(pointer));int slot=pointed?.Item1??-999;
            if(dragButton>=0&&(dragHandler!=(containerOpen?"container":"player")||dragSyncId!=CurrentSync()))CancelDrag();
            bool shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
            var carried=snapshot["carried"];bool carrying=((int?)carried?["count"]??0)>0;
            if(e.type==EventType.KeyDown&&slot>=0) {
                if(e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9) {Click(slot,(int)e.keyCode-(int)KeyCode.Alpha1,"SWAP");e.Use();}
                else if(e.keyCode==KeyCode.F) {Click(slot,40,"SWAP");e.Use();}
                else if(e.keyCode==KeyCode.Q) {Click(slot,Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)?1:0,"THROW");e.Use();}
            }
            else if(e.type==EventType.MouseDown&&(e.button<=1||(e.button==2&&runtime.Creative))) {
                CancelDrag();
                if(slot==-999&&bounds.Contains(pointer))return;
                if(shift&&slot>=0)Click(slot,e.button==1?1:0,"QUICK_MOVE");
                else if(e.button==0&&e.clickCount>=2&&slot>=0&&carrying)Click(slot,0,"PICKUP_ALL");
                else if(e.button==2&&!carrying&&slot>=0)Click(slot,2,"CLONE");
                else if(carrying&&slot>=0) {
                    dragButton=e.button;pressedSlot=slot;dragPending=true;dragHandler=containerOpen?"container":"player";
                    dragSyncId=CurrentSync();dragItem=carried.DeepClone();
                } else Click(slot,e.button==1?1:0,"PICKUP");
                e.Use();
            }
            else if(e.type==EventType.MouseDrag&&dragPending&&slot>=0) {
                if(slot!=pressedSlot&&!dragActive) {dragActive=true;Click(-999,dragButton<<2,"QUICK_CRAFT",dragHandler,dragSyncId);AddDragSlot(pressedSlot);}
                if(dragActive)AddDragSlot(slot);e.Use();
            }
            else if(e.type==EventType.MouseUp&&e.button==dragButton) {
                if(dragActive)CancelDrag();
                else {Click(pressedSlot,dragButton==1?1:0,"PICKUP",dragHandler,dragSyncId);CancelDrag();}
                e.Use();
            }
        }
        private bool Button(string caption,float x,float y,float width=200)
        {
            var rect=new Rect(x,y,width*scale,20*scale);bool hover=rect.Contains(pointer);
            Region("gui_widgets",rect,0,hover ? 86 : 66,200,20);
            Text(caption,x+(width*scale-caption.Length*6*scale)/2,y+6*scale,scale);
            if(GUI.enabled&&hover && Event.current.type==EventType.MouseDown && Event.current.button==0) {Event.current.Use();return true;}return false;
        }
        internal void Draw(JObject snapshot,JObject container,bool open,int selected,bool creative,bool dead,bool paused,bool connected,Vector2 crosshair,float mine)
        {
            scale=Mathf.Clamp(Mathf.Floor(Screen.height/240f),2,4);
            var raw=GameCursor.ReadScreenPosition();pointer=new Vector2(raw.x,Screen.height-raw.y);
            GUI.depth=-100;GUI.color=Color.white;
            hovered=null;
            var inventory=snapshot?["inventory"] as JArray;var stats=snapshot?["stats"] as JObject;
            left=(Screen.width-182*scale)/2;top=Screen.height-22*scale;
            Tile("gui_widgets",0,0,182,22,0,0,182,22);Tile("gui_widgets",selected*20-1,-1,24,24,0,22,24,24);
            if(inventory!=null) for(int i=0;i<9;i++) Item(inventory[i],new Rect(left+(3+i*20)*scale,top+3*scale,16*scale,16*scale));
            Tile("gui_widgets",-29,-1,29,24,24,22,29,24);Item(stats?["offhand"],new Rect(left-23*scale,top+3*scale,16*scale,16*scale));
            if(!creative && stats!=null) {
                int health=Mathf.CeilToInt((float?)stats["health"] ?? 20),food=(int?)stats["food"] ?? 20,armor=(int?)stats["armor"] ?? 0;
                int hearts=Mathf.CeilToInt(((float?)stats["maxHealth"] ?? 20)/2),absorption=Mathf.CeilToInt((float?)stats["absorption"] ?? 0),rows=Mathf.CeilToInt((hearts+Mathf.CeilToInt(absorption/2f))/10f);
                var activeEffects=stats["effects"] as JArray;int heartTexture=activeEffects?.Any(e=>(string)e["id"]=="minecraft:wither")==true?124:activeEffects?.Any(e=>(string)e["id"]=="minecraft:poison")==true?88:52;
                for(int i=0;i<hearts+Mathf.CeilToInt(absorption/2f);i++) {
                    float y=-17-(i/10)*10;Tile("gui_icons",(i%10)*8,y,9,9,PlayerSystem.HeartFlash?25:16,0,9,9);
                    if(PlayerSystem.HeartFlash&&i<hearts) {int prior=Mathf.CeilToInt(PlayerSystem.PreviousHealth)-i*2;if(prior>0)Tile("gui_icons",(i%10)*8,y,9,9,prior>=2?heartTexture+18:heartTexture+27,0,9,9);}
                    int value=i<hearts?health-i*2:absorption-(i-hearts)*2,texture=i<hearts?heartTexture:160;
                    if(value>0) Tile("gui_icons",(i%10)*8,y,9,9,value>=2?texture:texture+9,0,9,9);
                }
                for(int i=0;i<10;i++) {
                    Tile("gui_icons",173-i*8,-17,9,9,16,27,9,9);
                    if(food>i*2) Tile("gui_icons",173-i*8,-17,9,9,food>=i*2+2 ? 52 : 61,27,9,9);
                    if(armor>0) Tile("gui_icons",i*8,-17-rows*10,9,9,armor>=i*2+2 ? 34 : armor>i*2 ? 25 : 16,9,9,9);
                }
                Tile("gui_icons",0,-6,182,5,0,64,182,5);float xp=(float?)stats["xp"] ?? 0;
                if(xp>0) Tile("gui_icons",0,-6,182*xp,5,0,69,182*xp,5);
                int level=(int?)stats["level"] ?? 0;if(level>0) Text(level.ToString(),Screen.width/2-3*scale,top-17*scale,scale,Color.green);
            }
            if(!connected) Text("Connecting to Minecraft...",16,16,2);
            if(PlayerData.instance!=null) Text("Geo: "+PlayerData.instance.geo,16,connected?16:38,scale*.75f);
            if(!string.IsNullOrEmpty(CampaignBridge.DialogueText) && !dead && !paused) {
                if(dialogueFont==null) dialogueFont=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",18);
                var box=new Rect(Screen.width*.15f,Screen.height*.72f,Screen.width*.7f,Screen.height*.2f);
                GUI.color=new Color(.05f,.02f,.1f,.88f);GUI.DrawTexture(box,Texture2D.whiteTexture);GUI.color=Color.white;
                var style=new GUIStyle(GUI.skin.label){font=dialogueFont,fontSize=Mathf.RoundToInt(8*scale),wordWrap=true};style.normal.textColor=Color.white;
                GUI.Label(new Rect(box.x+12,box.y+10,box.width-24,box.height-32),CampaignBridge.DialogueText,style);
                if(!NativeChoice.Visible) {Text("Click to continue",box.x+12,box.yMax-20,scale*.7f);
                    if(Event.current.type==EventType.MouseDown && box.Contains(pointer)) {CampaignBridge.AdvanceDialogue();Event.current.Use();}}
            }
            var effects=stats?["effects"] as JArray;
            if(effects!=null) for(int i=0;i<effects.Count;i++) {
                string id=(string)effects[i]["id"],name=id?.Split(':').Last();var icon=Texture("effect_"+name);
                float x=Screen.width-28*scale,y=8*scale+i*32*scale;
                if(icon!=null) GUI.DrawTexture(new Rect(x,y,18*scale,18*scale),icon);
                int seconds=((int?)effects[i]["ticks"] ?? 0)/20;Text((seconds/60)+":"+(seconds%60).ToString("00"),x,y+19*scale,scale*.7f);
            }
            if((bool?)stats?["water"]==true && ((int?)stats?["air"] ?? 300)<300) {
                int bubbles=Mathf.CeilToInt(((int?)stats["air"] ?? 0)/30f);for(int i=0;i<10;i++) Tile("gui_icons",173-i*8,-27,9,9,i<bubbles ? 16 : 25,18,9,9);
            }
            bool nativeGui=false;
            if(dead || paused) {
                GUI.color=dead ? new Color(0.4f,0,0,0.65f) : new Color(0,0,0,0.65f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
                string heading=dead ? "You died!" : "Game Menu";Text(heading,Screen.width/2-heading.Length*9,Screen.height/2-100,3);
                if(Button(dead ? "Respawn" : "Back to Game",Screen.width/2-100*scale,Screen.height/2)) {if(dead) runtime.Send(new {type="respawn",scene=runtime.Room});else runtime.Resume();}
            } else if((open||runtime.CatalogOpen)&&(nativeGui=runtime.Gui?.Draw()==true)) { }
            else if(runtime.CatalogOpen) DrawCatalog();
            else if(open && inventory!=null) DrawInventory(snapshot,container,inventory,stats);
            string focused=GUI.GetNameOfFocusedControl();
            textInputFocused=open&&!dead&&!paused&&(focused=="HollowCraftCreativeSearch"||focused=="HollowCraftAnvilName");
            if(NativeShop.Visible&&!dead&&!paused) DrawShop();
            if(NativeChoice.Visible&&!dead&&!paused) {if(Button("Yes",Screen.width/2-105*scale,Screen.height*.88f,100)) NativeChoice.Decide(true);if(Button("No",Screen.width/2+5*scale,Screen.height*.88f,100)) NativeChoice.Decide(false);}
            if(!open && !NativeShop.Visible && !NativeChoice.Visible && !dead && !paused) {
                Region("gui_icons",new Rect(crosshair.x-7.5f*scale,crosshair.y-7.5f*scale,15*scale,15*scale),0,0,15,15);
                if(mine>0) {GUI.color=new Color(0,0,0,0.7f);GUI.DrawTexture(new Rect(crosshair.x-20,crosshair.y+24,40,4),Texture2D.whiteTexture);GUI.color=Color.white;GUI.DrawTexture(new Rect(crosshair.x-20,crosshair.y+24,40*mine,4),Texture2D.whiteTexture);}
            } else {
                Region("gui_icons",new Rect(pointer.x-7.5f*scale,pointer.y-7.5f*scale,15*scale,15*scale),0,0,15,15);
                if(open&&!nativeGui) Item(snapshot?["carried"],new Rect(pointer.x+10,pointer.y+10,16*scale,16*scale));
            }
            GUI.color=Color.white;
            if(CampaignBridge.Sitting&&!open&&!dead&&!paused) {if(Button("Leave Bench (W / Space)",Screen.width/2-110*scale,Screen.height-76*scale,220)) CampaignBridge.LeaveBench();}
            if(Time.unscaledTime<noticeUntil&&!string.IsNullOrEmpty(notice))Text(notice,Screen.width/2-notice.Length*3*scale,top-45*scale,scale);
            if(open&&hovered!=null&&((int?)hovered["count"] ?? 0)>0) {
                var lines=new System.Collections.Generic.List<string>{(string)hovered["name"] ?? (string)hovered["id"]};
                foreach(var line in hovered["tooltip"] as JArray ?? new JArray()) lines.Add((string)line);
                int maximum=(int?)hovered["maxDamage"] ?? 0;if(maximum>0) lines.Add("Durability: "+(maximum-((int?)hovered["damage"] ?? 0))+" / "+maximum);
                float w=lines.Max(l=>l.Length)*6*scale+12,h=lines.Count*10*scale+10,x=Mathf.Clamp(pointer.x+14,0,Screen.width-w),y=Mathf.Clamp(pointer.y+14,0,Screen.height-h);
                GUI.color=new Color(.08f,.02f,.13f,.96f);GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white;
                for(int i=0;i<lines.Count;i++) Text(lines[i],x+6,y+5+i*10*scale,scale,i==0?Color.white:new Color(.65f,.65f,1));
            }
        }
        private void DrawInventory(JObject snapshot,JObject container,JArray inventory,JObject stats)
        {
            drawnSlots.Clear();
            GUI.color=new Color(0,0,0,0.65f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            containerOpen=container!=null;string id=(string)container?["id"] ?? "inventory";var slots=container?["slots"] as JArray;
            bool furnace=id.Contains("furnace")||id.Contains("smoker"),craft=id.Contains("crafting_table"),brewing=id.Contains("brewing_stand"),anvil=id.Contains("anvil"),enchant=id.Contains("enchanting_table");
            bool chest=containerOpen && (id.Contains("chest")||id.Contains("barrel")),other=containerOpen&&!furnace&&!craft&&!chest;
            int rows=chest ? Math.Max(1,(slots.Count+8)/9) : 3;
            float height=chest ? rows*18+114 : id.Contains("hopper") ? 133 : 166,inventoryY=chest ? rows*18+31 : 84;
            left=(Screen.width-176*scale)/2;top=(Screen.height-height*scale)/2;
            if(chest) {Tile("gui_generic_54",0,0,176,rows*18+17,0,0,176,rows*18+17);Tile("gui_generic_54",0,rows*18+17,176,96,0,126,176,96);}
            else Tile((string)container?["texture"]??(furnace ? "gui_furnace" : craft ? "gui_crafting_table" : other ? "gui_"+id.Split(':').Last().Replace("damaged_anvil","anvil").Replace("chipped_anvil","anvil").Replace("dropper","dispenser") : "gui_inventory"),0,0,176,height,0,0,176,height);
            if(!containerOpen) {
                var equipment=stats?["equipment"] as JArray;
                for(int i=0;i<4;i++) Slot(equipment?[i],5+i,8,8+i*18);
                Slot(stats?["offhand"],45,77,62);
                var grid=snapshot["playerCraft"] as JArray;
                for(int i=1;i<=4;i++) Slot(grid?[i],i,98+(i-1)%2*18,18+(i-1)/2*18);
                Slot(grid?[0],0,154,28);
                Portrait();Text("Crafting",left+96*scale,top+6*scale,scale,new Color(0.25f,0.25f,0.25f));
            } else {
                Text((string)container["title"]??(furnace ? "Furnace" : craft ? "Crafting" : brewing ? "Brewing Stand" : anvil ? "Repair & Name" : enchant ? "Enchant" : chest ? "Chest" : id.Split(':').Last().Replace('_',' ')),left+8*scale,top+6*scale,scale,new Color(0.25f,0.25f,0.25f));
                for(int i=0;i<slots.Count;i++) {
                    float x,y;
                    if(furnace) {x=i==2 ? 116 : 56;y=i==0 ? 17 : i==1 ? 53 : 35;}
                    else if(craft) {x=i==0 ? 124 : 30+(i-1)%3*18;y=i==0 ? 35 : 17+(i-1)/3*18;}
                    else {x=8+i%9*18;y=18+i/9*18;}
                    x=(float?)slots[i]["x"] ?? x;y=(float?)slots[i]["y"] ?? y;Slot(slots[i],(int)slots[i]["slot"],x,y);
                }
                if(furnace) {
                    float fuel=Mathf.Clamp01((float?)container["fuel"] ?? 0),cook=Mathf.Clamp01((float?)container["cook"] ?? 0);
                    if(fuel>0) Tile("gui_furnace",56,36+14*(1-fuel),14,14*fuel,176,14*(1-fuel),14,14*fuel);
                    if(cook>0) {float width=Mathf.Min(24,Mathf.Floor(24*cook)+1);Tile("gui_furnace",79,34,width,16,176,14,width,16);}
                }
                if(brewing) {float progress=1-((int?)container["brew"] ?? 0)/400f;if((int?)container["brew"]>0) Tile("gui_brewing_stand",97,16,9,28*progress,176,0,9,28*progress);int fuel=(int?)container["brewFuel"] ?? 0;if(fuel>0) Tile("gui_brewing_stand",60,44,18*fuel/20f,4,176,29,18*fuel/20f,4);}
                if(anvil) {
                    string source=container["syncId"]+"|"+slots[0];
                    if(source!=anvilSource) {anvilSource=source;anvilName=((int?)slots[0]["count"]??0)>0?(string)slots[0]["name"]??"":"";}
                    GUI.SetNextControlName("HollowCraftAnvilName");
                    string name=GUI.TextField(new Rect(left+62*scale,top+24*scale,103*scale,12*scale),anvilName,50);
                    if(name!=anvilName) {anvilName=name;runtime.Send(new {type="anvilName",scene=runtime.Room,name});}
                    int cost=(int?)container["cost"] ?? 0;if(cost>0) Text("Cost: "+cost,left+85*scale,top+69*scale,scale,Color.green);
                }
                if(enchant) {
                    var choices=container["enchants"] as JArray;for(int i=0;i<3;i++) {int cost=(int?)choices?[i]?["cost"] ?? 0;
                        if(cost>0&&Button(((string)choices[i]["name"])+" "+cost,left+60*scale,top+(14+i*19)*scale,108)) ContainerButton(container,i);
                    }
                }
                DrawContainerChoices(container);
                Text("Inventory",left+8*scale,top+(inventoryY-11)*scale,scale,new Color(0.25f,0.25f,0.25f));
            }
            for(int i=0;i<36;i++) {
                int serverSlot=i<9 ? 36+i : i;
                float x=8+i%9*18,y=i<9 ? inventoryY+58 : inventoryY+(i/9-1)*18;
                if(containerOpen) {var map=((JArray)container["inventorySlots"]).FirstOrDefault(m=>(int)m["inventory"]==i);if(map==null) continue;serverSlot=(int)map["slot"];x=(float?)map["x"]??x;y=(float?)map["y"]??y;}
                Slot(inventory[i],serverSlot,x,y);
            }
            InventoryInput(snapshot,new Rect(left,top,176*scale,height*scale));
        }
        private void ContainerButton(JObject container,int button) {CancelDrag();runtime.Send(new {type="containerButton",scene=runtime.Room,button,syncId=(int?)container["syncId"]??-1});}
        private void DrawContainerChoices(JObject container) {
            string kind=(string)container["selector"];if(kind!="loom"&&kind!="stonecutter")return;
            string key=container["syncId"]+"|"+container["slots"]?[0]?["id"];
            if(key!=choiceKey) {choiceKey=key;choiceRow=0;}
            bool loom=kind=="loom";var choices=container["choices"] as JArray??new JArray();
            int visibleRows=loom?4:3,cellWidth=loom?14:16,cellHeight=loom?14:18;
            int maximum=Math.Max(0,(choices.Count+3)/4-visibleRows);choiceRow=Mathf.Clamp(choiceRow,0,maximum);
            float x=loom?60:52,y=loom?13:16;string texture=loom?"gui_loom":"gui_stonecutter";
            var grid=new Rect(left+x*scale,top+y*scale,4*cellWidth*scale,visibleRows*cellHeight*scale);var e=Event.current;
            if(e.type==EventType.ScrollWheel&&grid.Contains(pointer)) {choiceRow=Mathf.Clamp(choiceRow+(e.delta.y>0?1:-1),0,maximum);e.Use();}
            var scrollbar=new Rect(left+119*scale,top+15*scale,12*scale,56*scale);
            if(maximum>0&&(e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&e.button==0&&scrollbar.Contains(pointer)) {choiceRow=Mathf.RoundToInt(Mathf.Clamp01((pointer.y-scrollbar.y-7.5f*scale)/(41*scale))*maximum);e.Use();}
            Region(texture,new Rect(scrollbar.x,scrollbar.y+(maximum>0?(float)choiceRow/maximum:0)*41*scale,12*scale,15*scale),maximum>0?176:188,0,12,15);
            if(loom) {
                var slots=container["slots"] as JArray;
                if(slots!=null)for(int i=0;i<3&&i<slots.Count;i++)if(((int?)slots[i]["count"]??0)<=0)Tile(texture,(float)slots[i]["x"],(float)slots[i]["y"],16,16,176+i*16,0,16,16);
                if((bool?)container["patternFull"]==true)return;
                var output=slots?.FirstOrDefault(slot=>(int?)slot["slot"]==3);
                if(output!=null)Item(output,new Rect(left+139*scale,top+13*scale,24*scale,40*scale));
            }
            for(int i=0;i<visibleRows*4&&choiceRow*4+i<choices.Count;i++) {
                var choice=choices[choiceRow*4+i];var rect=new Rect(left+(x+i%4*cellWidth)*scale,top+(y+i/4*cellHeight)*scale,cellWidth*scale,cellHeight*scale);
                bool hover=rect.Contains(pointer),selected=(int?)container["selectedChoice"]==(int?)choice["button"];
                Region(texture,rect,0,166+(selected?cellHeight:hover?cellHeight*2:0),cellWidth,cellHeight);
                Item(choice,loom?rect:new Rect(rect.x,rect.y,16*scale,16*scale));
                if(!hover)continue;hovered=choice;
                if(e.type==EventType.MouseDown&&e.button==0) {ContainerButton(container,(int)choice["button"]);e.Use();}
            }
        }
        private void DrawCatalog()
        {
            GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            var groups=runtime.CatalogGroups??new JArray();catalogTab=Mathf.Clamp(catalogTab,0,Math.Max(0,groups.Count-1));
            var group=groups.Count>0?groups[catalogTab]:null;string type=(string)group?["type"]??"SEARCH";
            if(type=="INVENTORY") {
                DrawCreativeInventory(groups);return;
            }
            left=(Screen.width-195*scale)/2;top=(Screen.height-136*scale)/2;containerOpen=false;drawnSlots.Clear();
            Tile(type=="SEARCH"?"gui_creative_tab_item_search":"gui_creative_tab_items",0,0,195,136,0,0,195,136);
            if(type=="SEARCH") {
                GUI.SetNextControlName("HollowCraftCreativeSearch");
                string search=GUI.TextField(new Rect(left+82*scale,top+6*scale,80*scale,12*scale),catalogSearch,64);
                if(search!=catalogSearch) {catalogSearch=search;catalogPage=0;}
            } else Text((string)group?["name"]??"Creative Inventory",left+8*scale,top+6*scale,scale,new Color(.25f,.25f,.25f));
            var catalog=runtime.Catalog??new JArray();var indices=group?["entries"] as JArray;
            var entries=(indices!=null?indices.Where(i=>(int)i>=0&&(int)i<catalog.Count).Select(i=>catalog[(int)i]):catalog.AsEnumerable())
                .Where(e=>type!="SEARCH"||((string)e["id"]).IndexOf(catalogSearch,StringComparison.OrdinalIgnoreCase)>=0||((string)e["name"]??"").IndexOf(catalogSearch,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            int rows=Math.Max(0,(entries.Length+8)/9-5);catalogPage=Mathf.Clamp(catalogPage,0,rows);
            var e=Event.current;var grid=new Rect(left+8*scale,top+18*scale,162*scale,90*scale);
            if(e.type==EventType.ScrollWheel&&grid.Contains(pointer)) {catalogPage=Mathf.Clamp(catalogPage+(e.delta.y>0?1:-1),0,rows);e.Use();}
            var scrollbar=new Rect(left+175*scale,top+18*scale,12*scale,90*scale);
            if((e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&e.button==0&&scrollbar.Contains(pointer)) {
                catalogPage=Mathf.RoundToInt(Mathf.Clamp01((pointer.y-scrollbar.y-7.5f*scale)/(75*scale))*rows);e.Use();
            }
            Region("gui_creative_tabs",new Rect(scrollbar.x,scrollbar.y+(rows>0?(float)catalogPage/rows:0)*75*scale,12*scale,15*scale),rows>0?232:244,0,12,15);
            for(int i=0;i<45&&catalogPage*9+i<entries.Length;i++) {
                var entry=entries[catalogPage*9+i];var rect=new Rect(left+(9+i%9*18)*scale,top+(18+i/9*18)*scale,16*scale,16*scale);Item(entry,rect);
                if(!rect.Contains(pointer))continue;hovered=entry;
                GUI.color=new Color(1,1,1,.4f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;
                if((bool?)entry["locked"]==true)continue;
                if(e.type==EventType.MouseDown&&e.button<=2) {
                    CancelDrag();bool shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
                    TakeCreative(entry,e.button==1?1:64,shift?"inventory":"cursor",0);e.Use();
                } else if(e.type==EventType.KeyDown&&e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9) {
                    if(Input.GetKey(KeyCode.C)||Input.GetKey(KeyCode.X)) {e.Use();continue;}
                    TakeCreative(entry,64,"slot",(int)e.keyCode-(int)KeyCode.Alpha1);e.Use();
                }
            }
            var inventory=runtime.LatestSnapshot?["inventory"] as JArray;
            if(inventory!=null)for(int i=0;i<9;i++)Slot(inventory[i],36+i,9+i*18,112);
            DrawCreativeTabs(groups,195,136);
            if(e.type==EventType.MouseDown&&!new Rect(left,top-28*scale,195*scale,192*scale).Contains(pointer)) {runtime.Send(new {type="creativeDiscard",scene=runtime.Room});e.Use();}
            InventoryInput(runtime.LatestSnapshot,new Rect(left,top-28*scale,195*scale,192*scale));
        }
        private void TakeCreative(JToken entry,int count,string destination,int slot) {
            runtime.Send(new {type="creativeItem",scene=runtime.Room,catalogIndex=(int)entry["catalogIndex"],catalogRevision=runtime.CatalogRevision,count,destination,slot});
        }
        private void DrawCreativeInventory(JArray groups) {
            left=(Screen.width-195*scale)/2;top=(Screen.height-136*scale)/2;containerOpen=false;drawnSlots.Clear();
            DrawCreativeTabs(groups,195,136);
            Tile("gui_creative_tab_inventory",0,0,195,136,0,0,195,136);
            var snapshot=runtime.LatestSnapshot;var stats=runtime.Stats;var equipment=stats?["equipment"] as JArray;
            for(int i=0;i<4;i++)Slot(equipment?[i],5+i,54+(i/2)*54,6+(i%2)*27);
            Slot(stats?["offhand"],45,35,20);
            Portrait(73,6,30,40);
            var inventory=snapshot?["inventory"] as JArray;
            if(inventory!=null)for(int i=0;i<36;i++)Slot(inventory[i],i<9?36+i:i,9+i%9*18,i<9?112:54+(i/9-1)*18);
            var delete=new Rect(left+173*scale,top+112*scale,16*scale,16*scale);
            var e=Event.current;
            if(delete.Contains(pointer)) {
                GUI.color=new Color(1,1,1,.4f);GUI.DrawTexture(delete,Texture2D.whiteTexture);GUI.color=Color.white;
                if(e.type==EventType.MouseDown&&e.button<=1) {CancelDrag();runtime.Send(new {type="creativeDiscard",scene=runtime.Room,all=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)});e.Use();}
            }
            DrawCreativeTabs(groups,195,136);
            InventoryInput(snapshot,new Rect(left,top-28*scale,195*scale,192*scale));
        }
        private void DrawCreativeTabs(JArray groups,int width,int height) {
            for(int i=0;i<groups.Count;i++) {
                var group=groups[i];bool bottom=(string)group["row"]=="BOTTOM",special=(bool?)group["special"]??((string)group["type"]=="SEARCH"||(string)group["type"]=="INVENTORY");
                int column=(int)group["column"];float x=special?width-27*(7-column)+1:column*27,y=bottom?height-4:-28;
                var rect=new Rect(left+x*scale,top+y*scale,26*scale,32*scale);
                Region("gui_creative_tabs",rect,column*26,(bottom?64:0)+(i==catalogTab?32:0),26,32);
                Item(group["icon"],new Rect(rect.x+5*scale,rect.y+(bottom?7:9)*scale,16*scale,16*scale));
                if(rect.Contains(pointer)) {
                    hovered=group["icon"];
                    if(Event.current.type==EventType.MouseDown&&Event.current.button==0) {CancelDrag();catalogTab=i;catalogPage=0;GUI.FocusControl(null);Event.current.Use();}
                }
            }
        }
        private void DrawShop() {
            GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            float x=(Screen.width-210*scale)/2,y=(Screen.height-166*scale)/2;Region("gui_generic_54",new Rect(x,y,210*scale,166*scale),0,0,176,166);
            GUI.color=new Color(.77f,.77f,.77f);GUI.DrawTexture(new Rect(x+5*scale,y+20*scale,200*scale,118*scale),Texture2D.whiteTexture);GUI.color=Color.white;
            if(dialogueFont==null) dialogueFont=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",18);
            var style=new GUIStyle(GUI.skin.label){font=dialogueFont,fontSize=Mathf.RoundToInt(7*scale),wordWrap=true};style.normal.textColor=new Color(.15f,.15f,.15f);
            Text("Shop - Geo: "+PlayerData.instance.geo,x+8*scale,y+7*scale,scale,new Color(.15f,.15f,.15f));
            if(NativeShop.Confirming) {
                GUI.Label(new Rect(x+12*scale,y+35*scale,185*scale,35*scale),"确认购买？费用："+NativeShop.Cost,style);
                if(Button("Yes",x+12*scale,y+90*scale,85)) NativeShop.Decide(true);
                if(Button("No",x+113*scale,y+90*scale,85)) NativeShop.Decide(false);
            } else {
                int pages=Math.Max(1,(NativeShop.Items.Count+5)/6);shopPage=Mathf.Clamp(shopPage,0,pages-1);int count=Math.Min(6,NativeShop.Items.Count-shopPage*6);for(int i=0;i<count;i++) {var item=NativeShop.Items[shopPage*6+i];GUI.Label(new Rect(x+8*scale,y+(25+i*19)*scale,130*scale,19*scale),(string)item["name"]+"  "+item["cost"],style);
                    bool enabled=GUI.enabled;GUI.enabled=(bool)item["canBuy"];if(Button("Buy",x+144*scale,y+(23+i*19)*scale,56)) NativeShop.Buy((int)item["index"]);GUI.enabled=enabled;
                }
                if(Button("Close",x+55*scale,y+142*scale,100)) NativeShop.Close();
                if(pages>1) {if(Button("<",x+8*scale,y+142*scale,34)) shopPage--;if(Button(">",x+168*scale,y+142*scale,34)) shopPage++;}
            }
        }
        private void Portrait(float x=26,float y=8,float width=51,float height=70)
        {
            if(SteveAvatar.NativeTexture!=null) {GUI.DrawTextureWithTexCoords(new Rect(left+x*scale,top+y*scale,width*scale,height*scale),SteveAvatar.NativeTexture,new Rect(.25f,.14f,.5f,.65f));return;}
            if(SteveAvatar.PortraitTexture!=null) {GUI.DrawTexture(new Rect(left+x*scale,top+y*scale,width*scale,height*scale),SteveAvatar.PortraitTexture,ScaleMode.ScaleToFit);return;}
            if(width!=51) {Tile("steve_skin",x+width*.35f,y+2,width*.3f,height*.3f,8,8,8,8);Tile("steve_skin",x+width*.35f,y+height*.35f,width*.3f,height*.5f,20,20,8,12);return;}
            Tile("steve_skin",45,13,16,16,8,8,8,8);Tile("steve_skin",45,29,16,24,20,20,8,12);
            Tile("steve_skin",37,29,8,24,44,20,4,12);Tile("steve_skin",61,29,8,24,36,52,4,12);
            Tile("steve_skin",45,53,8,24,4,20,4,12);Tile("steve_skin",53,53,8,24,20,52,4,12);
        }
    }
}
