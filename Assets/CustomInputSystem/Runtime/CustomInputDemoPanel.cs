using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceDilemma.CustomInput
{
    // Small runtime example, hidden until F2 / Gamepad Start / Serial button2.
    // It changes no scene/prefab asset and does not implement generator game logic.
    public sealed class CustomInputDemoPanel : MonoBehaviour
    {
        InputHub hub;
        GameObject canvasRoot;
        Text title, valueText;
        Slider slider;
        Button solarButton, closeButton;
        int uiToken;
        FacilityInputReceiver solar, oxygen, selectedFacility;
        public ScrollRect Scroll { get; private set; }
        public Slider ValueSlider => slider;
        public bool IsVisible => canvasRoot && canvasRoot.activeSelf;

        public void Initialize(InputHub input)
        {
            hub = input;
            solar = new GameObject("Solar generator input example").AddComponent<FacilityInputReceiver>();
            solar.transform.SetParent(transform); solar.context = hub.settings.solarGenerator; solar.scalarAction = "PowerOutput";
            oxygen = new GameObject("Oxygen generator input example").AddComponent<FacilityInputReceiver>();
            oxygen.transform.SetParent(transform); oxygen.context = hub.settings.oxygenGenerator; oxygen.scalarAction = "OxygenOutput";
            solar.valueChanged.AddListener(DisplayValue); oxygen.valueChanged.AddListener(DisplayValue);
            hub.ContextChanged += ContextChanged; hub.ActionTriggered += OnAction;
            BuildUi(); ContextChanged(hub.CurrentContext);
        }
        public void ToggleMenu()
        {
            if (hub.CurrentContext == hub.settings.ui) CloseMenu();
            else { uiToken = hub.PushContext(hub.settings.ui); Focus(solarButton); }
        }
        public void CloseMenu()
        { if (uiToken != 0) { hub.PopContext(uiToken); uiToken = 0; } }
        public void OpenFacility(bool useOxygen)
        {
            CloseMenu(); selectedFacility?.Deactivate();
            selectedFacility = useOxygen ? oxygen : solar;
            selectedFacility.Activate(hub); Focus(slider);
        }
        public void CloseFacility()
        { selectedFacility?.Deactivate(); selectedFacility = null; }
        void ContextChanged(InputContextProfile profile)
        {
            if (!canvasRoot) return;
            canvasRoot.SetActive(profile.blocksGameplay);
            title.text = profile.contextId + " — Input Context Example";
            if (profile.blocksGameplay)
            {
                Scroll.StopMovement(); Canvas.ForceUpdateCanvases(); Scroll.verticalNormalizedPosition = 1;
                Focus(profile.actionMap == "UI" ? (Selectable)solarButton : slider);
            }
        }
        void OnAction(ActionSignal signal)
        {
            if (signal.phase != InputActionPhase.Performed || !hub.CurrentContext || signal.map != hub.CurrentContext.actionMap) return;
            if (signal.action == "Cancel") pendingBack = true;
            if (signal.action == "AdjustValue" || signal.action == "PowerOutput" || signal.action == "OxygenOutput") DisplayValue(signal.scalar);
        }
        bool pendingBack;
        void Update()
        {
            if (!pendingBack) return;
            pendingBack = false;
            if (hub.CurrentContext == hub.settings.ui) CloseMenu(); else CloseFacility();
        }
        void DisplayValue(float value)
        {
            slider.SetValueWithoutNotify(value);
            valueText.text = "Mapped value: " + value.ToString("F3") + " (example only)";
        }
        static void Focus(Selectable control)
        { if (EventSystem.current && control) EventSystem.current.SetSelectedGameObject(control.gameObject); }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        static Text Label(string name, Transform parent, string text, Vector2 size, Vector2 position, int fontSize = 18)
        {
            var label = Rect(name, parent, size, position).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.text = text;
            label.color = Color.white; label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter;
            return label;
        }
        static Button Button(string name, Transform parent, Vector2 position, UnityEngine.Events.UnityAction click)
        {
            var rect = Rect(name, parent, new Vector2(400, 42), position);
            rect.gameObject.AddComponent<Image>().color = new Color(.13f, .26f, .37f);
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors; colors.selectedColor = new Color(.25f, .8f, .7f); colors.highlightedColor = colors.selectedColor; button.colors = colors;
            button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(click);
            Label("Label", rect, name, new Vector2(390, 38), Vector2.zero);
            return button;
        }
        void BuildUi()
        {
            canvasRoot = new GameObject("Custom Input Context Examples", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var panel = Rect("Panel", canvasRoot.transform, new Vector2(520, 460), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(.025f, .05f, .09f, .98f);
            title = Label("Context", panel, "Input contexts", new Vector2(500, 44), new Vector2(0, 195));
            Label("Help", panel, "Left stick / arrows: focus. A / Enter: select. B / Esc: back.\nRight stick: scroll. Serial slider / right trigger: value.", new Vector2(490, 60), new Vector2(0, 140), 14);
            var viewport = Rect("Scroll viewport", panel, new Vector2(450, 190), new Vector2(0, 5));
            viewport.gameObject.AddComponent<Image>().color = new Color(.06f, .09f, .14f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Scroll = viewport.gameObject.AddComponent<ScrollRect>(); Scroll.viewport = viewport; Scroll.horizontal = false; Scroll.movementType = ScrollRect.MovementType.Clamped;
            var content = Rect("Choices", viewport, new Vector2(430, 250), new Vector2(0, 0));
            content.anchorMin = new Vector2(.5f, 1); content.anchorMax = content.anchorMin; content.pivot = new Vector2(.5f, 1); Scroll.content = content;
            solarButton = Button("Solar Generator", content, new Vector2(0, -30), () => OpenFacility(false));
            Button("Oxygen Generator", content, new Vector2(0, -85), () => OpenFacility(true));
            Button("UI selection example", content, new Vector2(0, -140), () => valueText.text = "UI Submit received");
            Button("Another focus target", content, new Vector2(0, -195), () => valueText.text = "UI navigation received");
            foreach (var child in content.GetComponentsInChildren<Button>())
            {
                var rect = (RectTransform)child.transform;
                Vector2 position = rect.anchoredPosition;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
                rect.anchoredPosition = position;
            }
            var sliderRect = Rect("Mapped value slider", panel, new Vector2(400, 28), new Vector2(0, -115));
            sliderRect.gameObject.AddComponent<Image>().color = new Color(.13f, .2f, .27f);
            slider = sliderRect.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1;
            var fillArea = Rect("Fill area", sliderRect, Vector2.zero, Vector2.zero);
            fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(4, 4); fillArea.offsetMax = new Vector2(-4, -4);
            var fill = Rect("Fill", fillArea, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = new Color(.15f, .8f, .6f);
            var handleArea = Rect("Handle area", sliderRect, Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(8, 0); handleArea.offsetMax = new Vector2(-8, 0);
            var handle = Rect("Handle", handleArea, new Vector2(16, 32), Vector2.zero); handle.gameObject.AddComponent<Image>().color = Color.white;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.onValueChanged.AddListener(value => { selectedFacility?.SetValueFromUi(value); valueText.text = "UI slider: " + value.ToString("F3"); });
            valueText = Label("Value", panel, "Mapped value: 0.000 (example only)", new Vector2(490, 30), new Vector2(0, -155), 16);
            closeButton = Button("Back", panel, new Vector2(0, -200), () => { if (hub.CurrentContext == hub.settings.ui) CloseMenu(); else CloseFacility(); });
        }
        void OnDestroy()
        {
            if (!hub) return;
            hub.ContextChanged -= ContextChanged; hub.ActionTriggered -= OnAction;
        }
    }
}
