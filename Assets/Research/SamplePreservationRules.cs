using UnityEngine;
namespace SpaceDilemma.Research
{
    [CreateAssetMenu(menuName="Space Dilemma/Sample Preservation Rules")]
    public sealed class SamplePreservationRules : ScriptableObject
    {
        [Header("Temporary quality model - not a physical temperature simulation")]
        [Min(0)] public float outageGraceHours=.5f;
        [Tooltip("Quality fraction lost per game hour after the outage grace period.")]
        [Min(0)] public float qualityLossPerHour=.15f;
        [Tooltip("At or below this quality, the stored sample cannot earn further credit.")]
        [Range(0,1)] public float minimumUsableQuality=.25f;
    }
}
