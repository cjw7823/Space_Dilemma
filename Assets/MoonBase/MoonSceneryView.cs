using SpaceDilemma.CustomInput;
using SpaceDilemma.Survival;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>An elevated viewpoint with yaw-only look and optional Serial knob height control.</summary>
[DefaultExecutionOrder(-150)]
public sealed class MoonSceneryView : MonoBehaviour
{
    const int KnobEntryPosition = 10;
    const int KnobMaximumPosition = 1000;
    const float MinimumViewHeight = 10f;

    public MoonBasePlayer player;
    public SurvivalSession session;
    public CinemachineCamera sceneryCamera;
    public InputContextProfile inputContext;
    public LunarEnvironment environment;
    public GameObject gameplayHud;
    public bool faceEarthOnEnter = true;
    [Min(10), Tooltip("Height above the player or occupied rover when entering the view.")]
    public float height = 1000f;
    [Range(-20, 60), Tooltip("Fixed downward viewing angle; vertical look input is ignored.")]
    public float pitch = 12f;
    [Min(1)] public float roofClearance = 5f;
    public bool IsActive { get; private set; }
    public Vector3 ViewPosition { get; private set; }
    public float Yaw { get; private set; }
    InputHub hub;
    int contextToken;
    bool focused = true;
    bool hudWasActive;
    int? previousKnobPosition;
    bool knobControlsHeight;
    float minimumViewY, maximumViewY;

    void Update()
    {
        if (!hub) hub = InputHub.Active;
        int? knobPosition = hub ? hub.Serial?.RawKnobPosition : null;
        bool crossedKnobThreshold = previousKnobPosition is < KnobEntryPosition &&
            knobPosition is >= KnobEntryPosition;
        previousKnobPosition = knobPosition;
        if (IsActive && session && session.Run.Outcome != SurvivalOutcome.Running) { Exit(); return; }
        if (!hub || !player || !focused || Time.timeScale <= 0 || !hub.GameplayAllowed) return;

        if (hub.WasPressed("SceneryView"))
            Toggle();
        else if (knobControlsHeight)
        {
            if (knobPosition is >= KnobEntryPosition)
                SetKnobHeight(knobPosition.Value);
            else
                Exit();
        }
        else if (!IsActive && crossedKnobThreshold)
            Enter(knobPosition);

        if (IsActive && hub.CurrentContext == inputContext && player.ControlCaptured)
        {
            float delta = hub.ReadLookDelta(player.mouseSensitivity, Time.deltaTime).x;
            Yaw = Mathf.Repeat(Yaw + delta, 360f);
        }
    }

    void LateUpdate()
    {
        if (IsActive && sceneryCamera)
            sceneryCamera.transform.SetPositionAndRotation(ViewPosition, Quaternion.Euler(pitch, Yaw, 0));
    }

    public void Toggle() { if (IsActive) Exit(); else Enter(); }
    public void Enter() => Enter(null);

    void Enter(int? knobPosition)
    {
        if (!hub) hub = InputHub.Active;
        if (IsActive || !hub || !hub.GameplayAllowed || !inputContext || !sceneryCamera || !player ||
            !focused || Time.timeScale <= 0 || (session && session.Run.Outcome != SurvivalOutcome.Running)) return;

        Vector3 anchor = player.IsDriving && player.rover ? player.rover.transform.position : player.transform.position;
        minimumViewY = anchor.y + MinimumViewHeight;
        // Both endpoints must clear any roof overhead at the entry position.
        foreach (var hit in Physics.RaycastAll(anchor + Vector3.up * .3f, Vector3.up, Mathf.Infinity,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var collider = hit.collider;
            if (collider is TerrainCollider || collider.transform.IsChildOf(player.transform) ||
                (player.rover && collider.transform.IsChildOf(player.rover.transform))) continue;
            minimumViewY = Mathf.Max(minimumViewY, collider.bounds.max.y + roofClearance);
        }

        var highestView = new Vector3(anchor.x, Mathf.Max(minimumViewY, anchor.y + height), anchor.z);
        if (environment && faceEarthOnEnter) highestView = ClearEarthSightline(highestView);
        maximumViewY = highestView.y;
        ViewPosition = highestView;
        knobControlsHeight = knobPosition.HasValue;
        if (knobControlsHeight) SetKnobHeight(knobPosition.Value);

        var output = Camera.main;
        Vector3 forward = environment && faceEarthOnEnter ? Vector3.ProjectOnPlane(environment.EarthDirection,Vector3.up) :
            output ? Vector3.ProjectOnPlane(output.transform.forward, Vector3.up) : player.transform.forward;
        Yaw = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y : player.transform.eulerAngles.y;
        sceneryCamera.transform.SetPositionAndRotation(ViewPosition, Quaternion.Euler(pitch, Yaw, 0));
        sceneryCamera.PreviousStateIsValid = false;
        contextToken = hub.PushContext(inputContext);
        IsActive = true;
        if(gameplayHud){hudWasActive=gameplayHud.activeSelf;gameplayHud.SetActive(false);}
        player.SetControlCaptured(true);
        if (player.IsDriving && player.rover) player.rover.Stop();
        sceneryCamera.gameObject.SetActive(true);
    }

    void SetKnobHeight(int position)
    {
        float t = Mathf.InverseLerp(KnobEntryPosition, KnobMaximumPosition, position);
        ViewPosition = new Vector3(ViewPosition.x, Mathf.Lerp(minimumViewY, maximumViewY, t), ViewPosition.z);
    }

    public void Exit()
    {
        if (!IsActive) return;
        IsActive = false;
        knobControlsHeight = false;
        if(gameplayHud)gameplayHud.SetActive(hudWasActive);
        if (sceneryCamera) sceneryCamera.gameObject.SetActive(false);
        if (hub && contextToken != 0) hub.PopContext(contextToken);
        contextToken = 0;
    }
    Vector3 ClearEarthSightline(Vector3 position)
    {
        Vector3 direction=environment.EarthDirection.normalized;
        if(direction.sqrMagnitude<.5f)return position;
        Vector3 right=Vector3.Cross(Vector3.up,direction).normalized;
        Vector3 up=Vector3.Cross(direction,right).normalized;
        float radius=Mathf.Tan((environment.AppliedEarthDiameter*.5f+.15f)*Mathf.Deg2Rad);
        var rays=new[]{direction,(direction+right*radius).normalized,(direction-right*radius).normalized,
            (direction+up*radius).normalized,(direction-up*radius).normalized};
        for(int attempt=0;attempt<4;attempt++)
        {
            float nextY=position.y;
            foreach(var ray in rays)foreach(var hit in Physics.RaycastAll(position,ray,50000,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.transform.IsChildOf(player.transform)||
                    (player.rover&&hit.collider.transform.IsChildOf(player.rover.transform)))continue;
                nextY=Mathf.Max(nextY,hit.collider.bounds.max.y+Mathf.Max(50,roofClearance));
            }
            if(nextY<=position.y+.01f)break;
            position.y = nextY;
        }
        return position;
    }
    void OnApplicationFocus(bool value) => focused = value;
    void OnDisable()
    {
        Exit();
        previousKnobPosition = null;
    }
}
