using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Survival;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

static class SceneryViewSetup
{
    [MenuItem("Tools/Moon Base/Set Up Scenery View")]
    public static void Setup()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SampleScene.unity")throw new InvalidOperationException("Open SampleScene first.");
        Directory.CreateDirectory("QA/SceneryView");
        if(!File.Exists("QA/SceneryView/SampleScene-before.unity"))EditorSceneManager.SaveScene(scene,"QA/SceneryView/SampleScene-before.unity",true);
        var player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();
        if(!player || !player.thirdPersonCamera || !Camera.main)throw new InvalidOperationException("The existing player and camera are required.");
        var view=player.GetComponent<MoonSceneryView>();if(!view)view=Undo.AddComponent<MoonSceneryView>(player.gameObject);
        Undo.RecordObject(player,"Connect scenery view");player.sceneryView=view;
        Undo.RecordObject(view,"Configure scenery view");view.player=player;view.session=UnityEngine.Object.FindFirstObjectByType<SurvivalSession>();
        if(!view.sceneryCamera)
        {
            var go=new GameObject("Scenery Camera");Undo.RegisterCreatedObjectUndo(go,"Create scenery camera");
            go.transform.SetParent(player.thirdPersonCamera.transform.parent,false);
            view.sceneryCamera=go.AddComponent<CinemachineCamera>();
            view.sceneryCamera.Lens=player.thirdPersonCamera.Lens;
            view.sceneryCamera.OutputChannel=player.thirdPersonCamera.OutputChannel;
            view.sceneryCamera.Priority=100;
        }
        var camera=view.sceneryCamera;
        camera.transform.SetPositionAndRotation(player.transform.position+Vector3.up*view.height,Quaternion.Euler(view.pitch,player.transform.eulerAngles.y,0));
        camera.gameObject.SetActive(false);

        const string profilePath="Assets/CustomInputSystem/Profiles/SceneryView.asset";
        var profile=AssetDatabase.LoadAssetAtPath<InputContextProfile>(profilePath);
        if(!profile){profile=ScriptableObject.CreateInstance<InputContextProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
        profile.contextId="Scenery View";profile.actionMap="Scenery";profile.priority=25;profile.blocksGameplay=false;
        var actions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/CustomInputSystem/Actions/UnifiedInput.inputactions");
        actions.FindActionMap("Scenery",true);
        profile.bindingOverrides=Array.Empty<BindingOverride>();
        EditorUtility.SetDirty(profile);view.inputContext=profile;
        view.environment=UnityEngine.Object.FindFirstObjectByType<LunarEnvironment>();
        view.gameplayHud=UnityEngine.Object.FindFirstObjectByType<SpaceDilemma.Hud.HudController>()?.Canvas.transform.Find("Gameplay HUD")?.gameObject;

        const string blendPath="Assets/MoonBase/Scenery Camera Blends.asset";
        var brain=Camera.main.GetComponent<CinemachineBrain>();
        if(!brain)throw new InvalidOperationException("Existing player camera needs CinemachineBrain.");
        var blends=AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(blendPath);
        if(!blends)
        {
            blends=ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
            blends.CustomBlends=brain.CustomBlends?brain.CustomBlends.CustomBlends:null;
            AssetDatabase.CreateAsset(blends,blendPath);
        }
        var entries=new List<CinemachineBlenderSettings.CustomBlend>(blends.CustomBlends??Array.Empty<CinemachineBlenderSettings.CustomBlend>());
        string normal=player.thirdPersonCamera.name, elevated=camera.name;
        entries.RemoveAll(b=>(b.From==normal && b.To==elevated)||(b.From==elevated && b.To==normal));
        entries.Add(new CinemachineBlenderSettings.CustomBlend{From=normal,To=elevated,Blend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,1.0f)});
        entries.Add(new CinemachineBlenderSettings.CustomBlend{From=elevated,To=normal,Blend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,.8f)});
        blends.CustomBlends=entries.ToArray();EditorUtility.SetDirty(blends);
        Undo.RecordObject(brain,"Set scenery camera transitions");brain.CustomBlends=blends;EditorUtility.SetDirty(brain);
        EditorUtility.SetDirty(view);EditorUtility.SetDirty(player);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("QA/SceneryView/setup.txt","Authored scenery camera, input context and 1.0s rise / 0.8s return blends. Existing third-person camera and default blend retained.");
    }
}

