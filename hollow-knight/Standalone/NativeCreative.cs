using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal sealed partial class NativeGame
    {
        private JArray nativeGroups=new JArray();private int catalogRevision;private NativeStack[][] savedHotbars;
        private List<NativeStack> BuildCatalogStacks() {
            var items=new List<NativeStack>();nativeGroups=new JArray();var map=new Dictionary<int,int>();
            if(NativeData.CreativeStacks.Count>0){for(int i=0;i<NativeData.CreativeStacks.Count;i++){var item=NativeStack.Read(NativeData.CreativeStacks[i]);if(!NativeCatalog.Visible(item.Id))continue;map[i]=items.Count;items.Add(item);}
                foreach(JObject source in NativeData.CreativeGroups){var tab=(JObject)source.DeepClone();string id=((string)tab["id"]).Split(':').Last();tab["name"]=id.Replace('_',' ');tab["icon"]=NativeStack.Read(tab["icon"]).Json();tab["entries"]=new JArray(((JArray)tab["entries"]).Select(v=>(int)v).Where(map.ContainsKey).Select(v=>map[v]));nativeGroups.Add(tab);}
            } else {
                items.AddRange(NativeCatalog.Items.Values.Where(i=>i.Id!="minecraft:air"&&NativeCatalog.Visible(i.Id)).Select(i=>NativeStack.New(i.Id,i.Maximum)));
                foreach(string kind in new[]{"potion","splash_potion","lingering_potion","tipped_arrow"})foreach(string potion in NativeData.Potions.Keys.Where(id=>id!="minecraft:empty")){var item=NativeStack.New(kind);item.Data["Potion"]=potion;items.Add(item);}
                string[] names={"blocks","equipment","items","food","redstone","transport"};for(int i=0;i<names.Length;i++){string name=names[i];nativeGroups.Add(new JObject{["id"]=name,["name"]=name,["type"]="CATEGORY",["column"]=i,["row"]="TOP",["special"]=false,["icon"]=NativeStack.New(name=="blocks"?"cobblestone":"iron_sword").Json(),["entries"]=new JArray(items.Select((item,index)=>new{item,index}).Where(p=>p.item.Definition.Group==name).Select(p=>p.index))});}
                nativeGroups.Add(new JObject{["id"]="search",["name"]="Search Items",["type"]="SEARCH",["column"]=6,["row"]="TOP",["special"]=true,["icon"]=NativeStack.New("compass").Json(),["entries"]=new JArray(Enumerable.Range(0,items.Count))});
                nativeGroups.Add(new JObject{["id"]="inventory",["name"]="Survival Inventory",["type"]="INVENTORY",["column"]=6,["row"]="BOTTOM",["special"]=true,["icon"]=NativeStack.New("chest").Json(),["entries"]=new JArray()});
            }
            LoadHotbars();foreach(JObject tab in nativeGroups)if((string)tab["type"]=="HOTBAR"){var entries=new JArray();foreach(var row in savedHotbars)foreach(var stack in row){entries.Add(items.Count);items.Add(stack.Copy());}tab["entries"]=entries;}
            return items;
        }
        private void LoadHotbars(){if(savedHotbars!=null)return;savedHotbars=Enumerable.Range(0,9).Select(_=>NativeInventory.Empty(9)).ToArray();string path=Path.Combine(root,"creative-hotbars.json");if(!File.Exists(path))return;var data=JArray.Parse(File.ReadAllText(path));if(data.Count!=9||data.Any(row=>!(row is JArray values)||values.Count!=9))throw new InvalidDataException("Invalid saved hotbar shape");for(int row=0;row<9;row++)for(int column=0;column<9;column++)savedHotbars[row][column]=NativeStack.Read(data[row][column]);}
        private void CreativeHotbar(JObject packet) {
            if(!Creative)return;int slot=(int?)packet["slot"]??-1;if(slot<0||slot>=9)return;string path=Path.Combine(root,"creative-hotbars.json");
            LoadHotbars();
            if((string)packet["action"]=="save"){savedHotbars[slot]=Inventory.Slots.Take(9).Select(s=>s.Copy()).ToArray();string temp=path+".tmp";File.WriteAllText(temp,new JArray(savedHotbars.Select(row=>new JArray(row.Select(s=>s.Json())))).ToString(Newtonsoft.Json.Formatting.None));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);catalogStacks=null;Catalog();Notice("Saved hotbar "+(slot+1));}
            else if((string)packet["action"]=="load"){for(int i=0;i<9;i++)Inventory.Slots[i]=savedHotbars[slot][i].Copy();Notice("Loaded hotbar "+(slot+1));}
        }
    }
}
