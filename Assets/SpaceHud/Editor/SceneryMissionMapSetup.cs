using System.IO;
using System;
using System.Linq;
using System.Text;
using SpaceDilemma.Hud;
using SpaceDilemma.Research;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

static class SceneryMissionMapSetup
{
    [MenuItem("Tools/Space HUD/Set Up Scenery and Mission Map")]
    public static void Setup()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity")throw new InvalidOperationException("Open SampleScene first.");
        Directory.CreateDirectory("QA/SceneryMissionMap");
        if(!File.Exists("QA/SceneryMissionMap/SampleScene-before.unity"))EditorSceneManager.SaveScene(scene,"QA/SceneryMissionMap/SampleScene-before.unity",true);
        var hud=UnityEngine.Object.FindFirstObjectByType<HudController>();var canvas=hud.Canvas.transform;
        var group=canvas.Find("Gameplay HUD") as RectTransform;
        if(!group)
        {
            group=Rect("Gameplay HUD",canvas);group.anchorMin=Vector2.zero;group.anchorMax=Vector2.one;group.offsetMin=group.offsetMax=Vector2.zero;
            // Keep modal UI outside this visual group so Esc and final results still work.
            foreach(var child in canvas.Cast<Transform>().Where(t=>t!=group&&!t.name.Contains("Overlay")).ToArray())
                Undo.SetTransformParent(child,group,"Group gameplay HUD");
            group.SetAsFirstSibling();
        }
        var view=UnityEngine.Object.FindFirstObjectByType<MoonSceneryView>();Undo.RecordObject(view,"Configure clean scenery view");
        view.gameplayHud=group.gameObject;view.environment=UnityEngine.Object.FindFirstObjectByType<LunarEnvironment>();view.faceEarthOnEnter=true;view.height=1000;
        EditorUtility.SetDirty(view);
        var viewport=hud.Minimap.Viewport;var frame=(RectTransform)viewport.parent;Undo.RecordObjects(new UnityEngine.Object[]{frame,viewport},"Fit minimap legend");
        frame.sizeDelta=new Vector2(118,128);viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
        viewport.offsetMin=new Vector2(5,15);viewport.offsetMax=new Vector2(-5,-5);
        var layer=viewport.Find("Mission Markers") as RectTransform;
        if(!layer){layer=Rect("Mission Markers",viewport);layer.anchorMin=Vector2.zero;layer.anchorMax=Vector2.one;layer.offsetMin=layer.offsetMax=Vector2.zero;}
        var sprites=Enumerable.Range(0,3).Select(SpriteFor).ToArray();
        Color[] colors={new Color32(255,192,70,255),new Color32(56,216,255,255),new Color32(115,245,126,255)};
        var targets=UnityEngine.Object.FindObjectsByType<ResearchTarget>(FindObjectsSortMode.None).OrderBy(t=>t.kind).ThenBy(t=>t.index).ToArray();
        foreach(var target in targets)
        {
            int kind=target.kind==ResearchKind.Instrument?1:target.kind==ResearchKind.Plant?2:0;
            string name=$"{target.kind} {target.index+1} Marker";var icon=layer.Find(name) as RectTransform;
            if(!icon)icon=Rect(name,layer);icon.anchorMin=icon.anchorMax=new Vector2(.5f,.5f);icon.sizeDelta=new Vector2(9,9);icon.anchoredPosition=Vector2.zero;
            var image=icon.GetComponent<Image>();if(!image)image=Undo.AddComponent<Image>(icon.gameObject);image.sprite=sprites[kind];image.color=colors[kind];image.raycastTarget=false;image.preserveAspect=true;
            var marker=icon.GetComponent<HudMapMarker>();if(!marker)marker=Undo.AddComponent<HudMapMarker>(icon.gameObject);
            marker.target=target.transform;marker.showOnMap=true;marker.hideWhenTargetInactive=true;marker.clampToEdge=true;
            EditorUtility.SetDirty(icon);EditorUtility.SetDirty(image);EditorUtility.SetDirty(marker);
        }
        hud.Minimap.PlayerIcon.SetAsLastSibling();
        var legend=frame.Find("Mission Legend") as RectTransform;if(!legend)legend=Rect("Mission Legend",frame);
        legend.anchorMin=legend.anchorMax=Vector2.zero;legend.pivot=Vector2.zero;legend.anchoredPosition=new Vector2(5,3);legend.sizeDelta=new Vector2(108,10);
        var controller=legend.GetComponent<MissionMapLegend>();if(!controller)controller=Undo.AddComponent<MissionMapLegend>(legend.gameObject);
        controller.language=UnityEngine.Object.FindFirstObjectByType<HudGameSettings>(FindObjectsInactive.Include);controller.labels=new TMP_Text[3];
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SpaceHud/SceneAssets/MalgunGothic-System-TMP.asset");
        for(int i=0;i<3;i++)
        {
            var symbol=legend.Find("Icon "+i) as RectTransform;if(!symbol)symbol=Rect("Icon "+i,legend);
            symbol.anchorMin=symbol.anchorMax=Vector2.zero;symbol.pivot=Vector2.zero;symbol.anchoredPosition=new Vector2(i*36,2);symbol.sizeDelta=new Vector2(6,6);
            var image=symbol.GetComponent<Image>();if(!image)image=Undo.AddComponent<Image>(symbol.gameObject);image.sprite=sprites[i];image.color=colors[i];image.raycastTarget=false;image.preserveAspect=true;
            var label=legend.Find("Label "+i) as RectTransform;if(!label)label=Rect("Label "+i,legend);
            label.anchorMin=label.anchorMax=Vector2.zero;label.pivot=Vector2.zero;label.anchoredPosition=new Vector2(i*36+7,0);label.sizeDelta=new Vector2(28,10);
            var text=label.GetComponent<TextMeshProUGUI>();if(!text)text=Undo.AddComponent<TextMeshProUGUI>(label.gameObject);
            text.font=font;text.fontSize=6;text.text=new[]{"SAMPLE","INST","PLANT"}[i];text.color=colors[i];text.raycastTarget=false;text.alignment=TextAlignmentOptions.MidlineLeft;text.textWrappingMode=TextWrappingModes.NoWrap;
            controller.labels[i]=text;EditorUtility.SetDirty(text);EditorUtility.SetDirty(image);EditorUtility.SetDirty(label);EditorUtility.SetDirty(symbol);
        }
        EditorUtility.SetDirty(controller);EditorUtility.SetDirty(frame);EditorUtility.SetDirty(viewport);EditorUtility.SetDirty(legend);
        hud.Minimap.Initialize(view.player.transform);EditorUtility.SetDirty(hud.Minimap);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("QA/SceneryMissionMap/setup.txt",$"Gameplay HUD grouped without modal overlays. Scenery height=1000m, face Earth=true. Authored {targets.Length} mission markers in three styles, all referencing ResearchTarget transforms. Minimap remains square with a separate legend.");
    }
    static RectTransform Rect(string name,Transform parent)
    {var go=new GameObject(name,typeof(RectTransform));Undo.RegisterCreatedObjectUndo(go,"Author minimap UI");var r=(RectTransform)go.transform;r.SetParent(parent,false);return r;}
    static Sprite SpriteFor(int kind)
    {
        const string folder="Assets/SpaceHud/SceneAssets/MissionMap";Directory.CreateDirectory(folder);
        string path=folder+"/"+new[]{"Sample","Instrument","Plant"}[kind]+".png";
        const int size=24;var mask=new bool[size,size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float a=(x-11.5f)/10,b=(y-11.5f)/10;
            bool pixel=kind switch
            {
                0=>Mathf.Abs(a)*.8f+Mathf.Abs(b)<.82f && a>-.8f,
                1=>(Mathf.Abs(a)<.10f&&b>-.72f&&b<.1f)||(Mathf.Abs(a)<.42f&&b>-.78f&&b<-.61f)||
                    (b>-.08f&&b<.45f&&a*a+(b-.4f)*(b-.4f)<.5f&&a*a+(b-.4f)*(b-.4f)>.30f)||
                    (b>.40f&&Mathf.Abs(Mathf.Sqrt(a*a+b*b)-.86f)<.08f),
                _=>(Mathf.Abs(a)<.08f&&b>-.75f&&b<.43f)||
                    (Mathf.Pow((a+.28f+b*.25f)/.42f,2)+Mathf.Pow((b-.02f)/.22f,2)<1)||
                    (Mathf.Pow((a-.28f-b*.15f)/.42f,2)+Mathf.Pow((b-.42f)/.22f,2)<1)||
                    (Mathf.Abs(a)<.33f&&b>-.79f&&b<-.66f)
            };
            mask[x,y]=pixel;
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            bool border=false;for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)if(x+dx>=0&&x+dx<size&&y+dy>=0&&y+dy<size&&mask[x+dx,y+dy])border=true;
            pixels[y*size+x]=mask[x,y]?new Color32(255,255,255,255):border?new Color32(0,0,0,255):new Color32(0,0,0,0);
        }
        texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=24;importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
