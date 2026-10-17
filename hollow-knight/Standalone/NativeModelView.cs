using System;
using System.Collections.Generic;
using UnityEngine;
namespace HollowCraft.Standalone
{
    // GPU render target, with one globally sorted mesh for HK's transparent shader.
    // Sorting the entire posed character also orders equipment against body parts.
    internal sealed class NativeModelView : IDisposable
    {
        private Camera camera,main;private GameObject cameraObject,quad;private RenderTexture target;private Mesh mesh;private Material material;private int mainMask;
        private readonly HashSet<Renderer> prepared=new HashSet<Renderer>();
        private GameObject batch;private Mesh batchMesh;private Material batchMaterial;private Texture2D atlas;
        private readonly Dictionary<Texture2D,Rect> atlasRects=new Dictionary<Texture2D,Rect>();
        private readonly List<Vector3> vertices=new List<Vector3>();private readonly List<Vector2> uv=new List<Vector2>();private readonly List<Color> colors=new List<Color>();
        private struct Triangle {internal int A,B,C;internal float Z;}
        private readonly List<Triangle> faces=new List<Triangle>();private readonly List<int> indices=new List<int>();
        private readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
        private void PoseMesh(GameObject model) {
            var renderers=model.GetComponentsInChildren<MeshRenderer>();var sources=new List<Texture2D>();
            foreach(var renderer in renderers){var texture=renderer.sharedMaterial?.mainTexture as Texture2D;if(texture!=null&&!sources.Contains(texture))sources.Add(texture);renderer.enabled=false;}
            if(batch==null){batch=new GameObject("HollowCraft.NativePosedMesh");UnityEngine.Object.DontDestroyOnLoad(batch);batch.layer=31;batchMesh=new Mesh();batchMesh.MarkDynamic();batch.AddComponent<MeshFilter>().sharedMesh=batchMesh;batchMaterial=new Material(Shader.Find("Sprites/Default"));batch.AddComponent<MeshRenderer>().sharedMaterial=batchMaterial;}
            if(sources.Count==0){batch.SetActive(false);return;}
            bool rebuild=atlas==null||sources.Count!=atlasRects.Count;foreach(var texture in sources)if(!atlasRects.ContainsKey(texture))rebuild=true;
            if(rebuild){if(atlas!=null)UnityEngine.Object.Destroy(atlas);atlas=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};var rects=atlas.PackTextures(sources.ToArray(),2,4096,false);atlasRects.Clear();for(int i=0;i<sources.Count;i++)atlasRects[sources[i]]=rects[i];batchMaterial.mainTexture=atlas;}
            vertices.Clear();uv.Clear();colors.Clear();faces.Clear();indices.Clear();
            foreach(var renderer in renderers){var source=renderer.GetComponent<MeshFilter>()?.sharedMesh;var texture=renderer.sharedMaterial?.mainTexture as Texture2D;if(source==null||texture==null)continue;int start=vertices.Count;var points=source.vertices;var coords=source.uv;var shades=source.colors;var triangles=source.triangles;var rect=atlasRects[texture];renderer.GetPropertyBlock(tint);Color color=tint.GetColor("_Color");if(color.a<=0)color=Color.white;
                for(int i=0;i<points.Length;i++){vertices.Add(renderer.transform.TransformPoint(points[i]));var point=i<coords.Length?coords[i]:Vector2.zero;uv.Add(new Vector2(rect.x+Mathf.Clamp01(point.x)*rect.width,rect.y+Mathf.Clamp01(point.y)*rect.height));colors.Add((i<shades.Length?shades[i]:Color.white)*color);}
                for(int i=0;i<triangles.Length;i+=3){int a=start+triangles[i],b=start+triangles[i+1],c=start+triangles[i+2];faces.Add(new Triangle{A=a,B=b,C=c,Z=(vertices[a].z+vertices[b].z+vertices[c].z)/3});}
            }
            faces.Sort((a,b)=>b.Z.CompareTo(a.Z));foreach(var face in faces){indices.Add(face.A);indices.Add(face.B);indices.Add(face.C);}
            batchMesh.Clear();batchMesh.SetVertices(vertices);batchMesh.SetUVs(0,uv);batchMesh.SetColors(colors);batchMesh.SetTriangles(indices,0);batchMesh.RecalculateBounds();batch.SetActive(true);
        }
        internal void Draw(GameObject model,Vector3 feet,int layer,int order,bool large) {
            int size=large?384:256;
            if(main!=Camera.main){if(main!=null)main.cullingMask=mainMask;main=Camera.main;if(main!=null){mainMask=main.cullingMask;main.cullingMask&=~(1<<31);}}
            if(camera==null){cameraObject=new GameObject("HollowCraft.NativeModelCamera");UnityEngine.Object.DontDestroyOnLoad(cameraObject);camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.nearClipPlane=.1f;camera.farClipPlane=16;}
            if(target==null||target.width!=size){if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}target=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point};target.Create();camera.targetTexture=target;if(quad!=null)UnityEngine.Object.Destroy(quad);if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(material!=null)UnityEngine.Object.Destroy(material);
                quad=new GameObject("HollowCraft.NativePlayerImage");UnityEngine.Object.DontDestroyOnLoad(quad);mesh=new Mesh();float half=size/128f,bottom=-48f/64;mesh.vertices=new[]{new Vector3(-half,bottom,0),new Vector3(half,bottom,0),new Vector3(half,bottom+size/64f,0),new Vector3(-half,bottom+size/64f,0)};mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();quad.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=quad.AddComponent<MeshRenderer>();material=new Material(Shader.Find("Sprites/Default")){mainTexture=target};renderer.sharedMaterial=material;renderer.sortingLayerID=layer;renderer.sortingOrder=order+2;
            }
            PoseMesh(model);
            camera.orthographicSize=size/128f;cameraObject.transform.position=new Vector3(feet.x,feet.y+(size/2f-48)/64,feet.z-6);cameraObject.transform.rotation=Quaternion.identity;camera.Render();quad.SetActive(true);quad.transform.position=feet;
        }
        internal void Hide(){if(quad!=null)quad.SetActive(false);if(batch!=null)batch.SetActive(false);}
        public void Dispose(){if(main!=null)main.cullingMask=mainMask;if(batch!=null)UnityEngine.Object.Destroy(batch);if(batchMesh!=null)UnityEngine.Object.Destroy(batchMesh);if(batchMaterial!=null)UnityEngine.Object.Destroy(batchMaterial);if(atlas!=null)UnityEngine.Object.Destroy(atlas);if(cameraObject!=null)UnityEngine.Object.Destroy(cameraObject);if(quad!=null)UnityEngine.Object.Destroy(quad);if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(material!=null)UnityEngine.Object.Destroy(material);}
    }
}
