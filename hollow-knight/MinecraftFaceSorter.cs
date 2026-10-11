using System;
using UnityEngine;
namespace HollowCraft
{
    // Reuse sort buffers; release the mutable mesh when a held model or portrait is replaced.
    internal sealed class MinecraftFaceSorter : MonoBehaviour
    {
        private Mesh mesh;private Vector3[] centers;private float[] depths;private int[] order,indices;private Matrix4x4 previous;private bool initialized;
        internal void Sort(Camera camera) {
            if(mesh==null) {
                var filter=GetComponent<MeshFilter>();var source=filter.sharedMesh;if(source.vertexCount%4!=0) return;
                mesh=Instantiate(source);mesh.name=source.name+" sorted instance";filter.sharedMesh=mesh;
                var vertices=mesh.vertices;int count=vertices.Length/4;centers=new Vector3[count];depths=new float[count];order=new int[count];indices=new int[count*6];
                for(int i=0;i<count;i++) centers[i]=(vertices[i*4]+vertices[i*4+1]+vertices[i*4+2]+vertices[i*4+3])*.25f;
            }
            Matrix4x4 matrix=camera.worldToCameraMatrix*transform.localToWorldMatrix;if(initialized&&matrix==previous) return;previous=matrix;initialized=true;
            for(int i=0;i<centers.Length;i++) {depths[i]=matrix.MultiplyPoint3x4(centers[i]).z;order[i]=i;}Array.Sort(depths,order);
            for(int i=0;i<order.Length;i++) {int start=order[i]*4,index=i*6;indices[index]=start;indices[index+1]=start+1;indices[index+2]=start+2;indices[index+3]=start;indices[index+4]=start+2;indices[index+5]=start+3;}mesh.SetTriangles(indices,0,false);
        }
        private void OnDestroy() {if(mesh!=null) Destroy(mesh);}
    }
}
