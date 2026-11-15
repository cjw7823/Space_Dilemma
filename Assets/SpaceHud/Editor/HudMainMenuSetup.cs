using System;
using SpaceDilemma.Hud;
using SpaceDilemma.Survival;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

static class HudMainMenuSetup
{
    static readonly Color Ice = new(0.91f, 0.98f, 1f);
    static readonly Color Cyan = new(0.35f, 0.86f, 1f);
    static readonly Color Ink = new(0.024f, 0.082f, 0.106f);

    [MenuItem("Tools/Space HUD/Set Up Main Menu")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open SampleScene in Edit mode.");
        var hud = UnityEngine.Object.FindFirstObjectByType<HudController>();
        var briefing = UnityEngine.Object.FindFirstObjectByType<RadiationBriefing>();
        if (!hud || !hud.Canvas || !hud.Pause)
            throw new InvalidOperationException("SampleScene needs the existing Space HUD.");
        var pause = hud.Pause;
        var pauseBindings = new SerializedObject(pause);
        if (pauseBindings.FindProperty("mainMenu").objectReferenceValue) return;
        var pauseOverlay = (GameObject)new SerializedObject(hud).FindProperty("pauseOverlay").objectReferenceValue;
        var pauseWindow = (GameObject)pauseBindings.FindProperty("pauseWindow").objectReferenceValue;
        var settings = (HudGameSettings)pauseBindings.FindProperty("settingsWindow").objectReferenceValue;
        var previousQuit = pauseWindow ? pauseWindow.transform.Find("Quit Game Button")?.GetComponent<Button>() : null;
        var logoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/PixelUISciFiFree/Font/Pixel UI Sci-Fi TMP.asset");
        var buttonFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SpaceHud/SceneAssets/MalgunGothic-System-TMP.asset");
        if (!pauseOverlay || !briefing || !settings || !previousQuit || !logoFont || !buttonFont)
            throw new InvalidOperationException("Main menu needs the existing pause menu, settings, briefing and HUD fonts.");

        var root = Rect("Main Menu Overlay", hud.Canvas.transform, Vector2.zero, Vector2.zero);
        root.SetSiblingIndex(pauseOverlay.transform.GetSiblingIndex());
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.gameObject.AddComponent<Image>().color = Color.black;

        var controls = Rect("Title and Actions", root, Vector2.zero, Vector2.zero);
        var logo = Rect("Space Dilemma Logo", controls, Vector2.zero, new Vector2(0, 83));
        DrawOrbit(logo);
        var top = Label("Space", logo, logoFont, "SPACE", 21, new Vector2(320, 30), new Vector2(0, 34), Cyan);
        top.characterSpacing = 8;
        var bottom = Label("Dilemma", logo, logoFont, "DILEMMA", 61, new Vector2(410, 72), new Vector2(0, -11), Ice);
        bottom.characterSpacing = 4;
        Bar("Logo Rule Left", logo, new Vector2(-85, -57), new Vector2(70, 1), 0, new Color(0.16f, 0.44f, 0.53f));
        Bar("Logo Rule Right", logo, new Vector2(85, -57), new Vector2(70, 1), 0, new Color(0.16f, 0.44f, 0.53f));
        Bar("Logo Rule Diamond", logo, new Vector2(0, -57), new Vector2(4, 4), 45, Cyan);

        var start = Action("Start Game", controls, buttonFont, "START GAME", -17, true, out var startLabel);
        var options = Action("Options", controls, buttonFont, "OPTIONS", -53, false, out var optionsLabel);
        var quit = Action("Quit Game", controls, buttonFont, "QUIT GAME", -89, false, out var quitLabel);
        LinkNavigation(start, quit, options);
        LinkNavigation(options, start, quit);
        LinkNavigation(quit, options, start);
        UnityEventTools.AddPersistentListener(start.onClick, pause.StartMission);
        UnityEventTools.AddPersistentListener(options.onClick, pause.ShowSettings);
        UnityEventTools.AddPersistentListener(quit.onClick, pause.QuitGame);

        var ui = root.gameObject.AddComponent<HudMainMenu>();
        var menuBindings = new SerializedObject(ui);
        Bind(menuBindings, "controls", controls.gameObject);
        Bind(menuBindings, "startButton", start);
        Bind(menuBindings, "optionsButton", options);
        Bind(menuBindings, "startLabel", startLabel);
        Bind(menuBindings, "optionsLabel", optionsLabel);
        Bind(menuBindings, "quitLabel", quitLabel);
        menuBindings.ApplyModifiedPropertiesWithoutUndo();
        Bind(pauseBindings, "mainMenu", ui);
        Bind(pauseBindings, "radiationBriefing", briefing);
        pauseBindings.ApplyModifiedProperties();
        root.gameObject.SetActive(false);

        ReplacePauseQuit(previousQuit, settings, pause);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the main menu to SampleScene.");
    }

