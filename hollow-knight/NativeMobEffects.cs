using System;
using UnityEngine;
namespace HollowCraft
{
    [DefaultExecutionOrder(5001)]
    internal sealed class NativeMobEffects : MonoBehaviour
    {
        internal float Factor=1;internal bool Burning;
        private Rigidbody2D body;private float baseX,lastX;private bool modified;private GameObject flame;
        private void FixedUpdate() {
            if(body==null) body=GetComponent<Rigidbody2D>();if(body==null||body.bodyType!=RigidbodyType2D.Dynamic) return;
            var velocity=body.linearVelocity;
            if(!modified||Math.Abs(velocity.x-lastX)>.05f) baseX=velocity.x;
            if(Factor==1) {if(modified&&Math.Abs(velocity.x-lastX)<.05f) body.linearVelocity=new Vector2(baseX,velocity.y);modified=false;return;}
            lastX=baseX*Factor;body.linearVelocity=new Vector2(lastX,velocity.y);modified=true;
        }
        private void LateUpdate() {
            if(!Burning) {if(flame!=null) flame.SetActive(false);return;}
            if(flame==null) {
                var texture=HollowCraftRuntime.Instance?.ItemTexture("minecraft:fire_0");if(texture==null) return;
                flame=new GameObject("HollowCraft.VanillaBurning");flame.transform.SetParent(transform,false);var renderer=flame.AddComponent<SpriteRenderer>();renderer.sprite=Sprite.Create(texture,new Rect(0,0,texture.width,Math.Min(texture.width,texture.height)),new Vector2(.5f,.1f),texture.width);var original=GetComponent<Renderer>()??GetComponentInChildren<Renderer>();if(original!=null) renderer.sortingLayerID=original.sortingLayerID;renderer.sortingOrder=50;renderer.color=new Color(1,1,1,.75f);
                MinecraftSpriteAnimation.Attach(renderer,"fire_0");
            }
            flame.SetActive(true);var collider=GetComponent<Collider2D>()??GetComponentInChildren<Collider2D>();if(collider!=null) {flame.transform.position=new Vector3(collider.bounds.center.x,collider.bounds.center.y,-.15f);flame.transform.localScale=new Vector3(collider.bounds.size.x,collider.bounds.size.y,1);}
        }
    }
}
