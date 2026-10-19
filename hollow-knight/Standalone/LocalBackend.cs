using System;
using Newtonsoft.Json.Linq;

namespace HollowCraft.Standalone
{
    // In-process command adapter. No sockets, worker processes, JVM or Minecraft world.
    internal sealed class LocalBackend : IGameplayBackend
    {
        private readonly NativeGame game;
        public bool Connected {get;private set;}=true;
        public bool Local=>true;
        public string Campaign {get;set;}="";
        public int SceneVisit {get;set;}
        public string LastError {get;private set;}="Native C# backend";
        internal LocalBackend(string root) {game=new NativeGame(System.IO.Path.Combine(root,"hollowcraft-native"),NativeAssets.Root,Environment.GetEnvironmentVariable("HOLLOWCRAFT_SETTINGS"));}
        public void Send(object value) {
            if(!Connected)return;
            var packet=JObject.FromObject(value);
            if(packet["scene"]!=null) {if(packet["campaign"]==null)packet["campaign"]=Campaign;if(packet["visit"]==null)packet["visit"]=SceneVisit;}
            game.Enqueue(packet);
        }
        public bool TryRead(out JObject value)=>game.TryRead(out value);
        public void Pump(float elapsed) {if(!Connected)return;try{game.Pump(elapsed);}catch(Exception error){Connected=false;LastError=error.Message;UnityEngine.Debug.LogError("[HollowCraft] Native rules stopped safely: "+error);}}
        public void Dispose() {if(!Connected)return;Connected=false;game.Dispose();}
    }
}
