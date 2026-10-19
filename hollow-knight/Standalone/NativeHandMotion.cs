using System;

namespace HollowCraft.Standalone
{
    internal struct NativeHandPose {internal NativeRigVector Position;internal NativeRigRotation Rotation;}
    internal static class NativeHandMotion
    {
        private static NativeRigRotation Product(NativeRigRotation a,NativeRigRotation b)=>new NativeRigRotation(a.W*b.X+a.X*b.W+a.Y*b.Z-a.Z*b.Y,a.W*b.Y-a.X*b.Z+a.Y*b.W+a.Z*b.X,a.W*b.Z+a.X*b.Y-a.Y*b.X+a.Z*b.W,a.W*b.W-a.X*b.X-a.Y*b.Y-a.Z*b.Z);
        // Item tracks occur after the grip matrix and before the model display.
        // The baked mesh contains G already, so move it with G*A*inverse(G).
        internal static NativeHandPose Pose(bool left,NativeRigVector pixels,NativeRigVector angles) {
            float s=(float)Math.Sqrt(.5);var grip=Product(new NativeRigRotation(s,0,0,s),new NativeRigRotation(0,-1,0,0));
            var inverse=new NativeRigRotation(-grip.X,-grip.Y,-grip.Z,grip.W);
            var rotation=Product(Product(grip,NativeRigSpace.Rotation(angles.X,angles.Y,angles.Z)),inverse);
            var pivot=new NativeRigVector((left?-1:1)/16f,-.625f,-.125f);
            var offset=grip.Apply(new NativeRigVector(-pixels.X/16,-pixels.Y/16,pixels.Z/16));var rotated=rotation.Apply(pivot);
            return new NativeHandPose{Position=new NativeRigVector(pivot.X+offset.X-rotated.X,pivot.Y+offset.Y-rotated.Y,pivot.Z+offset.Z-rotated.Z),Rotation=rotation};
        }
    }
}
