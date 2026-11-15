using System;

namespace SpaceDilemma.Survival
{
    public enum SurvivalOutcome { Running, Survived, Died }

    // Implementations return the fraction of radiation left after active shielding.
    public interface IRadiationShield
    {
        double Transmission(bool inside, bool speActive);
    }

    public sealed class RadiationExposure
    {
        public double Dose { get; private set; }

        public RadiationExposure(double initialDose = 0) => Dose = initialDose;

        public double Integrate(double hours, double rate, double limit, out bool reachedLimit)
        {
            reachedLimit = rate > 0 && (limit - Dose) / rate <= hours + 1e-9;
            if (!reachedLimit)
            {
                Dose += hours * rate;
                return hours;
            }

            double usedHours = Math.Min(hours, Math.Max(0, (limit - Dose) / rate));
            Dose = limit;
            return usedHours;
        }
    }

    // A frame may cross the start of the event or a terminal boundary.
    public sealed class SurvivalRun
    {
        readonly double duration, speHour, speEndHour, normalRate, limit, indoorTransmission;
        readonly bool fitSpeToRemainingDose;
        readonly double speRemainingDoseMultiplier;
        readonly RadiationExposure exposure;
        double speRate;
        double activeTransmission = 1;
        public double Hours { get; private set; }
        public double Remaining => duration - Hours;
        public double Dose => exposure.Dose;
        public double DoseLimit => limit;
        public double Rate { get; private set; }
        public double SpeOutdoorRate => speRate;
        public bool SpeRateCalibrated { get; private set; }
        public double SpeCalibrationHour { get; private set; }
        public double SpeStartDose { get; private set; }
        public double IndoorSpeHours { get; private set; }
        public double ShieldedSpeHours { get; private set; }
        public double ReductionHours { get; private set; }
        public double AvoidedSpeDose { get; private set; }
        public bool SpeActive => speHour < duration && Hours >= speHour && Hours < speEndHour;
        public SurvivalOutcome Outcome { get; private set; }
        public event Action<double> TimeChanged;
        public event Action SpeStarted;
        public event Action<SurvivalOutcome> Ended;

        public SurvivalRun(double duration, double speHour, double normalRate, double speRate,
            double limit, double indoorTransmission, double initialHours = 0, double initialDose = 0,
            double speDurationHours = 0, bool fitSpeToRemainingDose = false, double speRemainingDoseMultiplier = 1)
        {
            this.duration = Math.Max(.001, Nonnegative(duration));
            this.speHour = Nonnegative(speHour);
            speEndHour = Nonnegative(speDurationHours) > 0 ? this.speHour + speDurationHours : double.PositiveInfinity;
            this.normalRate = Nonnegative(normalRate);
            this.speRate = Nonnegative(speRate);
            this.fitSpeToRemainingDose = fitSpeToRemainingDose;
            this.speRemainingDoseMultiplier = Math.Max(1, Nonnegative(speRemainingDoseMultiplier));
            this.limit = Math.Max(.001, Nonnegative(limit));
            this.indoorTransmission = Math.Min(1, Nonnegative(indoorTransmission));
            Hours = Math.Min(this.duration, Nonnegative(initialHours));
            exposure = new RadiationExposure(Math.Min(this.limit, Nonnegative(initialDose)));
            CalibrateSpeRate();
        }

        static double Nonnegative(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;

        void CalibrateSpeRate()
        {
            if (!fitSpeToRemainingDose || SpeRateCalibrated || !SpeActive || Remaining <= 0) return;
            // Capture once at event entry, before applying indoor or powered shielding.
            // A custom start during the event uses that start's dose and remaining time.
            SpeCalibrationHour = Hours;
            SpeStartDose = Dose;
            speRate = Math.Max(0, limit - Dose) / Remaining * speRemainingDoseMultiplier;
            SpeRateCalibrated = true;
        }

        public void UpdateRate(bool inside, IRadiationShield shield)
        {
            CalibrateSpeRate();
            double active = Math.Clamp(shield?.Transmission(inside, SpeActive) ?? 1, 0, 1);
            activeTransmission = active;
            Rate = Outcome == SurvivalOutcome.Running
                ? (SpeActive ? speRate : normalRate) * (inside ? indoorTransmission : 1) * active
                : 0;
        }

        public void Advance(double hours, bool inside, IRadiationShield shield = null)
        {
            if (Outcome != SurvivalOutcome.Running || !double.IsFinite(hours) || hours < 0) return;
            if (Dose >= limit || Hours >= duration)
            {
                Finish(Dose >= limit, inside, shield);
                return;
            }
            if (hours == 0) return;

            while (hours > 0)
            {
                bool wasSpeActive = SpeActive;
                double boundary = duration;
                if (Hours < speHour) boundary = Math.Min(boundary, speHour);
                if (wasSpeActive) boundary = Math.Min(boundary, speEndHour);
                double remainingInSegment = boundary - Hours;
                double step = hours + 1e-9 >= remainingInSegment ? remainingInSegment : hours;
                UpdateRate(inside, shield);
                double used = exposure.Integrate(step, Rate, limit, out bool lethal);
                // Record only the interval actually used, before a boundary changes SPE state.
                if (wasSpeActive && inside)
                {
                    double reduction = 1 - activeTransmission;
                    IndoorSpeHours += used;
                    if (reduction > 1e-9) ShieldedSpeHours += used;
                    ReductionHours += used * reduction;
                    AvoidedSpeDose += used * speRate * indoorTransmission * reduction;
                }
                Hours = used == remainingInSegment ? boundary : Hours + used;
                hours -= used;
                if (lethal || Hours >= duration)
                {
                    Finish(lethal, inside, shield);
                    return;
                }
                if (!wasSpeActive && SpeActive)
                {
                    UpdateRate(inside, shield);
                    SpeStarted?.Invoke();
                }
            }

            UpdateRate(inside, shield);
            TimeChanged?.Invoke(Hours);
        }

        void Finish(bool lethal, bool inside, IRadiationShield shield)
        {
            Outcome = lethal ? SurvivalOutcome.Died : SurvivalOutcome.Survived;
            UpdateRate(inside, shield);
            TimeChanged?.Invoke(Hours);
            Ended?.Invoke(Outcome);
        }
    }
}
