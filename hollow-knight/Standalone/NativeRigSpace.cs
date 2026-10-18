using System;

namespace HollowCraft.Standalone
{
    // ModelPart and PlayerAnimator's global body are different coordinate spaces.
    // Keep the conversion independent of Unity Euler order and platform intrinsics.
    internal struct NativeRigVector
    {
        internal float X,Y,Z;
        internal NativeRigVector(float x,float y,float z){X=x;Y=y;Z=z;}
    }
    internal struct NativeRigRotation
    {
        internal float X,Y,Z,W;
        internal NativeRigRotation(float x,float y,float z,float w){X=x;Y=y;Z=z;W=w;}
        internal NativeRigVector Apply(NativeRigVector p) {
            float tx=2*(Y*p.Z-Z*p.Y),ty=2*(Z*p.X-X*p.Z),tz=2*(X*p.Y-Y*p.X);
            return new NativeRigVector(p.X+W*tx+Y*tz-Z*ty,p.Y+W*ty+Z*tx-X*tz,p.Z+W*tz+X*ty-Y*tx);
        }
    }
    internal static class NativeRigSpace
    {
        internal static NativeRigRotation Rotation(float pitch,float yaw,float roll,bool global=false) {
            double x=pitch*(global?.5:-.5),y=yaw*(global?.5:-.5),z=roll*.5;
            double sx=Math.Sin(x),cx=Math.Cos(x),sy=Math.Sin(y),cy=Math.Cos(y),sz=Math.Sin(z),cz=Math.Cos(z);
            return new NativeRigRotation((float)(cz*cy*sx-sz*sy*cx),(float)(cz*sy*cx+sz*cy*sx),(float)(sz*cy*cx-cz*sy*sx),(float)(cz*cy*cx+sz*sy*sx));
        }
        internal static NativeRigVector Angles(float x,float y,float z,float w,bool global=false) {
            float yaw=(float)Math.Asin(Math.Max(-1,Math.Min(1,2*(w*y-z*x))));
            float pitch=(float)Math.Atan2(2*(w*x+y*z),1-2*(x*x+y*y));
            float roll=(float)Math.Atan2(2*(w*z+x*y),1-2*(y*y+z*z));
            if(Math.Abs(Math.Cos(yaw))<.0001){pitch=0;roll=(float)Math.Atan2(2*(w*z-x*y),1-2*(x*x+z*z));}
            return new NativeRigVector(global?pitch:-pitch,global?yaw:-yaw,roll);
        }
        internal static NativeRigVector Pivot(float x,float y,float z)=>new NativeRigVector(-x/16,1.5f-y/16,z/16);
        internal static NativeRigVector BodyOffset(float x,float y,float z,NativeRigRotation rotation) {
            var pivot=rotation.Apply(new NativeRigVector(0,.7f,0));return new NativeRigVector(x-pivot.X,y+.7f-pivot.Y,z-pivot.Z);
        }
    }
}
