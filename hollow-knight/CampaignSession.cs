using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HollowCraft
{
    // Identity belongs to a new campaign, rather than just its reusable save-slot number.
    public static class CampaignSession
    {
        private static int profile=-1;
        private static JObject entries;
        internal static string Id;
        internal static bool Legacy,IntroPending;
        private static string FileName => Path.Combine(HollowCraftBootstrap.SaveRoot(),"hollowcraft-campaigns.json");
        private static void Read() {
            if(entries!=null)return;
            try {entries=File.Exists(FileName)?JObject.Parse(File.ReadAllText(FileName)):new JObject();}
            catch(Exception error) {Debug.LogError("[HollowCraft] Campaign identity: "+error);throw;}
        }
        private static void Save() {
            string temp=FileName+".tmp";File.WriteAllText(temp,entries.ToString());
            if(File.Exists(FileName))File.Replace(temp,FileName,FileName+".bak");else File.Move(temp,FileName);
        }
        public static void NewGame(GameManager manager) {
            Read();profile=manager.profileID;Id="campaign-"+Guid.NewGuid().ToString("N");Legacy=false;IntroPending=true;
            entries[profile.ToString()]=new JObject { ["id"]=Id,["legacy"]=false,["introDone"]=false };Save();
            Debug.Log("[HollowCraft] New campaign slot="+profile+" id="+Id);
        }
        internal static void Select(GameManager manager) {
            Read();if(profile==manager.profileID&&Id!=null)return;profile=manager.profileID;
            var entry=entries[profile.ToString()] as JObject;
            if(entry==null) {entry=new JObject { ["id"]="legacy-slot-"+profile,["legacy"]=true,["introDone"]=true };entries[profile.ToString()]=entry;Save();}
            Id=(string)entry["id"];Legacy=(bool?)entry["legacy"]??false;IntroPending=!((bool?)entry["introDone"]??true);
        }
        internal static void Landed() {
            if(!IntroPending)return;IntroPending=false;var entry=entries?[profile.ToString()] as JObject;
            if(entry!=null){entry["introDone"]=true;Save();}
        }
    }
}
