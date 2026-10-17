using System;

namespace HollowCraft.Standalone
{
    internal static class NativeCombatTimeline
    {
        // Better Combat 1.8.5 TransmissionSpeedModifier, pinned default config.
        // Age is wall-clock time / weapon cooldown, not animation progress.
        internal static float Tick(float age,float end,float upswing,float multiplier=.5f) {
            age=Math.Max(0,age);upswing=Math.Max(.01f,Math.Min(.99f,upswing));multiplier=Math.Max(.05f,Math.Min(1,multiplier));
            float first=Math.Min(age,upswing)*end/multiplier;
            float mix=Math.Max(0,(multiplier-.5f)/.5f),rate=(1-upswing)*(1-mix)+upswing/(1-upswing)*mix;
            float second=Math.Max(0,Math.Min(age,1)-upswing)*end*rate;
            return first+second+Math.Max(0,age-1)*end;
        }
        internal static float Recovery(float age,float duration) {
            float t=Math.Max(0,Math.Min(1,(age-1)*duration/.15f));return (float)((1+Math.Cos(t*Math.PI))/2);
        }
    }
}
