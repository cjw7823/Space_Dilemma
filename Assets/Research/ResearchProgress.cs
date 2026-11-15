using System;
using SpaceDilemma.Survival;
using SpaceDilemma.Power;
using UnityEngine;

namespace SpaceDilemma.Research
{
    public sealed class ResearchProgress : MonoBehaviour
    {
        [SerializeField, HideInInspector] ResearchDataRules rules;
        [SerializeField, HideInInspector] SamplePreservationRules preservationRules;
        [SerializeField] ResearchInteraction interaction;
        [SerializeField] SurvivalSession session;
        [SerializeField] ResearchDevice[] devices;
        public ResearchLedger Ledger { get; private set; }
        public ResearchDevice[] Devices => devices;
        public ResearchInteraction Interaction => interaction;
        public event Action Changed;
        BasePowerController power;

        void Start()
        {
            power=FindFirstObjectByType<BasePowerController>();
            var config = session.Scenario;
            Ledger = config ? new ResearchLedger(config, session.Run.Hours) :
                new ResearchLedger(rules, session.Run.Hours, preservationRules);
            if (config)
                foreach (var device in devices)
                    device.SetValid(config.Mission(device.mission).Device(device.index).initiallyValid);
            foreach (var device in devices) device.StateChanged += RefreshStates;
            interaction.Completed += OnCompleted;
            session.Run.TimeChanged += OnTimeChanged;
            session.Run.Ended += OnEnded;
            RefreshStates();
            if (!config) return;
            foreach (var device in devices)
            {
                int index = device.index;
                var mission = device.mission;
                double quality = mission == ResearchMission.Samples ? config.start.Slot(index).quality : 1;
                Ledger.SetInitialData(mission, index, config.Mission(mission).Device(index).initialData, quality);
            }
            Changed?.Invoke();
        }

        void OnTimeChanged(double hour)
        {
            // Inspector switches are sampled for this elapsed interval.
            RefreshStates();
            Ledger.AdvanceTo(hour);
            Changed?.Invoke();
        }

        void OnCompleted(ResearchTarget target) => RefreshStates();

        void RefreshStates()
        {
            foreach (var device in devices)
            {
                ResearchKind kind = device.mission switch
                {
                    ResearchMission.Samples => ResearchKind.Storage,
                    ResearchMission.Instruments => ResearchKind.Instrument,
                    _ => ResearchKind.Plant
                };
                bool activated = false;
                foreach (var target in interaction.Targets)
                    if (target.kind == kind && target.index == device.index && interaction.IsComplete(target))
                        activated = true;
                if (device.mission == ResearchMission.Samples)
                    activated &= interaction.StoredSampleBySlot.ContainsKey(device.index);
                Ledger.SetState(device.mission, device.index, activated, device.powerAvailable, device.researchValid);
                Ledger.SetShieldAbandoned(device.mission,device.index,
                    power && power.Budget.Abandoned((int)device.mission,device.index));
            }
            Changed?.Invoke();
        }

        void OnEnded(SurvivalOutcome outcome)
        {
            Ledger.Finish();
            Changed?.Invoke();
        }

        void OnDestroy()
        {
            foreach (var device in devices) if (device) device.StateChanged -= RefreshStates;
            if (interaction) interaction.Completed -= OnCompleted;
            if (session && session.Run != null)
            {
                session.Run.TimeChanged -= OnTimeChanged;
                session.Run.Ended -= OnEnded;
            }
        }
    }
}
