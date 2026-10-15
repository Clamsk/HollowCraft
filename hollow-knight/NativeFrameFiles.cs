using System;
using System.IO;
using System.Collections.Generic;

namespace HollowCraft
{
    internal static class NativeFrameFiles
    {
        // Inspect tiny directory metadata before reading RGBA. Never upload up to three
        // obsolete multi-megabyte textures in a single Unity frame when the producer is ahead.
        internal static IEnumerable<string> NewestFirst(string assets,string folder) {
            var files=new FileInfo[3];
            for(int i=0;i<3;i++)files[i]=new FileInfo(Path.Combine(assets,folder,"frame-"+i+".bin"));
            Array.Sort(files,(a,b)=>b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            foreach(var file in files)if(file.Exists)yield return file.FullName;
        }
    }
}
