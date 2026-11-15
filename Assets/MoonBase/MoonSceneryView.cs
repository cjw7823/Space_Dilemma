using SpaceDilemma.CustomInput;
using SpaceDilemma.Survival;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>A fixed elevated viewpoint with yaw-only look and no camera collision solver.</summary>
[DefaultExecutionOrder(-150)]
public sealed class MoonSceneryView : MonoBehaviour
{
    public MoonBasePlayer player;
    public SurvivalSession session;
    public CinemachineCamera sceneryCamera;
    public InputContextProfile inputContext;
    [Min(10), Tooltip("Height above the player or occupied rover when entering the view.")]
    public float height = 80f;
    [Range(-20, 60), Tooltip("Fixed downward viewing angle; vertical look input is ignored.")]
    public float pitch = 12f;
    [Min(1)] public float roofClearance = 5f;
    public bool IsActive { get; private set; }
    public Vector3 ViewPosition { get; private set; }
    public float Yaw { get; private set; }
    InputHub hub;
    int contextToken;
    bool focused = true;

    void Update()
    {
        if (!hub) hub = InputHub.Active;
        if (IsActive && session && session.Run.Outcome != SurvivalOutcome.Running) { Exit(); return; }
        if (!hub || !player || !focused || Time.timeScale <= 0 || !hub.GameplayAllowed) return;
        if (hub.WasPressed("SceneryView")) Toggle();
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
    public void Enter()
    {
        if (!hub) hub = InputHub.Active;
        if (IsActive || !hub || !hub.GameplayAllowed || !inputContext || !sceneryCamera || !player ||
            !focused || Time.timeScale <= 0 || (session && session.Run.Outcome != SurvivalOutcome.Running)) return;
        Vector3 anchor = player.IsDriving && player.rover ? player.rover.transform.position : player.transform.position;
        ViewPosition = anchor + Vector3.up * Mathf.Max(10, height);
        // Lift above a roof too, rather than letting third-person collision pull the view below it.
        foreach (var hit in Physics.RaycastAll(anchor + Vector3.up * .3f, Vector3.up, Mathf.Infinity,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var collider = hit.collider;
            if (collider is TerrainCollider || collider.transform.IsChildOf(player.transform) ||
                (player.rover && collider.transform.IsChildOf(player.rover.transform))) continue;
            var position = ViewPosition;
            position.y = Mathf.Max(position.y, collider.bounds.max.y + roofClearance);
            ViewPosition = position;
        }
        var output = Camera.main;
        Vector3 forward = output ? Vector3.ProjectOnPlane(output.transform.forward, Vector3.up) : player.transform.forward;
        Yaw = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y : player.transform.eulerAngles.y;
        sceneryCamera.transform.SetPositionAndRotation(ViewPosition, Quaternion.Euler(pitch, Yaw, 0));
        sceneryCamera.PreviousStateIsValid = false;
        contextToken = hub.PushContext(inputContext);
        IsActive = true;
        player.SetControlCaptured(true);
        if (player.IsDriving && player.rover) player.rover.Stop();
        sceneryCamera.gameObject.SetActive(true);
    }

    public void Exit()
    {
        if (!IsActive) return;
        IsActive = false;
        if (sceneryCamera) sceneryCamera.gameObject.SetActive(false);
        if (hub && contextToken != 0) hub.PopContext(contextToken);
        contextToken = 0;
    }
    void OnApplicationFocus(bool value) => focused = value;
    void OnDisable() => Exit();
}
