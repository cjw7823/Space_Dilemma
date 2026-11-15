using StarterAssets;
using SpaceDilemma.CustomInput;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Input capture and startup framing for the Starter Assets third-person motor.</summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(ThirdPersonController),typeof(StarterAssetsInputs),typeof(PlayerInput))]
public sealed class MoonBasePlayer : MonoBehaviour, IGameplayInputClient
{
    public CinemachineCamera thirdPersonCamera;
    public MoonRover rover;
    public MoonSceneryView sceneryView;
    [Range(.02f,3f)] public float mouseSensitivity=.12f;
    public bool showControls=true;
    public bool ControlCaptured { get; private set; }
    public bool IsDriving { get; private set; }
    public bool IsObservingScenery => sceneryView && sceneryView.IsActive;
    public bool ExitBlocked => exitBlocked;
    ThirdPersonController motor;
    StarterAssetsInputs input;
    PlayerInput playerInput;
    CharacterController bodyCollider;
    Renderer[] avatarRenderers;
    Animator avatarAnimator;
    bool animatorWasEnabled;
    Transform walkingTarget;
    CinemachineThirdPersonFollow follow;
    float walkingCameraDistance;
    float driveYaw, drivePitch;
    bool exitBlocked;
    bool focused=true;

    void Awake()
    {
        motor=GetComponent<ThirdPersonController>(); input=GetComponent<StarterAssetsInputs>(); playerInput=GetComponent<PlayerInput>();
        bodyCollider = GetComponent<CharacterController>();
        avatarRenderers = GetComponentsInChildren<Renderer>();
        avatarAnimator = GetComponentInChildren<Animator>();
        if (thirdPersonCamera)
        {
            walkingTarget = thirdPersonCamera.Follow;
            follow = thirdPersonCamera.GetComponent<CinemachineThirdPersonFollow>();
            walkingCameraDistance = follow.CameraDistance;
            if (rover) follow.AvoidObstacles.CollisionFilter &= ~(1 << rover.gameObject.layer);
        }
        // The prefab uses layer 8. Never let its ground probe detect its own capsule.
        motor.GroundLayers &= ~(1<<gameObject.layer);
        motor.ResponsiveLocomotion = true;
        input.cursorLocked=false; input.cursorInputForLook=false;
        SetControlCaptured(false);
    }

    // Run before the Starter Assets controller reads its starting look direction.
    void Start()
    {
        if (!thirdPersonCamera || !thirdPersonCamera.Follow) return;

        Physics.SyncTransforms();
        Transform target = thirdPersonCamera.Follow;
        Quaternion originalAvatarRotation = transform.rotation;
        Quaternion originalLook = target.rotation;
        Quaternion bestTurn = Quaternion.identity;
        float bestClearance = float.NegativeInfinity;
        const float minimumFramingDistance = 1.5f;
        Vector3 up = Vector3.up;

        // Keep the authored angle when possible. In a cramped spawn, test nearby headings first.
        foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
        {
            Quaternion turn = Quaternion.AngleAxis(angle, up);
            transform.rotation = turn * originalAvatarRotation;
            target.rotation = turn * originalLook;
            thirdPersonCamera.InternalUpdateCameraState(up, -1f);
            var state = thirdPersonCamera.State;
            float backDistance = Vector3.Dot(target.position - state.GetFinalPosition(), target.forward);
            if (backDistance > bestClearance)
            {
                bestClearance = backDistance;
                bestTurn = turn;
            }
            if (backDistance >= minimumFramingDistance) break;
        }

        transform.rotation = bestTurn * originalAvatarRotation;
        target.rotation = bestTurn * originalLook;
        thirdPersonCamera.InternalUpdateCameraState(up, -1f);
        var readyState = thirdPersonCamera.State;
        var output = Camera.main;
        if (output && output.transform.parent == transform.parent)
            output.transform.SetPositionAndRotation(readyState.GetFinalPosition(), readyState.GetFinalOrientation());
    }
    void Update()
    {
        var keyboard=Keyboard.current; var mouse=Mouse.current;
        var unified = InputHub.Active;
        bool useUnified = unified && unified.player == playerInput;
        if (useUnified && !unified.GameplayAllowed)
        {
            ClearInput();
            if (IsDriving) rover.Stop();
            return;
        }
        if (useUnified && focused && !ControlCaptured && unified.NonPointerGameplayActivity)
            SetControlCaptured(true);
        bool released=false;
        bool pauseActionAvailable = useUnified && unified.Actions.FindAction("Global/Pause", false) != null;
        if(keyboard!=null && (pauseActionAvailable ? keyboard.tabKey.wasPressedThisFrame : keyboard.escapeKey.wasPressedThisFrame)) { SetControlCaptured(false); released=true; }
        bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
        if(!released && focused && !ControlCaptured && !overUi && mouse!=null && mouse.leftButton.wasPressedThisFrame)
            SetControlCaptured(true);

        if (IsObservingScenery)
        {
            ClearInput();
            if (IsDriving && rover)
            {
                rover.Stop();
                transform.SetPositionAndRotation(rover.driverSeat.position, rover.transform.rotation);
            }
            return;
        }

        // Read the live PlayerInput action copy every frame, including releases.
        // Events alone can leave stale movement after focus changes or held jumps after landing.
        var actions=playerInput.actions;
        if(!ControlCaptured || !focused || actions==null)
        {
            ClearInput();
            if (IsDriving) rover.Stop();
            return;
        }
        Vector2 move=useUnified ? unified.ReadVector("Move") : actions.FindAction("Player/Move",false)?.ReadValue<Vector2>() ?? Vector2.zero;
        input.move=move.sqrMagnitude<.0225f ? Vector2.zero : Vector2.ClampMagnitude(move,1);
        input.look=useUnified ? unified.ReadLookInput(mouseSensitivity) : (actions.FindAction("Player/Look",false)?.ReadValue<Vector2>() ?? Vector2.zero)*mouseSensitivity;
        // Rate input must pass the motor's small-value threshold BEFORE multiplying by dt.
        input.lookFromTouch = useUnified && unified.LookUsesRate;
        if (rover && (useUnified ? unified.WasPressed("Interact") : keyboard != null && keyboard.tKey.wasPressedThisFrame))
        {
            if (IsDriving) LeaveRover();
            else if (rover.CanBoard(transform.position)) BoardRover();
        }
        if (IsDriving)
        {
            rover.Drive(input.move, Time.deltaTime);
            Vector2 frameLook = input.look * (input.lookFromTouch ? Time.deltaTime : 1f);
            driveYaw += frameLook.x;
            drivePitch = Mathf.Clamp(drivePitch + frameLook.y, -12f, 65f);
            rover.cameraTarget.rotation = rover.transform.rotation * Quaternion.Euler(drivePitch, driveYaw, 0f);
            transform.SetPositionAndRotation(rover.driverSeat.position, rover.transform.rotation);
            return;
        }
        if (!useUnified) input.lookFromTouch=false;
        input.jump=useUnified ? unified.WasPressed("Jump") : actions.FindAction("Player/Jump",false)?.WasPressedThisFrame() ?? false;
        input.sprint=useUnified ? unified.IsPressed("Sprint") : actions.FindAction("Player/Sprint",false)?.IsPressed() ?? false;
    }

