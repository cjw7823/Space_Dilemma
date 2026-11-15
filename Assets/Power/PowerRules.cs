using System;
using UnityEngine;

namespace SpaceDilemma.Power
{
    [Serializable]
    public sealed class PowerDeviceRule
    {
        [Min(0)] public float watts = 60;
        [Tooltip("Lower number is supplied first within this facility.")]
        public int priority;
    }

    [Serializable]
    public sealed class PowerFacilityRule
    {
        [Tooltip("Lower number is supplied first after life support and shielding.")]
        public int priority;
        public PowerDeviceRule[] devices = {
            new PowerDeviceRule { watts=60, priority=0 },
            new PowerDeviceRule { watts=70, priority=1 },
            new PowerDeviceRule { watts=80, priority=2 }
        };
    }

    [CreateAssetMenu(menuName="Power/Base Power Rules")]
    public sealed class PowerRules : ScriptableObject
    {
        [Header("Game test values, not NASA engineering specifications")]
        [Min(0)] public float availableWatts = 1000;
        [Min(0)] public float lifeSupportWatts = 250;
        [Min(0)] public float shieldMaximumWatts = 500;
        public PowerFacilityRule samples = new PowerFacilityRule { priority=0 };
        public PowerFacilityRule instruments = new PowerFacilityRule { priority=1 };
        public PowerFacilityRule plants = new PowerFacilityRule { priority=2 };
        public PowerFacilityRule Facility(int index) => index switch { 0=>samples, 1=>instruments, 2=>plants, _=>throw new ArgumentOutOfRangeException(nameof(index)) };
    }
}
