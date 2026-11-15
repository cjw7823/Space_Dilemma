using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

static class AstronautPlayerSetup
{
    const string Source="Assets/Model/Astronaut.glb";
    const string Body="Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerArmature.prefab";
    public const string Folder="Assets/MoonBase/AstronautPlayer";
    static readonly (HumanBodyBones bone,HumanBodyBones parent,Vector3 position)[] Layout={
        (HumanBodyBones.Hips,HumanBodyBones.LastBone,new(0,.85f,0)),
        (HumanBodyBones.Spine,HumanBodyBones.Hips,new(0,.98f,0)),
        (HumanBodyBones.Chest,HumanBodyBones.Spine,new(0,1.14f,0)),
        (HumanBodyBones.UpperChest,HumanBodyBones.Chest,new(0,1.32f,0)),
        (HumanBodyBones.Neck,HumanBodyBones.UpperChest,new(0,1.44f,0)),
        (HumanBodyBones.Head,HumanBodyBones.Neck,new(0,1.52f,0)),
        (HumanBodyBones.LeftShoulder,HumanBodyBones.UpperChest,new(-.12f,1.36f,0)),
        (HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftShoulder,new(-.29f,1.36f,0)),
        (HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftUpperArm,new(-.53f,1.36f,0)),
        (HumanBodyBones.LeftHand,HumanBodyBones.LeftLowerArm,new(-.76f,1.36f,0)),
        (HumanBodyBones.RightShoulder,HumanBodyBones.UpperChest,new(.12f,1.36f,0)),
        (HumanBodyBones.RightUpperArm,HumanBodyBones.RightShoulder,new(.29f,1.36f,0)),
        (HumanBodyBones.RightLowerArm,HumanBodyBones.RightUpperArm,new(.53f,1.36f,0)),
        (HumanBodyBones.RightHand,HumanBodyBones.RightLowerArm,new(.76f,1.36f,0)),
        (HumanBodyBones.LeftUpperLeg,HumanBodyBones.Hips,new(-.16f,.78f,0)),
        (HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftUpperLeg,new(-.20f,.44f,.015f)),
        (HumanBodyBones.LeftFoot,HumanBodyBones.LeftLowerLeg,new(-.23f,.13f,.025f)),
        (HumanBodyBones.LeftToes,HumanBodyBones.LeftFoot,new(-.23f,.06f,.18f)),
        (HumanBodyBones.RightUpperLeg,HumanBodyBones.Hips,new(.16f,.78f,0)),
        (HumanBodyBones.RightLowerLeg,HumanBodyBones.RightUpperLeg,new(.20f,.44f,.015f)),
        (HumanBodyBones.RightFoot,HumanBodyBones.RightLowerLeg,new(.23f,.13f,.025f)),
        (HumanBodyBones.RightToes,HumanBodyBones.RightFoot,new(.23f,.06f,.18f))
    };
    static float Blend(float a,float b,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));
    // Calibrated against the supplied source mesh at 1.8m height: the rear pack is
    // a rigid assembly extending below the waist and above the neck.
    internal static bool IsBackpack(Vector3 p)=>p.z<-.095f&&p.y>.55f&&p.y<1.70f&&Mathf.Abs(p.x)<.34f;
    static Dictionary<HumanBodyBones,float> Weights(Vector3 p)
    {
        var result=new Dictionary<HumanBodyBones,float>();
        void Add(HumanBodyBones b,float w){if(w>1e-6f)result[b]=result.GetValueOrDefault(b)+w;}
        if(IsBackpack(p)){Add(HumanBodyBones.UpperChest,1);return result;}
        if(p.y>=1.47f){Add(HumanBodyBones.Head,1);return result;}
        float arm=Blend(.25f,.35f,Mathf.Abs(p.x))*Blend(1.15f,1.25f,p.y);
        bool left=p.x<0;
        if(arm>0)
        {
            float elbow=Blend(.47f,.58f,Mathf.Abs(p.x)),wrist=Blend(.69f,.78f,Mathf.Abs(p.x));
            Add(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm,arm*(1-elbow)*(1-wrist));
            Add(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,arm*elbow*(1-wrist));
            Add(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,arm*wrist);
        }
        float remaining=1-arm;
        float leg=(1-Blend(.72f,.86f,p.y))*remaining;
        if(leg>0)
        {
            float knee=Blend(.38f,.50f,p.y),foot=1-Blend(.17f,.27f,p.y);
            Add(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg,leg*knee*(1-foot));
            Add(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,leg*(1-knee)*(1-foot));
            Add(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot,leg*foot);
        }
        remaining-=leg;
        if(p.z<-.19f&&p.y>.88f&&p.y<1.47f&&Mathf.Abs(p.x)<.29f)Add(HumanBodyBones.UpperChest,remaining);
        else
        {
            var spine=new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest,HumanBodyBones.Head};
            float[] heights={.85f,.98f,1.14f,1.32f,1.47f};
            int i=0;while(i<heights.Length-2&&p.y>heights[i+1])i++;
            float t=Blend(heights[i],heights[i+1],p.y);Add(spine[i],remaining*(1-t));Add(spine[i+1],remaining*t);
        }
        return result;
    }
    [MenuItem("Tools/Moon Base/Build Astronaut Player Visual")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("QA/AstronautPlayer");AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));SceneManager.MoveGameObjectToScene(source,scene);
            var filter=source.GetComponentsInChildren<MeshFilter>(true).Single();var original=filter.sharedMesh;
            var sourceVertices=original.vertices;var vertices=sourceVertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
            Bounds bounds=new Bounds(vertices[0],Vector3.zero);foreach(var p in vertices)bounds.Encapsulate(p);
            float scale=1.8f/bounds.size.y;
            var normals=original.normals;var normalMatrix=filter.transform.localToWorldMatrix.inverse.transpose;
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=(vertices[i]-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale;
                Quaternion q=Quaternion.identity;
                if(p.y>1.15f&&p.y<1.48f&&!IsBackpack(p))
                {
                    float weight=Blend(.24f,.35f,Mathf.Abs(p.x));q=Quaternion.AngleAxis(Mathf.Sign(p.x)*13*weight,Vector3.forward);
                    var pivot=new Vector3(Mathf.Sign(p.x)*.27f,1.36f,0);p=pivot+q*(p-pivot);
                }
                vertices[i]=p;if(normals.Length==vertices.Length)normals[i]=(q*normalMatrix.MultiplyVector(normals[i])).normalized;
            }
            var root=new GameObject("PlayerArmature");SceneManager.MoveGameObjectToScene(root,scene);
            var visual=new GameObject("Astronaut Visual");visual.transform.SetParent(root.transform,false);
            var rig=new GameObject("Astronaut Rig");rig.transform.SetParent(visual.transform,false);
            var bones=new Dictionary<HumanBodyBones,Transform>();
            foreach(var item in Layout)
            {
                var t=new GameObject("Suit_"+item.bone).transform;
                t.SetParent(item.parent==HumanBodyBones.LastBone?rig.transform:bones[item.parent],false);
                t.position=item.position;bones.Add(item.bone,t);
            }
            var mesh=UnityEngine.Object.Instantiate(original);mesh.name="Astronaut Skinned Mesh";mesh.vertices=vertices;
            if(normals.Length==vertices.Length)mesh.normals=normals;else mesh.RecalculateNormals();mesh.RecalculateTangents();
            var ordered=Layout.Select(item=>bones[item.bone]).ToArray();var indices=Layout.Select((item,i)=>(item.bone,i)).ToDictionary(p=>p.bone,p=>p.i);
            var weights=new BoneWeight[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var w=Weights(vertices[i]).OrderByDescending(pair=>pair.Value).Take(4).ToArray();float sum=w.Sum(pair=>pair.Value);
                var ids=new int[4];var values=new float[4];for(int j=0;j<w.Length;j++){ids[j]=indices[w[j].Key];values[j]=w[j].Value/sum;}
                weights[i]=new BoneWeight{boneIndex0=ids[0],boneIndex1=ids[1],boneIndex2=ids[2],boneIndex3=ids[3],weight0=values[0],weight1=values[1],weight2=values[2],weight3=values[3]};
            }
            mesh.boneWeights=weights;mesh.bindposes=ordered.Select(b=>b.worldToLocalMatrix*visual.transform.localToWorldMatrix).ToArray();mesh.RecalculateBounds();
            string meshPath=Folder+"/Astronaut Skinned Mesh.asset";
            var savedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(savedMesh){EditorUtility.CopySerialized(mesh,savedMesh);UnityEngine.Object.DestroyImmediate(mesh);mesh=savedMesh;}else AssetDatabase.CreateAsset(mesh,meshPath);
            var skin=visual.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterials=filter.GetComponent<Renderer>().sharedMaterials;
            skin.bones=ordered;skin.rootBone=bones[HumanBodyBones.Hips];skin.quality=SkinQuality.Bone4;skin.localBounds=new Bounds(new Vector3(0,.95f,0),new Vector3(2.8f,2.5f,2.5f));
            var desc=new HumanDescription
            {
                human=Layout.Select(item=>new HumanBone{boneName=bones[item.bone].name,humanName=item.bone.ToString(),limit=new HumanLimit{useDefaultValues=true}}).ToArray(),
                skeleton=root.GetComponentsInChildren<Transform>().Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),
                upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0,hasTranslationDoF=false
            };
            var avatar=AvatarBuilder.BuildHumanAvatar(root,desc);avatar.name="Astronaut Humanoid Avatar";
            if(!avatar.isValid||!avatar.isHuman)throw new InvalidOperationException("Generated astronaut Humanoid avatar is invalid; existing player has not been changed.");
            string avatarPath=Folder+"/Astronaut Humanoid Avatar.asset";var savedAvatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            if(savedAvatar){EditorUtility.CopySerialized(avatar,savedAvatar);UnityEngine.Object.DestroyImmediate(avatar);avatar=savedAvatar;}else AssetDatabase.CreateAsset(avatar,avatarPath);
            PrefabUtility.SaveAsPrefabAsset(visual,Folder+"/Astronaut Visual.prefab");AssetDatabase.SaveAssets();
            File.WriteAllText("QA/AstronautPlayer/build.txt",$"Valid Humanoid={avatar.isHuman}, vertices={mesh.vertexCount}, bones={ordered.Length}, submeshes={mesh.subMeshCount}, height=1.8m. Source GLB unchanged. Anatomical weights and A-to-T rest-pose correction baked into derived mesh.");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("Tools/Moon Base/Apply Astronaut Player Visual")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity"||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Open SampleScene in Edit mode.");
        var avatar=AssetDatabase.LoadAssetAtPath<Avatar>(Folder+"/Astronaut Humanoid Avatar.asset");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Astronaut Visual.prefab");
        if(!avatar||!avatar.isValid||!avatar.isHuman||!prefab)throw new InvalidOperationException("Build and validate the derived visual first.");
        if(!File.Exists("QA/AstronautPlayer/SampleScene-before.unity"))EditorSceneManager.SaveScene(scene,"QA/AstronautPlayer/SampleScene-before.unity",true);
        var player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();var animator=player.GetComponent<Animator>();
        var existing=player.transform.Find("Astronaut Visual");if(existing)Undo.DestroyObjectImmediate(existing.gameObject);
        foreach(var renderer in player.GetComponentsInChildren<Renderer>(true))
        {Undo.RecordObject(renderer,"Hide previous player mesh");renderer.enabled=false;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        var visual=(GameObject)PrefabUtility.InstantiatePrefab(prefab,player.transform);Undo.RegisterCreatedObjectUndo(visual,"Apply astronaut visual");
        visual.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);visual.transform.localScale=Vector3.one;
        Undo.RecordObject(animator,"Use astronaut Humanoid avatar");animator.avatar=avatar;EditorUtility.SetDirty(animator);PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("QA/AstronautPlayer/applied.txt","Astronaut visual and avatar applied to the existing PlayerArmature. Controller, movement, CharacterController, camera and interactions retained. Original body renderers disabled and retained.");
    }
    [MenuItem("Tools/Moon Base/Inspect Astronaut Source")]
    public static void Inspect()
    {
        Directory.CreateDirectory("QA/AstronautPlayer");
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));SceneManager.MoveGameObjectToScene(source,scene);
            var body=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Body));SceneManager.MoveGameObjectToScene(body,scene);
            var animator=body.GetComponent<Animator>();
            var output=new StringBuilder();
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var m=filter.sharedMesh;output.AppendLine($"Mesh {filter.name} readable={m.isReadable} vertices={m.vertexCount} submeshes={m.subMeshCount}, localBounds={m.bounds}, transform={filter.transform.localToWorldMatrix}");
                var points=m.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();Bounds b=new Bounds(points[0],Vector3.zero);foreach(var v in points)b.Encapsulate(v);
                float factor=1.8f/b.size.y;
                using(var writer=new BinaryWriter(File.Open("QA/AstronautPlayer/source-points.bin",FileMode.Create)))
                    foreach(var v in points){writer.Write((v.x-b.center.x)*factor);writer.Write((v.y-b.min.y)*factor);writer.Write((v.z-b.center.z)*factor);}
                output.AppendLine($"WORLD BOUNDS {b}, normalized scale={factor}");
                foreach(var material in filter.GetComponent<Renderer>().sharedMaterials)output.AppendLine($"Material {material.name} shader={material.shader.name} texture={material.mainTexture?.name}");
            }
            foreach(HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if(bone==HumanBodyBones.LastBone)continue;var t=animator.GetBoneTransform(bone);if(t)output.AppendLine($"BONE {bone} {t.name} position={t.position:F4}, rotation={t.rotation}, parent={t.parent.name}");
            }
            File.WriteAllText("QA/AstronautPlayer/inspection.txt",output.ToString());
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
