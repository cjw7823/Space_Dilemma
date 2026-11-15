using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Users;

namespace SpaceDilemma.CustomInput
{
    public interface IGameplayInputClient
    {
        bool ControlCaptured { get; }
        void SetControlCaptured(bool captured);
    }
    public readonly struct ActionSignal
    {
        public readonly string map, action, device;
        public readonly InputActionPhase phase;
        public readonly float scalar;
        public ActionSignal(InputAction.CallbackContext context)
        {
            map = context.action.actionMap.name; action = context.action.name;
            device = context.control?.device.displayName ?? ""; phase = context.phase;
            scalar = context.valueType == typeof(float) ? context.ReadValue<float>() : 0;
        }
    }

    [DefaultExecutionOrder(-250)]
    public sealed class InputHub : MonoBehaviour
    {
        public static InputHub Active { get; private set; }
        public CustomInputSettings settings;
        public PlayerInput player;
        public InputActionAsset Actions { get; private set; }
        public SerialInputBackend Serial { get; private set; }
        public InputContextStack Contexts { get; private set; }
        public InputContextProfile CurrentContext => Contexts?.Current;
        public bool GameplayAllowed => CurrentContext && !CurrentContext.blocksGameplay;
        public bool NonPointerGameplayActivity { get; private set; }
        public event Action<InputContextProfile> ContextChanged;
        public event Action<ActionSignal> ActionTriggered;
        public readonly Queue<string> Trace = new Queue<string>();
        readonly HashSet<string> pressed = new HashSet<string>();
        readonly List<InputAction> neutralGates = new List<InputAction>();
        readonly Dictionary<string, float> scalarValues = new Dictionary<string, float>();
        readonly Queue<Action> commands = new Queue<Action>();
        InputActionAsset originalActions;
        InputActionMap currentMap;
        bool originalAutoSwitch, capturedBeforeContext, initialized;
        string originalScheme, originalDefaultMap;
        InputDevice[] originalDevices;
        IGameplayInputClient gameplayClient;
        InputSystemUIInputModule uiModule;
        InputActionAsset originalUiActions;
        InputSystemUIInputModule originalPlayerUi;
        bool originalUiEnabled;
        InputActionReference oldMove, oldSubmit, oldCancel, oldPoint, oldClick, oldScroll;
        bool pendingDeviceRefresh;
        bool contextDirty;
        readonly List<InputActionReference> ownedReferences = new List<InputActionReference>();

        public void Initialize(CustomInputSettings configuration, PlayerInput target, bool allowAutomaticSerial = true)
        {
            if (initialized) return;
            if (!configuration || !configuration.actions || !configuration.gameplay) throw new InvalidOperationException("Custom Input settings/actions/Gameplay profile missing.");
            if (!target) throw new InvalidOperationException("Attach a PlayerInput to use the game bridge.");
            var eventSystem = FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.gameObject.scene == target.gameObject.scene);
            var sceneUiModule = eventSystem ? eventSystem.GetComponent<InputSystemUIInputModule>() : null;
            if (!sceneUiModule) throw new InvalidOperationException("Add an EventSystem with an InputSystemUIInputModule to the scene before initializing Custom Input.");
            settings = Instantiate(configuration); player = target;
            SerialPortPreferences.Load(settings.serial);
            originalActions = player.actions; originalAutoSwitch = player.neverAutoSwitchControlSchemes;
            originalPlayerUi = player.uiInputModule;
            originalScheme = player.currentControlScheme; originalDefaultMap = player.defaultActionMap;
            originalDevices = player.devices.ToArray();
            gameplayClient = player.GetComponents<MonoBehaviour>().OfType<IGameplayInputClient>().FirstOrDefault();
            Actions = Instantiate(settings.actions);
            Actions.name = settings.actions.name + " (runtime)";
            player.neverAutoSwitchControlSchemes = true;
            player.actions = Actions;
            // PlayerInput may create its own action copy. Always use that actual instance.
            Actions = player.actions;
            Serial = new SerialInputBackend(settings.serial, settings.calibration);
            Contexts = new InputContextStack(settings.gameplay);
            SetupEventSystem(sceneUiModule);
            foreach (var map in Actions.actionMaps) map.actionTriggered += OnAction;
            Contexts.Changed += ScheduleContext;
            InputSystem.onBeforeUpdate += BeforeInputUpdate;
            InputSystem.onDeviceChange += DeviceChanged;
            InputUser.onChange += UserChanged;
            initialized = true; Active = this;
            PairDevices(); ApplyContext(Contexts.Current);
            if (allowAutomaticSerial && settings.serial.connectOnStart && !string.IsNullOrEmpty(settings.serial.portName)) Serial.Connect();
        }

        void SetupEventSystem(InputSystemUIInputModule sceneUiModule)
        {
            uiModule = sceneUiModule;
            originalUiEnabled = uiModule.enabled; originalUiActions = uiModule.actionsAsset;
            oldMove = uiModule.move; oldSubmit = uiModule.submit; oldCancel = uiModule.cancel;
            oldPoint = uiModule.point; oldClick = uiModule.leftClick; oldScroll = uiModule.scrollWheel;
            uiModule.enabled = false;
            uiModule.actionsAsset = Actions;
            player.uiInputModule = uiModule;
            uiModule.moveRepeatDelay = .4f; uiModule.moveRepeatRate = .12f;
        }

