using System;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

static class SampleDiscoverySetup
{
    const string Folder = "Assets/Research/SampleInformation";
    [MenuItem("Tools/Research/Set Up Sample Information Popup")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open SampleScene first.");
        var existing = UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();
        if (existing) { if(existing.popup) Undo.DestroyObjectImmediate(existing.popup); Undo.DestroyObjectImmediate(existing); }
        Directory.CreateDirectory("QA/SampleDiscovery");
        if(!File.Exists("QA/SampleDiscovery/SampleScene-before.unity")) EditorSceneManager.SaveScene(scene, "QA/SampleDiscovery/SampleScene-before.unity", true);
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var infos = new[] {
            Info("15016-0",15,"Low-Ti Olivine Basalt","저티타늄 감람석 현무암",
                "Between Stations 2–3, Hadley Rille","해들리 릴, 관측 지점 2–3 사이",
                "This volcanic rock is about 3.4 billion years old. Gas bubbles in cooling lava left many round holes, taking up about half of the rock.\n\nDavid Scott picked it up during a rover stop; it became known as the ‘Seatbelt Rock’. Its holes help scientists understand how lunar lava flowed.",
                "약 34억 년 된 화산암입니다. 용암이 식을 때 생긴 기체 방울이 둥근 구멍으로 남았으며, 구멍은 돌 부피의 약 절반을 차지합니다.\n\n데이비드 스콧이 로버를 잠시 세운 사이 주워 ‘안전벨트 바위’라는 별명이 붙었습니다. 이 구멍들은 달의 용암이 어떻게 흘렀는지 알려 줍니다."),
            Info("65035-0",16,"Glass-Coated Dimict Breccia","유리질로 덮인 두 암석 성분의 각력암",
                "Station 5, Descartes Highlands","데카르트 고지, 관측 지점 5",
                "A breccia is made of broken rock pieces joined together. This sample contains two main rock types.\n\nNASA links its glass coating to the impact that formed South Ray Crater about 2 million years ago. Melt from the impact cooled into glass, trapping some lunar soil on the surface.",
                "각력암은 부서진 돌 조각들이 다시 뭉친 암석입니다. 이 시료에는 크게 두 종류의 암석 성분이 섞여 있습니다.\n\nNASA는 약 200만 년 전 사우스 레이 충돌구를 만든 충돌이 이 돌을 유리질로 덮었다고 설명합니다. 충돌로 녹은 물질이 유리처럼 굳을 때, 고운 달 흙도 표면에 달라붙었습니다."),
            Info("76535-0",17,"Troctolite","트록톨라이트 (감람석·사장석을 품은 암석)",
                "North Massif, Station 6, Taurus–Littrow Valley","타우루스–리트로 계곡 북쪽 산지, 관측 지점 6",
                "This rock has large grains of two minerals: olivine and plagioclase. Its big crystals show that it cooled slowly deep in the lunar crust.\n\nNASA estimates it crystallized about 4.3 billion years ago. Its magnetic record helps scientists investigate the young Moon's magnetic field.",
                "감람석과 사장석이라는 두 광물의 큰 알갱이가 거의 비슷한 양으로 들어 있습니다. 큰 결정은 달 지각 깊은 곳에서 천천히 식었다는 단서입니다.\n\nNASA는 약 43억 년 전 결정이 생겼다고 추정합니다. 이 돌에 남은 자기 기록은 어린 달에 자기장이 있었는지 연구하는 데 도움을 줍니다.")
        };
        var hud = UnityEngine.Object.FindFirstObjectByType<HudController>();
        var controller = Undo.AddComponent<SampleCollectionPopup>(hud.gameObject);
        controller.interaction = UnityEngine.Object.FindFirstObjectByType<ResearchInteraction>();
        controller.session = UnityEngine.Object.FindFirstObjectByType<SurvivalSession>();
        controller.pause = hud.GetComponent<HudPauseManager>();
        controller.language = UnityEngine.Object.FindFirstObjectByType<HudGameSettings>(FindObjectsInactive.Include);
        controller.samples = infos;
        var context = AssetDatabase.LoadAssetAtPath<InputContextProfile>(Folder + "/Sample Popup Context.asset");
        if(!context) { context = ScriptableObject.CreateInstance<InputContextProfile>(); AssetDatabase.CreateAsset(context, Folder + "/Sample Popup Context.asset"); }
        context.contextId = "Sample Information"; context.actionMap = "UI"; context.priority = 1100; context.blocksGameplay = true;
        EditorUtility.SetDirty(context); controller.context = context;

        var overlay = Rect("Sample Information Overlay", hud.Canvas.transform, Vector2.zero, Vector2.zero);
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color = new Color(0, .015f, .035f, .85f);
        controller.popup = overlay.gameObject;
        var frame = Rect("Sample Information Window", overlay, new Vector2(420, 306), Vector2.zero);
        frame.gameObject.AddComponent<Image>().color = new Color(.1f, .7f, .9f);
        var panel = Rect("Panel", frame, new Vector2(418, 304), Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(.025f, .065f, .105f);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SpaceHud/SceneAssets/MalgunGothic-System-TMP.asset");
        controller.title = Label("Title", panel, font, 14, new Vector2(390,24), new Vector2(0,130), new Color(.35f,.88f,1));
        controller.details = Label("Sample Details", panel, font, 11, new Vector2(390,52), new Vector2(0,87), Color.white);
        controller.description = Label("Sample Story", panel, font, 11, new Vector2(390,91), new Vector2(0,12), Color.white);
        controller.source = Label("NASA Citation", panel, font, 8, new Vector2(390,27), new Vector2(0,-55), new Color(.45f,.82f,.95f));
        controller.status = Label("Pause Status", panel, font, 9, new Vector2(390,29), new Vector2(0,-89), new Color(.75f,.83f,.9f));
        controller.sourceButton = Button("NASA Source", panel, font, new Vector2(-97,-127), out controller.sourceLabel);
        controller.closeButton = Button("Continue", panel, font, new Vector2(97,-127), out controller.closeLabel);
        UnityEventTools.AddPersistentListener(controller.closeButton.onClick, controller.Close);
        UnityEventTools.AddPersistentListener(controller.sourceButton.onClick, controller.OpenSource);
        controller.title.text = "시료 채취 완료 — 아폴로 15호";
        controller.details.text = "달 시료 15016-0\n"+infos[0].classificationKorean+"\n채취 장소: "+infos[0].locationKorean;
        controller.description.text = infos[0].descriptionKorean;
        controller.source.text = "출처: NASA / ARES — Astromaterials 3D\n"+infos[0].sourceUrl;
        controller.status.text = "게임 일시정지 · 창을 닫으면 시간이 다시 흐릅니다.\n실제 아폴로 시료이며, 이곳의 배치는 게임 설정입니다.";
        controller.closeLabel.text = "계속하기 (Esc)"; controller.sourceLabel.text = "NASA 원문 보기";
        overlay.gameObject.SetActive(false);

        foreach (var target in controller.interaction.Targets.Where(t => t.kind == ResearchKind.Sample))
        {
            var id = infos[target.index].sampleId;
            var renderers = target.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).ToArray();
            if (!renderers.Any(r => AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>()?.sharedMesh).Contains(id) || r.GetComponentsInParent<Transform>().Any(t=>t.name.Contains(id))))
                throw new InvalidOperationException("Sample model/index mismatch: " + target.name);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    var original = materials[m];
                    var texture = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.mainTexture;
                    if (!texture) texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/solid/"+id+"_SFM_Web-Resolution-Model_Coordinate-Registered.jpg");
                    string path = Folder+"/"+id+" Readable-"+m+".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
                    mat.name = id+" Readable"; mat.SetTexture("_BaseMap", texture); mat.SetColor("_BaseColor", Color.white);
                    mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Metallic", 0);
                    mat.EnableKeyword("_EMISSION"); mat.SetTexture("_EmissionMap", texture);
                    mat.SetColor("_EmissionColor", Color.white * .4f);
                    // URP validation enables _EMISSION only when AnyEmissive is set.
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    EditorUtility.SetDirty(mat); materials[m] = mat;
                }
                Undo.RecordObject(renderer,"Brighten lunar sample"); renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("QA/SampleDiscovery/setup.txt","Popup saved in scene; 3 sample information assets and textured emission materials installed.\n");
    }
    static LunarSampleInformation Info(string id,int mission,string ce,string ck,string le,string lk,string de,string dk)
    {
        string path=Folder+"/Apollo "+mission+" - "+id+".asset";
        var info=AssetDatabase.LoadAssetAtPath<LunarSampleInformation>(path);
        if(!info) { info=ScriptableObject.CreateInstance<LunarSampleInformation>(); AssetDatabase.CreateAsset(info,path); }
        info.sampleId=id;info.apolloMission=mission;info.classificationEnglish=ce;info.classificationKorean=ck;
        info.locationEnglish=le;info.locationKorean=lk;info.descriptionEnglish=de;info.descriptionKorean=dk;
        info.sourceUrl="https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample="+id;
        EditorUtility.SetDirty(info);return info;
    }
    static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 pos)
    {
        var go=new GameObject(name,typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go,"Create sample popup");
        go.layer=5;var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=pos;return r;
    }
    static TMP_Text Label(string name,Transform parent,TMP_FontAsset font,float size,Vector2 box,Vector2 pos,Color color)
    {
        var r=Rect(name,parent,box,pos);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.TopLeft;
        t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.raycastTarget=false;return t;
    }
    static Button Button(string name,Transform parent,TMP_FontAsset font,Vector2 pos,out TMP_Text label)
    {
        var r=Rect(name,parent,new Vector2(184,25),pos);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.04f,.23f,.34f);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
        label=Label("Label",r,font,10,new Vector2(180,23),Vector2.zero,Color.white);label.alignment=TextAlignmentOptions.Center;
        return b;
    }
}

