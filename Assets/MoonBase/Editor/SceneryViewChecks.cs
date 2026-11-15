using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Power;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
using StarterAssets;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
static class SceneryViewChecks
{
    const string Output="QA/SceneryView";
    static readonly Stack<IEnumerator> routines=new();
    static readonly List<string> checks=new(), errors=new();
    static readonly List<InputDevice> suspended=new();
    static Keyboard keyboard;
    static Mouse mouse;
    static Gamepad gamepad;
    static MoonBasePlayer player;
    static MoonSceneryView view;
    static int lastFrame=-1, failures;
    static float deadline;
    static bool background;
    static InputSettings.BackgroundBehavior inputBackground;
    static InputSettings.EditorInputBehaviorInPlayMode editorInput;
    static SceneryViewChecks(){EditorApplication.update+=Update;}
    [MenuItem("Tools/Moon Base/Validate Scenery View")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        SessionState.SetBool("SceneryView.Test",true);EditorApplication.isPlaying=true;
    }
    static void Update()
    {
        if(!SessionState.GetBool("SceneryView.Test",false)||!EditorApplication.isPlaying||EditorApplication.isPaused||Time.frameCount==lastFrame)return;
        lastFrame=Time.frameCount;
        try
        {
            if(player && InputHub.Active && InputHub.Active.GameplayAllowed)
            {player.SendMessage("OnApplicationFocus",true);view.SendMessage("OnApplicationFocus",true);player.SetControlCaptured(true);}
            if(routines.Count==0)
            {
                checks.Clear();errors.Clear();failures=0;deadline=Time.realtimeSinceStartup+100;
                Application.logMessageReceived+=Log;routines.Push(Cases());
            }
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Scenery view validation timed out.");
            while(routines.Count>0)
            {
                var top=routines.Peek();if(!top.MoveNext()){routines.Pop();continue;}
                if(top.Current is IEnumerator child){routines.Push(child);continue;}return;
            }
            Finish();
        }
        catch(Exception e){Check(false,e.ToString());Finish();}
    }
    static void Check(bool ok,string description){checks.Add((ok?"PASS ":"FAIL ")+description);if(!ok)failures++;}
    static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    static void Stick(Vector2 value,bool press=false)
    {var state=new GamepadState{rightStick=value};if(press)state=state.WithButton(GamepadButton.RightStick);InputSystem.QueueStateEvent(gamepad,state);}
    static void MouseDelta(float x,float y)=>InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(x,y)});
    static IEnumerator Frames(int count){while(count-->0)yield return null;}
    static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    static float Horizontal(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
    static IEnumerator Capture(string name)
    {
        foreach(int width in new[]{1280,1920})
        {
            SampleDiscoveryChecks.SetGameSize(width,width==1280?720:1080);yield return Frames(6);
            string path=Path.GetFullPath(Output+$"/{name}-{width}.png");if(File.Exists(path))File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);yield return Frames(8);
            Check(File.Exists(path)&&new FileInfo(path).Length>30000,"Actual Game view capture: "+name+" "+width);
        }
    }

    static IEnumerator CheckSerialKnob(InputHub hub, HudController hud)
    {
        var serial = hub.Serial;
        var rover = player.rover;
        var demo = hub.GetComponent<CustomInputDemoPanel>();
        var origin = rover.transform.position;
        int sequence = 0;
        void Knob(int value, int move = 500, int buttons = 0) =>
            serial.InjectLine($"S,{sequence++},{value},0,500,{move},500,500,{buttons}");

        Knob(300);
        yield return Frames(4);
        Check(!view.IsActive, "A knob already above the threshold on connection does not open scenery");
        Knob(0);
        yield return Frames(3);
        Knob(9);
        yield return Frames(3);
        Check(!view.IsActive, "Serial K below 10 leaves gameplay active");
        Knob(10);
        yield return Wait(1.2f);
        var low = view.ViewPosition;
        Check(view.IsActive && player.IsDriving && hub.CurrentContext == view.inputContext &&
            !view.gameplayHud.activeSelf && low.y >= origin.y + 10 - .01f,
            "Serial K=10 opens HUD-free rover scenery at the lowest clear height");

        Knob(505, 1000, 2);
        yield return Frames(6);
        var midpoint = view.ViewPosition;
        Check(player.IsDriving && Horizontal(rover.transform.position, origin) < .02f && !demo.IsVisible,
            "Vehicle movement and the Serial example-menu button cannot interrupt scenery");
        Knob(1000);
        yield return Frames(6);
        var high = view.ViewPosition;
        Check(high.y > low.y && Horizontal(low, high) < .01f &&
            Mathf.Abs(midpoint.y - (low.y + high.y) * .5f) < .02f,
            "Serial K=505 gives the exact midpoint of a vertical 10–1000 interpolation");
        yield return Capture("serial-knob-top");

        hud.Pause.Pause();
        yield return Frames(3);
        Knob(250);
        yield return Frames(5);
        Check(hud.Pause.IsPaused && view.ViewPosition == high, "Pausing also pauses knob height changes");
        hud.Pause.Resume();
        yield return Frames(5);
        Check(Mathf.Abs(view.ViewPosition.y - Mathf.Lerp(low.y, high.y, 240f / 990f)) < .02f,
            "Closing pause applies the current knob height");

        Keys(Key.L);
        yield return Frames(4);
        Keys();
        Knob(1000);
        yield return Frames(4);
        Check(!view.IsActive && view.gameplayHud.activeSelf,
            "L exits knob scenery and turning farther above 10 does not reopen it");
        Stick(Vector2.zero, true);
        yield return Frames(4);
        Stick(Vector2.zero);
        yield return Frames(3);
        Check(view.IsActive && Vector3.Distance(view.ViewPosition, high) < .02f,
            "R3 enters at the very same top height as Serial K=1000");
        var buttonView = view.ViewPosition;
        Knob(0);
        yield return Frames(4);
        Check(view.IsActive && view.ViewPosition == buttonView,
            "Lowering the knob does not change scenery entered with a button");
        Stick(Vector2.zero, true);
        yield return Frames(4);
        Stick(Vector2.zero);
        yield return Frames(3);
        Knob(10);
        yield return Frames(4);
        Knob(9);
        yield return Frames(5);
        Check(!view.IsActive && player.IsDriving && view.gameplayHud.activeSelf &&
            hub.CurrentContext == hub.settings.gameplay, "K below 10 exits knob scenery and restores the occupied rover view and HUD");

        Knob(600);
        yield return Frames(4);
        Check(view.IsActive, "Raising the knob through the threshold again reopens scenery");
        serial.Disconnect();
        yield return Frames(4);
        Check(!view.IsActive && view.gameplayHud.activeSelf,
            "Losing the Serial sample exits knob scenery and restores the HUD");
    }

    static IEnumerator Cases()
    {
        background=Application.runInBackground;Application.runInBackground=true;yield return Frames(8);
        HudPlayValidation.EnterGameplay();
        player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();view=player.sceneryView;
        var hub=InputHub.Active;var session=UnityEngine.Object.FindFirstObjectByType<SurvivalSession>();
        hub.Serial.BeginSimulation();
        var hud=UnityEngine.Object.FindFirstObjectByType<HudController>();var research=UnityEngine.Object.FindFirstObjectByType<ResearchInteraction>();
        var panel=UnityEngine.Object.FindFirstObjectByType<PowerPanel>();var brain=Camera.main.GetComponent<CinemachineBrain>();
        var motor=player.GetComponent<ThirdPersonController>();var cc=player.GetComponent<CharacterController>();
        session.Scenario.time.gameHoursPerSecond=.02f;
        inputBackground=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var d in InputSystem.devices.ToArray())if(d is Keyboard||d is Mouse||d is Gamepad){suspended.Add(d);InputSystem.DisableDevice(d);}
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();gamepad=InputSystem.AddDevice<Gamepad>();
        yield return Frames(6);player.SetControlCaptured(true);
        var oldFollow=player.thirdPersonCamera.Follow;var follow=player.thirdPersonCamera.GetComponent<CinemachineThirdPersonFollow>();
        float oldDistance=follow.CameraDistance;var oldLook=oldFollow.rotation;
        Check(view && !view.IsActive && !view.sceneryCamera.gameObject.activeSelf,"Scenery camera and controller are authored and start inactive");
        Check(session.ContainsPlayer(),"Walking test begins in the actual base interior");
        Check(hub.Actions.FindAction("Player/SceneryView").bindings.Any(b=>b.path=="<Keyboard>/l") &&
            hub.Actions.FindAction("Player/SceneryView").bindings.Any(b=>b.path=="<Gamepad>/rightStickPress"),"L and gamepad right-stick click bind to the same logical action");
        Vector3 position=player.transform.position;
        Keys(Key.L);yield return Frames(4);Keys();
        float blendDeadline = Time.realtimeSinceStartup + 6f;
        while ((brain.IsBlending || !ReferenceEquals(brain.ActiveVirtualCamera, view.sceneryCamera)) &&
            Time.realtimeSinceStartup < blendDeadline)
            yield return null;
        Check(view.IsActive && !player.IsDriving && hub.CurrentContext==view.inputContext,"Keyboard L enters scenery view while walking indoors");
        Check(view.ViewPosition.y>=position.y+view.height-.01f,"Scenery viewpoint rises to configured height despite the real base roof");
        Check(!brain.IsBlending && Vector3.Distance(Camera.main.transform.position,view.ViewPosition)<.1f,
            "Cinemachine completes the upward blend to the elevated camera");
        Check(view.sceneryCamera.GetComponent<CinemachineThirdPersonFollow>()==null && view.sceneryCamera.Follow==null,"Elevated camera has no third-person collision solver or follow target");
        var cameraPosition=view.ViewPosition;float yaw=view.Yaw;float xAngle=Camera.main.transform.eulerAngles.x;
        MouseDelta(0,800);Keys(Key.W,Key.Space,Key.LeftShift,Key.T,Key.E);yield return Frames(6);
        Check(Mathf.Abs(Mathf.DeltaAngle(yaw,view.Yaw))<.01f && Mathf.Abs(Mathf.DeltaAngle(xAngle,Camera.main.transform.eulerAngles.x))<.05f,"Vertical mouse input cannot change pitch or yaw");
        Check(Vector3.Distance(view.ViewPosition,cameraPosition)<.001f && Horizontal(player.transform.position,position)<.02f && !player.IsDriving,"Movement, jump and boarding inputs cannot move the walking observer");
        Check(research.Nearby==null && !panel.CanReach(),"Research and power-console interactions are unavailable in scenery view");
        MouseDelta(500,500);yield return Frames(4);Keys();
        Check(Mathf.Abs(Mathf.DeltaAngle(yaw,view.Yaw))>.1f && Mathf.Abs(Mathf.DeltaAngle(view.pitch,Camera.main.transform.eulerAngles.x))<.05f,"Horizontal mouse input rotates only world Y while mixed vertical input is ignored");
        Check(Mathf.Abs(Mathf.DeltaAngle(Camera.main.transform.eulerAngles.z,0))<.05f && Vector3.Distance(Camera.main.transform.position,cameraPosition)<.1f,"Yaw rotation keeps camera roll and elevated position fixed");
        var researchHud = UnityEngine.Object.FindFirstObjectByType<ResearchHud>(FindObjectsInactive.Include);
        var prompt = (TMPro.TMP_Text)new SerializedObject(researchHud).FindProperty("prompt").objectReferenceValue;
        Check(!prompt.gameObject.activeInHierarchy && view.gameplayHud && !view.gameplayHud.activeSelf,"Scenery hides the Mission Board and all gameplay HUD");
        double hours=session.Run.Hours;yield return Wait(.2f);Check(session.Run.Hours>hours && Time.timeScale==1,"Scenery mode leaves game time running");
        yield return Capture("walking-scenery");
        Keys(Key.Escape);yield return Frames(5);Keys();yield return Frames(4);
        yaw=view.Yaw;MouseDelta(600,600);yield return Frames(4);
        Check(hud.Pause.IsPaused && view.IsActive && Time.timeScale==0 && view.Yaw==yaw,"Pause freezes the current scenery view without losing it");
        Keys(Key.L);yield return Frames(3);Keys();Check(view.IsActive,"Scenery toggle cannot bypass the pause menu");
        Keys(Key.Escape);yield return Frames(4);Keys();yield return Frames(4);
        Check(!hud.Pause.IsPaused && view.IsActive && hub.CurrentContext==view.inputContext,"Resuming returns to the scenery input context");
        Keys(Key.L);yield return Frames(4);Keys();yield return Wait(1.0f);
        Check(!view.IsActive && ReferenceEquals(brain.ActiveVirtualCamera,player.thirdPersonCamera),"Second L restores the normal walking camera");
        Check(player.thirdPersonCamera.Follow==oldFollow && follow.CameraDistance==oldDistance && Quaternion.Angle(oldLook,oldFollow.rotation)<.1f,"Walking follow target, distance and look direction are preserved");

        var rover=player.rover;
        Check(rover.TryDismount(cc,out var approach),"An actual walkable rover-side position is available");
        cc.enabled=false;player.transform.position=approach;cc.enabled=true;Physics.SyncTransforms();Keys();yield return Frames(8);
        Stick(Vector2.zero,true);yield return Frames(4);Stick(Vector2.zero);yield return Wait(1.2f);
        Check(view.IsActive && !player.IsDriving,"Gamepad right-stick click enters scenery while walking outdoors");
        yaw=view.Yaw;Stick(new Vector2(0,1));yield return Wait(.2f);Stick(Vector2.zero);yield return Frames(3);
        Check(Mathf.Abs(Mathf.DeltaAngle(yaw,view.Yaw))<.01f,"Vertical gamepad stick input is ignored");
        Stick(new Vector2(.8f,.8f));yield return Wait(.25f);Stick(Vector2.zero);yield return Frames(3);
        Check(Mathf.Abs(Mathf.DeltaAngle(yaw,view.Yaw))>1 && Mathf.Abs(Mathf.DeltaAngle(Camera.main.transform.eulerAngles.x,view.pitch))<.05f,"Gamepad horizontal look uses rate input and keeps pitch fixed");
        Stick(Vector2.zero,true);yield return Frames(4);Stick(Vector2.zero);yield return Wait(1.0f);
        Check(!view.IsActive,"Second gamepad right-stick click exits scenery");
        Keys(Key.T);yield return Frames(4);Keys();yield return Frames(4);
        Check(player.IsDriving,"Actual T input boards the existing rover after viewing scenery");
        Vector3 roverPosition=rover.transform.position;
        Keys(Key.L,Key.W);yield return Frames(4);Keys(Key.W,Key.T,Key.Space);yield return Wait(1.2f);
        Check(view.IsActive && player.IsDriving && !cc.enabled,"Scenery view opens while retaining the occupied rover state");
        Check(Horizontal(rover.transform.position,roverPosition)<.02f && (!rover.driveAudio || !rover.driveAudio.isPlaying),"Rover throttle is suppressed and driving audio stops during scenery viewing");
        yield return Capture("rover-scenery");
        Keys();Keys(Key.L);yield return Frames(4);Keys();yield return Wait(1.0f);
        Check(!view.IsActive && player.IsDriving && player.thirdPersonCamera.Follow==rover.cameraTarget && follow.CameraDistance==rover.CameraDistance,"Returning from scenery restores the rover camera without dismounting");
        var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.name="Temporary scenery roof validation";
        roof.transform.position=rover.transform.position+Vector3.up*(view.height+15);roof.transform.localScale=new Vector3(30,2,30);Physics.SyncTransforms();
        try
        {
            Keys(Key.L);yield return Frames(4);Keys();yield return Wait(1.2f);
            Check(view.IsActive && player.IsDriving && view.ViewPosition.y>=roof.GetComponent<Collider>().bounds.max.y+view.roofClearance-.01f,"An occupied rover under a roof taller than the configured rise still gets a viewpoint above the roof");
            Check(Vector3.Distance(Camera.main.transform.position,view.ViewPosition)<.1f,"Camera is not pulled back below the tall roof by obstruction avoidance");
            view.enabled=false;yield return Frames(5);
            Check(!view.IsActive && hub.CurrentContext==hub.settings.gameplay && !view.sceneryCamera.gameObject.activeSelf,"Disabling the component releases the camera and only its own input context");
            view.enabled=true;
        }
        finally{UnityEngine.Object.DestroyImmediate(roof);}
        yield return CheckSerialKnob(hub, hud);
        Keys();yield return Frames(5);Keys(Key.L);yield return Frames(4);Keys();
        var shield=UnityEngine.Object.FindFirstObjectByType<ActiveRadiationShield>();
        session.Run.Advance(100,true,shield);yield return Frames(5);
        Check(!view.IsActive && !hub.GameplayAllowed && Time.timeScale==0,"Mission end exits scenery without resuming the ended game or removing its terminal input block");
        Check(errors.Count==0,"No Unity errors during the scenery scenarios: "+string.Join(" | ",errors));
    }
    static void Finish()
    {
        SessionState.SetBool("SceneryView.Test",false);while(routines.Count>0)(routines.Pop() as IDisposable)?.Dispose();
        if(view)view.Exit();Application.logMessageReceived-=Log;
        foreach(var d in new InputDevice[]{keyboard,mouse,gamepad})if(d!=null && d.added)InputSystem.RemoveDevice(d);
        foreach(var d in suspended)if(d.added)InputSystem.EnableDevice(d);suspended.Clear();SampleDiscoveryChecks.RestoreGameSize();
        InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Application.runInBackground=background;
        File.WriteAllLines(Output+"/play-checks.txt",checks.Concat(new[]{$"RESULT checks={checks.Count} failures={failures}"}));EditorApplication.isPlaying=false;
    }
}