        public int PushContext(InputContextProfile profile)
        {
            if (Actions.FindActionMap(profile.actionMap, false) == null) throw new ArgumentException("Unknown Action Map: " + profile.actionMap);
            return Contexts.Push(profile);
        }
        public void PopContext(int token) => Contexts.Pop(token);
        void ScheduleContext(InputContextProfile profile) => contextDirty = true;

        void ApplyContext(InputContextProfile profile)
        {
            // Capture state before disabling; the profile already is the stack winner here.
            if (profile.blocksGameplay && !capturedBeforeContext && gameplayClient != null)
                capturedBeforeContext = gameplayClient.ControlCaptured;
            foreach (var map in Actions.actionMaps) map.Disable();
            uiModule.enabled = false;
            foreach (var reference in ownedReferences) Destroy(reference);
            ownedReferences.Clear();
            Actions.RemoveAllBindingOverrides();
            currentMap = Actions.FindActionMap(profile.actionMap, true);
            foreach (var change in profile.bindingOverrides)
            {
                var action = currentMap.FindAction(change.action, false);
                if (action == null) continue;
                int index = string.IsNullOrEmpty(change.bindingId) ? change.bindingIndex : action.bindings.ToList().FindIndex(b => b.id.ToString() == change.bindingId);
                if (index < 0 || index >= action.bindings.Count) continue;
                action.ApplyBindingOverride(index, new InputBinding { overridePath = change.path, overrideProcessors = change.processors });
            }
            pressed.Clear(); scalarValues.Clear(); neutralGates.Clear(); NonPointerGameplayActivity = false;
            Actions.FindActionMap("Global", false)?.Enable();
            currentMap.Enable();
            if (profile.blocksGameplay)
            {
                uiModule.point = Reference("Global/Point"); uiModule.leftClick = Reference("Global/Click");
                uiModule.move = Reference(profile.actionMap + "/" + (profile.actionMap == "UI" ? "Navigate" : "Select"));
                uiModule.submit = Reference(profile.actionMap + "/" + (profile.actionMap == "UI" ? "Submit" : "Confirm"));
                uiModule.cancel = Reference(profile.actionMap + "/Cancel");
                uiModule.scrollWheel = Reference("Global/Scroll");
                uiModule.enabled = true;
                gameplayClient?.SetControlCaptured(false);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else
            {
                // The always-visible HUD still needs pointer clicks during gameplay.
                // Navigation/submit stay disabled until a modal context is active.
                uiModule.point = Reference("Global/Point"); uiModule.leftClick = Reference("Global/Click");
                uiModule.scrollWheel = Reference("Global/Scroll");
                uiModule.move = null; uiModule.submit = null; uiModule.cancel = null;
                uiModule.enabled = true;
                if (capturedBeforeContext)
                { gameplayClient?.SetControlCaptured(true); capturedBeforeContext = false; }
            }
            // A stick/key held across a modal/context boundary must return to neutral first.
            foreach (var action in currentMap.actions)
                if (IsDirectional(action) && !ControlsNeutral(action)) { action.Disable(); neutralGates.Add(action); }
            AddTrace("Context → " + profile.contextId + "; enabled maps: Global + " + profile.actionMap + "; neutral gates=" + neutralGates.Count);
            ContextChanged?.Invoke(profile);
        }
        InputActionReference Reference(string path)
        {
            var action = Actions.FindAction(path, false);
            if (action == null) return null;
            var reference = InputActionReference.Create(action); ownedReferences.Add(reference); return reference;
        }
        static bool IsDirectional(InputAction action) => action.expectedControlType == "Vector2" && action.name != "Point";
        static bool ControlsNeutral(InputAction action)
        {
            foreach (var control in action.controls)
            {
                if (control.device is Pointer) continue; // Pointer deltas reset in Input System.
                if (control.EvaluateMagnitude() > .15f) return false;
            }
            return true;
        }
        void PairDevices()
        {
            if (!player || !player.user.valid) return;
            var user = player.user;
            user.ActivateControlScheme((string)null);
            foreach (var device in InputSystem.devices)
            {
                if (!(device is Keyboard || device is Mouse || device is Gamepad || device is Joystick || device is SerialController)) continue;
                // Do not take a device away from a different local player.
                bool ownedByAnother = InputUser.all.Any(u => u.id != user.id && u.pairedDevices.Contains(device));
                if (!ownedByAnother && !user.pairedDevices.Contains(device)) InputUser.PerformPairingWithDevice(device, user);
            }
            Actions.bindingMask = null;
        }
        void DeviceChanged(InputDevice device, InputDeviceChange change)
        { pendingDeviceRefresh = true; }
        void UserChanged(InputUser user, InputUserChange change, InputDevice device)
        {
            if (player && player.user.valid && user.id == player.user.id && change == InputUserChange.ControlSchemeChanged)
                pendingDeviceRefresh = true;
        }
        void BeforeInputUpdate()
        {
            if (!initialized || InputState.currentUpdateType == InputUpdateType.BeforeRender || InputState.currentUpdateType == InputUpdateType.Editor) return;
            pressed.Clear(); NonPointerGameplayActivity = false;
            if (pendingDeviceRefresh) { PairDevices(); pendingDeviceRefresh = false; }
            Serial.Pump(SerialInputBackend.Now);
            for (int i = neutralGates.Count - 1; i >= 0; i--)
                if (ControlsNeutral(neutralGates[i])) { neutralGates[i].Enable(); neutralGates.RemoveAt(i); }
        }
        void Update()
        {
            if (!initialized) return;
            // Never change Action Maps inside an Action callback.
            while (commands.Count > 0) commands.Dequeue()();
            if (contextDirty) { contextDirty = false; ApplyContext(Contexts.Current); }
        }
        void OnAction(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed && context.phase != InputActionPhase.Canceled) return;
            string path = context.action.actionMap.name + "/" + context.action.name;
            if (context.performed && context.action.type == InputActionType.Button) pressed.Add(path);
            if (context.action.type == InputActionType.PassThrough && context.valueType == typeof(float)) scalarValues[path] = context.ReadValue<float>();
            if (context.performed && context.action.actionMap.name == "Player" && (context.control.device is Gamepad || context.control.device is SerialController))
                NonPointerGameplayActivity = true;
            if (context.performed && context.action.name == "ToggleUI") commands.Enqueue(() => GetComponent<CustomInputDemoPanel>()?.ToggleMenu());
            ActionTriggered?.Invoke(new ActionSignal(context));
            if (context.action.name != "Point" && context.action.name != "Look") AddTrace(path + " " + context.phase + " ← " + context.control?.device.displayName);
        }
        public Vector2 ReadVector(string action) => currentMap?.FindAction(action, false)?.ReadValue<Vector2>() ?? Vector2.zero;
        public float ReadScalar(string action) => scalarValues.TryGetValue((currentMap?.name ?? "") + "/" + action, out var value) ? value : 0;
        public bool WasPressed(string action) => currentMap != null && pressed.Contains(currentMap.name + "/" + action);
        public bool IsPressed(string action) => currentMap?.FindAction(action, false)?.IsPressed() ?? false;
        public bool LookUsesRate => !(currentMap?.FindAction("Look", false)?.activeControl?.device is Pointer);
        public Vector2 ReadLookInput(float mouseSensitivity)
        {
            var action = currentMap?.FindAction("Look", false);
            if (action == null) return Vector2.zero;
            var value = action.ReadValue<Vector2>();
            return action.activeControl?.device is Pointer ? value * mouseSensitivity : value * settings.lookDegreesPerSecond;
        }
        public Vector2 ReadLookDelta(float mouseSensitivity, float deltaTime) => ReadLookInput(mouseSensitivity) * (LookUsesRate ? deltaTime : 1f);
        public void AddTrace(string message)
        { Trace.Enqueue(message); while (Trace.Count > 80) Trace.Dequeue(); }

