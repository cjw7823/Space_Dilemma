using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StarterAssets;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class MoonBaseValidation
{
    const string Out="QA/MoonBase";
    static readonly List<string> checks=new List<string>();
    static int failures,stage,lastFrame;
    static float stageTime,walkDistance,walkDuration,peak,startY;
    static Vector3 origin,stageOrigin,stopPosition;
    static GameObject movementWall;
    static Vector3 roverStart;
    static Quaternion rotation;
    static Quaternion lookingBefore;
    static Keyboard keyboard;
    static Mouse mouse;
    static readonly List<string> errors=new List<string>();
    static MoonBaseValidation() { EditorApplication.update+=Update; }
    static void Check(bool pass,string message) {checks.Add((pass?"PASS ":"FAIL ")+message); if(!pass) failures++;}
    static GameObject Map()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="Map");
    static MoonBasePlayer Player()=>UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();
    [MenuItem("Tools/Moon Base/Validate Materials and Scene")]
    public static void Run()
    {
        Directory.CreateDirectory(Out); checks.Clear(); failures=0;
        var player=Player(); var map=Map();
        Check(player,"Starter Assets player exists");
        Check(Terrain.activeTerrains.Length==16,"All 16 terrain tiles present");
        var renderers=map.GetComponentsInChildren<Renderer>(true);
        var mats=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
        Check(renderers.Length==292,"Existing base renderer count preserved: "+renderers.Length);
        Check(mats.Length==15,"15 base materials connected: "+mats.Length);
        Check(mats.All(m=>m.shader.name.StartsWith("Universal Render Pipeline/") && m.shader.isSupported),"All base materials use supported URP shaders");
        foreach(var mat in mats)
        {
            string filename=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mat));
            string originalName=filename.Substring(0,filename.Length-4);
            var ids=AssetDatabase.FindAssets("t:Material",new[]{"Assets/Sci-Fi Styled Modular Pack"});
            var src=ids.Select(id=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id))).First(m=>m.name==originalName);
            Check(!src.HasProperty("_MainTex") || src.GetTexture("_MainTex")==mat.GetTexture("_BaseMap"),"Preserved base texture: "+originalName);
            if(originalName=="glass" || originalName=="window") Check(mat.GetFloat("_Surface")==1 && mat.GetFloat("_ZWrite")==0,"Transparent glass retained: "+originalName);
            if(originalName.Contains("lattice")) Check(mat.IsKeywordEnabled("_ALPHATEST_ON"),"Cutout lattice retained: "+originalName);
        }
        var motor=player.GetComponent<ThirdPersonController>(); var cc=player.GetComponent<CharacterController>();
        Check(motor && cc && player.GetComponent<Animator>().runtimeAnimatorController,"Official motor, CharacterController and Animator connected");
        Check((motor.GroundLayers.value&(1<<player.gameObject.layer))==0,"Ground layer mask excludes player capsule");
        Check(player.thirdPersonCamera.Follow==motor.CinemachineCameraTarget.transform,"Third-person camera follows the character eye target");
        Check(player.transform.parent.GetComponentsInChildren<CinemachineCamera>(true).Length==1,"Player rig contains one virtual camera");
        Check(player.thirdPersonCamera.GetComponent<CinemachineThirdPersonFollow>().AvoidObstacles.Enabled,"Third person collision avoidance enabled");
        var pi=player.GetComponent<PlayerInput>();
        Check(new[]{"Move","Look","Jump","Sprint"}.All(a=>pi.actions.FindAction("Player/"+a,false)!=null),"Official Input System actions connected");
        var old=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="Main Camera");
        Check(!old.activeSelf && old.transform.position==new Vector3(0,1,-10),"Original camera preserved and inactive");
        Check(map.GetComponentsInChildren<MoonBaseDoor>(true).Length==5,"Five existing doors connected");
        Physics.SyncTransforms();
        Check(!Physics.CheckCapsule(player.transform.position+Vector3.up*.35f,player.transform.position+Vector3.up*1.5f,.27f,motor.GroundLayers,QueryTriggerInteraction.Ignore),"Spawn has standing capsule clearance");
        var before=JsonUtility.FromJson<MoonBaseSetup.TransformRows>(File.ReadAllText(Out+"/transforms-before.json"));
        var now=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToDictionary(MoonBaseSetup.PathOf,t=>t);
        var changed=before.rows.Where(r=>!now.ContainsKey(r.path) || Vector3.Distance(r.position,now[r.path].position)>.005f || Quaternion.Angle(r.rotation,now[r.path].rotation)>.01f || Vector3.Distance(r.scale,now[r.path].lossyScale)>.001f).Select(r=>r.path).ToArray();
        Check(changed.Length==0,"Existing world transforms preserved (changes="+string.Join(",",changed)+")");
        var doorInfo=map.GetComponentsInChildren<MoonBaseDoor>(true).Select(d=>$"{d.name} controller={d.GetComponent<Animator>().runtimeAnimatorController.name} position={d.transform.position:F3} forward={d.transform.forward} right={d.transform.right} bounds={d.GetComponent<Collider>()?.bounds}");
        File.WriteAllLines(Out+"/doors.txt",doorInfo);
        var go=new GameObject("QA overview camera"); var camera=go.AddComponent<Camera>();
        camera.nearClipPlane=.1f; camera.farClipPlane=40000; camera.fieldOfView=60;
        camera.clearFlags=CameraClearFlags.Skybox; go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
        try
        {
            go.transform.position=map.transform.position+new Vector3(30,22,-40); go.transform.LookAt(map.transform.position+new Vector3(0,2,-3));
            foreach(int width in new[]{1280,1920}) Render(camera,width,Out+"/base-urp-"+width+".png");
        }
        finally {UnityEngine.Object.DestroyImmediate(go);}
        Save("editor-validation.txt");
    }
    static void Save(string file) { File.WriteAllLines(Out+"/"+file,checks.Concat(new[]{"RESULT passes="+(checks.Count-failures)+" failures="+failures})); }
    public static void Render(Camera camera,int width,string file)
    {
        int height=width*9/16;
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var active=RenderTexture.active; var target=camera.targetTexture; float aspect=camera.aspect;
        var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt; camera.aspect=(float)width/height; camera.Render(); RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            File.WriteAllBytes(file,texture.EncodeToPNG());
            // A magenta shader-error pixel is saturated red+blue with little green.
            int magenta=texture.GetPixels32().Count(c=>c.r>200&&c.b>200&&c.g<40);
            Check(magenta<width*height*.001,"Render has no large error-magenta surface: "+Path.GetFileName(file)+" pixels="+magenta);
        }
        finally { camera.targetTexture=target; camera.aspect=aspect; RenderTexture.active=active; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture); }
    }
    [MenuItem("Tools/Moon Base/Validate Play Mode")]
    public static void StartPlay()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool("MoonBase.PlayCheck",true);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        EditorApplication.isPlaying=true;
    }
    static void OnLog(string condition,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(condition); }
    static void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys)); }
    static void Warp(MoonBasePlayer p)
    { var cc=p.GetComponent<CharacterController>(); cc.enabled=false; p.transform.SetPositionAndRotation(origin,rotation); cc.enabled=true; Physics.SyncTransforms(); }
    static void Next(int next,MoonBasePlayer p) {stage=next; stageTime=Time.time; stageOrigin=p.transform.position;}
    static void Update()
    {
        if(!SessionState.GetBool("MoonBase.PlayCheck",false)||!EditorApplication.isPlaying||EditorApplication.isPaused||lastFrame==Time.frameCount) return;
        lastFrame=Time.frameCount;
        try { PlayUpdate(); }
        catch(Exception e)
        {
            Directory.CreateDirectory(Out); File.WriteAllText(Out+"/play-error.txt",e.ToString());
            Check(false,e.Message); Finish();
        }
    }
    static void PlayUpdate()
    {
        var p=Player(); if(!p) throw new InvalidOperationException("Missing player in Play mode");
        var motor=p.GetComponent<ThirdPersonController>();
        var camera=Camera.main;
        if(stage==0)
        {
            checks.Clear(); failures=0; errors.Clear(); Application.logMessageReceived+=OnLog;
            var brain = camera.GetComponent<CinemachineBrain>();
            var target = p.thirdPersonCamera.Follow;
            var follow = p.thirdPersonCamera.GetComponent<CinemachineThirdPersonFollow>();
            float distance = Vector3.Dot(target.position - camera.transform.position, target.forward);
            Check(brain.ActiveVirtualCamera == p.thirdPersonCamera, "First sampled frame uses the third-person camera");
            Check(distance >= 1.48f, $"Camera starts {distance:F2} m behind the character");
            Check(!Physics.CheckSphere(camera.transform.position, camera.nearClipPlane, follow.AvoidObstacles.CollisionFilter,
                QueryTriggerInteraction.Ignore), "Starting camera near plane clears scene colliders");
            keyboard=InputSystem.AddDevice<Keyboard>("MoonBaseQATestKeyboard"); mouse=InputSystem.AddDevice<Mouse>("MoonBaseQATestMouse");
            p.GetComponent<PlayerInput>().SwitchCurrentControlScheme("KeyboardMouse",keyboard,mouse);
            p.SetControlCaptured(true); p.showControls=false;
            Keys(); Next(1,p); return;
        }
        float elapsed=Time.time-stageTime;
        switch(stage)
        {
            case 1:
                if(elapsed<1.5f) return;
                Check(motor.Grounded,"Spawn settles on base floor"); origin=p.transform.position; rotation=p.transform.rotation;
                p.SetControlCaptured(true);
                Check(p.ControlCaptured,"Player captures controls in Play mode");
                foreach(int w in new[]{1280,1920}) Render(camera,w,Out+"/play-third-"+w+".png");
                Keys(Key.V); Next(2,p); break;
            case 2:
                if(elapsed<.1f) return;
                Keys();
                Check(camera.GetComponent<CinemachineBrain>().ActiveVirtualCamera==p.thirdPersonCamera,"V leaves third-person camera active");
                lookingBefore=motor.CinemachineCameraTarget.transform.rotation;
                InputSystem.QueueStateEvent(mouse,new MouseState {delta=new Vector2(0,40)}); Next(3,p); break;
            case 3:
                if(elapsed<.15f) return;
                Check(Quaternion.Angle(lookingBefore,motor.CinemachineCameraTarget.transform.rotation)>.5f,"Mouse look rotates camera target");
                // Aim the walking check down clear floor space. The spawn camera may turn
                // away from the authored direction to avoid nearby furniture.
                var walkTarget = motor.CinemachineCameraTarget.transform;
                Vector3 walkDirection = FindClearWalkingDirection(p.GetComponent<CharacterController>(), camera.transform.forward);
                p.transform.rotation = Quaternion.LookRotation(walkDirection);
                walkTarget.rotation = p.transform.rotation;
                motor.SyncLookDirection();
                p.thirdPersonCamera.PreviousStateIsValid = false;
                rotation=p.transform.rotation;
                origin=p.transform.position;
                Next(4,p); break;
            case 4:
                if (elapsed < .1f) return;
                Keys(Key.W); Next(5,p); break;
            case 5:
                if(elapsed<1) return;
                walkDuration=elapsed; walkDistance=Vector2.Distance(new Vector2(stageOrigin.x,stageOrigin.z),new Vector2(p.transform.position.x,p.transform.position.z));
                Check(walkDistance>1,"W moves the official controller: "+walkDistance+" m");
                Check(p.GetComponent<Animator>().GetFloat("Speed")>0,"Walking drives the official Animator");
                Keys(); Next(6,p); break;
            case 6:
                if(elapsed<.08f) return;
                float releaseDrift = Vector3.ProjectOnPlane(p.transform.position - stageOrigin, Vector3.up).magnitude;
                Check(releaseDrift<.006f,$"Horizontal movement stops on W release: {releaseDrift:F4} m drift");
                stopPosition=p.transform.position; Next(7,p); break;
            case 7:
                if(elapsed<.5f) return;
                Check(Vector2.Distance(new Vector2(stopPosition.x,stopPosition.z),new Vector2(p.transform.position.x,p.transform.position.z))<.03f,"Key release clears horizontal input");
                Warp(p); Keys(Key.W,Key.LeftShift); Next(8,p); break;
            case 8:
                if(elapsed<.65f) return;
                float distance=Vector2.Distance(new Vector2(stageOrigin.x,stageOrigin.z),new Vector2(p.transform.position.x,p.transform.position.z));
                Check(distance/elapsed>walkDistance/walkDuration*1.25f,"Shift sprint is faster than walking");
                Keys(); Warp(p); Next(9,p); break;
            case 9:
                if(elapsed<.6f) return;
                startY=p.transform.position.y; peak=startY; Keys(Key.Space); Next(10,p); break;
            case 10:
                peak=Mathf.Max(peak,p.transform.position.y);
                if(elapsed<2) return;
                Check(peak-startY>.5f,"Space performs a jump: rise="+(peak-startY));
                Check(Mathf.Abs(p.transform.position.y-startY)<.2f&&motor.Grounded,"Holding Space does not repeatedly jump after landing");
                Keys(); p.SetControlCaptured(false); Next(11,p); break;
            case 11:
                if(elapsed<.1f) return;
                Keys(Key.W,Key.Space); Next(12,p); break;
            case 12:
                if(elapsed<.3f) return;
                Check(p.GetComponent<StarterAssetsInputs>().move==Vector2.zero&&!p.GetComponent<StarterAssetsInputs>().jump,"Released cursor ignores movement and jump");
                Keys();
                Warp(p);
                p.SetControlCaptured(true);
                var heading = Quaternion.Euler(0, camera.transform.eulerAngles.y, 0);
                p.transform.rotation = heading;
                movementWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                movementWall.name = "Temporary movement collision check";
                movementWall.GetComponent<Renderer>().enabled = false;
                movementWall.transform.SetPositionAndRotation(p.transform.position + Vector3.up + heading * Vector3.forward * .5f, heading);
                movementWall.transform.localScale = new Vector3(3f, 2f, .2f);
                Physics.SyncTransforms();
                Keys(Key.W); Next(13,p); break;
            case 13:
                if(elapsed<.65f) return;
                Check(p.GetComponent<Animator>().GetFloat("Speed")<.1f,"Walking into a wall stops the locomotion animation");
                Keys();
                DestroyMovementWall();
                if (p.rover)
                {
                    var rover = p.rover;
                    var cc = p.GetComponent<CharacterController>();
                    Check(!rover.CanBoard(p.transform.position), "Rover can't be boarded from the base interior");
                    if (!rover.TryDismount(cc, out var boardPoint)) throw new InvalidOperationException("No clear boarding position.");
                    cc.enabled = false;
                    p.transform.position = boardPoint;
                    cc.enabled = true;
                    Physics.SyncTransforms();
                    Keys(Key.T); Next(14,p); break;
                }
                Check(errors.Count==0,"No Play mode errors: "+string.Join(" | ",errors)); Finish(); break;
            case 14:
                if(elapsed<.2f) return;
                Check(p.IsDriving && !motor.enabled && !p.GetComponent<CharacterController>().enabled,"T boards rover and disables walking");
                Check(p.thirdPersonCamera.Follow==p.rover.cameraTarget,"Third-person camera follows the rover");
                foreach (int width in new[] {1280,1920}) Render(camera,width,Out+"/rover-driving-"+width+".png");
                roverStart = p.rover.transform.position;
                Keys(Key.W); Next(15,p); break;
            case 15:
                if(elapsed<2f) return;
                float roverDistance = Vector3.ProjectOnPlane(p.rover.transform.position - roverStart, Vector3.up).magnitude;
                Check(roverDistance>motor.SprintSpeed*elapsed*1.5f,$"Rover forward travel is faster than sprinting: {roverDistance:F2} m / {elapsed:F2}s");
                Check(Vector3.Distance(p.transform.position,p.rover.driverSeat.position)<.01f,"Player stays with the rover while driving");
                var driven = p.rover.transform;
                var chassis = p.rover.GetComponent<BoxCollider>();
                movementWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                movementWall.name = "Temporary rover collision check";
                movementWall.GetComponent<Renderer>().enabled = false;
                movementWall.transform.SetPositionAndRotation(
                    driven.position + driven.forward * (chassis.size.z * .5f + .8f) + Vector3.up, driven.rotation);
                movementWall.transform.localScale = new Vector3(6f, 4f, .2f);
                Physics.SyncTransforms();
                Next(18,p); break;
            case 18:
                if(elapsed<.5f) return;
                float clearance = Vector3.Dot(movementWall.transform.position - p.rover.transform.position, p.rover.transform.forward);
                Check(clearance >= p.rover.GetComponent<BoxCollider>().size.z * .45f,
                    $"Rover stops before crossing obstacle: clearance={clearance:F3} m");
                DestroyMovementWall();
                Keys(); Next(16,p); break;
            case 16:
                if(elapsed<.1f) return;
                Keys(Key.T); Next(17,p); break;
            case 17:
                if(elapsed<.3f) return;
                Check(!p.IsDriving && motor.enabled && p.GetComponent<CharacterController>().isGrounded,"T dismounts to clear ground and restores walking");
                Check(p.thirdPersonCamera.Follow==motor.CinemachineCameraTarget.transform,"Dismount restores player camera target");
                Keys();
                Check(errors.Count==0,"No Play mode errors: "+string.Join(" | ",errors)); Finish(); break;
        }
    }
    static void Finish()
    {
        DestroyMovementWall();
        Application.logMessageReceived-=OnLog;
        if(keyboard!=null&&keyboard.added) InputSystem.RemoveDevice(keyboard);
        if(mouse!=null&&mouse.added) InputSystem.RemoveDevice(mouse);
        Save("play-validation.txt"); stage=0; lastFrame=-1;
        SessionState.SetBool("MoonBase.PlayCheck",false); EditorApplication.isPlaying=false;
    }

    static void DestroyMovementWall()
    {
        if (movementWall) UnityEngine.Object.Destroy(movementWall);
        movementWall = null;
    }

    static Vector3 FindClearWalkingDirection(CharacterController controller, Vector3 cameraForward)
    {
        Vector3 forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
        Vector3 center = controller.transform.TransformPoint(controller.center);
        Vector3 segment = Vector3.up * (controller.height * .5f - controller.radius);
        float bestDistance = -1f;
        Vector3 bestDirection = forward;
        foreach (float turn in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
        {
            Vector3 direction = Quaternion.AngleAxis(turn, Vector3.up) * forward;
            float clearance = Physics.CapsuleCast(center - segment, center + segment, controller.radius * .95f,
                direction, out var hit, 4f, ~(1 << controller.gameObject.layer), QueryTriggerInteraction.Ignore) ? hit.distance : 4f;
            if (clearance <= bestDistance) continue;
            bestDistance = clearance;
            bestDirection = direction;
        }
        return bestDirection;
    }
}
