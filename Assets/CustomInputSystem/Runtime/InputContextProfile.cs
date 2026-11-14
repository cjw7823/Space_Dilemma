using System;
using UnityEngine;
namespace SpaceDilemma.CustomInput
{
    [CreateAssetMenu(menuName = "Custom Input/Context Profile")]
    public sealed class InputContextProfile : ScriptableObject
    {
        public string contextId = "Facility";
        public string actionMap = "Facility";
        public int priority = 50;
        public bool blocksGameplay = true;
        public BindingOverride[] bindingOverrides = Array.Empty<BindingOverride>();
    }
}