    static void ReplacePauseQuit(Button button, HudGameSettings settings, HudPauseManager pause)
    {
        var settingsBindings = new SerializedObject(settings);
        var labels = settingsBindings.FindProperty("menuTexts");
        for (int i = 0; i < labels.arraySize; i++)
        {
            var entry = labels.GetArrayElementAtIndex(i);
            var english = entry.FindPropertyRelative("english");
            if (english.stringValue != "QUIT GAME") continue;
            english.stringValue = "MAIN MENU";
            entry.FindPropertyRelative("korean").stringValue = "메인 화면";
        }
        settingsBindings.ApplyModifiedProperties();

        Undo.RecordObject(button, "Route pause button to the main menu");
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (button.onClick.GetPersistentTarget(i) == pause &&
                button.onClick.GetPersistentMethodName(i) == nameof(HudPauseManager.QuitGame))
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, pause.ReturnToMainMenu);
        PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        if (button is HeyHeyThere.PixelUISciFiFree.PixelButton pixel)
        {
            Undo.RecordObject(pixel.label, "Label main menu action");
            pixel.text = "MAIN MENU";
            PrefabUtility.RecordPrefabInstancePropertyModifications(pixel.label);
        }
        Undo.RecordObject(button.gameObject, "Rename main menu action");
        button.gameObject.name = "Main Menu Button";
        PrefabUtility.RecordPrefabInstancePropertyModifications(button.gameObject);
    }

    // Build the small orbit emblem entirely from authored UGUI geometry.
    static void DrawOrbit(RectTransform logo)
    {
        var center = new Vector2(0, 71);
        Vector2 OrbitPoint(float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            var ellipse = new Vector2(Mathf.Cos(angle) * 21, Mathf.Sin(angle) * 6);
            Vector3 tilted = Quaternion.Euler(0, 0, 24) * ellipse;
            return center + (Vector2)tilted;
        }
        for (int i = 0; i < 19; i++)
        {
            var a = OrbitPoint(-30 + i * 17);
            var b = OrbitPoint(-30 + (i + 1) * 17);
            var segment = b - a;
            Bar("Orbit", logo, (a + b) * .5f, new Vector2(segment.magnitude + .25f, 1.05f),
                Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg, Cyan);
        }
        Bar("Lunar Core", logo, center, new Vector2(8, 8), 45, Ice);
        Bar("Orbital Marker", logo, OrbitPoint(-30), new Vector2(3.6f, 3.6f), 45, Cyan);
    }

    static void Bind(SerializedObject bindings, string name, UnityEngine.Object value) =>
        bindings.FindProperty(name).objectReferenceValue = value;

    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(obj, "Build Space Dilemma main menu");
        obj.layer = 5;
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string copy,
        float size, Vector2 box, Vector2 position, Color color)
    {
        var label = Rect(name, parent, box, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = copy;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    static void Bar(string name, Transform parent, Vector2 position, Vector2 size, float angle, Color color)
    {
        var rect = Rect(name, parent, size, position);
        rect.localRotation = Quaternion.Euler(0, 0, angle);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    static Button Action(string name, Transform parent, TMP_FontAsset font, string copy, float y, bool primary, out TMP_Text label)
    {
        var rect = Rect(name, parent, new Vector2(190, 28), new Vector2(0, y));
        var image = rect.gameObject.AddComponent<Image>();
        image.color = primary ? Cyan : Ink;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = primary ? new Color(.80f, .85f, .88f) : new Color(.64f, .72f, .76f);
        colors.selectedColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(.63f, .72f, .76f);
        button.colors = colors;
        var stroke = primary ? new Color(.68f, .96f, 1f) : new Color(.19f, .37f, .44f);
        Bar("Top Edge", rect, new Vector2(0, 13.5f), new Vector2(190, 1), 0, stroke);
        Bar("Bottom Edge", rect, new Vector2(0, -13.5f), new Vector2(190, 1), 0, stroke);
        Bar("Left Edge", rect, new Vector2(-94.5f, 0), new Vector2(1, 28), 0, stroke);
        Bar("Right Edge", rect, new Vector2(94.5f, 0), new Vector2(1, 28), 0, stroke);
        label = Label("Label", rect, font, copy, 12, new Vector2(184, 26), Vector2.zero, primary ? Ink : Ice);
        label.fontStyle = FontStyles.Bold;
        return button;
    }

    static void LinkNavigation(Button item, Button above, Button below) =>
        item.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = above, selectOnDown = below };
}
