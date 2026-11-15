using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HeyHeyThere.PixelUISciFiFree;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
static class SampleDiscoveryChecks
{
    const string Out="QA/SampleDiscovery";
    static readonly List<string> checks=new();
    static readonly List<InputDevice> suspended=new();
    static readonly Stack<IEnumerator> routines=new();
    static Keyboard keyboard;
    static MoonBasePlayer player;
    static int lastFrame=-1, failures, oldLanguage;
    static bool hadLanguage, oldBackground;
    static InputSettings.BackgroundBehavior background;
    static InputSettings.EditorInputBehaviorInPlayMode editorInput;
    static EditorWindow gameView;
    static int originalSize=-1;
    static object sizeGroup;
    static readonly List<int> addedSizes=new();
    static SampleDiscoveryChecks() { EditorApplication.update+=Update; }
    [MenuItem("Tools/Research/Validate Sample Popups")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        checks.Clear(); failures=0; SessionState.SetBool("SampleDiscovery.Test",true); EditorApplication.isPlaying=true;
    }
    static void Update()
    {
        if(!SessionState.GetBool("SampleDiscovery.Test",false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame) return;
        lastFrame=Time.frameCount;
        try
        {
            if(player && InputHub.Active && InputHub.Active.GameplayAllowed)
            { player.SendMessage("OnApplicationFocus",true); player.SetControlCaptured(true); }
            if(routines.Count==0)routines.Push(Cases());
            while(routines.Count>0)
            {
                var top=routines.Peek();
                if(!top.MoveNext()){routines.Pop();continue;}
                if(top.Current is IEnumerator child){routines.Push(child);continue;}
                return;
            }
            Finish();
        }
        catch(Exception ex) { Check(false,ex.ToString()); Finish(); }
    }
    static void Check(bool ok,string description) { checks.Add((ok?"PASS ":"FAIL ")+description); if(!ok) failures++; }
    static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    static IEnumerator Cases()
    {
        oldBackground=Application.runInBackground;Application.runInBackground=true;
        for(int i=0;i<8;i++) yield return null;
        var ui=UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();
        var hud=UnityEngine.Object.FindFirstObjectByType<HudController>();
        var hub=InputHub.Active; player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();
        Check(ui && ui.samples.Length==3 && !ui.IsOpen && !ui.popup.activeSelf,"Authored popup starts hidden with three NASA records");
        File.WriteAllText(Out+"/render-diagnostics.txt",$"Pipeline: {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline}\nMain: {Camera.main.name}, enabled {Camera.main.enabled}, rect {Camera.main.rect}, near {Camera.main.nearClipPlane}, far {Camera.main.farClipPlane}\n");
        var probe=RenderTexture.GetTemporary(8,8,0);var active=RenderTexture.active;RenderTexture.active=probe;GL.Clear(true,true,Color.magenta);
        var probeImage=new Texture2D(8,8,TextureFormat.RGB24,false);probeImage.ReadPixels(new Rect(0,0,8,8),0,0);probeImage.Apply();
        File.AppendAllText(Out+"/render-diagnostics.txt",$"GPU {SystemInfo.graphicsDeviceName}, probe {probeImage.GetPixel(1,1)}\n");
        RenderTexture.active=active;RenderTexture.ReleaseTemporary(probe);UnityEngine.Object.DestroyImmediate(probeImage);
        background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var d in InputSystem.devices.ToArray()) if(d is Keyboard || d is Mouse || d is Gamepad)
        { suspended.Add(d); InputSystem.DisableDevice(d); }
        keyboard=InputSystem.AddDevice<Keyboard>();
        const string langKey="SpaceDilemma.Hud.Language";hadLanguage=PlayerPrefs.HasKey(langKey);oldLanguage=PlayerPrefs.GetInt(langKey);
        var config=ui.session.Scenario;config.time.gameHoursPerSecond=.02f;config.interaction.holdSeconds=.12f;
        config.radiation.lethalDose=10000;
        foreach(var sample in ui.interaction.Targets.Where(t=>t.kind==ResearchKind.Sample)) yield return LightCheck(sample);
        int events=0; ui.interaction.Completed+=target=> { if(target.kind==ResearchKind.Sample) events++; };
        for(int index=0;index<3;index++)
        {
            var target=ui.interaction.Targets.Single(t=>t.kind==ResearchKind.Sample && t.index==index);
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;
            player.transform.position=target.transform.position+Vector3.right*.8f;cc.enabled=true;Physics.SyncTransforms();
            player.SetControlCaptured(true);Keys();
            for(int i=0;i<6;i++) yield return null;
            Check(ui.interaction.FindNearby()==target,"Nearest model matches sample "+(index+1));
            int carried=ui.interaction.CarriedSampleCount;
            Keys(Key.E);float until=Time.realtimeSinceStartup+4;
            while(!ui.IsOpen && Time.realtimeSinceStartup<until) yield return null;
            Check(ui.IsOpen && ui.Current==ui.samples[index],"Actual E-hold opens matching Apollo "+ui.samples[index].apolloMission+" popup");
            Check(ui.interaction.IsComplete(target) && !target.gameObject.activeSelf && ui.interaction.CarriedSampleCount==carried+1,
                "Sample hidden and inventory incremented exactly once: "+index);
            Check(Time.timeScale==0 && !hub.GameplayAllowed && !hud.Pause.IsPaused,"Popup freezes world and blocks gameplay without opening pause menu");
            double hours=ui.session.Run.Hours,dose=ui.session.Run.Dose;
            until=Time.realtimeSinceStartup+.3f;
            while(Time.realtimeSinceStartup<until) yield return null;
            Check(ui.session.Run.Hours==hours && ui.session.Run.Dose==dose,"Game time and exposure stay fixed during popup: "+index);
            Check(events==index+1,"Held E cannot collect another sample while popup is open");
            Check(ui.source.text.Contains(ui.samples[index].sourceUrl),"Popup displays exact NASA source URL");
            foreach(bool english in new[]{false,true})
            {
                if(english) ui.language.UseEnglish();else ui.language.UseKorean();
                yield return null;
                foreach(int width in new[]{1280,1920}) yield return Capture(hud,width,width==1280?720:1080,$"sample-{index+1}-{(english?"en":"ko")}-{width}.png");
                Check(ui.description.text==(english?ui.Current.descriptionEnglish:ui.Current.descriptionKorean),"Localized story matches record");
            }
            hud.Pause.Pause(); Check(!hud.Pause.IsPaused && Time.timeScale==0,"Pause button cannot nest a second pause under sample modal");
            if(index==0) { Keys(Key.Escape);yield return null;yield return null;Keys(); }
            else if(index==1) ui.closeButton.onClick.Invoke();
            else { Keys(Key.Backspace);yield return null;yield return null;Keys(); }
            for(int i=0;i<8;i++) yield return null;
            Check(!ui.IsOpen && !ui.popup.activeSelf && !hud.Pause.IsPaused && hub.GameplayAllowed && Time.timeScale==1,
                "Close resumes gameplay via "+(index==0?"Escape":index==1?"authored Continue button":"Backspace"));
            Check(ui.session.Run.Hours>hours,"Game clock advances after closing sample "+(index+1));
        }
        hud.Pause.Pause();yield return null;Check(hud.Pause.IsPaused && Time.timeScale==0,"Ordinary ESC pause still works after all collection popups");
        hud.Pause.Resume();yield return null;Check(!hud.Pause.IsPaused && Time.timeScale==1,"Ordinary pause resumes normally");
        Time.timeScale=.5f; ui.Open(ui.samples[0]);ui.Close();Check(Time.timeScale==.5f,"Popup restores previous non-unit time scale");Time.timeScale=1;
        ui.Open(ui.samples[0]);ui.enabled=false;yield return null;Check(!ui.IsOpen && Time.timeScale==1 && !hud.Pause.BlockExternalPause,"Disabling popup owner releases time and pause lock");
        if(hadLanguage) PlayerPrefs.SetInt(langKey,oldLanguage);else PlayerPrefs.DeleteKey(langKey);
        PlayerPrefs.Save();
    }
    static IEnumerator Capture(HudController hud,int width,int height,string file)
    {
        SetGameSize(width,height);
        for(int f=0;f<5;f++)yield return null;
        Canvas.ForceUpdateCanvases();
        var popup=UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();
        foreach(var t in new[]{popup.title,popup.details,popup.description,popup.source,popup.status})
        {
            t.ForceMeshUpdate();Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text fits authored box: "+file+" / "+t.name);
        }
        string path=Path.GetFullPath(Out+"/"+file);if(File.Exists(path))File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        for(int f=0;f<8;f++)yield return null;
        Check(File.Exists(path) && new FileInfo(path).Length>40000,"Game view screenshot captured: "+file);
    }
    public static void SetGameSize(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;
        var gvType=assembly.GetType("UnityEditor.GameView");
        gameView=(EditorWindow)Resources.FindObjectsOfTypeAll(gvType).First();
        var selected=gvType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(originalSize<0)originalSize=(int)selected.GetValue(gameView);
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var groupEnum=assembly.GetType("UnityEditor.GameViewSizeGroupType");
        sizeGroup=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupEnum,"Standalone")});
        var type=sizeGroup.GetType();int count=(int)type.GetMethod("GetTotalCount").Invoke(sizeGroup,null);int chosen=-1;
        for(int i=0;i<count;i++)
        {
            var size=type.GetMethod("GetGameViewSize").Invoke(sizeGroup,new object[]{i});var st=size.GetType();
            if((int)st.GetProperty("width").GetValue(size)==width && (int)st.GetProperty("height").GetValue(size)==height) { chosen=i;break; }
        }
        if(chosen<0)
        {
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");var mode=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.Parse(mode,"FixedResolution"),width,height,"Sample QA"},null);
            type.GetMethod("AddCustomSize").Invoke(sizeGroup,new[]{size});chosen=count;addedSizes.Add(count);
        }
        selected.SetValue(gameView,chosen);gameView.Repaint();
    }
    static IEnumerator LightCheck(ResearchTarget target)
    {
        var renderers=target.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
        var layers=renderers.Select(r=>r.gameObject.layer).ToArray();
        var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);var states=lights.Select(l=>l.enabled).ToArray();
        var oldAmbient=RenderSettings.ambientLight;var oldMode=RenderSettings.ambientMode;float oldReflection=RenderSettings.reflectionIntensity;
        var otherCameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);var cameraStates=otherCameras.Select(c=>c.enabled).ToArray();
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);var canvasStates=canvases.Select(c=>c.enabled).ToArray();
        var camera=new GameObject("Sample QA Camera").AddComponent<Camera>();
        var image=new Texture2D(2,2,TextureFormat.RGB24,false);
        try
        {
            SetGameSize(1280,720);
            foreach(var c in otherCameras)c.enabled=false;foreach(var c in canvases)c.enabled=false;
            foreach(var l in lights) l.enabled=false;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.reflectionIntensity=0;
            foreach(var r in renderers) r.gameObject.layer=31;
            Bounds b=renderers[0].bounds;foreach(var r in renderers) b.Encapsulate(r.bounds);
            float radius=b.size.magnitude;
            Check(renderers.SelectMany(r=>r.sharedMaterials).All(m=>m.IsKeywordEnabled("_EMISSION")),"Authored sample emission survives URP material validation: "+target.index);
            File.AppendAllText(Out+"/render-diagnostics.txt",$"Sample {target.index}: {b}, materials {string.Join(",",renderers.SelectMany(r=>r.sharedMaterials).Select(m=>$"{m.name} / {m.shader.name} / emission {m.IsKeywordEnabled("_EMISSION")} / {m.GetColor("_EmissionColor")} / map {m.GetTexture("_EmissionMap")}"))}\n");
            camera.transform.position=b.center+new Vector3(-1,.7f,-1).normalized*radius*1.8f;camera.transform.LookAt(b.center);
            camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.01f;camera.farClipPlane=radius*5;camera.fieldOfView=40;
            float[] brightness=new float[2];
            for(int pass=0;pass<2;pass++)
            {
                foreach(var r in renderers) foreach(var m in r.sharedMaterials)
                { m.SetColor("_EmissionColor",Color.white*(pass==0?0:.4f)); if(pass==0)m.DisableKeyword("_EMISSION");else m.EnableKeyword("_EMISSION"); }
                for(int f=0;f<5;f++) yield return null;
                string path=Path.GetFullPath(Out+$"/sample-{target.index+1}-no-light-{(pass==0?"before":"after")}.png");
                if(File.Exists(path))File.Delete(path);ScreenCapture.CaptureScreenshot(path);
                for(int f=0;f<8;f++)yield return null;
                ImageConversion.LoadImage(image,File.ReadAllBytes(path));
                brightness[pass]=image.GetPixels().Average(c=>Mathf.Max(c.r,c.g,c.b));
            }
            Check(brightness[1]>brightness[0]+.005f && brightness[1]>brightness[0]*5,$"Sample {target.index+1} visible with all lights/ambient off ({brightness[0]:F4} -> {brightness[1]:F4})");
        }
        finally
        {
            for(int i=0;i<renderers.Length;i++) { renderers[i].gameObject.layer=layers[i];foreach(var m in renderers[i].sharedMaterials) { m.SetColor("_EmissionColor",Color.white*.4f);m.EnableKeyword("_EMISSION"); } }
            for(int i=0;i<lights.Length;i++) lights[i].enabled=states[i];
            for(int i=0;i<otherCameras.Length;i++)otherCameras[i].enabled=cameraStates[i];
            for(int i=0;i<canvases.Length;i++)canvases[i].enabled=canvasStates[i];
            RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientMode=oldMode;RenderSettings.reflectionIntensity=oldReflection;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }
    public static void RestoreGameSize()
    {
        if(gameView && originalSize>=0)gameView.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(gameView,originalSize);
        if(sizeGroup!=null)foreach(int index in addedSizes.OrderByDescending(x=>x))sizeGroup.GetType().GetMethod("RemoveCustomSize").Invoke(sizeGroup,new object[]{index});
        addedSizes.Clear();originalSize=-1;
    }
    static void Finish()
    {
        SessionState.SetBool("SampleDiscovery.Test",false);while(routines.Count>0)(routines.Pop() as IDisposable)?.Dispose();
        if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);
        RestoreGameSize();
        foreach(var d in suspended)if(d.added)InputSystem.EnableDevice(d);suspended.Clear();
        InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Application.runInBackground=oldBackground;
        File.WriteAllLines(Out+"/play-checks.txt",checks.Concat(new[]{$"RESULT checks={checks.Count} failures={failures}"}));
        EditorApplication.isPlaying=false;
    }
}
