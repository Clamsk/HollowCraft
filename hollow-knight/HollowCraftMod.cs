using UnityEngine;
using System;
using System.IO;

namespace HollowCraft
{
    // A single StartManager.Awake call loads this component. No old-version game assembly is substituted.
    public static class HollowCraftBootstrap
    {
        private static GameObject root;
        public static void Initialize()
        {
            if (root != null) return;
            root = new GameObject("HollowCraft.Runtime");
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.AddComponent<HollowCraftRuntime>();
            root.AddComponent<SteveAvatar>();
            root.AddComponent<MinecraftEnvironment>();
            Debug.Log("[HollowCraft] 0.8 loaded. Campaign inventory banks, three-hit iron sword balance, opening water and jump building; localhost:31674.");
        }
        public static string SaveRoot()
        {
            string path = Environment.GetEnvironmentVariable("HOLLOWCRAFT_SAVE_ROOT");
            if (string.IsNullOrWhiteSpace(path)) return Application.persistentDataPath;
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
