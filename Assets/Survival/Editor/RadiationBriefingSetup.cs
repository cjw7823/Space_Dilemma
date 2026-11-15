using System;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Survival;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

static class RadiationBriefingSetup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    static readonly Color Accent = new(0.37f, 0.89f, 1f);
    static readonly Color Body = new(0.89f, 0.95f, 1f);
    static readonly Color Card = new(0.047f, 0.113f, 0.16f);

    [MenuItem("Tools/Survival/Set Up Radiation Briefing")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before editing the briefing.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Open SampleScene before setting up the briefing.");

        var hud = UnityEngine.Object.FindFirstObjectByType<HudController>();
        var session = UnityEngine.Object.FindFirstObjectByType<SurvivalSession>();
        var language = UnityEngine.Object.FindFirstObjectByType<HudGameSettings>(FindObjectsInactive.Include);
        var context = AssetDatabase.LoadAssetAtPath<InputContextProfile>(
            "Assets/Research/SampleInformation/Sample Popup Context.asset");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/SpaceHud/SceneAssets/MalgunGothic-System-TMP.asset");
        if (!hud || !session || !language || !context || !font)
            throw new InvalidOperationException("SampleScene is missing a briefing dependency.");
        if (hud.GetComponent<RadiationBriefing>())
            return;

        var controller = Undo.AddComponent<RadiationBriefing>(hud.gameObject);
        var bindings = new SerializedObject(controller);
        Bind(bindings, "session", session);
        Bind(bindings, "pause", hud.GetComponent<HudPauseManager>());
        Bind(bindings, "language", language);
        Bind(bindings, "context", context);

        var overlay = Rect("Radiation Briefing Overlay", hud.Canvas.transform);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color = new Color(0.008f, 0.024f, 0.043f, 0.94f);
        Bind(bindings, "overlay", overlay.gameObject);

        var frame = Row("Window Frame", overlay, 0.035f, 0.965f, 166, -166);
        frame.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.61f, 0.76f);
        var panel = Rect("Panel", frame);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.one;
        panel.offsetMax = -Vector2.one;
        panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.063f, 0.091f);

        Bind(bindings, "title", Text(Row("Title", panel, 0.035f, 0.965f, 150, 125), font, 18, Accent));
        Bind(bindings, "intro", Text(Row("Introduction", panel, 0.035f, 0.965f, 122, 105), font, 10, Body));
        var measured = Row("Observations Card", panel, 0.035f, 0.488f, 99, -33);
        var game = Row("Game Calculation Card", panel, 0.512f, 0.965f, 99, -33);
        BindCard(bindings, "observationsHeading", "observations", measured, font, 132);
        BindCard(bindings, "calculationHeading", "calculation", game, font, 132);
        var shielding = Row("Protection Card", panel, 0.035f, 0.965f, -42, -98);
        BindCard(bindings, "protectionHeading", "protection", shielding, font, 56);
        Bind(bindings, "sources", Text(Row("Scientific Sources", panel, 0.035f, 0.965f, -106, -125), font, 8.7f, Accent));

        var radLab = ActionButton(panel, "RadLab Source", font, 0.035f, 0.228f, Card, out var radLabLabel);
        var donki = ActionButton(panel, "DONKI Source", font, 0.244f, 0.437f, Card, out var donkiLabel);
        var begin = ActionButton(panel, "Begin Mission", font, 0.69f, 0.965f,
            new Color(0.04f, 0.35f, 0.46f), out var beginLabel);
        Bind(bindings, "radLabButton", radLab);
        Bind(bindings, "donkiButton", donki);
        Bind(bindings, "continueButton", begin);
        Bind(bindings, "radLabLabel", radLabLabel);
        Bind(bindings, "donkiLabel", donkiLabel);
        Bind(bindings, "continueLabel", beginLabel);
        Navigate(radLab, null, donki);
        Navigate(donki, radLab, begin);
        Navigate(begin, donki, null);
        UnityEventTools.AddPersistentListener(radLab.onClick, controller.OpenRadLab);
        UnityEventTools.AddPersistentListener(donki.onClick, controller.OpenDonki);
        UnityEventTools.AddPersistentListener(begin.onClick, controller.Close);
        bindings.ApplyModifiedProperties();
        controller.Refresh();
        overlay.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the radiation briefing to SampleScene.");
    }

    static void BindCard(SerializedObject bindings, string heading, string body, RectTransform card, TMP_FontAsset font, int height)
    {
        card.gameObject.AddComponent<Image>().color = Card;
        int top = height / 2;
        Bind(bindings, heading, Text(Row("Heading", card, 0.04f, 0.96f, top - 6, top - 23), font, 11.5f, Accent));
        Bind(bindings, body, Text(Row("Explanation", card, 0.04f, 0.96f, top - 25, -top + 6), font, 9.6f, Body));
    }

    static void Bind(SerializedObject target, string name, UnityEngine.Object value) =>
        target.FindProperty(name).objectReferenceValue = value;

    static RectTransform Rect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(obj, "Create radiation briefing");
        obj.layer = 5;
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static RectTransform Row(string name, Transform parent, float left, float right, float top, float bottom)
    {
        var rect = Rect(name, parent);
        rect.anchorMin = new Vector2(left, 0.5f);
        rect.anchorMax = new Vector2(right, 0.5f);
        rect.offsetMin = new Vector2(0, bottom);
        rect.offsetMax = new Vector2(0, top);
        return rect;
    }

    static TMP_Text Text(RectTransform rect, TMP_FontAsset font, float size, Color color)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.85f;
        label.fontSizeMax = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        return label;
    }

    static Button ActionButton(RectTransform parent, string name, TMP_FontAsset font,
        float left, float right, Color color, out TMP_Text label)
    {
        var rect = Row(name, parent, left, right, -130, -153);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.87f, 0.93f, 0.98f);
        colors.selectedColor = Color.white;
        colors.highlightedColor = new Color(0.7f, 0.88f, 1f);
        colors.colorMultiplier = 1.2f;
        button.colors = colors;
        label = Text(Row("Label", rect, 0.02f, 0.98f, 10, -10), font, 10, Color.white);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    static void Navigate(Button button, Button left, Button right) =>
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = left, selectOnRight = right };
}
