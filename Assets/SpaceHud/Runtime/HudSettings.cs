using SpaceDilemma.CustomInput;
using UnityEngine;

namespace SpaceDilemma.Hud
{
    [CreateAssetMenu(menuName = "Space Dilemma/HUD Settings")]
    public sealed class HudSettings : ScriptableObject
    {
        public InputContextProfile pauseContext;
        [Header("Example objective progress")]
        public int exampleCurrent;
        public int exampleTotal = 1;
    }
}
