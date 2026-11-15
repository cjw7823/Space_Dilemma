using System;
using SpaceDilemma.Research;
using UnityEngine;

namespace SpaceDilemma.Results
{
    [CreateAssetMenu(menuName="Space Dilemma/Evaluation Rules")]
    public sealed class EvaluationRules : ScriptableObject
    {
        [Header("Research weights; survival is reported separately")]
        [Min(0)] public float samplesWeight=1;
        [Min(0)] public float instrumentsWeight=1;
        [Min(0)] public float plantsWeight=1;

        public ResearchWeights GetResearchWeights() => new(samplesWeight, instrumentsWeight, plantsWeight);
    }

    public readonly struct ResearchWeights
    {
        public double Samples { get; }
        public double Instruments { get; }
        public double Plants { get; }

        public ResearchWeights(double samples, double instruments, double plants)
        {
            samples = Nonnegative(samples);
            instruments = Nonnegative(instruments);
            plants = Nonnegative(plants);

            // Divide before summing so even very large relative inputs stay finite.
            double largest = Math.Max(samples, Math.Max(instruments, plants));
            if (largest == 0)
            {
                Samples = Instruments = Plants = 1.0 / 3;
                return;
            }

            samples /= largest;
            instruments /= largest;
            plants /= largest;
            double total = samples + instruments + plants;
            Samples = samples / total;
            Instruments = instruments / total;
            Plants = plants / total;
        }

        public double OverallPercent(ResearchLedger research) => OverallPercent(
            research.Percent(ResearchMission.Samples),
            research.Percent(ResearchMission.Instruments),
            research.Percent(ResearchMission.Plants));

        public double OverallPercent(double samples, double instruments, double plants) =>
            Math.Min(100, ClampPercent(samples) * Samples +
                         ClampPercent(instruments) * Instruments +
                         ClampPercent(plants) * Plants);

        internal static double ClampPercent(double value) => Math.Min(100, Nonnegative(value));

        static double Nonnegative(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
    }
}
