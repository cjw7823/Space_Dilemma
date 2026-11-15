using UnityEngine;

namespace SpaceDilemma.Research
{
    [CreateAssetMenu(menuName = "Space Dilemma/Research Interaction Rules")]
    public sealed class ResearchRules : ScriptableObject
    {
        [Min(.1f)] public float interactionRange = 2f;
        [Min(.05f)] public float holdSeconds = 1.25f;
        public bool resetWhenInterrupted = true;
    }
}
