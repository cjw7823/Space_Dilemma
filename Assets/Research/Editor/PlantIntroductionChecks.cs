using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Research;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
static class PlantIntroductionChecks
{
    const string Output="QA/PlantIntroduction";
    static readonly Stack<IEnumerator> routines=new();
    static readonly List<string> checks=new();
    static readonly List<InputDevice> suspended=new();
    static Keyboard keyboard;
    static MoonBasePlayer player;
    static int lastFrame=-1,failures,oldLanguage;
    static bool background,hadLanguage;
    static InputSettings.BackgroundBehavior inputBackground;
    static InputSettings.EditorInputBehaviorInPlayMode editorInput;
    static float deadline;
    static PlantIntroductionChecks(){EditorApplication.update+=Update;}
    [MenuItem("Tools/Research/Validate Plant Introduction")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        SessionState.SetBool("PlantIntroduction.Test",true);EditorApplication.isPlaying=true;
    }
    static void Update()
    {
        if(!SessionState.GetBool("PlantIntroduction.Test",false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;
        lastFrame=Time.frameCount;
        try
        {
            if(player && InputHub.Active && InputHub.Active.GameplayAllowed){player.SendMessage("OnApplicationFocus",true);player.SetControlCaptured(true);}
            if(routines.Count==0){checks.Clear();failures=0;deadline=Time.realtimeSinceStartup+90;routines.Push(Cases());}
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Plant validation timed out.");
            while(routines.Count>0)
            {
                var top=routines.Peek();if(!top.MoveNext()){routines.Pop();continue;}
                if(top.Current is IEnumerator child){routines.Push(child);continue;}return;
            }
            Finish();
        }
        catch(Exception e){Check(false,e.ToString());Finish();}
    }
    static void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
    static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    static IEnumerator Frames(int count){while(count-->0)yield return null;}
    static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    static IEnumerator Cases()
    {
        background=Application.runInBackground;Application.runInBackground=true;
        yield return Frames(8);
        var ui=UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();var hub=InputHub.Active;
        player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();
        inputBackground=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var d in InputSystem.devices.ToArray())if(d is Keyboard || d is Mouse || d is Gamepad){suspended.Add(d);InputSystem.DisableDevice(d);}
        keyboard=InputSystem.AddDevice<Keyboard>();
        const string languageKey="SpaceDilemma.Hud.Language";hadLanguage=PlayerPrefs.HasKey(languageKey);oldLanguage=PlayerPrefs.GetInt(languageKey);
        ui.session.Scenario.time.gameHoursPerSecond=.02f;ui.session.Scenario.interaction.holdSeconds=.25f;
        Check(ui.plantInformation && ui.plantPopup && !ui.plantPopup.activeSelf && !ui.PlantIntroductionShown,"Authored plant introduction starts hidden and unshown");
        Check(ui.plantInformation.referencePowerWatts==735 && ui.plantInformation.observationMinutes==1720,"Reusable data stores 735 W and 28 h 40 min");
        Check(Math.Abs(ui.plantInformation.ReferenceEnergyKwh-21.07)<.000001,"Energy computed from power and exact time is 21.07 kWh");
        var cc=player.GetComponent<CharacterController>();
        void Place(ResearchTarget target){cc.enabled=false;player.transform.position=target.transform.position-Vector3.up*.77f;cc.enabled=true;Physics.SyncTransforms();player.SetControlCaptured(true);}
        var first=ui.interaction.Targets.Single(t=>t.kind==ResearchKind.Plant && t.index==2);
        Place(first);Keys();yield return Frames(6);
        Check(ui.interaction.FindNearby()==first,"First plant can be index 2 rather than a hard-coded index 0");
        Keys(Key.E);yield return Wait(.07f);Keys();yield return Frames(3);
        Check(!ui.IsOpen && !ui.interaction.IsComplete(first),"Interrupted interaction does not show an introduction");
        Keys(Key.E);yield return Wait(.45f);
        Check(ui.IsOpen && ui.IsPlantIntroduction && ui.PlantIntroductionShown && ui.plantPopup.activeSelf,"First completed plant interaction opens the introduction once");
        Check(ui.interaction.IsComplete(first) && ui.interaction.Count(ResearchKind.Plant)==1,"Opening introduction preserves completed plant state");
        Check(Time.timeScale==0 && !hub.GameplayAllowed,"Plant introduction pauses time and blocks gameplay");
        double hours=ui.session.Run.Hours,dose=ui.session.Run.Dose;yield return Wait(.25f);
        Check(ui.session.Run.Hours==hours && ui.session.Run.Dose==dose,"Time and exposure remain fixed while reading");
        Keys();
        foreach(bool english in new[]{false,true})
        {
            if(english)ui.language.UseEnglish();else ui.language.UseKorean();
            Check(ui.plantPower.text.Contains("735") && ui.plantPower.text.Contains("21.07"),"Power panel uses numeric reference data in "+(english?"English":"Korean"));
            foreach(int width in new[]{1280,1920})
            {
                int height=width==1280?720:1080;SampleDiscoveryChecks.SetGameSize(width,height);yield return Frames(6);Canvas.ForceUpdateCanvases();
                foreach(var text in new[]{ui.plantTitle,ui.plantResearchTitle,ui.plantResearch,ui.plantPowerTitle,ui.plantPower,ui.plantAdaptation,ui.plantSources,ui.plantStatus})
                {text.ForceMeshUpdate();Check(text.preferredHeight<=text.rectTransform.rect.height+1,$"{width} {(english?"en":"ko")} text fits: {text.name}");}
                string path=Path.GetFullPath(Output+$"/plant-{(english?"en":"ko")}-{width}.png");if(File.Exists(path))File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);yield return Frames(8);Check(File.Exists(path)&&new FileInfo(path).Length>50000,"Captured actual Game view "+path);
            }
        }
        ui.pause.Pause();Check(!ui.pause.IsPaused && Time.timeScale==0,"Ordinary pause does not nest below the plant introduction");
        Keys(Key.Escape);yield return Frames(3);Keys();yield return Frames(6);
        Check(!ui.IsOpen && !ui.plantPopup.activeSelf && Time.timeScale==1 && hub.GameplayAllowed,"Escape closes plant popup and resumes gameplay");
        Check(ui.session.Run.Hours>hours,"Game time resumes after closing");
        ui.enabled=false;ui.enabled=true;Check(ui.PlantIntroductionShown,"Component re-enable keeps the once-per-session latch");
        foreach(int index in new[]{0,1})
        {
            var target=ui.interaction.Targets.Single(t=>t.kind==ResearchKind.Plant && t.index==index);Place(target);Keys();yield return Frames(6);
            Keys(Key.E);yield return Wait(.45f);Keys();yield return Frames(5);
            Check(ui.interaction.IsComplete(target) && !ui.IsOpen && Time.timeScale==1,"Later plant completes without another popup: "+index);
        }
        ui.OpenPlantIntroduction();Check(!ui.IsOpen,"A second plant-introduction request is ignored");
        var sample=ui.interaction.Targets.First(t=>t.kind==ResearchKind.Sample);cc.enabled=false;player.transform.position=sample.transform.position+Vector3.right*.8f;cc.enabled=true;Physics.SyncTransforms();
        Keys();yield return Frames(6);Keys(Key.E);yield return Wait(.45f);Keys();yield return Frames(3);
        Check(ui.IsOpen && !ui.IsPlantIntroduction && ui.Current==ui.samples[sample.index] && ui.popup.activeSelf,"Apollo sample popup still works after plant introduction");
        ui.closeButton.onClick.Invoke();yield return Frames(4);Check(!ui.IsOpen && Time.timeScale==1,"Sample Continue button still resumes time");
        ui.pause.Pause();yield return Frames(3);ui.pause.Resume();yield return Frames(3);Check(Time.timeScale==1 && !ui.pause.IsPaused,"Normal pause/resume remains available");
    }
    static void Finish()
    {
        SessionState.SetBool("PlantIntroduction.Test",false);while(routines.Count>0)(routines.Pop() as IDisposable)?.Dispose();
        var ui=UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();if(ui)ui.Close();
        if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);foreach(var d in suspended)if(d.added)InputSystem.EnableDevice(d);suspended.Clear();
        SampleDiscoveryChecks.RestoreGameSize();InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Application.runInBackground=background;
        const string key="SpaceDilemma.Hud.Language";if(hadLanguage)PlayerPrefs.SetInt(key,oldLanguage);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
        File.WriteAllLines(Output+"/play-checks.txt",checks.Concat(new[]{$"RESULT checks={checks.Count} failures={failures}"}));EditorApplication.isPlaying=false;
    }
}