        public void Shutdown()
        {
            if (!initialized) return;
            initialized = false;
            InputSystem.onBeforeUpdate -= BeforeInputUpdate; InputSystem.onDeviceChange -= DeviceChanged;
            InputUser.onChange -= UserChanged;
            Serial.Dispose();
            foreach (var map in Actions.actionMaps) map.actionTriggered -= OnAction;
            Contexts.Changed -= ScheduleContext;
            if (uiModule)
            {
                uiModule.enabled = false;
                uiModule.move = null; uiModule.submit = null; uiModule.cancel = null;
                uiModule.point = null; uiModule.leftClick = null; uiModule.scrollWheel = null;
            }
            if (player)
            {
                player.uiInputModule = null;
                player.actions = originalActions; player.defaultActionMap = originalDefaultMap;
                player.uiInputModule = originalPlayerUi;
                player.neverAutoSwitchControlSchemes = originalAutoSwitch;
                if (player.user.valid)
                {
                    var user = player.user; user.UnpairDevices();
                    foreach (var device in originalDevices) if (device.added) InputUser.PerformPairingWithDevice(device, user);
                    user.ActivateControlScheme(originalScheme);
                }
            }
            if (uiModule)
            {
                uiModule.actionsAsset = originalUiActions;
                uiModule.move = oldMove; uiModule.submit = oldSubmit; uiModule.cancel = oldCancel;
                uiModule.point = oldPoint; uiModule.leftClick = oldClick; uiModule.scrollWheel = oldScroll;
                uiModule.enabled = originalUiEnabled;
            }
            if (Actions) Destroy(Actions);
            foreach (var reference in ownedReferences) Destroy(reference);
            ownedReferences.Clear();
            if (settings) Destroy(settings);
            if (Active == this) Active = null;
        }
        void OnDisable() => Shutdown();
    }
}
