using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Research;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
static class InstrumentInstallationChecks
{
    const string Output="QA/InstrumentInstallation";
    static readonly Stack<IEnumerator> routines=new();
    static readonly List<string> checks=new(),errors=new();
    static readonly List<InputDevice> suspended=new();
    static Keyboard keyboard;
    static MoonBasePlayer player;
    static int lastFrame=-1,failures;
    static float deadline;
    static bool background;
    static InputSettings.BackgroundBehavior inputBackground;
    static InputSettings.EditorInputBehaviorInPlayMode editorInput;
    static InstrumentInstallationChecks(){EditorApplication.update+=Update;}
    [MenuItem("Tools/Research/Validate Instrument Installation")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        SessionState.SetBool("InstrumentInstallation.Test",true);EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
    static void Log(string text,string trace,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+trace);}
    static void Update()
    {
        if(!SessionState.GetBool("InstrumentInstallation.Test",false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;
        lastFrame=Time.frameCount;
        try
        {
            if(player&&InputHub.Active&&InputHub.Active.GameplayAllowed){player.SendMessage("OnApplicationFocus",true);player.SetControlCaptured(true);}
            if(routines.Count==0){checks.Clear();errors.Clear();failures=0;deadline=Time.realtimeSinceStartup+90;Application.logMessageReceived+=Log;routines.Push(Cases());}
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Instrument installation test timed out.");
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
    static IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
    static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    static IEnumerator Capture(InstrumentInstallation view,string stage)
    {
        var camera=Camera.main;var brain=camera.GetComponent<CinemachineBrain>();bool wasEnabled=brain.enabled;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        try
        {
            brain.enabled=false;camera.transform.position=view.transform.position+new Vector3(-4,2.5f,-5);camera.transform.LookAt(view.transform.position+Vector3.up);
            foreach(int width in new[]{1280,1920})
            {
                SampleDiscoveryChecks.SetGameSize(width,width==1280?720:1080);yield return Frames(8);
                string path=Path.GetFullPath(Output+$"/{stage}-{width}.png");ScreenCapture.CaptureScreenshot(path);yield return Frames(6);
                Check(File.Exists(path)&&new FileInfo(path).Length>50000,$"Actual Game view: {stage} {width}");
            }
        }
        finally{camera.transform.SetPositionAndRotation(position,rotation);brain.enabled=wasEnabled;}
    }
    static IEnumerator Cases()
    {
        Directory.CreateDirectory(Output);background=Application.runInBackground;Application.runInBackground=true;
        inputBackground=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var d in InputSystem.devices.ToArray())if(d is Keyboard||d is Mouse||d is Gamepad){suspended.Add(d);InputSystem.DisableDevice(d);}
        keyboard=InputSystem.AddDevice<Keyboard>();yield return Frames(8);
        var ui=UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();var interaction=ui.interaction;
        player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();
        ui.session.Scenario.time.gameHoursPerSecond=.02f;ui.session.Scenario.interaction.holdSeconds=.25f;
        var targets=interaction.Targets.Where(t=>t.kind==ResearchKind.Instrument).OrderBy(t=>t.index).ToArray();
        Check(targets.Length==3,"Actual scene has three instrument targets");
        foreach(var target in targets)
        {
            var view=target.GetComponent<InstrumentInstallation>();
            Check(view&&view.placementOutline&&view.installedModel,$"Site {target.index} has authored outline and model references");
            Check(view.placementOutline.activeSelf&&!view.installedModel.activeSelf&&!view.IsInstalled,$"Site {target.index} starts as an empty wire cube");
            Check(!target.GetComponent<Renderer>().enabled,$"Site {target.index} hides the old solid primitive");
            var filter=view.placementOutline.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;
            Check(mesh.vertexCount==96&&mesh.triangles.Length==432&&view.placementOutline.GetComponentsInChildren<Collider>().Length==0,$"Site {target.index} draws twelve edge bars with no cube faces or collider");
            var ghost=view.placementOutline.GetComponent<Renderer>().bounds;
            Check(Mathf.Abs(ghost.size.x-ghost.size.y)<.01f&&Mathf.Abs(ghost.size.y-ghost.size.z)<.01f,$"Site {target.index} stays a regular cube despite its nonuniform parent scale");
            InstrumentInstallation.TryModelBounds(view.installedModel.transform,out var bounds);
            Check(bounds.min.y>=target.transform.position.y-.01f&&ghost.min.y>=target.transform.position.y-.01f,$"Site {target.index} model and outline do not sink below their ground anchor");
            var terrain=Terrain.activeTerrains.First(t=>{var p=target.transform.position-t.GetPosition();return p.x>=0&&p.z>=0&&p.x<=t.terrainData.size.x&&p.z<=t.terrainData.size.z;});
            float ground=terrain.GetPosition().y+terrain.SampleHeight(target.transform.position);
            Check(Mathf.Abs(ground-target.transform.position.y)<.01f,$"Site {target.index} anchor touches actual terrain");
            float contact=float.PositiveInfinity;
            for(int i=0;i<4;i++)
            {
                var point=new Vector3((i&1)==0?ghost.min.x:ghost.max.x,ghost.min.y,(i&2)==0?ghost.min.z:ghost.max.z);
                contact=Mathf.Min(contact,point.y-terrain.GetPosition().y-terrain.SampleHeight(point));
            }
            Check(Mathf.Abs(contact)<.012f,$"Site {target.index} wire cube touches the slope at its supporting corner without buried lower edges");
        }
        var cc=player.GetComponent<CharacterController>();
        void Place(ResearchTarget target)
        {cc.enabled=false;player.transform.position=target.transform.position+Vector3.back*.9f;cc.enabled=true;Physics.SyncTransforms();player.SetControlCaptured(true);}
        int completed=0;interaction.Completed+=_=>completed++;
        foreach(var target in targets)
        {
            var view=target.GetComponent<InstrumentInstallation>();Place(target);Keys();yield return Frames(6);
            Check(interaction.FindNearby()==target,$"Site {target.index} remains reachable through the empty preview");
            if(target.index==0)
            {
                yield return Capture(view,"before-installation");
                Keys(Key.E);yield return Wait(.08f);Keys();yield return Frames(4);
                Check(!interaction.IsComplete(target)&&view.placementOutline.activeSelf&&!view.installedModel.activeSelf,"Interrupted installation keeps the outline and hidden model");
            }
            double hour=ui.session.Run.Hours;Keys(Key.E);yield return Wait(.45f);
            Check(interaction.IsComplete(target)&&view.IsInstalled&&!view.placementOutline.activeSelf&&view.installedModel.activeSelf,$"Site {target.index} E completion replaces outline with its existing model");
            Check(!ui.IsOpen&&!ui.popup.activeSelf&&!ui.plantPopup.activeSelf&&Time.timeScale==1&&InputHub.Active.GameplayAllowed,$"Site {target.index} installation shows no popup and never pauses");
            Check(ui.session.Run.Hours>hour,$"Site {target.index} game clock advances during installation");
            Keys();yield return Frames(5);
            if(target.index==0)yield return Capture(view,"after-installation");
            Check(interaction.Count(ResearchKind.Instrument)==target.index+1,$"Site {target.index} records installation once");
        }
        Check(completed==3,"Exactly three installation completion events reach the existing research system");
        var research=UnityEngine.Object.FindFirstObjectByType<ResearchProgress>();yield return Wait(.2f);
        Check(targets.All(t=>research.Ledger.Get(ResearchMission.Instruments,t.index).Activated),"All three installed devices activate their existing research ledger entries");
        var last=targets[2].GetComponent<InstrumentInstallation>();var old=last.transform.position;
        last.transform.position+=Vector3.up*5;last.PlaceOnGround();
        Check(Mathf.Abs(last.transform.position.y-old.y)<.01f&&last.IsInstalled&&last.installedModel.activeSelf&&!last.placementOutline.activeSelf,"Ground realignment after moving a site preserves installed presentation");
        Check(errors.Count==0,"No new Unity errors: "+string.Join(" | ",errors));
    }
    static void Finish()
    {
        SessionState.SetBool("InstrumentInstallation.Test",false);while(routines.Count>0)(routines.Pop() as IDisposable)?.Dispose();
        Application.logMessageReceived-=Log;
        if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
        foreach(var d in suspended)if(d.added)InputSystem.EnableDevice(d);suspended.Clear();SampleDiscoveryChecks.RestoreGameSize();
        InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Application.runInBackground=background;
        Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/play-checks.txt",checks.Concat(new[]{$"RESULT checks={checks.Count} failures={failures}"}));EditorApplication.isPlaying=false;
    }
}
