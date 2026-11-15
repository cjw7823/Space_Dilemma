using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceDilemma.CustomInput
{
    // The example's Canvas and controls live in SampleScene and can be edited in the Inspector.
    public sealed class CustomInputDemoPanel : MonoBehaviour
    {
        [Header("Scene UI references")]
        [SerializeField] GameObject canvasRoot;
        [SerializeField] Text title;
        [SerializeField] Text valueText;
        [SerializeField] Slider slider;
        [SerializeField] ScrollRect scroll;
        [SerializeField] Button solarButton;
        [SerializeField] FacilityInputReceiver solar;
        [SerializeField] FacilityInputReceiver oxygen;
        InputHub hub;
        FacilityInputReceiver selectedFacility;
        int uiToken;
        bool pendingBack;
        public ScrollRect Scroll => scroll;
        public Slider ValueSlider => slider;
        public bool IsVisible => canvasRoot && canvasRoot.activeSelf;

        public void Initialize(InputHub input)
        {
            if (hub) return;
            if (!input || !canvasRoot || !title || !valueText || !slider || !scroll || !solarButton || !solar || !oxygen)
            { Debug.LogError("Custom Input example is missing scene UI references.", this); return; }
            hub = input;
            solar.valueChanged.AddListener(DisplayValue); oxygen.valueChanged.AddListener(DisplayValue);
            hub.ContextChanged += ContextChanged; hub.ActionTriggered += OnAction;
            ContextChanged(hub.CurrentContext);
        }
        public void ToggleMenu()
        {
            if (!hub) return;
            if (hub.CurrentContext && hub.CurrentContext.blocksGameplay && !Owns(hub.CurrentContext)) return;
            if (hub.CurrentContext == hub.settings.ui) CloseMenu();
            else { uiToken = hub.PushContext(hub.settings.ui); Focus(solarButton); }
        }
        public void CloseMenu() { if (uiToken != 0) { hub.PopContext(uiToken); uiToken = 0; } }
        public void OpenSolar() => OpenFacility(false);
        public void OpenOxygen() => OpenFacility(true);
        public void OpenFacility(bool useOxygen)
        {
            CloseMenu(); selectedFacility?.Deactivate();
            selectedFacility = useOxygen ? oxygen : solar;
            selectedFacility.Activate(hub); Focus(slider);
        }
        public void CloseFacility() { selectedFacility?.Deactivate(); selectedFacility = null; }
        public void Back() { if (hub && hub.CurrentContext == hub.settings.ui) CloseMenu(); else CloseFacility(); }
        public void ShowUiExample() { if (valueText) valueText.text = "UI Submit received"; }
        public void ShowNavigationExample() { if (valueText) valueText.text = "UI navigation received"; }
        public void OnSliderChanged(float value)
        {
            selectedFacility?.SetValueFromUi(value);
            if (valueText) valueText.text = "UI slider: " + value.ToString("F3");
        }
        void ContextChanged(InputContextProfile profile)
        {
            if (!canvasRoot || !profile) return;
            bool ownsContext = Owns(profile);
            canvasRoot.SetActive(ownsContext);
            title.text = profile.contextId + " — Input Context Example";
            if (ownsContext)
            {
                scroll.StopMovement(); Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
                Focus(profile.actionMap == "UI" ? (Selectable)solarButton : slider);
            }
        }
        void OnAction(ActionSignal signal)
        {
            if (signal.phase != InputActionPhase.Performed || !Owns(hub.CurrentContext) || signal.map != hub.CurrentContext.actionMap) return;
            if (signal.action == "Cancel") pendingBack = true;
            if (signal.action == "AdjustValue" || signal.action == "PowerOutput" || signal.action == "OxygenOutput") DisplayValue(signal.scalar);
        }
        bool Owns(InputContextProfile profile) => profile && (profile == hub.settings.ui || profile == hub.settings.solarGenerator || profile == hub.settings.oxygenGenerator);
        void Update()
        {
            if (!pendingBack) return;
            pendingBack = false; Back();
        }
        void DisplayValue(float value)
        {
            slider.SetValueWithoutNotify(value);
            valueText.text = "Mapped value: " + value.ToString("F3") + " (example only)";
        }
        static void Focus(Selectable control)
        { if (EventSystem.current && control) EventSystem.current.SetSelectedGameObject(control.gameObject); }
        void OnDestroy()
        {
            if (hub) { hub.ContextChanged -= ContextChanged; hub.ActionTriggered -= OnAction; }
            if (solar) solar.valueChanged.RemoveListener(DisplayValue);
            if (oxygen) oxygen.valueChanged.RemoveListener(DisplayValue);
        }
    }
}
