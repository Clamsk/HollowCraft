using System.Reflection;
using UnityEngine;

namespace HollowCraft
{
    // Scope direct camera tracking to the first Tutorial_01 landing only.
    [DefaultExecutionOrder(10000)]
    internal sealed class IntroPresentation : MonoBehaviour
    {
        private static IntroPresentation instance;
        private static readonly FieldInfo targetField=typeof(CameraController).GetField("camTarget",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private float showUntil;
        private string campaign;
        private void Awake() {instance=this;}
        internal static void Landed() {if(instance!=null) {instance.campaign=CampaignSession.Id;instance.showUntil=Time.unscaledTime+2.1f;}}
        private void LateUpdate() {
            var runtime=HollowCraftRuntime.Instance;var manager=GameManager.instance;var hero=HeroController.instance;
            if(runtime==null||manager==null||hero==null||!hero.gameObject.activeInHierarchy||!manager.IsGameplayScene()||manager.GetSceneNameString()!="Tutorial_01")return;
            if(campaign!=CampaignSession.Id) {campaign=CampaignSession.Id;showUntil=0;}
            if(!CampaignSession.IntroPending&&Time.unscaledTime>=showUntil)return;
            var controller=manager.cameraCtrl;var body=hero.GetComponent<Collider2D>();
            if(controller==null||body==null||targetField?.GetValue(controller)==null)return;
            var rb=hero.GetComponent<Rigidbody2D>();float down=rb!=null?Mathf.Clamp(rb.linearVelocity.y*.08f,-2.5f,0):0;
            // Follow without native camera damping during the opening fall. Keep
            // the landing surface in view, then hand back from the current target.
            controller.SnapTo(hero.transform.position.x,body.bounds.min.y+2+down);
        }
        private void OnDestroy() {if(instance==this)instance=null;}
    }
}
