using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StarterAssets;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MoonBaseSetup
{
    const string Folder="Assets/MoonBase/Materials";
    const string Starter="Assets/Starter Assets/Runtime/";
    public const string Output="QA/MoonBase";
    [MenuItem("Tools/Moon Base/Apply URP Materials and Player")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before setup.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open SampleScene first.");
        var map=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Map");
        if(!map) throw new InvalidOperationException("Expected the user's existing Map root.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Output);
        // Capture all pre-existing world transforms before adding the player or door behavior.
        if(!File.Exists(Output+"/transforms-before.json")) SaveTransforms(Output+"/transforms-before.json");
        var log=new List<string>();
        var converted=new Dictionary<Material,Material>();
        foreach(var renderer in map.GetComponentsInChildren<Renderer>(true))
        {
            var mats=renderer.sharedMaterials; bool changed=false;
            for(int i=0;i<mats.Length;i++)
            {
                var original=mats[i]; if(!original) continue;
                if(!AssetDatabase.GetAssetPath(original).StartsWith("Assets/Sci-Fi Styled Modular Pack/",StringComparison.Ordinal)) continue;
                if(!converted.TryGetValue(original,out var replacement))
                { replacement=Convert(original); converted.Add(original,replacement); log.Add(AssetDatabase.GetAssetPath(original)+" -> "+AssetDatabase.GetAssetPath(replacement)+" ("+replacement.shader.name+")"); }
                mats[i]=replacement; changed=true;
            }
            if(changed) { Undo.RecordObject(renderer,"Use URP base materials"); renderer.sharedMaterials=mats; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
        }
        var player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();
        if(!player)
        {
            var rig=new GameObject("Moon Player"); Undo.RegisterCreatedObjectUndo(rig,"Add Moon Player");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Starter+"ThirdPersonController/Prefabs/PlayerArmature.prefab");
            var avatar=(GameObject)PrefabUtility.InstantiatePrefab(prefab,rig.transform);
            avatar.name="PlayerArmature";
            avatar.transform.SetPositionAndRotation(map.transform.position + Vector3.right * 12f, Quaternion.Euler(0,-90,0));
            var motor=avatar.GetComponent<ThirdPersonController>();
            motor.GroundLayers=~(1<<avatar.layer); motor.MoveSpeed=2; motor.SprintSpeed=5.335f;
            motor.CinemachineCameraTarget.transform.localPosition=new Vector3(0,1.6f,0);
            motor.TopClamp=80; motor.BottomClamp=-80;
            motor.FootstepAudioVolume=.15f;
            var inputs=avatar.GetComponent<StarterAssetsInputs>(); inputs.cursorLocked=false; inputs.cursorInputForLook=false;
            var playerInput=avatar.GetComponent<PlayerInput>(); playerInput.defaultActionMap="Player";
            var output=new GameObject("Player Camera"); output.transform.SetParent(rig.transform);
            output.tag="MainCamera";
            var camera=output.AddComponent<Camera>(); camera.nearClipPlane=.05f; camera.farClipPlane=40000; camera.fieldOfView=60; camera.clearFlags=CameraClearFlags.Skybox;
            output.AddComponent<AudioListener>();
            var additional=output.AddComponent<UniversalAdditionalCameraData>(); additional.renderPostProcessing=true; additional.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var brain=output.AddComponent<CinemachineBrain>(); brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut,0);
            var third=CreateThirdPersonCamera(rig.transform,motor.CinemachineCameraTarget.transform);
            player=avatar.AddComponent<MoonBasePlayer>(); player.thirdPersonCamera=third;
            log.Add(PlacePlayerOnFloor(map, player));
            CenterRigOnPlayer(player);
            foreach(var previous in scene.GetRootGameObjects().Where(g=>g.name=="Main Camera")) previous.SetActive(false);
        }
        int doors=0;
        foreach(var animator in map.GetComponentsInChildren<Animator>(true))
        {
            if(!animator.runtimeAnimatorController || !animator.parameters.Any(p=>p.name=="character_nearby" && p.type==AnimatorControllerParameterType.Bool)) continue;
            var proximity=animator.GetComponent<MoonBaseDoor>(); if(!proximity) proximity=Undo.AddComponent<MoonBaseDoor>(animator.gameObject);
            proximity.player=player.transform; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; doors++;
        }
        AddAccessRamp(map,log);
        log.Add($"Materials converted={converted.Count}, doors connected={doors}, renderers={map.GetComponentsInChildren<Renderer>(true).Length}");
        Physics.SyncTransforms();
        foreach(var light in map.GetComponentsInChildren<Light>(true)) log.Add($"Existing light: {light.name}, {light.type}, intensity={light.intensity}, active={light.isActiveAndEnabled}");
        log.Add($"Terrain at base origin: {Height(map.transform.position)}, base origin Y={map.transform.position.y}");
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllLines(Output+"/setup.txt",log);
        SaveTransforms(Output+"/transforms-after.json");
        File.WriteAllText(Output+"/setup-complete.txt",DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject=player.gameObject;
        SceneView.lastActiveSceneView?.LookAt(player.transform.position+Vector3.up*1.5f,Quaternion.Euler(15,-90,0),8);
    }

    // Move the scene pivot to the spawn while keeping the cameras at their world positions.
    internal static void CenterRigOnPlayer(MoonBasePlayer player)
    {
        Transform avatar = player.transform;
        Transform rig = avatar.parent;
        var children = rig.Cast<Transform>().ToArray();
        var positions = children.Select(child => child.position).ToArray();
        Undo.RecordObjects(children.Cast<UnityEngine.Object>().Append(rig).ToArray(), "Center Moon Player on spawn");
        rig.position = avatar.position;
        for (int i = 0; i < children.Length; i++)
        {
            children[i].position = positions[i];
            PrefabUtility.RecordPrefabInstancePropertyModifications(children[i]);
        }
    }

    // Match the starting capsule to the same supporting surface used by CharacterController.Move.
    internal static string PlacePlayerOnFloor(GameObject map, MoonBasePlayer player)
    {
        Physics.SyncTransforms();
        var controller = player.GetComponent<CharacterController>();
        var motor = player.GetComponent<ThirdPersonController>();
        int mask = motor.GroundLayers.value & ~(1 << player.gameObject.layer);
        Vector3 preferred = player.transform.position;
        var floors = map.GetComponentsInChildren<BoxCollider>()
            .Where(floor => floor.name.StartsWith("floor_", StringComparison.Ordinal) && floor.enabled && !floor.isTrigger)
            .ToArray();
        if (floors.Length == 0) throw new InvalidOperationException("No demo room floor found.");
        float preferredFloor = floors.Where(floor => floor.name.StartsWith("floor_5", StringComparison.Ordinal))
            .Select(floor => floor.bounds.max.y).DefaultIfEmpty(preferred.y).Min();
        float[] offsets = { 0f, -.5f, .5f, -1f, 1f, -1.5f, 1.5f };
        var candidates = from floor in floors
                         where Mathf.Abs(floor.bounds.max.y - preferredFloor) <= .5f
                         from x in offsets
                         from z in offsets
                         let point = new Vector3(floor.bounds.center.x + x, floor.bounds.max.y, floor.bounds.center.z + z)
                         orderby Vector2.SqrMagnitude(new Vector2(point.x - preferred.x, point.z - preferred.z))
                         select point;

        float halfHeight = controller.height * .5f;
        float radius = controller.radius;
        Vector3 horizontalCenter = player.transform.TransformVector(new Vector3(controller.center.x, 0, controller.center.z));
        foreach (var point in candidates)
        {
            // Require the floor directly below and check a wider capsule for nearby props.
            if (!Physics.Raycast(point + Vector3.up * (controller.height + .1f), Vector3.down,
                    out var hit, controller.height + .2f, mask, QueryTriggerInteraction.Ignore) ||
                !floors.Contains(hit.collider as BoxCollider) || Mathf.Abs(hit.point.y - point.y) > .03f)
                continue;

            const float clearance = .1f;
            Vector3 lower = hit.point + Vector3.up * (radius + clearance + .01f);
            Vector3 upper = hit.point + Vector3.up * (controller.height - radius);
            if (Physics.OverlapCapsule(lower, upper, radius + clearance, mask, QueryTriggerInteraction.Ignore)
                .Any(collider => collider != hit.collider)) continue;

            // At rest PhysX keeps the capsule skin above the collider surface.
            float pivotOffset = halfHeight - controller.center.y + controller.skinWidth + .002f;
            Undo.RecordObject(player.transform, "Place player on base floor");
            player.transform.position = hit.point - horizontalCenter + Vector3.up * pivotOffset;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            Undo.RecordObject(motor, "Exclude player from ground checks");
            motor.GroundLayers = mask;
            PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
            return $"Player spawn {player.transform.position:F5} on {hit.collider.name} at Y={hit.point.y:F5}";
        }

        throw new InvalidOperationException("No clear spawn point on the base floor.");
    }

    static void AddAccessRamp(GameObject map,List<string> log)
    {
        if(GameObject.Find("Moon Access Ramp")) return;
        var door=map.GetComponentsInChildren<MoonBaseDoor>(true).FirstOrDefault(d=>d.GetComponent<Animator>().runtimeAnimatorController.name=="frame_door_3");
        if(!door) return;
        Vector3 outward=-door.transform.forward;
        var start=door.transform.position-outward*.6f; start.y=map.transform.position.y+.181f;
        var end=start+outward*25; end.y=Height(end)+.06f;
        float slope=Mathf.Atan2(start.y-end.y,25)*Mathf.Rad2Deg;
        if(slope<1 || slope>25) {log.Add("Ramp omitted: measured slope="+slope); return;}
        var go=new GameObject("Moon Access Ramp"); Undo.RegisterCreatedObjectUndo(go,"Add access ramp"); go.transform.position=start;
        Vector3 side=door.transform.right*1.6f;
        float bottom=Mathf.Min(start.y,end.y)-.3f;
        var world=new[]{start-side,start+side,end-side,end+side,new Vector3((start-side).x,bottom,(start-side).z),new Vector3((start+side).x,bottom,(start+side).z),new Vector3((end-side).x,bottom,(end-side).z),new Vector3((end+side).x,bottom,(end+side).z)};
        var mesh=new Mesh {name="Moon Access Ramp"}; mesh.vertices=world.Select(p=>p-start).ToArray();
        mesh.triangles=new[]{0,1,2,1,3,2,0,4,1,1,4,5,2,3,6,3,7,6,0,2,4,2,6,4,1,5,3,3,5,7,4,6,5,5,6,7};
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        const string meshPath="Assets/MoonBase/AccessRamp.asset";
        AssetDatabase.CreateAsset(mesh,meshPath);
        go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshCollider>().sharedMesh=mesh;
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetColor("_BaseColor",new Color(.22f,.24f,.26f)); material.SetFloat("_Smoothness",.1f);
        AssetDatabase.CreateAsset(material,Folder+"/AccessRamp.mat"); go.AddComponent<MeshRenderer>().sharedMaterial=material;
        log.Add("Added 3.2 m wide, 25 m access ramp at exterior door; slope="+slope+" degrees");
    }
    static CinemachineCamera CreateThirdPersonCamera(Transform parent, Transform target)
    {
        const string path=Starter+"ThirdPersonController/Prefabs/PlayerFollowCamera.prefab";
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent);
        go.name="Third Person Camera";
        var cam=go.GetComponent<CinemachineCamera>(); cam.Follow=target;
        cam.Lens.FieldOfView=60; cam.Lens.NearClipPlane=.05f; cam.Lens.FarClipPlane=40000;
        cam.Priority=20;
        var noise=go.GetComponent<CinemachineBasicMultiChannelPerlin>(); if(noise) noise.AmplitudeGain=0;
        var follow=go.GetComponent<CinemachineThirdPersonFollow>();
        follow.CameraDistance=3; follow.ShoulderOffset=new Vector3(.35f,0,0);
        follow.Damping=new Vector3(.05f,.1f,.1f);
        follow.AvoidObstacles.Enabled=true; follow.AvoidObstacles.CollisionFilter=~(1<<8); follow.AvoidObstacles.IgnoreTag="Player";
        return cam;
    }
    static Material Convert(Material src)
    {
        string path=Folder+"/"+src.name.Replace('/','_')+"_URP.mat";
        var dst=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(dst) return dst;
        bool additive=src.shader.name.Contains("Particles/Additive"); bool leaves=src.shader.name.Contains("Leaves");
        dst=new Material(Shader.Find(additive?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Lit")); dst.name=src.name+" URP";
        CopyTexture(src,dst,"_MainTex","_BaseMap");
        dst.SetColor("_BaseColor",src.HasProperty(additive?"_TintColor":"_Color")?src.GetColor(additive?"_TintColor":"_Color"):Color.white);
        if(!additive)
        {
            // The pack's base-color-normal image is grayscale height noise. Sampling it as
            // a tangent-space normal in URP creates speckled lighting on the shared base material.
            var bump = src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;
            if (bump && AssetDatabase.GetAssetPath(bump) != "Assets/Sci-Fi Styled Modular Pack/Textures/base-color-normal.png")
            {
                CopyTexture(src, dst, "_BumpMap", "_BumpMap");
                CopyFloat(src, dst, "_BumpScale", "_BumpScale", 1);
            }
            CopyTexture(src,dst,"_MetallicGlossMap","_MetallicGlossMap");
            CopyTexture(src,dst,"_OcclusionMap","_OcclusionMap"); CopyTexture(src,dst,"_EmissionMap","_EmissionMap");
            CopyFloat(src,dst,"_Metallic","_Metallic",0);
            CopyFloat(src,dst,src.HasProperty("_MetallicGlossMap")&&src.GetTexture("_MetallicGlossMap")?"_GlossMapScale":"_Glossiness","_Smoothness",.5f);
            CopyFloat(src,dst,"_OcclusionStrength","_OcclusionStrength",1); CopyFloat(src,dst,"_Cutoff","_Cutoff",.5f);
            CopyFloat(src,dst,"_SmoothnessTextureChannel","_SmoothnessTextureChannel",0);
            if(dst.GetTexture("_BumpMap")) dst.EnableKeyword("_NORMALMAP");
            if(dst.GetTexture("_MetallicGlossMap")) dst.EnableKeyword("_METALLICSPECGLOSSMAP");
            if(dst.GetTexture("_OcclusionMap")) dst.EnableKeyword("_OCCLUSIONMAP");
            if(dst.GetFloat("_SmoothnessTextureChannel")>.5f) dst.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            var emission=src.HasProperty("_EmissionColor")?src.GetColor("_EmissionColor"):Color.black;
            dst.SetColor("_EmissionColor",emission);
            if(emission.maxColorComponent>0 || src.IsKeywordEnabled("_EMISSION")) dst.EnableKeyword("_EMISSION");
            dst.globalIlluminationFlags=src.globalIlluminationFlags;
        }
        int mode=src.HasProperty("_Mode")?(int)src.GetFloat("_Mode"):0;
        bool transparent=additive||(!leaves && mode>=2), clip=leaves||mode==1;
        dst.SetFloat("_Surface",transparent?1:0); dst.SetFloat("_AlphaClip",clip?1:0);
        dst.SetFloat("_Cull",leaves?0:2);
        if(clip) dst.EnableKeyword("_ALPHATEST_ON");
        if(transparent)
        {
            dst.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            bool premult=!additive&&mode==3;
            dst.SetFloat("_Blend",additive?2:premult?1:0);
            dst.SetFloat("_SrcBlend",(float)(premult?BlendMode.One:BlendMode.SrcAlpha));
            dst.SetFloat("_DstBlend",(float)(additive?BlendMode.One:BlendMode.OneMinusSrcAlpha));
            dst.SetFloat("_SrcBlendAlpha",(float)BlendMode.One); dst.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            dst.SetFloat("_ZWrite",0); dst.SetShaderPassEnabled("ShadowCaster",false);
            if(premult) dst.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            dst.SetOverrideTag("RenderType","Transparent"); dst.renderQueue=3000;
        }
        else { dst.SetFloat("_SrcBlend",1); dst.SetFloat("_DstBlend",0); dst.SetFloat("_ZWrite",1); dst.SetOverrideTag("RenderType",clip?"TransparentCutout":"Opaque"); dst.renderQueue=clip?2450:2000; }
        dst.enableInstancing=src.enableInstancing;
        AssetDatabase.CreateAsset(dst,path); return dst;
    }
    static void CopyFloat(Material a,Material b,string from,string to,float fallback) { if(b.HasProperty(to)) b.SetFloat(to,a.HasProperty(from)?a.GetFloat(from):fallback); }
    static void CopyTexture(Material a,Material b,string from,string to)
    { if(!a.HasProperty(from)||!b.HasProperty(to)) return; b.SetTexture(to,a.GetTexture(from)); b.SetTextureScale(to,a.GetTextureScale(from)); b.SetTextureOffset(to,a.GetTextureOffset(from)); }
    public static float Height(Vector3 p)
    {
        foreach(var t in Terrain.activeTerrains) {var origin=t.transform.position; var size=t.terrainData.size; if(p.x>=origin.x&&p.x<=origin.x+size.x&&p.z>=origin.z&&p.z<=origin.z+size.z) return t.SampleHeight(p)+origin.y;}
        return float.NaN;
    }
    [Serializable] public class TransformRows { public TransformRow[] rows; }
    [Serializable] public class TransformRow { public string path; public Vector3 position,scale; public Quaternion rotation; }
    public static void SaveTransforms(string file)
    {
        var rows=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>new TransformRow {path=PathOf(t),position=t.position,rotation=t.rotation,scale=t.lossyScale}).ToArray();
        File.WriteAllText(file,JsonUtility.ToJson(new TransformRows {rows=rows},true));
    }
    public static string PathOf(Transform t) { return t.parent?PathOf(t.parent)+"/"+t.name+"["+t.GetSiblingIndex()+"]":t.name; }
}
