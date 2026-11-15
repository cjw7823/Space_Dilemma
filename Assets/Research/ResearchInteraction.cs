using System;
using System.Collections.Generic;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Scenario;
using SpaceDilemma.Survival;
using UnityEngine;

namespace SpaceDilemma.Research
{
    [DefaultExecutionOrder(110)]
    public sealed class ResearchInteraction : MonoBehaviour
    {
        [SerializeField, HideInInspector] ResearchRules rules;
        [SerializeField] MoonBasePlayer player;
        [SerializeField] SurvivalSession session;
        [SerializeField] ResearchTarget[] targets;

        readonly HashSet<ResearchTarget> completed = new HashSet<ResearchTarget>();
        readonly Dictionary<ResearchTarget, float> partial = new Dictionary<ResearchTarget, float>();
        readonly Dictionary<int, int> storedSampleBySlot = new Dictionary<int, int>();
        ResearchTarget previous;
        ScenarioInteraction Settings => session.Scenario?.interaction;
        public float HoldSeconds => Mathf.Max(.05f, Settings?.holdSeconds ?? rules.holdSeconds);
        public bool ResetWhenInterrupted => Settings?.resetWhenInterrupted ?? rules.resetWhenInterrupted;
        public bool NeedsRelease { get; private set; }

        public ResearchTarget Nearby { get; private set; }
        public float Progress => Nearby && partial.TryGetValue(Nearby, out float time)
            ? Mathf.Clamp01(time / HoldSeconds)
            : 0;
        public IReadOnlyList<ResearchTarget> Targets => targets;
        public IReadOnlyDictionary<int, int> StoredSampleBySlot => storedSampleBySlot;
        public int CarriedSampleCount => Count(ResearchKind.Sample) - storedSampleBySlot.Count;
        public event Action<ResearchTarget> Completed;
        public bool IsComplete(ResearchTarget target) => completed.Contains(target);
        public int Count(ResearchKind kind) => completed.Count(target => target.kind == kind);
        public bool CanUse(ResearchTarget target) => target && !IsComplete(target) &&
            (target.kind != ResearchKind.Storage || NextSample() >= 0);

        void Awake()
        {
            var config = session.Scenario;
            if (!config) return;
            for (int index = 0; index < 3; index++)
            {
                if (config.start.SampleCollected(index)) CompleteStartingTarget(ResearchKind.Sample, index);
                if (config.instruments.Device(index).initiallyCompleted) CompleteStartingTarget(ResearchKind.Instrument, index);
                if (config.plants.Device(index).initiallyCompleted) CompleteStartingTarget(ResearchKind.Plant, index);

                int sampleIndex = (int)config.start.Slot(index).sample - 1;
                if (sampleIndex < 0 || sampleIndex >= 3 || storedSampleBySlot.ContainsValue(sampleIndex)) continue;
                var sample = FindTarget(ResearchKind.Sample, sampleIndex);
                var storage = FindTarget(ResearchKind.Storage, index);
                if (!sample || !storage) continue;
                MarkComplete(sample);
                completed.Add(storage);
                storedSampleBySlot.Add(index, sampleIndex);
            }
        }

        void Start()
        {
            Physics.SyncTransforms();
            foreach (var target in targets)
            {
                if(!target || !target.isActiveAndEnabled)continue;
                if(target.kind==ResearchKind.Sample)target.PlaceOnGround();
                if(target.kind==ResearchKind.Instrument && target.TryGetComponent<InstrumentInstallation>(out var installation))
                { installation.PlaceOnGround();installation.SetInstalled(IsComplete(target)); }
            }
        }

        ResearchTarget FindTarget(ResearchKind kind, int index) =>
            Array.Find(targets, target => target && target.kind == kind && target.index == index);

        void CompleteStartingTarget(ResearchKind kind, int index)
        {
            var target = FindTarget(kind, index);
            if (target) MarkComplete(target);
        }

        void MarkComplete(ResearchTarget target)
        {
            completed.Add(target);
            if (target.kind == ResearchKind.Sample) target.gameObject.SetActive(false);
            if (target.kind == ResearchKind.Instrument && target.TryGetComponent<InstrumentInstallation>(out var installation))
                installation.SetInstalled(true);
        }

        void Update()
        {
            var input = InputHub.Active;
            bool pressed = input && input.IsPressed("ResearchInteract");
            if (!pressed) NeedsRelease = false;
            bool usable = input && input.GameplayAllowed && input.player.gameObject == player.gameObject &&
                !player.IsDriving && !player.IsObservingScenery && player.ControlCaptured && !session.IsPaused &&
                session.Run.Outcome == SurvivalOutcome.Running;
            Nearby = usable ? FindNearby() : null;
            if (previous != Nearby)
            {
                Interrupt(previous);
                previous = Nearby;
            }
            if (NeedsRelease || !CanUse(Nearby) || !pressed)
            {
                Interrupt(Nearby);
                return;
            }

            partial.TryGetValue(Nearby, out float time);
            time += Time.deltaTime;
            if (time < HoldSeconds)
            {
                partial[Nearby] = time;
                return;
            }

            var target = Nearby;
            if (target.kind == ResearchKind.Storage)
                storedSampleBySlot.Add(target.index, NextSample());
            MarkComplete(target);
            if (target.kind == ResearchKind.Sample) Nearby = null;
            partial.Remove(target);
            NeedsRelease = true;
            Completed?.Invoke(target);
        }

        void Interrupt(ResearchTarget target)
        {
            if (target && ResetWhenInterrupted) partial.Remove(target);
        }

        public ResearchTarget FindNearby()
        {
            ResearchTarget best = null;
            ResearchTarget completedNearby = null;
            float range = Mathf.Max(.1f, Settings?.researchRange ?? rules.interactionRange);
            float rangeSquared = range * range;
            float bestDistance = rangeSquared;
            float completedDistance = rangeSquared;
            Vector3 reachPoint = player.transform.position + Vector3.up * .8f;
            foreach (var target in targets)
            {
                if (!target || !target.isActiveAndEnabled) continue;
                Vector3 position = target.InteractionPoint(reachPoint);
                float distance = (position - reachPoint).sqrMagnitude;
                if (distance > rangeSquared) continue;
                if (IsComplete(target))
                {
                    if (!HasPriority(target, distance, completedNearby, completedDistance)) continue;
                    completedDistance = distance;
                    completedNearby = target;
                    continue;
                }
                if (!HasPriority(target, distance, best, bestDistance)) continue;
                bestDistance = distance;
                best = target;
            }
            return best ? best : completedNearby;
        }

        // Slots can share the transform position of a single storage model.
        static bool HasPriority(ResearchTarget candidate, float distance, ResearchTarget current, float currentDistance) =>
            !current || distance < currentDistance ||
            (distance == currentDistance && candidate.kind == current.kind && candidate.index < current.index);

        int NextSample()
        {
            for (int id = 0; id < 3; id++)
                if (!storedSampleBySlot.ContainsValue(id) &&
                    completed.Any(target => target.kind == ResearchKind.Sample && target.index == id))
                    return id;
            return -1;
        }
    }
}
