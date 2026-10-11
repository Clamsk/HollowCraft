using System;
using UnityEngine;

namespace HollowCraft
{
    internal sealed class GameModeMenu
    {
        internal static readonly string[] Values={"creative","survival","adventure","spectator"};
        internal static readonly string[] Labels={"Creative Mode","Survival Mode","Adventure Mode","Spectator Mode"};
        internal static readonly string[] Icons={"minecraft:grass_block","minecraft:iron_sword","minecraft:map","minecraft:ender_eye"};
        internal bool Open {get;private set;}
        internal bool ConsumedEscape {get;private set;}
        internal int Selected {get;private set;}
        private string previous="creative";
        private Vector2 initialPointer;
        internal void Tick(HollowCraftRuntime runtime,bool f3,bool f4,bool escape,bool? focused=null)
        {
            ConsumedEscape=false;
            if(Open&&(!(focused??Application.isFocused)||escape)) {Open=false;ConsumedEscape=escape;return;}
            if(f3&&f4) {
                if(!Open) {Selected=Array.IndexOf(Values,previous==runtime.Mode?(runtime.Creative?"survival":"creative"):previous);if(Selected<0)Selected=0;Open=true;initialPointer=GameCursor.ReadScreenPosition();}
                else Selected=(Selected+1)%Values.Length;
            }
            if(Open&&!f3) {previous=runtime.Mode;Open=false;runtime.ChangeMode(Values[Selected]);}
        }
        internal void Hover(int index) {if((GameCursor.ReadScreenPosition()-initialPointer).sqrMagnitude>4)Selected=index;}
        internal void Cancel() {Open=false;}
    }
}
