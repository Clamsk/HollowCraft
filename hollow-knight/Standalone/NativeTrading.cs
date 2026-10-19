using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    internal static class NativeTradeData
    {
        internal static JArray Trades=new JArray();
        internal static void Load(string root){string path=Path.Combine(root,"native-trades.json");if(File.Exists(path))Trades=(JArray)JObject.Parse(File.ReadAllText(path))["trades"];}
    }
    internal sealed partial class NativeGame
    {
        private JObject tradingMob;
        private static readonly Dictionary<string,string> professions=new Dictionary<string,string>{{"composter","farmer"},{"blast_furnace","armorer"},{"smithing_table","toolsmith"},{"grindstone","weaponsmith"},{"loom","shepherd"},{"fletching_table","fletcher"},{"cartography_table","cartographer"},{"lectern","librarian"},{"brewing_stand","cleric"},{"barrel","fisherman"},{"smoker","butcher"},{"stonecutter","mason"},{"cauldron","leatherworker"}};
        private bool OpenTrading(JObject mob) {
            if((string)mob["kind"]!="minecraft:villager"||(bool?)mob["baby"]==true)return false;
            if(mob["profession"]==null){var site=Room.Blocks.Values.FirstOrDefault(c=>Math.Abs(Number(c,"x")-Number(mob,"x"))<5&&Math.Abs(Number(c,"y")-Number(mob,"y"))<3&&professions.ContainsKey(((string)c["id"]).Split(':').Last()));if(site==null){Notice("Villager: place a job site nearby");return true;}mob["profession"]="minecraft:"+professions[((string)site["id"]).Split(':').Last()];mob["jobX"]=site["x"].DeepClone();mob["jobY"]=site["y"].DeepClone();}
            EnsureTrades(mob);CloseContainer();tradingMob=mob;container=new NativeContainer("hollowcraft:trader",3){Sync=++syncId,Choice=0};return true;
        }
        private void EnsureTrades(JObject mob) {
            int xp=(int?)mob["tradeXp"]??0,level=xp>=250?5:xp>=150?4:xp>=70?3:xp>=10?2:1;
            var offers=mob["trades"] as JArray;if(offers==null){offers=new JArray();mob["trades"]=offers;}int seed=17;foreach(char letter in (string)mob["uuid"]??"")seed=unchecked(seed*31+letter);var rng=new Random(seed&int.MaxValue);
            for(int rank=1;rank<=level;rank++) {
                var factories=NativeTradeData.Trades.OfType<JObject>().Where(t=>(string)t["profession"]==(string)mob["profession"]&&(int)t["level"]==rank).GroupBy(t=>(int)t["factory"]).OrderBy(_=>rng.Next()).Take(2).ToArray();
                foreach(var factory in factories){var sample=factory.ElementAt(rng.Next(factory.Count()));if(offers.OfType<JObject>().Any(t=>(int)t["level"]==rank&&(int)t["factory"]==factory.Key))continue;var added=(JObject)sample.DeepClone();added["uses"]=0;offers.Add(added);}
            }
            mob["tradeLevel"]=level;
        }
        private static bool BuyMatches(NativeStack item,NativeStack price)=>price.Empty?item.Empty:!item.Empty&&item.Id==price.Id&&item.Count>=price.Count&&price.Data.Properties().All(p=>JToken.DeepEquals(item.Data[p.Name],p.Value));
        private WorkResult TradePreview() {
            var plan=new WorkResult();if(tradingMob==null||Number(tradingMob,"health")<=0)return plan;var offers=tradingMob["trades"] as JArray;int choice=container.Choice;if(offers==null||choice<0||choice>=offers.Count)return plan;var offer=(JObject)offers[choice];if((int)offer["uses"]>=(int)offer["maxUses"])return plan;
            var first=NativeStack.Read(offer["buy"]);var second=NativeStack.Read(offer["second"]);if(BuyMatches(container.Slots[0],first)&&BuyMatches(container.Slots[1],second))plan.Consume=new[]{first.Count,second.Count};else if(BuyMatches(container.Slots[1],first)&&BuyMatches(container.Slots[0],second))plan.Consume=new[]{second.Count,first.Count};else return plan;
            plan.Stack=NativeStack.Read(offer["sell"]);return plan;
        }
        private void TradeTaken() {var offer=(JObject)tradingMob["trades"][container.Choice];offer["uses"]=(int)offer["uses"]+1;tradingMob["tradeXp"]=(int?)tradingMob["tradeXp"]??0;tradingMob["tradeXp"]=(int)tradingMob["tradeXp"]+(int)offer["experience"];AddExperience(random.Next(3,7));EnsureTrades(tradingMob);Sound("minecraft:entity.villager.yes",.8);}
        private void TickTrading(JObject mob) {
            if(mob["trades"]==null||tick%200!=0)return;int bx=(int?)mob["jobX"]??int.MaxValue,by=(int?)mob["jobY"]??int.MaxValue;
            if(Math.Abs(Number(mob,"x")-bx)>3||Math.Abs(Number(mob,"y")-by)>3||BlockAt(bx,by)==null||tick-Number(mob,"restockedAt",tick)<12000){if(mob["restockedAt"]==null)mob["restockedAt"]=tick;return;}
            foreach(JObject offer in (JArray)mob["trades"])offer["uses"]=0;mob["restockedAt"]=tick;
        }
    }
}
