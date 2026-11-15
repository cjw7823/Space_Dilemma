using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeyHeyThere.PixelUISciFiFree;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Runs in the currently open scene, including unsaved changes; never opens or saves a scene.
[InitializeOnLoad]
static class HudPlayValidation
{
    const string Running = "SpaceHud.PlayValidation";
    const string Output = "QA/SpaceHud";
    static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    static readonly List<string> checks = new List<string>(), errors = new List<string>();
    static int lastFrame = -1, failures;
    static float deadline;
    static Keyboard keyboard;
    static Mouse mouse;
    static InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
    static InputSettings.BackgroundBehavior originalBackground;
    static bool savedInputSettings;

    static HudPlayValidation() { EditorApplication.update += Update; }
    [MenuItem("Tools/Space HUD/Run Play Validation")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before starting validation.");
        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open SampleScene first; the validator will not replace your open scene.");
        Directory.CreateDirectory(Output);
        SessionState.SetInt("SpaceHud.Terrains", UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        var authored = UnityEngine.Object.FindFirstObjectByType<HudController>();
        SessionState.SetBool("SpaceHud.Authored", authored && authored.gameObject.scene == EditorSceneManager.GetActiveScene());
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    static void Update()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isPaused || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (routines.Count == 0)
            {
                checks.Clear(); errors.Clear(); failures = 0; deadline = Time.realtimeSinceStartup + 120;
                Application.logMessageReceived += OnLog; routines.Push(Cases());
            }
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("HUD play validation timed out");
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
    static void Check(bool ok, string message) { checks.Add((ok ? "PASS " : "FAIL ") + message); if (!ok) failures++; }
    static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; do { yield return null; } while (Time.realtimeSinceStartup < until); }
    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    static void MouseAt(Vector2 position, bool down = false)
    {
        var state = new MouseState { position = position };
        if (down) state = state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse, state);
    }
    static void Click(Button button)
    {
        var rect = (RectTransform)button.transform;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
        // Deliver a UGUI pointer click after testing the raycast separately.
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Cases()
    {
        yield return Wait(.6f);
        var hud = HudController.Active; var hub = InputHub.Active;
        var player = UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();
        var environment = UnityEngine.Object.FindFirstObjectByType<LunarEnvironment>();
        Check(SessionState.GetBool("SpaceHud.Authored", false) && hud && hub && player && environment, "HUD already exists in SampleScene before Play; existing player, input and common clock available");
        if (!hud || !hub || !player || !environment || !hud.Minimap || !hud.Pause) yield break;
        Check(hub.Serial.Status == SerialConnectionStatus.Disconnected, "Validation does not open or reset the configured COM device");
        originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        originalBackground = InputSystem.settings.backgroundBehavior; savedInputSettings = true;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard = InputSystem.AddDevice<Keyboard>("HUD validation keyboard");
        mouse = InputSystem.AddDevice<Mouse>("HUD validation mouse"); Keys(); MouseAt(Vector2.zero);
        yield return Wait(.3f);
        player.SendMessage("OnApplicationFocus", true);
        Check(UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == SessionState.GetInt("SpaceHud.Terrains", -1), "Existing terrain count unchanged during Play");
        Check(Camera.main && Camera.main != hud.Minimap.MapCamera && hud.Minimap.MapCamera.targetTexture == hud.Minimap.Texture, "Separate orthographic minimap camera and RenderTexture; main camera unchanged");
        Check(hud.Minimap.MapCamera.orthographic && hud.Minimap.MapCamera.clearFlags == CameraClearFlags.SolidColor && (hud.Minimap.MapCamera.cullingMask & (1 << LayerMask.NameToLayer("UI"))) == 0, "Minimap excludes skybox and UI layer");
        CaptureMinimap(hud.Minimap.Texture);
        Vector3 before = player.transform.position; Quaternion rotation = player.transform.rotation;
        var controller = player.GetComponent<CharacterController>(); var motor = player.GetComponent<ThirdPersonController>();
        bool wasEnabled = controller.enabled, motorWasEnabled = motor.enabled; motor.enabled = false; controller.enabled = false;
        player.transform.position = before + new Vector3(7, 0, 4); player.transform.rotation = Quaternion.Euler(0, 90, 0);
        yield return Wait(.15f);
        Check(Mathf.Abs(hud.Minimap.MapCamera.transform.position.x - player.transform.position.x) < .01f && Mathf.Abs(hud.Minimap.MapCamera.transform.position.z - player.transform.position.z) < .01f, "Minimap follows the player position");
        Vector3 mapUp = Vector3.ProjectOnPlane(hud.Minimap.MapCamera.transform.up, Vector3.up).normalized;
        float iconAngle = hud.Minimap.playerIconUpRotation - Vector3.SignedAngle(mapUp, Vector3.ProjectOnPlane(player.transform.forward, Vector3.up), Vector3.up);
        Check(Mathf.Abs(Mathf.DeltaAngle(hud.Minimap.PlayerIcon.localEulerAngles.z, iconAngle)) < .1f, "Scene-authored player icon follows heading");
        player.transform.SetPositionAndRotation(before, rotation); controller.enabled = wasEnabled; motor.enabled = motorWasEnabled;
        var marker = hud.Minimap.PlayerIcon.parent.GetComponentInChildren<HudMapMarker>(true);
        Check(marker && marker.target && marker.Icon, "An authored minimap marker references an existing world object and Image");
        if (marker)
        {
            var previous = marker.target; marker.target = player.transform; yield return Wait(.1f);
            Check(marker.Icon.enabled && marker.Icon.rectTransform.anchoredPosition.magnitude < 1, "Authored marker updates with its target");
            marker.target = previous;
        }
        string initialTitle = hud.Objective.Title, initialDescription = hud.Objective.Description;
        string initialKoreanTitle = hud.Objective.KoreanTitle, initialKoreanDescription = hud.Objective.KoreanDescription;
        hud.Objective.Set("Validation objective", "Repair generator", 1, 2);
        Check(hud.ObjectiveTitle.text == "Validation objective" && hud.ObjectiveDescription.text == "Repair generator" && hud.ObjectiveProgress.text == "1 / 2", "External objective data updates all three labels");
        yield return Wait(.3f); Capture(hud, 1280, 720, "hud-changed-objective-1280.png");
        Check(hud.ObjectiveTitle.Text.font.HasCharacter('태', true, true), "Windows Korean objective glyph resolves through the system font");
        var defaults = Resources.Load<HudSettings>("SpaceHudSettings");
        hud.Objective.SetLocalized(initialTitle, initialDescription, initialKoreanTitle, initialKoreanDescription,
            defaults.exampleCurrent, defaults.exampleTotal, true);
        Check(hud.ObjectiveProgress.text.Contains("EXAMPLE") || hud.ObjectiveProgress.text.Contains("예시"), "Demo objective is clearly marked as example");
        var startHours = environment.sunHoursFromStart; string clock = hud.ClockLabel.text;
        yield return Wait(.25f);
        Check(environment.sunHoursFromStart > startHours && hud.ClockLabel.text != clock, "HUD clock advances with existing celestial simulation time");
        Capture(hud, 1920, 1080, "hud-1920.png"); Capture(hud, 1280, 720, "hud-1280.png");
        Capture(hud, 1024, 768, "hud-1024x768.png");
        // The offscreen captures temporarily change canvas size. Let the real Game view lay it out again.
        yield return Wait(.25f); Canvas.ForceUpdateCanvases();
        var pauseRect = (RectTransform)hud.PauseButton.transform;
        Vector2 pausePoint = RectTransformUtility.WorldToScreenPoint(null, pauseRect.TransformPoint(pauseRect.rect.center));
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pausePoint }, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == hud.PauseButton.gameObject || hits[0].gameObject.transform.IsChildOf(hud.PauseButton.transform)),
            "Pause button is the top UI raycast at " + pausePoint + "; hits=" + string.Join(",", hits.Select(h => h.gameObject.name)));
        Check(hub.player.devices.Contains(mouse), "Synthetic mouse is paired with the actual PlayerInput");

        player.SetControlCaptured(false); Time.timeScale = .75f;
        Click(hud.PauseButton); yield return Wait(.15f);
        Check(hud.Pause.IsPaused && hud.Pause.Popup.activeSelf && Time.timeScale == 0, "UGUI pointer-click event opens the Pause modal and stops time");
        yield return Wait(.2f);
        Check(!hub.GameplayAllowed && !player.ControlCaptured && Cursor.lockState == CursorLockMode.None, "Pause context blocks player input and unlocks cursor");
        startHours = environment.sunHoursFromStart; before = player.transform.position;
        Quaternion cameraBefore = Camera.main.transform.rotation; Keys(Key.W); MouseAt(new Vector2(400, 300), true);
        yield return Wait(.3f); Keys(); MouseAt(new Vector2(400, 300));
        Check(Mathf.Approximately(environment.sunHoursFromStart, startHours), "Simulation clock remains stopped during pause");
        Check(Vector3.Distance(before, player.transform.position) < .001f && Quaternion.Angle(cameraBefore, Camera.main.transform.rotation) < .01f, "Player and camera stay still despite synthetic paused input");
        Capture(hud, 1920, 1080, "pause-1920.png"); Capture(hud, 1280, 720, "pause-1280.png");
        bool wasPaused = hud.Pause.IsPaused;
        Click(hud.ContinueButton); yield return Wait(.15f);
        Check(wasPaused && !hud.Pause.IsPaused && Mathf.Approximately(Time.timeScale, .75f), "Continue button works while paused and restores previous time scale");
        yield return Wait(.2f); Check(hub.GameplayAllowed, "Previous gameplay context restored after Continue");
        Keys(Key.Escape); yield return Wait(.15f); Keys(); yield return Wait(.12f);
        Check(hud.Pause.IsPaused && !hub.GameplayAllowed, "Global Pause logical action opens modal on Escape");
        Keys(Key.Escape); yield return Wait(.15f); Keys(); yield return Wait(.2f);
        Check(!hud.Pause.IsPaused && hub.GameplayAllowed && Mathf.Approximately(Time.timeScale, .75f), "Second Escape resumes once without context conflicts");
        player.SetControlCaptured(true); Keys(Key.W); yield return Wait(.2f);
        Check(player.GetComponent<StarterAssetsInputs>().move.y > .5f, "Keyboard movement reaches existing player again after resume");
        Keys(); Time.timeScale = 1;
        var demo = hub.GetComponent<CustomInputDemoPanel>(); Keys(Key.F2); yield return Wait(.15f); Keys(); yield return Wait(.15f);
        Check(demo.IsVisible && hub.CurrentContext == hub.settings.ui, "Existing F2 example context still opens through Input Actions");
        Keys(Key.Escape); yield return Wait(.16f); Keys(); yield return Wait(.12f);
        Check(hud.Pause.IsPaused && !demo.IsVisible, "Pause overrides existing UI without showing both modals");
        Keys(Key.Escape); yield return Wait(.16f); Keys(); yield return Wait(.15f);
        Check(!hud.Pause.IsPaused && demo.IsVisible && hub.CurrentContext == hub.settings.ui, "Closing pause restores previous UI context");
        Keys(Key.Backspace); yield return Wait(.15f); Keys(); yield return Wait(.15f);
        Check(!demo.IsVisible && hub.GameplayAllowed, "Backspace closes the existing example UI without invoking Pause");
        yield return CheckSettings(hud, hub);
        Time.timeScale = .6f; hud.Pause.Pause(); yield return Wait(.15f);
        UnityEngine.Object.Destroy(hud.gameObject); yield return Wait(.2f);
        Check(Mathf.Approximately(Time.timeScale, .6f) && hub.GameplayAllowed, "Destroying a paused HUD restores its prior time scale and input context");
        Time.timeScale = 1;
        Check(errors.Count == 0, "No Unity errors during HUD Play validation: " + string.Join(" | ", errors.Select(e => e.Split('\n')[0])));
    }

    static IEnumerator CheckSettings(HudController hud, InputHub hub)
    {
        var menu = hud.Pause.Popup.GetComponentInChildren<HudGameSettings>(true);
        var pauseWindow = hud.Pause.Popup.transform.Find("Pause Window");
        var settingsButton = pauseWindow.Find("Settings Button")?.GetComponent<Button>();
        var quitButton = pauseWindow.Find("Quit Game Button")?.GetComponent<Button>();
        Check(menu && settingsButton && quitButton, "Settings and Quit Game are authored in SampleScene");
        if (!menu || !settingsButton || !quitButton) yield break;
        Check(quitButton.interactable && Enumerable.Range(0, quitButton.onClick.GetPersistentEventCount()).Any(i =>
            quitButton.onClick.GetPersistentTarget(i) == hud.Pause &&
            quitButton.onClick.GetPersistentMethodName(i) == nameof(HudPauseManager.QuitGame)), "Quit Game is enabled and wired to the quit action");
        hud.Pause.Pause();
        yield return Wait(.2f);
        Click(settingsButton);
        yield return Wait(.2f);
        Check(menu.gameObject.activeInHierarchy && !pauseWindow.gameObject.activeSelf &&
            Time.timeScale == 0 && !hub.GameplayAllowed, "Settings replaces the pause window and keeps gameplay paused");
        const string languageKey = "SpaceDilemma.Language";
        bool hadLanguage = PlayerPrefs.HasKey(languageKey);
        int oldLanguage = PlayerPrefs.GetInt(languageKey);
        try
        {
            Click(menu.transform.Find("English").GetComponent<Button>());
            yield return Wait(.15f);
            CheckEnglishUi(hud, "opening Settings in English");
            Check(hud.ObjectiveTitle.text == hud.Objective.Title && hud.ObjectiveDescription.text == hud.Objective.Description &&
                !string.IsNullOrWhiteSpace(hud.Objective.Title) && !string.IsNullOrWhiteSpace(hud.Objective.Description),
                "English renders both translated example objective fields");
            Click(menu.transform.Find("Korean").GetComponent<Button>());
            Check(!menu.English && settingsButton.GetComponent<PixelButton>().text == "설정" &&
                PlayerPrefs.GetInt(languageKey) == 1, "Language button updates and remembers Korean");
            var title = menu.transform.Find("Title").GetComponent<TMP_Text>();
            Check(title.text == "설정" && title.font.HasCharacter('설', true, true), "Settings renders a Korean title with its assigned font");
            Check(hud.ObjectiveTitle.text == hud.Objective.KoreanTitle && hud.ObjectiveDescription.text == hud.Objective.KoreanDescription &&
                hud.ObjectiveTitle.text.Any(IsNonAsciiLetter), "Korean switches both objective fields to the scene's Korean text");
            var port = menu.transform.Find("COM Port").GetComponent<TMP_InputField>();
            var baud = menu.transform.Find("Baud Rate").GetComponent<TMP_InputField>();
            Check(port.text == hub.settings.serial.portName && baud.text == hub.settings.serial.baudRate.ToString(),
                "Settings displays current COM and baud values");
            port.text = "INVALID";
            Click(menu.transform.Find("Connect").GetComponent<Button>());
            var diagnostics = menu.transform.Find("Serial Diagnostics").GetComponent<TMP_Text>();
            Check(hub.Serial.Status == SerialConnectionStatus.Disconnected && diagnostics.text.Contains("COM3"),
                "Invalid COM input displays feedback without opening a device");
            port.text = hub.settings.serial.portName;
            menu.RefreshPorts();
            hub.Serial.BeginSimulation();
            hub.Serial.InjectLine("S,1,500,0,500,500,500,500,0");
            yield return Wait(.3f);
            Check(diagnostics.text.Contains("모의 입력") && diagnostics.text.Contains("S=1") &&
                diagnostics.text.Contains("S,1,500"), "Live diagnostics report a simulated packet while paused");
            Capture(hud, 1280, 720, "settings-ko-1280.png");
            hud.Pause.BackFromSettings();
            Check(settingsButton.GetComponent<PixelButton>().text == "설정" &&
                quitButton.GetComponent<PixelButton>().text == "게임 종료" &&
                hud.ContinueButton.text == "계속하기", "Korean reaches the visible pause menu actions");
            hud.Pause.ShowSettings();
            Click(menu.transform.Find("Disconnect").GetComponent<Button>());
            Check(hub.Serial.Status == SerialConnectionStatus.Disconnected, "Disconnect control releases the serial source");
            hub.Serial.Diagnostics.lastError = "@fault: 액세스가 거부되었습니다.";
            Click(menu.transform.Find("English").GetComponent<Button>());
            Check(menu.English && quitButton.GetComponent<PixelButton>().text == "QUIT GAME", "English updates pause and settings text");
            yield return Wait(.15f);
            Check(diagnostics.text.Contains("Could not open or read the COM port"), "English translates a locally worded Windows COM fault");
            CheckEnglishUi(hud, "switching Korean to English with a localized COM error");
            Capture(hud, 1920, 1080, "settings-en-1920.png");
            hub.Serial.Diagnostics.lastError = "";
            Keys(Key.Escape); yield return Wait(.15f); Keys(); yield return Wait(.15f);
            Check(!menu.gameObject.activeSelf && pauseWindow.gameObject.activeSelf && hud.Pause.IsPaused,
                "Escape returns from settings to the pause menu");
            CheckEnglishUi(hud, "returning to the English pause menu");
            Keys(Key.Escape); yield return Wait(.15f); Keys(); yield return Wait(.15f);
            Check(!hud.Pause.IsPaused && hub.GameplayAllowed, "Escape resumes from pause after closing settings");
            CheckEnglishUi(hud, "resuming gameplay in English");

            var demo = hub.GetComponent<CustomInputDemoPanel>();
            Keys(Key.F2); yield return Wait(.15f); Keys(); yield return Wait(.15f);
            Check(demo.IsVisible, "F2 diagnostics can be opened after changing language");
            CheckEnglishUi(hud, "opening the F2 test menu in English");
            demo.OpenSolar(); yield return Wait(.15f);
            Check(demo.IsVisible && hub.CurrentContext == hub.settings.solarGenerator, "F2 solar test screen is visible");
            CheckEnglishUi(hud, "opening the F2 solar test screen in English");
            demo.OpenOxygen(); yield return Wait(.15f);
            Check(demo.IsVisible && hub.CurrentContext == hub.settings.oxygenGenerator, "F2 oxygen test screen is visible");
            CheckEnglishUi(hud, "opening the F2 oxygen test screen in English");
            demo.CloseFacility(); yield return Wait(.15f);

            hud.Objective.Set("태양광 발전소 복구", "발전기 정상화", 0, 1);
            Check(hud.ObjectiveTitle.text == "Current Objective" && hud.ObjectiveDescription.text == "English description unavailable.",
                "A Korean-only dynamic objective uses an English fallback");
            CheckEnglishUi(hud, "receiving an objective without an English translation");
        }
        finally
        {
            hub.Serial.Diagnostics.lastError = "";
            if (menu.English != (oldLanguage == 0))
            {
                if (oldLanguage == 0) menu.UseEnglish();
                else menu.UseKorean();
            }
            if (!hadLanguage) PlayerPrefs.DeleteKey(languageKey);
            else PlayerPrefs.SetInt(languageKey, oldLanguage);
            PlayerPrefs.Save();
        }
    }

    static bool IsNonAsciiLetter(char c) => c > '\u007f' && (char.IsLetterOrDigit(c) || char.IsSurrogate(c));

    static void CheckEnglishUi(HudController hud, string stage)
    {
        var scene = hud.gameObject.scene;
        var activeText = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(label => label.isActiveAndEnabled && label.gameObject.scene == scene)
            .Select(label => (label.name, label.text))
            .Concat(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                .Where(label => label.isActiveAndEnabled && label.gameObject.scene == scene)
                .Select(label => (label.name, label.text))).ToArray();
        var leaks = activeText.Where(item => item.text?.Any(IsNonAsciiLetter) == true)
            .Select(item => item.name + " = " + item.text).ToArray();
        Check(activeText.Length > 0 && leaks.Length == 0, $"English UI after {stage}: {activeText.Length} active text elements, " +
            (leaks.Length == 0 ? "no non-English letters" : string.Join(" | ", leaks)));
    }
    static void CaptureMinimap(RenderTexture map)
    {
        var old = RenderTexture.active; var texture = new Texture2D(map.width, map.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = map; texture.ReadPixels(new Rect(0, 0, map.width, map.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/minimap.png", texture.EncodeToPNG());
            Check(texture.GetPixels32().Select(c => (c.r / 16, c.g / 16, c.b / 16)).Distinct().Count() > 12, "Minimap texture contains rendered scene geometry");
        }
        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(texture); }
    }
    static void Capture(HudController hud, int width, int height, string name)
    {
        var camera = Camera.main; var canvas = hud.Canvas; var pixel = canvas.GetComponent<PixelCanvas>();
        var scaler = canvas.GetComponent<CanvasScaler>(); var mode = canvas.renderMode; var world = canvas.worldCamera;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; float oldAspect = camera.aspect, oldDistance = canvas.planeDistance, oldScale = scaler.scaleFactor;
        var target = RenderTexture.GetTemporary(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            pixel.enabled = false; scaler.scaleFactor = Mathf.Max(1, height / 360);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = target; camera.aspect = (float)width / height; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name, image.EncodeToPNG());
            Check(image.GetPixels32().Any(c => c.r > 20 || c.g > 20 || c.b > 20), "Scene and HUD render captured at " + width + "x" + height + ": " + name);
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = world; canvas.planeDistance = oldDistance;
            camera.targetTexture = oldTarget; camera.aspect = oldAspect; RenderTexture.active = oldActive;
            scaler.scaleFactor = oldScale; pixel.enabled = true;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Finish()
    {
        SessionState.SetBool(Running, false); routines.Clear(); Application.logMessageReceived -= OnLog;
        if (savedInputSettings) { InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput; InputSystem.settings.backgroundBehavior = originalBackground; }
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        Time.timeScale = 1;
        Directory.CreateDirectory(Output);
        File.WriteAllLines(Output + "/play-checks.txt", checks.Concat(new[] { $"RESULT checks={checks.Count} failures={failures}", "ERRORS:", string.Join("\n", errors) }));
        Debug.Log("HUD Play validation finished: " + (checks.Count - failures) + " passed, " + failures + " failed. See " + Output);
        EditorApplication.isPlaying = false;
    }
}