    void BoardRover()
    {
        IsDriving = true;
        exitBlocked = false;
        driveYaw = 0f;
        drivePitch = 24f;
        motor.enabled = false;
        bodyCollider.enabled = false;
        if (avatarAnimator)
        {
            animatorWasEnabled = avatarAnimator.enabled;
            if (animatorWasEnabled)
            {
                avatarAnimator.SetFloat("Speed", 0f);
                avatarAnimator.SetFloat("MotionSpeed", 0f);
                avatarAnimator.enabled = false;
            }
        }
        foreach (var renderer in avatarRenderers) renderer.forceRenderingOff = true;
        follow.CameraDistance = rover.CameraDistance;
        rover.cameraTarget.rotation = rover.transform.rotation * Quaternion.Euler(drivePitch, 0f, 0f);
        thirdPersonCamera.Follow = rover.cameraTarget;
        thirdPersonCamera.PreviousStateIsValid = false;
    }

    void LeaveRover()
    {
        if (!rover.TryDismount(bodyCollider, out var position))
        {
            exitBlocked = true;
            return;
        }

        IsDriving = false;
        exitBlocked = false;
        transform.SetPositionAndRotation(position, rover.transform.rotation);
        walkingTarget.rotation = Quaternion.Euler(10f, rover.transform.eulerAngles.y, 0f);
        motor.SyncLookDirection();
        motor.Grounded = true;
        bodyCollider.enabled = true;
        motor.enabled = true;
        if (avatarAnimator) avatarAnimator.enabled = animatorWasEnabled;
        foreach (var renderer in avatarRenderers) renderer.forceRenderingOff = false;
        follow.CameraDistance = walkingCameraDistance;
        thirdPersonCamera.Follow = walkingTarget;
        thirdPersonCamera.PreviousStateIsValid = false;
        ClearInput();
    }
    public void SetControlCaptured(bool captured)
    {
        ControlCaptured=captured && focused;
        if(input) { input.cursorLocked=ControlCaptured; input.cursorInputForLook=ControlCaptured; }
        Cursor.lockState=ControlCaptured?CursorLockMode.Locked:CursorLockMode.None;
        Cursor.visible=!ControlCaptured;
        if(!ControlCaptured) ClearInput();
    }
    void ClearInput() { if(input) {input.move=Vector2.zero; input.look=Vector2.zero; input.jump=false; input.sprint=false;} }
    void OnApplicationFocus(bool hasFocus) { focused=hasFocus; if(!hasFocus) SetControlCaptured(false); }
    void OnDisable() { if (IsDriving && rover) rover.Stop(); SetControlCaptured(false); }
}
