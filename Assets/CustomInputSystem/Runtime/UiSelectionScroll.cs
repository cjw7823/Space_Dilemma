using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SpaceDilemma.CustomInput
{
    // InputSystemUIInputModule routes pointer-wheel events under the mouse.
    // Non-pointer sticks scroll the selected UI hierarchy via the same EventSystem.
    public sealed class UiSelectionScroll : MonoBehaviour
    {
        void Update()
        {
            var hub = GetComponent<InputHub>();
            if (!hub || !hub.CurrentContext || !hub.CurrentContext.blocksGameplay || !EventSystem.current) return;
            var action = hub.Actions.FindAction(hub.CurrentContext.actionMap + "/Scroll", false);
            if (action == null || !action.enabled || action.activeControl == null || action.activeControl.device is Pointer) return;
            Vector2 value = action.ReadValue<Vector2>();
            var target = EventSystem.current.currentSelectedGameObject;
            if (!target || value.sqrMagnitude < .0001f) return;
            var data = new PointerEventData(EventSystem.current) { scrollDelta = value * Time.unscaledDeltaTime };
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.scrollHandler);
        }
    }
}
