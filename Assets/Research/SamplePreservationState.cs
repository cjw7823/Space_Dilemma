using System;
namespace SpaceDilemma.Research
{
    public sealed class SamplePreservationState
    {
        readonly double grace,loss,minimum;
        public double Quality { get; private set; }=1;
        public double OutageHours { get; private set; }
        public bool Usable => Quality>minimum;
        public SamplePreservationState(SamplePreservationRules rules) : this(rules.outageGraceHours,
            rules.qualityLossPerHour, rules.minimumUsableQuality) { }

        internal SamplePreservationState(double outageGraceHours, double qualityLossPerHour, double minimumUsableQuality)
        {
            grace=Math.Max(0,outageGraceHours);
            loss=Math.Max(0,qualityLossPerHour);
            minimum=Math.Clamp(minimumUsableQuality,0,1);
        }

        internal void SetInitialQuality(double quality) => Quality = double.IsFinite(quality) ? Math.Clamp(quality, 0, 1) : 1;
        public void Advance(double hours,bool cooled)
        {
            if(!double.IsFinite(hours) || hours<=0) return;
            if(cooled) { OutageHours=0; return; }
            double previous=OutageHours; OutageHours+=hours;
            double damagingHours=Math.Max(0,OutageHours-grace)-Math.Max(0,previous-grace);
            Quality=Math.Max(0,Quality-loss*damagingHours);
        }
    }
}
