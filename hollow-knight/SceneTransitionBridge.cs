using UnityEngine;

namespace HollowCraft
{
    // These hooks run synchronously before HK starts moving the hero through a gate.
    // IsInSceneTransition alone becomes false before some entrance coroutines finish.
    public static class SceneTransitionBridge
    {
        public static void Begin() => HollowCraftRuntime.Instance?.BeginNativeTransition();
        public static void Enter(HeroController hero) {
            if(HollowCraftRuntime.Instance?.OpeningTransition==true)return;
            Begin();
        }
        public static void Arrived(HeroController hero) => HollowCraftRuntime.Instance?.NativeSceneArrived(hero);
    }
}
