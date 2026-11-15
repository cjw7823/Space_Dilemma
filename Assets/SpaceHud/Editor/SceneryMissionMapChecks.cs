using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Research;
using SpaceDilemma.Results;
using SpaceDilemma.Survival;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
static class SceneryMissionMapChecks
{
    const string Output="QA/SceneryMissionMap";
    static readonly Stack<IEnumerator> routines=new();
    static readonly List<string> checks=new(),errors=new();
    static readonly List<InputDevice> suspended=new();
    static Keyboard keyboard;
    static Mouse mouse;
    static Gamepad gamepad;
    static MoonBasePlayer player;
    static MoonSceneryView view;
    static int lastFrame=-1,failures,oldLanguage;
    static bool background,hadLanguage;
    static float deadline;
    static InputSettings.BackgroundBehavior inputBackground;
    static InputSettings.EditorInputBehaviorInPlayMode editorInput;
    static HudMapMarker editMarker;
    static Vector3 editPosition;
    static Vector2 editIcon;
    static double editUntil;
    static SceneryMissionMapChecks(){EditorApplication.update+=Update;}
    [MenuItem("Tools/Space HUD/Validate Scenery and Mission Map")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
        var player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();
        var marker=UnityEngine.Object.FindObjectsByType<HudMapMarker>(FindObjectsSortMode.None).Where(m=>m.target&&m.target.GetComponent<ResearchTarget>())
            .OrderBy(m=>Vector3.Distance(m.target.position,player.transform.position)).First();
        MissionMapEditorPreview.RefreshNow();
        editMarker=marker;editPosition=marker.target.position;editIcon=marker.Icon.rectTransform.anchoredPosition;editUntil=EditorApplication.timeSinceStartup+.3;
        File.WriteAllText("C:/work/Space_Dilemma/QA/SceneryMissionMap/run-entered.txt",marker.target.name+" original="+editPosition);
        marker.target.position+=Vector3.right*12;EditorApplication.QueuePlayerLoopUpdate();
    }
    static void Check(bool ok,string text){checks.Add((ok?"PASS ":"FAIL ")+text);if(!ok)failures++;}
    static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack);}
    static void Update()
    {
        if(editMarker&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<editUntil)return;
            bool followed=Vector2.Distance(editIcon,editMarker.Icon.rectTransform.anchoredPosition)>.1f;
            editMarker.target.position=editPosition;editMarker=null;
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/editor-checks.txt",(followed?"PASS ":"FAIL ")+"Moving a mission Transform in Edit mode automatically updates its authored minimap icon.");
            SessionState.SetBool("SceneryMissionMap.EditorFollow",followed);SessionState.SetBool("SceneryMissionMap.Test",true);EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool("SceneryMissionMap.Test",false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;
        lastFrame=Time.frameCount;
        try
        {
            if(player&&InputHub.Active&&InputHub.Active.GameplayAllowed){player.SendMessage("OnApplicationFocus",true);view.SendMessage("OnApplicationFocus",true);player.SetControlCaptured(true);}
            if(routines.Count==0){checks.Clear();errors.Clear();failures=0;deadline=Time.realtimeSinceStartup+150;Application.logMessageReceived+=Log;routines.Push(Cases());}
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Scenery/minimap checks timed out.");
            while(routines.Count>0)
            {
                var top=routines.Peek();if(!top.MoveNext()){routines.Pop();continue;}
                if(top.Current is IEnumerator nested){routines.Push(nested);continue;}return;
            }
            Finish();
        }
        catch(Exception e){Check(false,e.ToString());Finish();}
    }
    static IEnumerator Frames(int n){while(n-->0)yield return null;}
    static IEnumerator Wait(float s){float end=Time.realtimeSinceStartup+s;while(Time.realtimeSinceStartup<end)yield return null;}
    static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    static void R3(bool down){var state=new GamepadState();if(down)state=state.WithButton(GamepadButton.RightStick);InputSystem.QueueStateEvent(gamepad,state);}
    static IEnumerator Capture(string label)
    {
        foreach(int width in new[]{1280,1920})
        {
            SampleDiscoveryChecks.SetGameSize(width,width==1280?720:1080);yield return Frames(8);
            string path=Path.GetFullPath(Output+$"/{label}-{width}.png");ScreenCapture.CaptureScreenshot(path);yield return Frames(5);
            Check(File.Exists(path)&&new FileInfo(path).Length>40000,$"Actual Game view captured: {label} {width}");
        }
    }
    static bool Near(Vector2 a,Vector2 b)=>Vector2.Distance(a,b)<.05f;
    static bool EarthClear(Vector3 origin,Vector3 direction)=>!Physics.RaycastAll(origin,direction,50000,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
        .Any(hit=>!hit.collider.transform.IsChildOf(player.transform)&&(!player.rover||!hit.collider.transform.IsChildOf(player.rover.transform)));
    static IEnumerator Cases()
    {
        background=Application.runInBackground;Application.runInBackground=true;inputBackground=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var d in InputSystem.devices.ToArray())if(d is Keyboard||d is Mouse||d is Gamepad){suspended.Add(d);InputSystem.DisableDevice(d);}
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();gamepad=InputSystem.AddDevice<Gamepad>();yield return Frames(10);
        HudPlayValidation.EnterGameplay();
        player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();view=player.sceneryView;
        var hud=UnityEngine.Object.FindFirstObjectByType<HudController>();var map=hud.Minimap;var session=view.session;session.Scenario.time.gameHoursPerSecond=.02f;
        var ui=UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();var language=ui.language;
        const string languageKey="SpaceDilemma.Language";hadLanguage=PlayerPrefs.HasKey(languageKey);oldLanguage=PlayerPrefs.GetInt(languageKey);
        var markers=UnityEngine.Object.FindObjectsByType<HudMapMarker>(FindObjectsSortMode.None).Where(m=>m.target&&m.target.GetComponent<ResearchTarget>()).ToArray();
        Check(SessionState.GetBool("SceneryMissionMap.EditorFollow",false),"Edit-mode mission movement automatically follows without a setup rerun");
        Check(markers.Length==12&&markers.Select(m=>m.target).Distinct().Count()==12,"Twelve authored markers reference all sample, storage, instrument and plant points");
        Check(markers.Select(m=>m.Icon.sprite).Distinct().Count()==3&&markers.Select(m=>m.Icon.color).Distinct().Count()==3,"The three mission categories have distinct sprites and colors");
        Check(markers.All(m=>!m.Icon.raycastTarget&&m.clampToEdge&&m.hideWhenTargetInactive&&m.transform.IsChildOf(map.Viewport)),"Icons stay in UI, ignore pointer input and use edge/inactive-target policies");
        Check(Near(map.PlayerIcon.anchoredPosition,Vector2.zero)&&map.PlayerIcon.GetSiblingIndex()==map.Viewport.childCount-1,"Player marker remains centered and renders above mission markers");
        Check(Mathf.Abs(map.Viewport.rect.width-map.Viewport.rect.height)<.01f,"Minimap remains square after adding the separate legend");
        foreach(var marker in markers)
        {
            Vector3 flat=marker.target.position;flat.y=player.transform.position.y;var p=map.MapCamera.WorldToViewportPoint(flat);
            var expected=new Vector2((p.x-.5f)*map.Viewport.rect.width,(p.y-.5f)*map.Viewport.rect.height);
            if(p.x>.05f&&p.x<.95f&&p.y>.05f&&p.y<.95f)Check(marker.Icon.enabled&&Near(marker.Icon.rectTransform.anchoredPosition,expected),"Marker projects current mission position: "+marker.name);
        }
        var moving=markers.First(m=>m.target.GetComponent<ResearchTarget>().kind==ResearchKind.Instrument);var original=moving.target.position;var before=moving.Icon.rectTransform.anchoredPosition;
        moving.target.position+=new Vector3(12,500,-7);yield return Frames(4);
        Check(Vector2.Distance(before,moving.Icon.rectTransform.anchoredPosition)>.5f&&moving.Icon.enabled,"Moving a mission during Play updates its icon, even above the map camera height");
        moving.target.position=original;yield return Frames(3);Check(Near(before,moving.Icon.rectTransform.anchoredPosition),"Restoring the mission Transform restores its marker position");
        var parent=moving.target.parent;var parentOriginal=parent.position;parent.position+=Vector3.right*8;yield return Frames(3);
        Check(Vector2.Distance(before,moving.Icon.rectTransform.anchoredPosition)>.5f,"Moving a whole mission-site group moves its referenced markers");parent.position=parentOriginal;yield return Frames(3);
        moving.target.position=player.transform.position+new Vector3(3000,1000,2000);yield return Frames(3);
        var edge=moving.Icon.rectTransform.anchoredPosition;var half=moving.Icon.rectTransform.rect.size*.5f;
        Check(moving.Icon.enabled&&Mathf.Abs(edge.x)+half.x<=map.Viewport.rect.width*.5f+.1f&&Mathf.Abs(edge.y)+half.y<=map.Viewport.rect.height*.5f+.1f,"Far mission points stay visible fully inside the minimap edge");moving.target.position=original;yield return Frames(3);
        var sample=markers.First(m=>m.target.GetComponent<ResearchTarget>().kind==ResearchKind.Sample);sample.target.gameObject.SetActive(false);yield return Frames(3);
        Check(!sample.Icon.enabled,"An inactive collected sample no longer leaves a misleading map icon");sample.target.gameObject.SetActive(true);yield return Frames(3);
        Check(sample.Icon.enabled,"A reactivated or edited target restores its marker");
        var legend=map.Viewport.parent.GetComponentInChildren<MissionMapLegend>();
        language.UseKorean();yield return Frames(3);Check(legend.labels.Select(t=>t.text).SequenceEqual(new[]{"시료","장비","식물"}),"Legend labels use the current Korean language");
        yield return Capture("mission-map-ko");
        language.UseEnglish();yield return Frames(3);Check(legend.labels.Select(t=>t.text).SequenceEqual(new[]{"SAMPLE","INST","PLANT"}),"Legend labels use the current English language");yield return Capture("mission-map-en");
        foreach(var label in legend.labels){label.ForceMeshUpdate();Check(label.preferredWidth<=label.rectTransform.rect.width+.5f,"Legend label fits: "+label.text);}
        var oldFollow=player.thirdPersonCamera.Follow;var oldLook=oldFollow.rotation;var oldPosition=player.transform.position;
        Keys(Key.L);yield return Frames(4);Keys();yield return Wait(1.2f);
        Check(view.IsActive&&view.height>=1000&&view.ViewPosition.y>=oldPosition.y+1000,"L enters a viewpoint at least 1000m above the current player");
        Check(!view.gameplayHud.activeSelf&&!view.gameplayHud.activeInHierarchy,"Scenery disables the whole gameplay HUD group");
        Check(!hud.Canvas.GetComponentsInChildren<Graphic>().Any(g=>g.enabled&&g.gameObject.activeInHierarchy),"No HUD text, minimap, clock, gauge or button remains visible in pure scenery");
        var earth=view.environment.EarthDirection;var vp=Camera.main.WorldToViewportPoint(Camera.main.transform.position+earth*10000);
        Check(vp.z>0&&vp.x>.05f&&vp.x<.95f&&vp.y>.05f&&vp.y<.95f,"Scenery entry faces the actual Earth direction and keeps it in frame");
        Check(EarthClear(view.ViewPosition,earth),"Terrain and buildings do not block Earth from the elevated viewpoint");
        bool allClear=true;for(int hour=0;hour<=48;hour++)allClear&=EarthClear(view.ViewPosition,view.environment.CelestialTable.Evaluate(hour*3600).earthDirection);
        Check(allClear,"Earth center sightline stays clear for hourly samples across the existing 48h ephemeris");
        double hours=session.Run.Hours;yield return Wait(.15f);Check(session.Run.Hours>hours&&Time.timeScale==1,"Pure scenery keeps game time running");
        yield return Capture("pure-scenery-earth");
        var pos=view.ViewPosition;float yaw=view.Yaw,pitch=Camera.main.transform.eulerAngles.x;
        InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(350,700)});yield return Frames(4);
        Check(Mathf.Abs(Mathf.DeltaAngle(yaw,view.Yaw))>.1f&&Mathf.Abs(Mathf.DeltaAngle(pitch,Camera.main.transform.eulerAngles.x))<.05f&&Vector3.Distance(pos,view.ViewPosition)<.01f,"Mouse look still rotates only world Y with fixed height and pitch");
        Keys(Key.Escape);yield return Frames(4);Keys();yield return Frames(3);
        Check(hud.Pause.IsPaused&&hud.Pause.Popup.activeInHierarchy&&!view.gameplayHud.activeInHierarchy,"Esc menu remains visible outside the hidden gameplay HUD");
        Keys(Key.L);yield return Frames(3);Keys();Check(view.IsActive,"L cannot bypass the pause menu");hud.Pause.Resume();yield return Frames(3);
        Check(view.IsActive&&!view.gameplayHud.activeSelf&&Time.timeScale==1,"Closing pause returns to pure scenery");
        Keys(Key.L);yield return Frames(3);Keys();yield return Wait(.9f);
        Check(!view.IsActive&&view.gameplayHud.activeSelf&&ReferenceEquals(oldFollow,player.thirdPersonCamera.Follow)&&Quaternion.Angle(oldLook,oldFollow.rotation)<.1f,"L restores HUD and the original walking view");
        Check(markers.All(m=>m.gameObject.activeInHierarchy)&&sample.Icon.enabled,"Mission markers resume after leaving scenery");
        view.gameplayHud.SetActive(false);view.Enter();view.Exit();Check(!view.gameplayHud.activeSelf,"Exit preserves an already-hidden HUD state");view.gameplayHud.SetActive(true);yield return Frames(3);
        var cc=player.GetComponent<CharacterController>();Check(player.rover.TryDismount(cc,out var approach),"Actual rover has a valid boarding approach");
        cc.enabled=false;player.transform.position=approach;cc.enabled=true;Physics.SyncTransforms();yield return Frames(4);Keys(Key.T);yield return Frames(5);Keys();yield return Frames(3);
        Check(player.IsDriving,"Existing rover boarding still works");R3(true);yield return Frames(4);R3(false);yield return Wait(1.2f);
        Check(view.IsActive&&player.IsDriving&&!view.gameplayHud.activeSelf,"Gamepad R3 also enters a HUD-free rover scenery view");
        R3(true);yield return Frames(4);R3(false);yield return Wait(.9f);Check(!view.IsActive&&player.IsDriving&&view.gameplayHud.activeSelf,"R3 returns to the rover and restores its HUD");
        view.Enter();view.enabled=false;yield return Frames(3);Check(!view.IsActive&&view.gameplayHud.activeSelf,"Disabling scenery restores HUD and releases its camera");view.enabled=true;yield return Frames(3);
        view.Enter();session.Run.Advance(session.Scenario.time.speStartHour-session.Run.Hours,true);yield return Frames(4);
        var warning=UnityEngine.Object.FindFirstObjectByType<SpeHud>();Check(warning.panel.activeSelf&&!warning.panel.activeInHierarchy,"SPE warnings also stay hidden in the complete appreciation view");
        session.Run.Advance(100,true);yield return Frames(5);var result=UnityEngine.Object.FindFirstObjectByType<FinalEvaluation>();
        Check(!view.IsActive&&view.gameplayHud.activeSelf&&result.popup.activeInHierarchy&&Time.timeScale==0&&!InputHub.Active.GameplayAllowed,"Mission end exits scenery, restores UI and shows the terminal result without resuming time");
        Check(errors.Count==0,"No new Unity errors: "+string.Join(" | ",errors));
    }
    static void Finish()
    {
        SessionState.SetBool("SceneryMissionMap.Test",false);while(routines.Count>0)(routines.Pop() as IDisposable)?.Dispose();Application.logMessageReceived-=Log;
        if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);if(gamepad!=null&&gamepad.added)InputSystem.RemoveDevice(gamepad);
        foreach(var d in suspended)if(d.added)InputSystem.EnableDevice(d);suspended.Clear();SampleDiscoveryChecks.RestoreGameSize();
        InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Application.runInBackground=background;
        const string key="SpaceDilemma.Language";if(hadLanguage)PlayerPrefs.SetInt(key,oldLanguage);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
        Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/play-checks.txt",checks.Concat(new[]{$"RESULT checks={checks.Count} failures={failures}"}));EditorApplication.isPlaying=false;
    }
}
