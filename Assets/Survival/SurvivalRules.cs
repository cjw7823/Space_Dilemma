using UnityEngine;

namespace SpaceDilemma.Survival
{
    [CreateAssetMenu(menuName = "Space Dilemma/Survival Rules")]
    public sealed class SurvivalRules : ScriptableObject
    {
        [Header("Game time")]
        [Min(.001f)] public float durationHours = 48f;
        [Min(0f)] public float speStartHour = 24f;
        [Min(.0001f)] public float gameHoursPerSecond = .02f;

        [Header("Placeholder gameplay values — not NASA data")]
        [Tooltip("Unshielded background dose rate, in mSv/game hour.")]
        [Min(0f)] public float normalRate = .1f;
        [Tooltip("Unshielded dose rate during SPE, in mSv/game hour.")]
        [Min(0f)] public float speRate = 3f;
        [Tooltip("Cumulative death threshold, in mSv.")]
        [Min(.001f)] public float lethalDose = 50f;
        [Tooltip("Fraction of outdoor dose reaching the player inside the base.")]
        [Range(0f, 1f)] public float indoorTransmission = .35f;
    }
}
