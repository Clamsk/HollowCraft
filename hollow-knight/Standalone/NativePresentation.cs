using UnityEngine;
namespace HollowCraft.Standalone
{
    internal sealed class NativePresentation : MonoBehaviour
    {
        private float totemAt=-100;
        internal void Totem(){totemAt=Time.unscaledTime;}
        private void OnGUI(){float age=Time.unscaledTime-totemAt;if(age<0||age>2.25f||HollowCraftRuntime.Instance?.InGame!=true)return;
            float progress=age/2.25f,scale=Mathf.Sin(progress*Mathf.PI),size=Screen.height*.25f*scale;var center=new Vector2(Screen.width*.5f,Screen.height*.48f);Color previous=GUI.color;GUI.depth=-180;
            GUI.color=new Color(.25f,.85f,.25f,.12f*scale);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
            for(int i=0;i<32;i++){float angle=i*2.4f+age*.8f,rad=(.06f+progress*.55f)*Screen.height;GUI.color=i%2==0?new Color(.4f,1,.3f,1-progress):new Color(1,.8f,.2f,1-progress);GUI.DrawTexture(new Rect(center.x+Mathf.Cos(angle)*rad,center.y+Mathf.Sin(angle)*rad,3*scale+1,3*scale+1),Texture2D.whiteTexture);}
            GUI.color=new Color(1,1,1,Mathf.Clamp01((1-progress)*6));Matrix4x4 matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Sin(progress*Mathf.PI*4)*18*(1-progress),center);GUI.DrawTexture(new Rect(center.x-size/2,center.y-size/2,size,size),HollowCraftRuntime.Instance.ItemTexture("minecraft:totem_of_undying"),ScaleMode.ScaleToFit);GUI.matrix=matrix;GUI.color=previous;
        }
    }
}
