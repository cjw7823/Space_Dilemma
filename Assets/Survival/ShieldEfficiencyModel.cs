using System;
namespace SpaceDilemma.Survival
{
    // Replaceable model; SurvivalSession depends only on IRadiationShield.
    public sealed class ShieldEfficiencyModel
    {
        readonly double maximumReduction;
        public ShieldEfficiencyModel(double maximumReduction) => this.maximumReduction=double.IsFinite(maximumReduction)?Math.Clamp(maximumReduction,0,1):0;
        public double Reduction(double suppliedOutput) => maximumReduction*(double.IsFinite(suppliedOutput)?Math.Clamp(suppliedOutput,0,1):0);
        public double Transmission(bool inside,bool speActive,double suppliedOutput) => inside && speActive?1-Reduction(suppliedOutput):1;
    }
}
