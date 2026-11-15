using System;
using System.IO;
using SpaceDilemma.Hud;
using SpaceDilemma.Research;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

static class PlantIntroductionSetup
{
    [MenuItem("Tools/Research/Set Up Plant Introduction")]
    public static void Setup()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SampleScene.unity")throw new InvalidOperationException("Open SampleScene first.");
        var ui=UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();
        if(!ui)throw new InvalidOperationException("Sample information controller is missing.");
        Directory.CreateDirectory("QA/PlantIntroduction");
        if(!File.Exists("QA/PlantIntroduction/SampleScene-before.unity"))EditorSceneManager.SaveScene(scene,"QA/PlantIntroduction/SampleScene-before.unity",true);
        const string path="Assets/Research/Plant Experiment Information.asset";
        var info=AssetDatabase.LoadAssetAtPath<PlantExperimentInformation>(path);
        if(!info){info=ScriptableObject.CreateInstance<PlantExperimentInformation>();AssetDatabase.CreateAsset(info,path);}
        info.referencePowerWatts=735;info.observationMinutes=28*60+40;
        info.objectiveKorean="지구에서는 뿌리가 대체로 중력 방향으로 자랍니다. 그렇다면 중력이 지구의 약 1/6인 달에서는 뿌리가 어떤 방향으로 자랄까요?\n\n이 가상 연구의 목표는 달의 약한 중력이 뿌리의 성장과 방향에 어떤 영향을 주는지 알아보는 것입니다.\n\n같은 종류의 식물에 빛·물·온도 조건을 맞추고, 지구 중력의 대조군과 달 중력의 실험군을 비교하는 연구를 계획합니다. 뿌리 끝의 방향, 휘어진 정도, 길이를 시간에 따라 기록합니다.\n\n관찰 시간은 28시간 40분입니다. 이 시간은 수확이나 식량 생산에 걸리는 시간이 아닙니다. 약한 중력에서도 뿌리가 물과 영양분에 잘 닿도록, 미래 달 기지의 재배 장치를 설계하는 데 도움이 될 연구입니다.";
        info.objectiveEnglish="On Earth, roots generally grow in the direction of gravity. Which way would they grow on the Moon, where gravity is about one sixth as strong?\n\nThis hypothetical study investigates how lunar partial gravity affects root growth and orientation.\n\nThe proposed experiment compares the same plant species at Earth gravity and lunar gravity, with light, water and temperature controlled. Record root-tip direction, curvature and length over time.\n\nObserve for 28 hours 40 minutes. This is an observation period, not a harvest time. The study could help design lunar growing systems that keep roots in contact with water and nutrients. No experimental outcome is assumed.";
        info.adaptationKorean="게임용 가상 달 실험입니다. 735W는 NASA의 식물 재배시설 설계값을 참고했고, GRAVI-1에서는 시간만 가져왔습니다. 달에서 검증된 소비전력·실험 결과를 뜻하지 않습니다. 출력이나 가동 시간이 달라지면 전력량도 달라집니다.";
        info.adaptationEnglish="A hypothetical lunar study for this game. Power is adapted from a NASA plant-habitat design; only the timing is taken from GRAVI-1. These are not measured lunar power use or results. Energy use changes with power and operating time.";
        info.powerSourceUrl="https://www.nasa.gov/science-research/kennedy-engineers-designing-plant-habitat-for-space-station/";
        info.durationSourceUrl="https://ntrs.nasa.gov/api/citations/20090029998/downloads/20090029998.pdf#page=102";
        EditorUtility.SetDirty(info);Undo.RecordObject(ui,"Add plant introduction");ui.plantInformation=info;
        if(ui.plantPopup)Undo.DestroyObjectImmediate(ui.plantPopup);
        var canvas=UnityEngine.Object.FindFirstObjectByType<HudController>().Canvas;
        var overlay=Rect("Plant Experiment Overlay",canvas.transform,Vector2.zero,Vector2.zero);
        overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color=new Color(0,.015f,.035f,.88f);ui.plantPopup=overlay.gameObject;
        var frame=Rect("Plant Experiment Window",overlay,new Vector2(596,328),Vector2.zero);
        frame.gameObject.AddComponent<Image>().color=new Color(.1f,.7f,.9f);
        var panel=Rect("Panel",frame,new Vector2(594,326),Vector2.zero);
        panel.gameObject.AddComponent<Image>().color=new Color(.025f,.065f,.105f);
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SpaceHud/SceneAssets/MalgunGothic-System-TMP.asset");
        ui.plantTitle=Label("Title",panel,font,14,new Vector2(566,23),new Vector2(0,140),Cyan);
        ui.plantResearchTitle=Label("Research Heading",panel,font,11,new Vector2(294,17),new Vector2(-136,112),Cyan);
        ui.plantResearch=Label("Root Direction Explanation",panel,font,8.5f,new Vector2(294,175),new Vector2(-136,13),Color.white);
        ui.plantPowerTitle=Label("Power Heading",panel,font,11,new Vector2(250,17),new Vector2(158,112),Cyan);
        ui.plantPower=Label("Power and Time",panel,font,10,new Vector2(250,114),new Vector2(158,46),Color.white);
        ui.plantAdaptation=Label("Game Adaptation",panel,font,8,new Vector2(250,59),new Vector2(158,-47),new Color(.72f,.83f,.9f));
        var divider=Rect("Column Divider",panel,new Vector2(1,190),new Vector2(22,16));divider.gameObject.AddComponent<Image>().color=new Color(.1f,.4f,.55f);
        ui.plantSources=Label("NASA Citations",panel,font,7,new Vector2(566,39),new Vector2(0,-97),Cyan);
        ui.plantStatus=Label("Pause Status",panel,font,8,new Vector2(566,13),new Vector2(0,-125),Color.white);
        var powerButton=Button("NASA Power Source",panel,font,new Vector2(-190,-146),out ui.plantPowerSourceLabel);
        var timeButton=Button("NASA Duration Source",panel,font,new Vector2(0,-146),out ui.plantDurationSourceLabel);
        ui.plantCloseButton=Button("Continue",panel,font,new Vector2(190,-146),out ui.plantCloseLabel);
        UnityEventTools.AddPersistentListener(powerButton.onClick,ui.OpenPlantPowerSource);
        UnityEventTools.AddPersistentListener(timeButton.onClick,ui.OpenPlantDurationSource);
        UnityEventTools.AddPersistentListener(ui.plantCloseButton.onClick,ui.Close);
        ui.RefreshPlant(false);overlay.gameObject.SetActive(false);
        EditorUtility.SetDirty(ui);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("QA/PlantIntroduction/setup.txt","Authored two-column plant introduction; 735 W, 1720 minutes, computed 21.07 kWh. Existing scenario power and progress settings retained.");
    }
    static Color Cyan=>new Color(.35f,.88f,1);
    static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 pos)
    {
        var go=new GameObject(name,typeof(RectTransform));Undo.RegisterCreatedObjectUndo(go,"Create plant introduction");go.layer=5;
        var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=pos;return r;
    }
    static TMP_Text Label(string name,Transform parent,TMP_FontAsset font,float size,Vector2 box,Vector2 pos,Color color)
    {
        var r=Rect(name,parent,box,pos);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.color=color;
        t.alignment=TextAlignmentOptions.TopLeft;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.raycastTarget=false;return t;
    }
    static Button Button(string name,Transform parent,TMP_FontAsset font,Vector2 pos,out TMP_Text label)
    {
        var r=Rect(name,parent,new Vector2(180,21),pos);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.04f,.23f,.34f);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
        label=Label("Label",r,font,9,new Vector2(178,20),Vector2.zero,Color.white);label.alignment=TextAlignmentOptions.Center;return b;
    }
}

