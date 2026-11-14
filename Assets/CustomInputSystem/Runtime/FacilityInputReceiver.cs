using UnityEngine;
using UnityEngine.Events;

namespace SpaceDilemma.CustomInput
{
    // Add to any facility; selection/interaction calls Activate, deselection calls Deactivate.
    public sealed class FacilityInputReceiver : MonoBehaviour
    {
        public InputContextProfile context;
        public string scalarAction = "PowerOutput";
        public UnityEvent<float> valueChanged = new UnityEvent<float>();
        public UnityEvent confirmed = new UnityEvent();
        public float LastValue { get; private set; }
        InputHub hub;
        int token;
        public void Activate() => Activate(InputHub.Active);
        public void Activate(InputHub input)
        {
            if (hub || !input || !context) return;
            hub = input; token = hub.PushContext(context);
            hub.ActionTriggered += OnAction;
        }
        void OnAction(ActionSignal signal)
        {
            if (!hub || hub.CurrentContext != context || signal.map != context.actionMap || signal.phase != UnityEngine.InputSystem.InputActionPhase.Performed) return;
            if (signal.action == scalarAction) { LastValue = signal.scalar; valueChanged.Invoke(LastValue); }
            else if (signal.action == "Confirm") confirmed.Invoke();
        }
        public void Deactivate()
        {
            if (!hub) return;
            var active = hub; hub = null;
            active.ActionTriggered -= OnAction; active.PopContext(token); token = 0;
        }
        public void SetValueFromUi(float value)
        {
            if (!hub || hub.CurrentContext != context) return;
            LastValue = Mathf.Clamp01(value); valueChanged.Invoke(LastValue);
        }
        void OnDisable() => Deactivate();
    }
}
