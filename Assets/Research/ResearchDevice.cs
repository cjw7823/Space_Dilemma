using System;
using UnityEngine;

namespace SpaceDilemma.Research
{
    // Device switches are authored in the scene and can later be controlled by the power system.
    public sealed class ResearchDevice : MonoBehaviour
    {
        public ResearchMission mission;
        [Range(0, 2)] public int index;
        [Tooltip("Discrete power state. Off stops new data and retains existing data.")]
        public bool powerAvailable = true;
        [Tooltip("Placeholder validity flag for a later sample temperature / quality model.")]
        public bool researchValid = true;
        public event Action StateChanged;

        public void SetPowered(bool powered)
        {
            if (powerAvailable == powered) return;
            powerAvailable = powered;
            StateChanged?.Invoke();
        }

        public void SetValid(bool valid)
        {
            if (researchValid == valid) return;
            researchValid = valid;
            StateChanged?.Invoke();
        }
    }
}
