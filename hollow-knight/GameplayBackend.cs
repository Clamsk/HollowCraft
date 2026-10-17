using System;
using Newtonsoft.Json.Linq;

namespace HollowCraft
{
    internal interface IGameplayBackend : IDisposable
    {
        bool Connected {get;}
        bool Local {get;}
        string Campaign {get;set;}
        int SceneVisit {get;set;}
        string LastError {get;}
        void Send(object value);
        bool TryRead(out JObject value);
        void Pump(float elapsed);
    }

    internal static class GameplayBackend
    {
        internal static bool Standalone=>!string.Equals(Environment.GetEnvironmentVariable("HOLLOWCRAFT_BACKEND"),"bridge",StringComparison.OrdinalIgnoreCase);
        internal static IGameplayBackend Create()=>Standalone?(IGameplayBackend)new Standalone.LocalBackend(HollowCraftBootstrap.SaveRoot()):new BridgeClient();
    }
}
