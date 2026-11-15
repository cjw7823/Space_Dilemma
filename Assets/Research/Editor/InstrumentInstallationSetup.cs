using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SpaceDilemma.Research;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

static class InstrumentInstallationSetup
{
    [MenuItem("Tools/Research/Set Up Instrument Installation")]
    public static void Setup()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity")throw new InvalidOperationException("Open SampleScene first.");
        const string folder="Assets/Research/InstrumentInstallation";
        Directory.CreateDirectory(folder);Directory.CreateDirectory("QA/InstrumentInstallation");AssetDatabase.Refresh();
        if(!File.Exists("QA/InstrumentInstallation/SampleScene-before.unity"))EditorSceneManager.SaveScene(scene,"QA/InstrumentInstallation/SampleScene-before.unity",true);
        var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Placement Outline.mat");
        if(!material)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.name="Placement Outline";
            material.SetColor("_BaseColor",new Color(.15f,.85f,1,.7f));material.SetFloat("_Surface",1);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=(int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material,folder+"/Placement Outline.mat");
        }
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/Unit Wire Cube.asset");
        if(!mesh){mesh=WireCube();AssetDatabase.CreateAsset(mesh,folder+"/Unit Wire Cube.asset");}
        var summary=new StringBuilder();
        foreach(var target in UnityEngine.Object.FindObjectsByType<ResearchTarget>(FindObjectsSortMode.None).Where(t=>t.kind==ResearchKind.Instrument).OrderBy(t=>t.index))
        {
            var view=target.GetComponent<InstrumentInstallation>();if(!view)view=Undo.AddComponent<InstrumentInstallation>(target.gameObject);
            Undo.RecordObject(view,"Configure instrument installation");
            if(!view.installedModel)
            {
                var children=target.transform.Cast<Transform>().ToArray();
                if(children.Length==0)throw new InvalidOperationException(target.name+" requires an authored child model.");
                var group=new GameObject("Installed Model");Undo.RegisterCreatedObjectUndo(group,"Group installed instrument");group.transform.SetParent(target.transform,false);
                foreach(var child in children)Undo.SetTransformParent(child,group.transform,"Preserve instrument model");
                view.installedModel=group;
            }
            if(!view.placementOutline)
            {
                var outline=new GameObject("Placement Outline");Undo.RegisterCreatedObjectUndo(outline,"Create instrument outline");outline.transform.SetParent(target.transform,false);
                outline.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=outline.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;view.placementOutline=outline;
            }
            if(!InstrumentInstallation.TryModelBounds(view.installedModel.transform,out var bounds))throw new InvalidOperationException(target.name+" has no model renderers.");
            float side=Mathf.Max(1.5f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))+.15f);
            var ghost=view.placementOutline.transform;ghost.rotation=Quaternion.identity;
            Vector3 scale=target.transform.lossyScale;ghost.localScale=new Vector3(side/Mathf.Abs(scale.x),side/Mathf.Abs(scale.y),side/Mathf.Abs(scale.z));
            var original=target.GetComponent<Renderer>();if(original){Undo.RecordObject(original,"Hide old instrument primitive");original.enabled=false;EditorUtility.SetDirty(original);}
            foreach(var collider in target.GetComponents<Collider>()){Undo.RecordObject(collider,"Preserve nonblocking site");collider.enabled=false;EditorUtility.SetDirty(collider);}
            Undo.RecordObjects(new UnityEngine.Object[]{target.transform,view.installedModel.transform,ghost},"Ground instrument presentations");
            view.PlaceOnGround();view.SetInstalled(false);EditorUtility.SetDirty(view);EditorUtility.SetDirty(target.transform);
            EditorUtility.SetDirty(view.installedModel);EditorUtility.SetDirty(view.installedModel.transform);EditorUtility.SetDirty(ghost);EditorUtility.SetDirty(view.placementOutline);
            summary.AppendLine($"{target.index}: {target.name}, side={side:F3}m, root={target.transform.position:F3}, child meshes={view.installedModel.GetComponentsInChildren<Renderer>(true).Length}");
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("QA/InstrumentInstallation/setup.txt",summary.ToString());
    }
    static Mesh WireCube()
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        int[] faces={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
        void Bar(Vector3 center,Vector3 size)
        {
            int start=vertices.Count;
            Vector3[] corners={new(-1,-1,-1),new(1,-1,-1),new(1,1,-1),new(-1,1,-1),new(-1,-1,1),new(1,-1,1),new(1,1,1),new(-1,1,1)};
            foreach(var c in corners)vertices.Add(center+Vector3.Scale(c,size*.5f));foreach(int f in faces)triangles.Add(start+f);
        }
        const float edge=.006f;
        foreach(float a in new[]{-.5f,.5f})foreach(float b in new[]{-.5f,.5f})
        {
            Bar(new Vector3(0,a+.5f,b),new Vector3(1,edge,edge));
            Bar(new Vector3(a,.5f,b),new Vector3(edge,1,edge));
            Bar(new Vector3(a,b+.5f,0),new Vector3(edge,edge,1));
        }
        // Lift the bars by half their width so the very bottom touches the ground.
        float extent=1+edge;for(int i=0;i<vertices.Count;i++)vertices[i]=new Vector3(vertices[i].x/extent,(vertices[i].y+edge*.5f)/extent,vertices[i].z/extent);
        var mesh=new Mesh{name="Unit Wire Cube (12 edges)"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
}
