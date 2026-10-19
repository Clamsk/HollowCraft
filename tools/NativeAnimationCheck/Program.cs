using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using HollowCraft.Standalone;

var clips=new Dictionary<string,NativeAnimationClip>();
var errors=new Dictionary<string,(int Count,double Max,string First)>();int checkedAxes=0;
foreach(JObject row in JArray.Parse(File.ReadAllText(args[1]))) {
    string file=(string)row["clip"],bone=(string)row["bone"];
    if(!clips.TryGetValue(file,out var clip))clips[file]=clip=new NativeAnimationClip(JObject.Parse(File.ReadAllText(Path.Combine(args[0],file))));
    var axes=(bool)row["rotation"]?new[]{"pitch","yaw","roll"}:new[]{"x","y","z"};
    for(int i=0;i<3;i++) {
        float expected=(float)row["value"][i],tick=(float)row["tick"],baseline=(float)row["baseline"][i];
        float actual=clip.Sample(bone,axes[i],tick,baseline);double error=Math.Abs(actual-expected);checkedAxes++;
        if(error<.0001)continue;
        if(!errors.TryGetValue(file,out var value))value=(0,0,$"{bone}/{axes[i]} @ {tick}: got {actual}, expected {expected}, baseline {baseline}");
        errors[file]=(value.Count+1,Math.Max(value.Max,error),value.First);
    }
}
foreach(var pair in errors)Console.WriteLine($"{pair.Key}: {pair.Value.Count} mismatches, max {pair.Value.Max:F6}; {pair.Value.First}");
Console.WriteLine($"{checkedAxes} axes compared against original PlayerAnimator 1.0.2-rc1; {errors.Count} clips differ.");
Environment.ExitCode=errors.Count==0?0:1;
string rigPath=Path.Combine(Path.GetDirectoryName(args[1]),"rig-samples.json");int rigCount=0,rigErrors=0;double rigMax=0;
foreach(JObject row in JArray.Parse(File.ReadAllText(rigPath))) {
    var clip=clips[(string)row["clip"]];string bone=(string)row["bone"];float tick=(float)row["tick"];
    var local=NativeRigSpace.Rotation(clip.Sample(bone,"pitch",tick),clip.Sample(bone,"yaw",tick),clip.Sample(bone,"roll",tick));
    var global=NativeRigSpace.Rotation(clip.Sample("body","pitch",tick),clip.Sample("body","yaw",tick),clip.Sample("body","roll",tick),true);
    float x=bone=="rightArm"?-5:bone=="leftArm"?5:bone=="rightLeg"?-1.9f:bone=="leftLeg"?1.9f:0;
    float y=bone.EndsWith("Arm")?2:bone.EndsWith("Leg")?12:0,z=bone.EndsWith("Leg")?.1f:0;
    var pivot=NativeRigSpace.Pivot(clip.Sample(bone,"x",tick,x),clip.Sample(bone,"y",tick,y),clip.Sample(bone,"z",tick,z));
    var point=local.Apply(new NativeRigVector(-3f/16,-5f/16,-2f/16));
    point=global.Apply(new NativeRigVector(point.X+pivot.X,point.Y+pivot.Y,point.Z+pivot.Z));
    var root=NativeRigSpace.BodyOffset(clip.Sample("body","x",tick),clip.Sample("body","y",tick),clip.Sample("body","z",tick),global);
    double error=Math.Max(Math.Abs(point.X+root.X-(float)row["point"][0]),Math.Max(Math.Abs(point.Y+root.Y-(float)row["point"][1]),Math.Abs(point.Z+root.Z-(float)row["point"][2])));
    rigMax=Math.Max(rigMax,error);rigCount++;if(error>.0001)rigErrors++;
    var angles=NativeRigSpace.Angles(local.X,local.Y,local.Z,local.W);var roundTrip=NativeRigSpace.Rotation(angles.X,angles.Y,angles.Z);
    double dot=Math.Abs(local.X*roundTrip.X+local.Y*roundTrip.Y+local.Z*roundTrip.Z+local.W*roundTrip.W);if(Math.Abs(dot-1)>.0001)throw new Exception("ZYX rotation round trip failed");
}
Console.WriteLine($"{rigCount} articulated points compared against JOML/Minecraft matrices; {rigErrors} differ, max error {rigMax:E3} blocks.");
if(rigErrors>0)Environment.ExitCode=1;

string handsPath=Path.Combine(Path.GetDirectoryName(args[1]),"hand-samples.json");int handCount=0,handErrors=0;double handMax=0;
if(File.Exists(handsPath))foreach(JObject row in JArray.Parse(File.ReadAllText(handsPath))) {
    var clip=clips[(string)row["clip"]];string bone=(string)row["bone"];float tick=(float)row["tick"];
    var pos=new NativeRigVector(clip.Sample(bone,"x",tick),clip.Sample(bone,"y",tick),clip.Sample(bone,"z",tick));var angles=new NativeRigVector(clip.Sample(bone,"pitch",tick),clip.Sample(bone,"yaw",tick),clip.Sample(bone,"roll",tick));
    var pose=NativeHandMotion.Pose(bone=="leftItem",pos,angles);var point=pose.Rotation.Apply(new NativeRigVector(-3f/16,-5f/16,-2f/16));double error=Math.Max(Math.Abs(point.X+pose.Position.X-(float)row["point"][0]),Math.Max(Math.Abs(point.Y+pose.Position.Y-(float)row["point"][1]),Math.Abs(point.Z+pose.Position.Z-(float)row["point"][2])));handCount++;handMax=Math.Max(handMax,error);if(error>.0001)handErrors++;
}
Console.WriteLine($"{handCount} item-track grip points compared against original/JOML matrices; {handErrors} differ, max {handMax:E3} blocks.");if(handErrors>0)Environment.ExitCode=1;