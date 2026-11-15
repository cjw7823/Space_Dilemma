using UnityEngine;

namespace SpaceDilemma.Research
{
    [CreateAssetMenu(menuName = "Research/Plant Experiment Information")]
    public sealed class PlantExperimentInformation : ScriptableObject
    {
        [Header("Reusable experiment reference values — one habitat set")]
        [Min(0)] public float referencePowerWatts = 735;
        [Min(1)] public int observationMinutes = 1720;
        public double ObservationHours => observationMinutes / 60d;
        public double ReferenceEnergyKwh => referencePowerWatts / 1000d * ObservationHours;
        [TextArea(5, 15)] public string objectiveEnglish, objectiveKorean;
        [TextArea(3, 8)] public string adaptationEnglish, adaptationKorean;
        public string powerSourceUrl;
        public string durationSourceUrl;
    }
}
