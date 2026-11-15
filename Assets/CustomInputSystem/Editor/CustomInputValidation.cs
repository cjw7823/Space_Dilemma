using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpaceDilemma.CustomInput.Editor
{
    [InitializeOnLoad]
    public static class CustomInputValidation
    {
        const string Output = CustomInputSetup.Root + "/Tests/Results";
        const string Running = "CustomInput.Validation.Running";
        const string ExitPending = "CustomInput.Validation.ExitPending";
        const string Batch = "CustomInput.Validation.Batch";
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        static int failures, lastFrame = -1;
        static float deadline;
        static Keyboard keyboard;
        static Mouse mouse;
        static Gamepad pad;
        static InputHub hub;
        static MoonBasePlayer player;
        static int seq;
        static InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        static InputSettings.BackgroundBehavior originalBackground;

        static CustomInputValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(ExitPending, false) && !EditorApplication.isPlaying)
                {
                    SessionState.SetBool(ExitPending, false);
                    if (SessionState.GetBool(Batch, false)) EditorApplication.Exit(SessionState.GetInt("CustomInput.Validation.Failures", 1) == 0 ? 0 : 1);
                }
            };
        }
        [MenuItem("Tools/Custom Input/Run All Checks (Editor + Play)")]
        public static void RunInteractive() { SessionState.SetBool(Batch, false); Run(); }
        public static void RunBatch() { SessionState.SetBool(Batch, true); Run(); }
        static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            Directory.CreateDirectory(Output);
            CustomInputSetup.CreateDefaults();
            checks.Clear(); failures = 0;
            try { EditorChecks(); } catch (Exception ex) { Check(false, ex.ToString()); }
            Save("editor-checks.txt");
            SessionState.SetInt("CustomInput.Validation.EditorFailures", failures);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetInt("CustomInput.Validation.Terrains", UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }
        static void Check(bool ok, string message)
        { checks.Add((ok ? "PASS " : "FAIL ") + message); if (!ok) failures++; }
        static void Save(string name)
        { Directory.CreateDirectory(Output); File.WriteAllLines(Output + "/" + name, checks.Concat(new[] { $"RESULT checks={checks.Count} failures={failures}" })); }

        static void EditorChecks()
        {
            var detectedPorts = WindowsSerialTransport.GetPortNames();
            Check(detectedPorts.All(name => name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)), "Native Windows COM enumeration succeeds; detected=" + detectedPorts.Length);
            var settings = new SerialPortSettings(); var calibration = new SerialCalibration();
            const string sample = "S,42,750,100,500,900,510,490,5";
            Check(SerialProtocol.TryParse(sample, settings, calibration, out var packet), "New protocol sample parses");
            Check(packet.sequence == 42 && packet.analog.SequenceEqual(new[] { 750, 100, 500, 900, 510, 490 }) && packet.buttons == 5, "K,S1,JX1,JY1,JX2,JY2 order preserved");
            foreach (string bad in new[] { "", "S,1,0,0,0,0,0,0", "S,1,0,0,0,0,0,0,0,0", "S,x,0,0,0,0,0,0,0", "S,1,1001,0,0,0,0,0,0", "S,65536,0,0,0,0,0,0,0", "S,1,-1,0,0,0,0,0,0", "S,1,0,0,0,0,0,0,16", "S,1, 0,0,0,0,0,0,0", "ID,1,CTRL_A,UNIT01,8" })
                Check(!SerialProtocol.TryParse(bad, settings, calibration, out _), "Malformed/out-of-range rejected: " + bad);
            Check(SerialProtocol.TryParse("S,0,0,1000,0,1000,0,1000,15", settings, calibration, out _), "Inclusive field bounds");
            var decoder = new SerialLineDecoder(96); var frames = new List<string>(); int invalid = 0;
            void Feed(string text) { var bytes = Encoding.ASCII.GetBytes(text); decoder.Feed(bytes, bytes.Length, frames.Add, () => invalid++); }
            Feed("S,42,750,"); Check(frames.Count == 0, "Partial packet buffered");
            Feed("100,500,900,510,490,5\r\n" + sample + "\n");
            Check(frames.Count == 2 && frames.All(s => s == sample), "Split and multiple packets; CRLF compatibility");
            Feed(new string('x', 96) + "\n" + sample + "\n");
            Check(invalid == 1 && frames.Last() == sample, "Oversize packet discarded through LF; parser recovers");
            decoder.Feed(new byte[] { 255, 10 }, 2, frames.Add, () => invalid++);
            Check(invalid == 2, "Non-ASCII packet discarded");
            Check(calibration.NormalizeStick(500, 500, calibration.joystick1X, calibration.joystick1Y) == Vector2.zero, "Configurable default neutral");
            Check(calibration.NormalizeStick(520, 500, calibration.joystick1X, calibration.joystick1Y) == Vector2.zero, "Radial dead zone");
            calibration.joystick1X.invert = true;
            Check(calibration.NormalizeStick(1000, 500, calibration.joystick1X, calibration.joystick1Y).x < -.99f, "Axis inversion");
            calibration.joystick1X.center = 600;
            Check(calibration.NormalizeStick(600, 500, calibration.joystick1X, calibration.joystick1Y) == Vector2.zero, "Non-default neutral calibration");
            calibration.joystick1X.center = 500; calibration.joystick1X.invert = false;
            calibration.slider.minimum = 100; calibration.slider.maximum = 900; calibration.slider.invert = true;
            Check(Mathf.Abs(calibration.slider.Normalize(300, false) - .75f) < .001f, "Scalar range, normalization and inversion");
            calibration.slider.sensitivity = .5f;
            Check(Mathf.Abs(calibration.slider.Normalize(300, false) - .375f) < .001f, "Scalar sensitivity is configurable");
            calibration.slider.enabled = false;
            Check(calibration.slider.Normalize(300, false) == 0, "Analog input can be disabled");
            calibration.joystick1X.minimum = -1000; calibration.joystick1X.center = 0;
            Check(SerialProtocol.TryParse("S,1,500,500,-1000,500,500,500,0", settings, calibration, out _), "Configured negative analog range is accepted");
            calibration = new SerialCalibration();
            var defaults = Resources.Load<CustomInputSettings>("CustomInputSettings");
            Check(defaults && defaults.actions && defaults.gameplay && defaults.ui && defaults.solarGenerator && defaults.oxygenGenerator, "Default settings and context assets generated");
            var stack = new InputContextStack(defaults.gameplay);
            int facility = stack.Push(defaults.solarGenerator), ui = stack.Push(defaults.ui);
            Check(stack.Current == defaults.ui, "UI priority overrides Facility");
            stack.Pop(ui); Check(stack.Current == defaults.solarGenerator, "UI pop restores previous facility");
            stack.Pop(facility); Check(stack.Current == defaults.gameplay, "Facility pop restores Gameplay");
            var equal = UnityEngine.Object.Instantiate(defaults.solarGenerator); equal.contextId = "Second facility";
            int first = stack.Push(defaults.solarGenerator), second = stack.Push(equal);
            Check(stack.Current == equal, "Equal priority uses last-pushed token"); stack.Pop(first);
            Check(stack.Current == equal, "Out-of-order pop preserves higher stack entry"); stack.Pop(second); UnityEngine.Object.DestroyImmediate(equal);

            using (var backend = new SerialInputBackend(settings, calibration))
            {
                var masks = new List<uint>(); backend.PacketReceived += (_, state) => masks.Add(state.buttons);
                backend.BeginSimulation(); backend.InjectLine("S,65535,500,0,500,500,500,500,0"); backend.Pump(SerialInputBackend.Now); InputSystem.Update();
                backend.InjectLine("S,0,500,0,500,500,500,500,2"); backend.InjectLine("S,1,500,0,500,500,500,500,0");
                backend.Pump(SerialInputBackend.Now); InputSystem.Update();
                Check(masks.SequenceEqual(new uint[] { 0, 2, 0 }), "Backend preserves every short button transition in receive order (Actions checked in Play mode)");
                Check(backend.Diagnostics.sequenceGaps == 0, "SEQ wraps at configured maximum");
                backend.InjectLine("S,3,500,0,500,500,500,500,2"); backend.Pump(SerialInputBackend.Now); InputSystem.Update();
                Check(backend.Diagnostics.sequenceGaps == 1, "SEQ gap counted");
                Check(backend.Device.button1.isPressed, "Held serial button reaches device state");
                backend.Disconnect(); InputSystem.Update(); Check(!backend.Device.button1.isPressed, "Disconnect releases held device state");
                calibration.buttonBits[0] = 3;
                backend.BeginSimulation(); backend.InjectLine("S,1,500,0,500,500,500,500,8"); backend.Pump(SerialInputBackend.Now); InputSystem.Update();
                Check(backend.Device.button0.isPressed, "Button bit routing independent of physical labels");
                calibration.buttonEnabled[0] = false;
                backend.InjectLine("S,2,500,0,500,500,500,500,8"); backend.Pump(SerialInputBackend.Now); InputSystem.Update();
                Check(!backend.Device.button0.isPressed, "Button channel can be disabled");
                calibration.knobInterpretation = KnobInterpretation.DeltaBetweenPackets;
                backend.InjectLine("S,3,600,0,500,500,500,500,0"); backend.Pump(SerialInputBackend.Now); InputSystem.Update();
                Check(Mathf.Abs(backend.Device.knob.ReadValue() - .1f) < .001f, "Optional knob delta interpretation");
            }
            TestTransportRecovery();
            var original = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Starter Assets/Runtime/InputSystem/StarterAssets.inputactions");
            foreach (var action in original.FindActionMap("Player").actions)
                Check(defaults.actions.FindAction("Player/" + action.name).id == action.id, "Existing Player action identity retained: " + action.name);
            Check(defaults.actions.controlSchemes.Any(s => s.name == "KeyboardMouse"), "Original control scheme names retained");
        }

        sealed class FakeTransport : ISerialTransport
        {
            public readonly ConcurrentQueue<byte[]> chunks = new ConcurrentQueue<byte[]>();
            public bool failOpen;
            public void Open(SerialPortSettings settings) { if (failOpen) throw new IOException("Simulated port busy"); }
            public int Read(byte[] buffer)
            { if (!chunks.TryDequeue(out var chunk)) { Thread.Sleep(5); return 0; } Array.Copy(chunk, buffer, chunk.Length); return chunk.Length; }
            public void Dispose() { }
        }
        static void TestTransportRecovery()
        {
            var first = new FakeTransport { failOpen = true }; var next = new FakeTransport(); int attempts = 0;
            var config = new SerialPortSettings { inputTimeoutSeconds = .1f, startupGraceSeconds = 0, reconnectSeconds = .05f };
            using (var backend = new SerialInputBackend(config, new SerialCalibration(), () => ++attempts == 1 ? first : next))
            {
                backend.Connect(); Thread.Sleep(30); backend.Pump(SerialInputBackend.Now);
                Check(backend.Status == SerialConnectionStatus.Faulted, "Connection failure is reported without throwing on main thread");
                Thread.Sleep(70); backend.Pump(SerialInputBackend.Now); Thread.Sleep(30);
                next.chunks.Enqueue(Encoding.ASCII.GetBytes("S,1,500,0,500,500,500,500,1\n"));
                Thread.Sleep(30); backend.Pump(SerialInputBackend.Now); InputSystem.Update();
                Check(attempts == 2 && backend.Status == SerialConnectionStatus.Connected && backend.Device.button0.isPressed, "Reconnect restores packets after failed open (fake transport)");
                backend.Pump(SerialInputBackend.Now + 1); InputSystem.Update();
                Check(backend.Diagnostics.timedOut && !backend.Device.button0.isPressed, "Timeout clears virtual device and held buttons");
                Thread.Sleep(30); backend.Pump(SerialInputBackend.Now + 2); Thread.Sleep(30);
                next.chunks.Enqueue(Encoding.ASCII.GetBytes("S,2,500,0,500,500,500,500,0\n"));
                Thread.Sleep(30); backend.Pump(SerialInputBackend.Now); InputSystem.Update();
                Check(backend.Status == SerialConnectionStatus.Connected && backend.Diagnostics.packets >= 2, "Packets resume after timeout/reconnect (fake transport)");
            }
        }

        static void Update()
        {
            if (SessionState.GetBool(ExitPending, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(ExitPending, false);
                if (SessionState.GetBool(Batch, false)) EditorApplication.Exit(SessionState.GetInt("CustomInput.Validation.Failures", 1) == 0 ? 0 : 1);
                return;
            }
            if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isPaused || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                if (routines.Count == 0)
                {
                    checks.Clear(); errors.Clear(); failures = 0; deadline = Time.realtimeSinceStartup + 90;
                    Application.logMessageReceived += OnLog;
                    routines.Push(PlayCases());
                }
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Play validation timeout");
                while (routines.Count > 0)
                {
                    var top = routines.Peek();
                    if (!top.MoveNext()) { routines.Pop(); continue; }
                    if (top.Current is IEnumerator child) { routines.Push(child); continue; }
                    return;
                }
                Finish();
            }
            catch (Exception ex) { Check(false, ex.ToString()); Finish(); }
        }
        static void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        static IEnumerator Wait(float seconds)
        { float until = Time.realtimeSinceStartup + seconds; do { yield return null; } while (Time.realtimeSinceStartup < until); }
        static void Packet(int x = 500, int y = 500, int x2 = 500, int y2 = 500, int slider = 0, int knob = 500, int buttons = 0)
        { hub.Serial.InjectLine($"S,{seq++ % 65536},{knob},{slider},{x},{y},{x2},{y2},{buttons}"); }
        static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static void Pad(Vector2 move = default, Vector2 look = default, params GamepadButton[] buttons)
        {
            var state = new GamepadState { leftStick = move, rightStick = look };
            foreach (var button in buttons) state = state.WithButton(button);
            InputSystem.QueueStateEvent(pad, state);
        }
        static IEnumerator PlayCases()
        {
            yield return Wait(.3f);
            HudPlayValidation.EnterGameplay();
            hub = InputHub.Active; player = UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();
            originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Check(hub && player, "Actual SampleScene bootstraps InputHub and existing MoonBasePlayer");
            int terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Check(terrains == SessionState.GetInt("CustomInput.Validation.Terrains", -1), "Scene Terrain count preserved in Play mode: " + terrains);
            var starter = player.GetComponent<StarterAssetsInputs>();
            var pi = player.GetComponent<PlayerInput>();
            Check(pi == hub.player && pi.actions == hub.Actions, "Existing PlayerInput uses the unified runtime Action copy");
            Check(pi.uiInputModule && pi.uiInputModule.actionsAsset == pi.actions, "InputSystemUIInputModule shares the actual PlayerInput actions");
            Check(pi.neverAutoSwitchControlSchemes, "Automatic exclusive device switching disabled");
            keyboard = InputSystem.AddDevice<Keyboard>("CustomInput test Keyboard"); mouse = InputSystem.AddDevice<Mouse>("CustomInput test Mouse"); pad = InputSystem.AddDevice<Gamepad>("CustomInput test HID-compatible Gamepad");
            hub.Serial.BeginSimulation(); Packet(); Keys(); Pad();
            yield return Wait(.15f); player.SendMessage("OnApplicationFocus", true); player.SetControlCaptured(true);
            Check(player.ControlCaptured, "Synthetic focus enables actual-player input in batch mode");
            Check(pi.devices.Contains(keyboard) && pi.devices.Contains(mouse) && pi.devices.Contains(pad) && pi.devices.Contains(hub.Serial.Device), "Keyboard, mouse, Gamepad and SerialController paired simultaneously");
            var walkMotor = player.GetComponent<ThirdPersonController>();
            var walkBody = player.GetComponent<CharacterController>();
            Vector3 clearDirection = FindClearWalkingDirection(walkBody, Camera.main.transform.forward);
            player.transform.rotation = Quaternion.LookRotation(clearDirection);
            walkMotor.CinemachineCameraTarget.transform.rotation = Quaternion.LookRotation(clearDirection);
            walkMotor.SyncLookDirection(); Camera.main.transform.rotation = Quaternion.LookRotation(clearDirection);
            yield return Wait(.15f);
            Vector3 walkStart = player.transform.position;
            Keys(Key.W); yield return Wait(.4f);
            Check(starter.move.y > .9f, "Existing WASD reaches StarterAssets movement");
            Check(Vector3.ProjectOnPlane(player.transform.position - walkStart, Vector3.up).magnitude > .02f, "Actual Starter Assets CharacterController moves in SampleScene");
            Keys(); yield return Wait(.1f); Check(starter.move == Vector2.zero, "Keyboard release clears movement");
            Keys(Key.W, Key.LeftShift); yield return Wait(.1f); Check(starter.sprint, "Existing Shift sprint preserved"); Keys(); yield return Wait(.2f);
            float jumpStart = player.transform.position.y;
            Keys(Key.Space); yield return Wait(.2f); Keys();
            Check(player.transform.position.y > jumpStart + .05f, "Existing Space jumps through actual Starter Assets motor");
            yield return Wait(.6f);
            var cameraTarget = player.GetComponent<ThirdPersonController>().CinemachineCameraTarget.transform;
            Quaternion before = cameraTarget.rotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100, 60) });
            yield return Wait(.15f); Check(Quaternion.Angle(before, cameraTarget.rotation) > .1f, "Mouse delta rotates the existing third-person camera");
            Pad(new Vector2(1, 0)); yield return Wait(.15f); Check(starter.move.x > .9f, "Unity Gamepad left stick reaches same movement action");
            before = cameraTarget.rotation; Pad(default, new Vector2(.8f, .2f)); yield return Wait(.2f);
            Check(Quaternion.Angle(before, cameraTarget.rotation) > 1, "Gamepad stick rotates actual camera using a rate, not mouse delta"); Pad();
            before = cameraTarget.rotation; Packet(x2: 1000); yield return Wait(.2f);
            Check(Quaternion.Angle(before, cameraTarget.rotation) > 1, "Serial joystick2 rotates the same camera"); Packet();
            Pad(); Packet(x: 0); yield return Wait(.15f); Check(starter.move.x < -.9f, "Serial joystick1 reaches same movement action");
            Pad(new Vector2(.5f, 0)); Packet(x: 1000); yield return Wait(.15f);
            Check(starter.move.x > .9f && starter.move.magnitude <= 1.001f, "Stronger vector wins without summing opposite devices");
            Pad(); Packet(); Keys(); yield return Wait(.15f);
            Pad(new Vector2(1, 0)); yield return Wait(.1f); Packet(x: 0); yield return Wait(.1f);
            Check(starter.move.x > .9f, "Equal-magnitude vector tie keeps current Gamepad owner");
            Pad(); yield return Wait(.1f); Check(starter.move.x < -.9f, "Vector ownership hands over when active device returns to neutral"); Packet(); yield return Wait(.1f);
            int performed = 0, canceled = 0;
            var jump = hub.Actions.FindAction("Player/Jump");
            Action<InputAction.CallbackContext> down = _ => performed++;
            Action<InputAction.CallbackContext> up = _ => canceled++;
            jump.performed += down; jump.canceled += up;
            Packet(buttons: 2); Packet(buttons: 0); yield return Wait(.15f);
            Check(performed == 1 && canceled == 1, "Short Serial jump press/release survives one frame on actual PlayerInput actions");
            Pad(default, default, GamepadButton.South); yield return Wait(.1f);
            Packet(buttons: 2); yield return Wait(.1f); Pad(); yield return Wait(.1f);
            Check(jump.IsPressed(), "Releasing Gamepad does not release Jump still held by Serial");
            Packet(); yield return Wait(.1f); Check(!jump.IsPressed(), "Button OR releases after all sources release");
            jump.performed -= down; jump.canceled -= up;

            // Exercise the existing T interaction path with non-keyboard devices.
            var rover = player.rover;
            Check(rover, "Existing rover reference retained");
            Vector3 walkingPosition = player.transform.position; Quaternion walkingRotation = player.transform.rotation;
            var capsule = player.GetComponent<CharacterController>(); var chassis = rover.GetComponent<BoxCollider>();
            var candidates = new[] { Vector3.right * (chassis.size.x * .5f + .65f), Vector3.left * (chassis.size.x * .5f + .65f), Vector3.back * (chassis.size.z * .5f + .65f) };
            Vector3 boardPosition = candidates.Select(rover.transform.TransformPoint).FirstOrDefault(p => rover.CanBoard(p));
            Check(boardPosition != Vector3.zero, "Scene has a reachable rover interaction position");
            capsule.enabled = false; player.transform.position = boardPosition; capsule.enabled = true; Physics.SyncTransforms();
            Pad(default, default, GamepadButton.West); yield return Wait(.15f); Pad();
            Check(player.IsDriving, "Gamepad Interact boards existing rover through same gameplay logic as T");
            Packet(buttons: 1); Packet(); yield return Wait(.15f);
            Check(!player.IsDriving, "Short Serial Interact exits rover through the existing dismount logic");
            capsule.enabled = false; player.transform.SetPositionAndRotation(walkingPosition, walkingRotation); capsule.enabled = true; Physics.SyncTransforms();
            yield return Wait(.2f);

            var demo = hub.GetComponent<CustomInputDemoPanel>();
            Packet(x: 1000); yield return Wait(.1f); demo.ToggleMenu();
            yield return Wait(.1f);
            Check(hub.CurrentContext == hub.settings.ui && demo.IsVisible, "UI context and real uGUI panel opened");
            Check(starter.move == Vector2.zero && !player.ControlCaptured && !hub.Actions.FindActionMap("Player").enabled, "UI blocks character motion and releases cursor");
            Check(hub.ReadVector("Navigate") == Vector2.zero, "Held joystick gated at context transition");
            Packet(); yield return Wait(.15f);
            int moves = 0;
            foreach (var selectable in demo.GetComponentsInChildren<Selectable>(true))
            {
                var trigger = selectable.gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.Move }; entry.callback.AddListener(_ => moves++); trigger.triggers.Add(entry);
            }
            Pad(new Vector2(0, -1)); yield return Wait(.85f); Pad();
            Check(moves >= 2 && moves <= 6, "EventSystem navigation uses bounded initial-delay/repeat interval; moves=" + moves);
            foreach (var trigger in demo.GetComponentsInChildren<EventTrigger>(true)) UnityEngine.Object.DestroyImmediate(trigger);
            yield return Wait(.15f);
            float scrollBefore = demo.Scroll.verticalNormalizedPosition;
            EventSystem.current.SetSelectedGameObject(demo.Scroll.content.GetComponentInChildren<Button>().gameObject);
            Pad(default, new Vector2(0, -1)); yield return Wait(.4f); Pad();
            Check(demo.Scroll.verticalNormalizedPosition < scrollBefore, "Second gamepad stick scrolls real ScrollRect");
            Packet(slider: 750); yield return Wait(.1f); Check(Mathf.Abs(demo.ValueSlider.value - .75f) < .001f, "Serial slider adjusts a real uGUI value");
            demo.CloseMenu(); yield return Wait(.1f);
            Check(hub.GameplayAllowed && player.ControlCaptured, "Closing UI restores Gameplay and previous captured state");

            var signals = new List<string>(); Action<ActionSignal> recorder = signal => { if (signal.phase == InputActionPhase.Performed) signals.Add(signal.map + "/" + signal.action); };
            hub.ActionTriggered += recorder;
            demo.OpenFacility(false); yield return Wait(.1f); Packet(slider: 200); yield return Wait(.15f);
            Check(hub.CurrentContext == hub.settings.solarGenerator && Mathf.Abs(demo.ValueSlider.value - .2f) < .001f && signals.Contains("SolarGenerator/PowerOutput"), "Same serial slider maps to SolarGenerator/PowerOutput");
            demo.OpenFacility(true); yield return Wait(.1f); Packet(slider: 600); yield return Wait(.15f);
            Check(hub.CurrentContext == hub.settings.oxygenGenerator && Mathf.Abs(demo.ValueSlider.value - .6f) < .001f && signals.Contains("OxygenGenerator/OxygenOutput"), "Same serial slider remaps to OxygenGenerator/OxygenOutput");
            Check(!hub.Actions.FindActionMap("SolarGenerator").enabled, "Previous facility map disabled to prevent duplicate execution");
            var padState = new GamepadState { rightTrigger = .9f }; InputSystem.QueueStateEvent(pad, padState); yield return Wait(.15f);
            Check(Mathf.Abs(demo.ValueSlider.value - .9f) < .001f, "Gamepad trigger performs the same facility action");
            Packet(slider: 300); yield return Wait(.15f);
            Check(Mathf.Abs(demo.ValueSlider.value - .3f) < .001f, "Absolute scalar ownership moves to last changed source, not greatest value");
            demo.ToggleMenu(); yield return Wait(.1f); demo.CloseMenu(); yield return Wait(.1f);
            Check(hub.CurrentContext == hub.settings.oxygenGenerator, "Nested UI restores previous facility context");
            demo.CloseFacility(); Pad(); Packet(); yield return Wait(.15f);
            hub.ActionTriggered -= recorder;
            var custom = UnityEngine.Object.Instantiate(hub.settings.solarGenerator);
            custom.contextId = "AlternateSolarMapping";
            var power = hub.Actions.FindAction("SolarGenerator/PowerOutput");
            custom.bindingOverrides = new[] { new BindingOverride { action = "PowerOutput", bindingId = power.bindings.First(b => b.path == "<SerialController>/slider").id.ToString(), path = "<SerialController>/knob", processors = "" } };
            int mappingToken = hub.PushContext(custom); yield return Wait(.1f); Packet(knob: 800, slider: 200); yield return Wait(.1f);
            Check(Mathf.Abs(hub.ReadScalar("PowerOutput") - .8f) < .001f, "Context ScriptableObject changes a binding without code or changing Gamepad binding");
            hub.PopContext(mappingToken); yield return Wait(.1f); UnityEngine.Object.DestroyImmediate(custom);
            Packet(x: 1000); yield return Wait(.1f); demo.ToggleMenu(); yield return Wait(.1f); demo.CloseMenu(); yield return Wait(.15f);
            Check(starter.move == Vector2.zero, "Returning to Gameplay does not resurrect a held joystick");
            Packet(); yield return Wait(.15f); Packet(x: 1000); yield return Wait(.1f);
            Check(starter.move.x > .9f, "Movement resumes after neutral and new actuation");
            hub.Serial.Disconnect(); yield return Wait(.15f);
            Check(starter.move == Vector2.zero && hub.Serial.Diagnostics.normalized.buttons == 0, "Serial disconnect clears gameplay state");
            Keys(Key.W); yield return Wait(.15f); Check(starter.move.y > .9f, "Keyboard remains usable with Serial disconnected");
            Keys(); InputSystem.RemoveDevice(pad); yield return Wait(.15f);
            Check(hub.GameplayAllowed && pi.actions.FindAction("Player/Move").enabled, "HID removal preserves context and PlayerInput map");
            Keys(Key.W); yield return Wait(.15f); Check(starter.move.y > .9f, "Keyboard remains usable after HID removal");
            Keys(); yield return Wait(.15f);
            Keys(Key.F2); yield return Wait(.1f); Keys();
            Check(hub.CurrentContext == hub.settings.ui, "Keyboard F2 opens the real UI context via Input Action");
            hub.Serial.BeginSimulation(); Packet(buttons: 2); Packet(); yield return Wait(.15f);
            Check(hub.GameplayAllowed, "Short Serial UI Cancel restores Gameplay");
            Packet(buttons: 4); Packet(); yield return Wait(.15f);
            Check(hub.CurrentContext == hub.settings.ui, "Serial menu button opens same UI context");
            pad = InputSystem.AddDevice<Gamepad>("CustomInput reconnected test Gamepad"); yield return Wait(.1f);
            Pad(default, default, GamepadButton.East); yield return Wait(.15f); Pad();
            Check(hub.GameplayAllowed && pi.devices.Contains(pad), "Reconnected Gamepad can cancel UI without disabling Keyboard");
            demo.ToggleMenu(); yield return Wait(.1f);
            EventSystem.current.SetSelectedGameObject(demo.Scroll.content.GetComponentsInChildren<Button>()[0].gameObject);
            Pad(default, default, GamepadButton.South); yield return Wait(.15f); Pad();
            Check(hub.CurrentContext == hub.settings.solarGenerator, "Gamepad Submit invokes actual selected uGUI Button and opens Solar context");
            demo.CloseFacility(); yield return Wait(.1f); demo.ToggleMenu(); yield return Wait(.1f);
            EventSystem.current.SetSelectedGameObject(demo.Scroll.content.GetComponentsInChildren<Button>()[1].gameObject);
            Packet(buttons: 1); Packet(); yield return Wait(.15f);
            Check(hub.CurrentContext == hub.settings.oxygenGenerator, "Short Serial Submit invokes the same EventSystem Button path");
            demo.CloseFacility(); yield return Wait(.1f);
            Packet(buttons: 2); yield return Wait(.1f); demo.ToggleMenu(); yield return Wait(.15f);
            Check(hub.CurrentContext == hub.settings.ui, "Held Gameplay button does not become an accidental UI Cancel on entry");
            Packet(); yield return Wait(.1f); Packet(buttons: 2); Packet(); yield return Wait(.15f);
            Check(hub.GameplayAllowed, "UI Cancel works after held button releases and is pressed again");
            demo.ToggleMenu(); yield return Wait(.15f); RenderScene(1280); RenderScene(1920); demo.CloseMenu();
            yield return Wait(.15f);
            hub.Shutdown(); yield return Wait(.15f);
            Check(InputHub.Active == null && !pi.neverAutoSwitchControlSchemes && pi.actions.FindActionMap("Player").enabled, "Disabling integration restores original PlayerInput asset and switching behavior");
            pi.SwitchCurrentControlScheme("KeyboardMouse", keyboard, mouse); player.SetControlCaptured(true);
            Keys(Key.W); yield return Wait(.15f); Check(starter.move.y > .9f, "Original fallback keyboard path works after integration shutdown"); Keys();
            Check(errors.Count == 0, "No Unity errors during actual-scene integration: " + string.Join(" | ", errors));
        }
        static void RenderScene(int width)
        {
            var camera = Camera.main; int height = width * 9 / 16;
            var canvas = hub.GetComponentInChildren<Canvas>(true); var originalMode = canvas.renderMode; var originalCamera = canvas.worldCamera;
            var target = RenderTexture.GetTemporary(width, height, 24);
            var originalTarget = camera.targetTexture; var originalActive = RenderTexture.active; float aspect = camera.aspect;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                camera.targetTexture = target; camera.aspect = (float)width / height; Canvas.ForceUpdateCanvases();
                camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(Output + "/sample-scene-" + width + ".png", texture.EncodeToPNG());
                Check(texture.GetPixels32().Any(c => c.r > 20 || c.g > 20 || c.b > 20), "Actual scene+UI rendered at " + width + "x" + height);
            }
            finally
            {
                canvas.renderMode = originalMode; canvas.worldCamera = originalCamera;
                camera.targetTexture = originalTarget; camera.aspect = aspect; RenderTexture.active = originalActive;
                RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        static Vector3 FindClearWalkingDirection(CharacterController controller, Vector3 cameraForward)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
            Vector3 center = controller.transform.TransformPoint(controller.center);
            Vector3 segment = Vector3.up * (controller.height * .5f - controller.radius);
            float best = -1; Vector3 result = forward;
            foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
            {
                var direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                float clearance = Physics.CapsuleCast(center - segment, center + segment, controller.radius * .95f,
                    direction, out var hit, 4f, ~(1 << controller.gameObject.layer), QueryTriggerInteraction.Ignore) ? hit.distance : 4f;
                if (clearance > best) { best = clearance; result = direction; }
            }
            return result;
        }
        static void Finish()
        {
            SessionState.SetBool(Running, false); routines.Clear();
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            InputSystem.settings.backgroundBehavior = originalBackground;
            Application.logMessageReceived -= OnLog;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            Save("play-checks.txt");
            SessionState.SetInt("CustomInput.Validation.Failures", failures + SessionState.GetInt("CustomInput.Validation.EditorFailures", 0));
            SessionState.SetBool(ExitPending, true);
            EditorApplication.isPlaying = false;
        }
    }
}
