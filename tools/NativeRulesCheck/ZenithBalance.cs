using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using HollowCraft.Standalone;

internal static partial class Program
{
    private static void ZenithBalance(string root,string assets) {
        using(var game=new NativeGame(Path.Combine(root,"zenith-balance"),assets)) {
            const string scene="zenith",campaign="damage";
            Send(game,scene,campaign,"scene","exactX",0,"exactY",1);Latest(game);
            game.Inventory.Slots[0]=NativeStack.New("hollowcraft_bridge:zenith");
            var boss=new JObject{["id"]=9201,["generation"]=1,["health"]=1024,["maxHealth"]=1024,["nativeMaxHealth"]=2000,["nativeHurtTicks"]=4,["x"]=1,["y"]=-5,["width"]=12,["height"]=12};
            Send(game,scene,campaign,"pose","enemies",new JArray(boss));Latest(game);
            JObject state=null;
            for(int attack=1;attack<=10;attack++) {
                Send(game,scene,campaign,"zenithUse","held",true,"x",1,"y",2);Latest(game);
                Send(game,scene,campaign,"zenithUse","held",false);Latest(game);
                for(int i=0;i<25;i++)game.Pump(.05);state=Latest(game);
                double native=(double)state["enemies"][0]["health"]/1024*2000;
                Check(Math.Abs(native-Math.Max(0,2000-attack*200))<.001,"Zenith complete use "+attack+" deals exactly 200 native HP without repeated/critical blade damage");
            }
            Check((double)state["enemies"][0]["health"]==0,"A 2000 HP unphased target dies on the tenth complete Zenith use");

            // One native transition may refill/change the proxy generation while
            // the other two swords of the accepted attack are still in flight.
            boss["generation"]=2;boss["health"]=1024;boss["phaseFloor"]=1024d*1850/2000;
            Send(game,scene,campaign,"pose","enemies",new JArray(boss));Latest(game);
            Send(game,scene,campaign,"zenithUse","held",true,"x",1,"y",2);state=Latest(game);
            Check(Math.Abs((double)state["enemies"][0]["health"]-1024d*1850/2000)<.001,"Zenith clips at the next native phase boundary");
            boss["generation"]=3;boss["health"]=1024d*1850/2000;boss["phaseFloor"]=0;
            Send(game,scene,campaign,"pose","enemies",new JArray(boss));Latest(game);
            Send(game,scene,campaign,"zenithUse","held",false);Latest(game);
            for(int i=0;i<25;i++)game.Pump(.05);state=Latest(game);
            Check(Math.Abs((double)state["enemies"][0]["health"]-1024d*1850/2000)<.001,"Remaining swords cannot spend another damage budget after native generation/phase changes");
            Send(game,scene,campaign,"zenithUse","held",true,"x",1,"y",2);Latest(game);
            Send(game,scene,campaign,"zenithUse","held",false);Latest(game);
            for(int i=0;i<25;i++)game.Pump(.05);state=Latest(game);
            Check(Math.Abs((double)state["enemies"][0]["health"]-1024d*1650/2000)<.001,"The next attack resumes normal damage without carrying clipped phase damage");

            boss["generation"]=4;boss["health"]=1024;boss["nativeHurtTicks"]=0;
            Send(game,scene,campaign,"pose","enemies",new JArray(boss));Latest(game);
            for(int i=0;i<20;i++){Send(game,scene,campaign,"zenithUse","held",true,"x",1,"y",2);Latest(game);}
            Send(game,scene,campaign,"zenithUse","held",false);Latest(game);
            for(int i=0;i<25;i++)game.Pump(.05);state=Latest(game);
            Check(Math.Abs((double)state["enemies"][0]["health"]-1024d*1600/2000)<.001,"Holding Zenith for one second emits two complete 200-HP attacks, even without native hurt immunity");

            Send(game,scene,campaign,"zenithUse","held",true,"x",1,"y",2);Latest(game);
            Send(game,"next",campaign,"scene","exactX",0,"exactY",1);state=Latest(game);
            var fresh=(JObject)boss.DeepClone();fresh["id"]=9202;fresh["health"]=1024;
            Send(game,"next",campaign,"pose","enemies",new JArray(fresh));Latest(game);
            for(int i=0;i<25;i++)game.Pump(.05);state=Latest(game);
            Check((double)state["enemies"][0]["health"]==1024,"Scene changes cancel held input, queued swords and the current Zenith volley");

            // Independent native HP scales must not depend on the proxy HP cap.
            game.Inventory.Slots[0]=NativeStack.New("hollowcraft_bridge:zenith");
            fresh["generation"]=2;fresh["health"]=fresh["maxHealth"]=600;fresh["nativeMaxHealth"]=1000;
            var second=(JObject)fresh.DeepClone();second["id"]=9203;second["health"]=second["maxHealth"]=1024;second["nativeMaxHealth"]=10000;
            Send(game,"next",campaign,"pose","enemies",new JArray(fresh,second));Latest(game);
            Send(game,"next",campaign,"zenithUse","held",true,"x",1,"y",2);Latest(game);
            Send(game,"next",campaign,"zenithUse","held",false);Latest(game);
            for(int i=0;i<25;i++)game.Pump(.05);state=Latest(game);
            var values=(JArray)state["enemies"];
            Check(Math.Abs((double)values.First(v=>(int)v["id"]==9202)["health"]-480)<.001,"An uncapped native proxy converts 200 HK HP correctly");
            Check(Math.Abs((double)values.First(v=>(int)v["id"]==9203)["health"]-1024d*.98)<.001,"A capped 10000-HP target receives its own 200-HP budget in the same volley");
        }
    }
}
