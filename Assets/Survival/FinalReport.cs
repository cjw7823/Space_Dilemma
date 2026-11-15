using System;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
namespace SpaceDilemma.Results
{
    // Immutable end-of-run snapshot; changing UI language never reads live game totals again.
    public sealed class FinalReport
    {
        readonly double[] percentages=new double[3];
        readonly bool[] shieldInterrupted=new bool[3];
        readonly bool[,] abandonedDevices=new bool[3,3];
        public bool ShieldAbandoned(ResearchMission mission,int device)=>abandonedDevices[(int)mission,device];
        public bool ShieldInterrupted(ResearchMission mission)=>shieldInterrupted[(int)mission];
        public SurvivalOutcome Outcome { get; }
        public double Hours { get; }
        public double Dose { get; }
        public double OverallPercent { get; }
        public double IndoorSpeHours { get; }
        public double ShieldedSpeHours { get; }
        public double MeanIndoorReduction { get; }
        public double AvoidedSpeDose { get; }
        public int StoredSamples { get; }
        public int UsableSamples { get; }
        public double MissionPercent(ResearchMission mission)=>percentages[(int)mission];
        public FinalReport(SurvivalRun run,ResearchLedger research,EvaluationRules rules)
            : this(run,research,rules.GetResearchWeights()) { }
        public FinalReport(SurvivalRun run,ResearchLedger research,double samplesWeight,double instrumentsWeight,double plantsWeight)
            : this(run,research,new ResearchWeights(samplesWeight,instrumentsWeight,plantsWeight)) { }
        internal FinalReport(SurvivalRun run, ResearchLedger research, ResearchWeights weights)
        {
            if(run.Outcome==SurvivalOutcome.Running) throw new InvalidOperationException("A final report requires an ended survival run.");
            if(!research.IsFinished) throw new InvalidOperationException("Freeze research before capturing a final report.");
            Outcome=run.Outcome; Hours=run.Hours; Dose=run.Dose;
            IndoorSpeHours=run.IndoorSpeHours; ShieldedSpeHours=run.ShieldedSpeHours;
            MeanIndoorReduction=IndoorSpeHours>0?Math.Clamp(run.ReductionHours/IndoorSpeHours,0,1):0;
            AvoidedSpeDose=run.AvoidedSpeDose;
            for(int i=0;i<3;i++)
            {
                percentages[i]=ResearchWeights.ClampPercent(research.Percent((ResearchMission)i));
                shieldInterrupted[i]=research.LostResearchToShield((ResearchMission)i);
                for(int d=0;d<3;d++) abandonedDevices[i,d]=research.Get((ResearchMission)i,d).AbandonedForShield;
                var sample=research.Get(ResearchMission.Samples,i);
                if(!sample.Activated) continue;
                StoredSamples++;
                if(sample.Valid && (sample.Preservation?.Usable ?? true)) UsableSamples++;
            }
            OverallPercent = weights.OverallPercent(percentages[0], percentages[1], percentages[2]);
        }
    }
}
