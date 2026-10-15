using System;
using UnityEngine;

namespace HollowCraft
{
    // Physical landing/jump boundaries must survive 20 Hz pose sampling.
    internal static class NativeFallTracker
    {
        private static HeroController hero;
        private static bool initialized,airborne;
        private static float previousY,distance;
        private static int serial;
        internal static void Reset() {hero=null;initialized=airborne=false;distance=0;}
        internal static void Sample(HeroController value) {
            var runtime=HollowCraftRuntime.Instance;
            if(value==null||runtime==null||!runtime.InGame||runtime.NativeControlLocked||WorldCreatures.Riding) {Reset();return;}
            float feet=value.GetComponent<Collider2D>().bounds.min.y;
            if(hero!=value||!initialized) {hero=value;previousY=feet;initialized=true;}
            if(runtime.Spectator||MinecraftEnvironment.Flying||MinecraftEnvironment.Gliding||(bool?)runtime.Stats?["water"]==true||(bool?)runtime.Stats?["climbing"]==true) {
                distance=0;airborne=false;previousY=feet;return;
            }
            bool grounded=MinecraftControls.Grounded(value);
            if(airborne)distance+=Math.Max(0,previousY-feet);
            if(grounded)ConfirmLanding(value,feet);
            else airborne=true;
            previousY=feet;
        }
        internal static void BeforeJump(HeroController value) {
            // Called while the physics probe still confirms the floor, even when
            // the landing and following jump both happen between network poses.
            if(hero==value&&initialized) {
                float feet=value.GetComponent<Collider2D>().bounds.min.y;
                if(airborne)distance+=Math.Max(0,previousY-feet);
                ConfirmLanding(value,feet);previousY=feet;
            }
            else {hero=value;previousY=value.GetComponent<Collider2D>().bounds.min.y;initialized=true;}
            airborne=true;distance=0;
        }
        private static void ConfirmLanding(HeroController value,float feet) {
            if(airborne)HollowCraftRuntime.Instance?.SendNativeLanding(++serial,value.transform.position.x,feet,distance);
            airborne=false;distance=0;
        }
    }
}
