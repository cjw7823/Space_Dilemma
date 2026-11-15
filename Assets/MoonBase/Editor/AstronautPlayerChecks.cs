using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
using StarterAssets;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
static class AstronautPlayerChecks
{
    const string Output="QA/AstronautPlayer";
    static readonly Stack<IEnumerator> routines=new();
    static readonly List<string> checks=new(),errors=new();
    static readonly List<InputDevice> suspended=new();
    static Keyboard keyboard;
    static Gamepad gamepad;
    static MoonBasePlayer player;
    static int lastFrame=-1,failures;
    static float deadline;
    static bool background;
    static InputSettings.BackgroundBehavior inputBackground;
    static InputSettings.EditorInputBehaviorInPlayMode editorInput;
    static AstronautPlayerChecks(){EditorApplication.update+=Update;}
    [MenuItem("Tools/Moon Base/Validate Astronaut Player")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
        SessionState.SetBool("AstronautPlayer.Test",true);EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string text){checks.Add((ok?"PASS ":"FAIL ")+text);if(!ok)failures++;}
    static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack);}
    static void Update()
    {
        if(!SessionState.GetBool("AstronautPlayer.Test",false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;
        lastFrame=Time.frameCount;
        try
        {
            if(player&&InputHub.Active&&InputHub.Active.GameplayAllowed){player.SendMessage("OnApplicationFocus",true);player.SetControlCaptured(true);}
            if(routines.Count==0){checks.Clear();errors.Clear();failures=0;deadline=Time.realtimeSinceStartup+150;Application.logMessageReceived+=Log;routines.Push(Cases());}
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Astronaut checks timed out.");
            while(routines.Count>0)
            {
                var top=routines.Peek();if(!top.MoveNext()){routines.Pop();continue;}
                if(top.Current is IEnumerator child){routines.Push(child);continue;}return;
            }
            Finish();
        }
        catch(Exception e){Check(false,e.ToString());Finish();}
    }
    static IEnumerator Frames(int n){while(n-->0)yield return null;}
    static IEnumerator Wait(float s){float end=Time.realtimeSinceStartup+s;while(Time.realtimeSinceStartup<end)yield return null;}
    static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    static IEnumerator Capture(string label,SkinnedMeshRenderer skin,bool front=true)
    {
        var camera=Camera.main;var brain=camera.GetComponent<CinemachineBrain>();bool active=brain.enabled;float fov=camera.fieldOfView;
        Vector3 pos=camera.transform.position;Quaternion rot=camera.transform.rotation;
        try
        {
            brain.enabled=false;camera.fieldOfView=40;
            void Frame(){camera.transform.position=player.transform.position+new Vector3(front?2.3f:-2.3f,1.45f,front?3.4f:-3.4f);camera.transform.LookAt(player.transform.position+Vector3.up*.95f);}
            foreach(int width in new[]{1280,1920})
            {
                SampleDiscoveryChecks.SetGameSize(width,width==1280?720:1080);
                for(int i=0;i<8;i++){Frame();yield return null;}
                string path=Path.GetFullPath(Output+$"/{label}-{width}.png");Frame();ScreenCapture.CaptureScreenshot(path);
                for(int i=0;i<5;i++){Frame();yield return null;}
                Check(File.Exists(path)&&new FileInfo(path).Length>30000,$"Actual scene {label} captured at {width}");
            }
        }
        finally{camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(pos,rot);brain.enabled=active;}
    }
    static Vector3[] Bake(SkinnedMeshRenderer skin)
    {var mesh=new Mesh();skin.BakeMesh(mesh);var result=mesh.vertices;UnityEngine.Object.DestroyImmediate(mesh);return result;}
    static double Difference(Vector3[] a,Vector3[] b)=>a.Where((v,i)=>i%31==0).Select((v,j)=>Vector3.Distance(v,b[j*31])).Average();
    static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
    static IEnumerator Cases()
    {
        background=Application.runInBackground;Application.runInBackground=true;inputBackground=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var d in InputSystem.devices.ToArray())if(d is Keyboard||d is Mouse||d is Gamepad){suspended.Add(d);InputSystem.DisableDevice(d);}
        keyboard=InputSystem.AddDevice<Keyboard>();gamepad=InputSystem.AddDevice<Gamepad>();yield return Frames(12);
        HudPlayValidation.EnterGameplay();
        player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();var motor=player.GetComponent<ThirdPersonController>();var cc=player.GetComponent<CharacterController>();var animator=player.GetComponent<Animator>();
        var session=UnityEngine.Object.FindFirstObjectByType<SurvivalSession>();session.Scenario.time.gameHoursPerSecond=.02f;
        var skin=player.transform.Find("Astronaut Visual").GetComponent<SkinnedMeshRenderer>();
        Check(skin&&skin.enabled&&animator.avatar&&animator.avatar.isValid&&animator.avatar.isHuman,"Authored astronaut skin uses a valid Humanoid avatar on the original player Animator");
        Check(animator.GetBoneTransform(HumanBodyBones.Hips)?.name=="Suit_Hips"&&skin.bones.All(b=>b&&b.IsChildOf(player.transform)),"Avatar resolves the new suit skeleton rather than the old mannequin");
        Check(player.GetComponentsInChildren<Renderer>(true).Where(r=>r!=skin).All(r=>!r.enabled),"Previous character mesh is disabled and retained");
        Check(!animator.applyRootMotion&&motor.Gravity==-1.62f&&Math.Abs(motor.JumpHeight-1.2f)<.001f,"Existing movement ownership, lunar gravity and jump height are retained");
        Check(skin.sharedMesh.vertexCount==19143&&skin.sharedMesh.subMeshCount==2&&skin.sharedMaterials.All(m=>m&&m.mainTexture),"Derived skin preserves source topology, two textured material slots and UV data");
        Check(skin.sharedMesh.boneWeights.All(w=>Math.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.0001),"Every vertex has normalized skin weights with at most four influences");
        void Place(Vector3 point){cc.enabled=false;player.transform.SetPositionAndRotation(point,Quaternion.identity);cc.enabled=true;Physics.SyncTransforms();player.thirdPersonCamera.Follow.rotation=Quaternion.Euler(10,0,0);motor.SyncLookDirection();player.SetControlCaptured(true);}
        var site=UnityEngine.Object.FindObjectsByType<ResearchTarget>(FindObjectsSortMode.None).First(t=>t.kind==ResearchKind.Instrument);
        var point=site.transform.position+new Vector3(-5,1,-4);Place(point);Keys();yield return Wait(2.0f);
        var idle=Bake(skin);
        var rest=skin.sharedMesh.vertices;
        var pack=Enumerable.Range(0,rest.Length).Where(i=>AstronautPlayerSetup.IsBackpack(rest[i])).ToArray();
        int packBottom=pack.OrderBy(i=>rest[i].y).First(),packTop=pack.OrderByDescending(i=>rest[i].y).First();
        float packSpan=Vector3.Distance(rest[packBottom],rest[packTop]);
        Check(pack.Length>100&&pack.All(i=>skin.sharedMesh.boneWeights[i].weight0>.999f&&skin.bones[skin.sharedMesh.boneWeights[i].boneIndex0].name=="Suit_UpperChest"),"The entire rear pack, including its below-waist panel, is rigidly bound to the chest");
        Check(idle.All(Finite)&&idle.Max(v=>v.y)-idle.Min(v=>v.y)<2.1f,"Idle deformation is finite and remains within human scale");
        Check(idle.Min(v=>v.y)>-.10f&&idle.Min(v=>v.y)<.15f,$"Idle boot sole stays near the controller ground origin ({idle.Min(v=>v.y):F3}m)");
        yield return Capture("idle-front",skin);yield return Capture("idle-back",skin,false);
        var start=player.transform.position;Keys(Key.W);yield return Wait(.6f);var walk=Bake(skin);
        Check(Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up).magnitude>.25f&&animator.GetFloat("Speed")>.1f,"Virtual W drives the existing walk controller and animation parameters");
        Check(Difference(idle,walk)>.015,"Walking visibly deforms the astronaut mesh rather than moving a rigid figure");
        Check(Mathf.Abs(Vector3.Distance(walk[packBottom],walk[packTop])-packSpan)<.015f,"Walking preserves rigid backpack dimensions");
        yield return Capture("walking",skin);
        Keys(Key.W,Key.LeftShift);yield return Wait(.45f);var run=Bake(skin);
        Check(animator.GetFloat("Speed")>motor.MoveSpeed+.2f&&Difference(walk,run)>.01,"Sprint uses a distinct animated running pose");
        yield return Capture("running",skin);
        Keys();yield return Wait(.6f);Check(motor.Grounded,"Player is grounded before jumping");
        float baseY=player.transform.position.y;Keys(Key.Space);yield return Frames(2);Keys();yield return Wait(.55f);var jump=Bake(skin);
        Check(player.transform.position.y>baseY+.2f&&!motor.Grounded,"Virtual Space keeps the existing lunar jump working");
        Check(jump.All(Finite)&&Difference(idle,jump)>.015,"Jump and fall poses animate the new skin without invalid vertices");
        Check(Mathf.Abs(Vector3.Distance(jump[packBottom],jump[packTop])-packSpan)<.015f,"Jumping cannot stretch the rear pack toward the legs or head");
        yield return Capture("jump",skin);yield return Wait(3.2f);
        Check(motor.Grounded,"Astronaut lands and returns to grounded locomotion");
        var pause=UnityEngine.Object.FindFirstObjectByType<HudPauseManager>();pause.Pause();yield return Frames(3);
        Check(Time.timeScale==0&&pause.IsPaused,"Existing pause still freezes the game with the new character");pause.Resume();yield return Frames(3);
        Check(Time.timeScale==1&&!pause.IsPaused,"Pause resumes normally");
        var rover=player.rover;Check(rover.TryDismount(cc,out var approach),"A valid approach point exists beside the actual rover");Place(approach);Keys();yield return Frames(5);
        Keys(Key.T);yield return Frames(5);Keys();yield return Frames(5);
        Check(player.IsDriving&&skin.forceRenderingOff&&!animator.enabled,"Rover boarding hides the astronaut and suspends its Animator");
        Keys(Key.L);yield return Wait(1.1f);Keys();yield return Frames(3);
        Check(player.IsObservingScenery&&player.IsDriving,"Scenery mode remains available while riding with the new skin");
        InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.RightStick));yield return Frames(4);InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return Frames(4);
        Check(!player.IsObservingScenery&&player.IsDriving,"Gamepad R3 returns from scenery without dismounting");
        Keys(Key.T);yield return Frames(5);Keys();yield return Frames(5);
        Check(!player.IsDriving&&!skin.forceRenderingOff&&skin.enabled&&animator.enabled,"Dismount restores astronaut visibility and animation");
        Check(player.GetComponentsInChildren<Renderer>(true).Where(r=>r!=skin).All(r=>!r.enabled),"Dismount never reveals the retained mannequin");
        Keys(Key.W);yield return Wait(.4f);Keys();yield return Frames(5);
        Check(animator.GetBoneTransform(HumanBodyBones.Head).name=="Suit_Head"&&Bake(skin).All(Finite),"Animation remains bound and valid after the rover/scenery round trip");
        Check(errors.Count==0,"No new Unity errors during astronaut scenarios: "+string.Join(" | ",errors));
    }
    static void Finish()
    {
        SessionState.SetBool("AstronautPlayer.Test",false);while(routines.Count>0)(routines.Pop() as IDisposable)?.Dispose();Application.logMessageReceived-=Log;
        if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(gamepad!=null&&gamepad.added)InputSystem.RemoveDevice(gamepad);
        foreach(var d in suspended)if(d.added)InputSystem.EnableDevice(d);suspended.Clear();SampleDiscoveryChecks.RestoreGameSize();
        InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Application.runInBackground=background;
        Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/play-checks.txt",checks.Concat(new[]{$"RESULT checks={checks.Count} failures={failures}"}));EditorApplication.isPlaying=false;
    }
}
