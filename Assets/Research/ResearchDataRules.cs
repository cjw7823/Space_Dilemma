using System;
using UnityEngine;

namespace SpaceDilemma.Research
{
    public enum ResearchMission { Samples, Instruments, Plants }

    [Serializable]
    public sealed class ResearchDeviceRule
    {
        [Tooltip("Placeholder valid data units per game hour.")]
        [Min(0f)] public float unitsPerGameHour = 1f;
        [Min(0f)] public float maximumData = 24f;
        [Tooltip("Weight applied to this device's data when calculating mission achievement.")]
        [Min(0f)] public float contribution = 1f;
    }

    [Serializable]
    public sealed class ResearchMissionRule
    {
        [Min(.001f)] public float targetData = 72f;
        [Range(0f, 100f)] public float maximumPercent = 100f;
        public ResearchDeviceRule[] devices =
            { new ResearchDeviceRule(), new ResearchDeviceRule(), new ResearchDeviceRule() };
    }

    [CreateAssetMenu(menuName = "Space Dilemma/Research Data Rules")]
    public sealed class ResearchDataRules : ScriptableObject
    {
        [Header("Placeholder gameplay rates and targets — not NASA measurements")]
        public ResearchMissionRule samples = new ResearchMissionRule();
        public ResearchMissionRule instruments = new ResearchMissionRule();
        public ResearchMissionRule plants = new ResearchMissionRule();

        public ResearchMissionRule For(ResearchMission mission) => mission switch
        {
            ResearchMission.Samples => samples,
            ResearchMission.Instruments => instruments,
            ResearchMission.Plants => plants,
            _ => throw new ArgumentOutOfRangeException(nameof(mission))
        };
    }
}
