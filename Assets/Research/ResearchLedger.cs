using System;
using SpaceDilemma.Scenario;

namespace SpaceDilemma.Research
{
    public sealed class ResearchDataState
    {
        public bool Activated { get; internal set; }
        public bool Powered { get; internal set; } = true;
        public bool Valid { get; internal set; } = true;
        public SamplePreservationState Preservation { get; internal set; }
        public bool Running => Activated && Powered && Valid && (Preservation?.Usable ?? true);
        public double Data { get; internal set; }
        public double ValidGameHours { get; internal set; }
        public bool AbandonedForShield { get; internal set; }
    }

    // Uses absolute session time so repeated callbacks cannot duplicate accumulated data.
    public sealed class ResearchLedger
    {
        readonly ResearchDataState[,] states = new ResearchDataState[3, 3];
        readonly double[,] rates = new double[3, 3], capacities = new double[3, 3], weights = new double[3, 3];
        readonly double[] targets = new double[3], maximumPercent = new double[3];
        double lastHour;
        public bool IsFinished { get; private set; }
        public event Action Changed;

        public ResearchLedger(ResearchDataRules rules, double initialHour, SamplePreservationRules preservationRules = null)
            : this(rules.For, initialHour, preservationRules ? () => new SamplePreservationState(preservationRules) : null) { }

        public ResearchLedger(ScenarioRules rules, double initialHour)
            : this(mission => rules.Mission(mission).ToResearchRule(), initialHour,
                () => new SamplePreservationState(rules.preservation.outageGraceHours,
                    rules.preservation.qualityLossPerHour, rules.preservation.minimumUsableQuality)) { }

        ResearchLedger(Func<ResearchMission, ResearchMissionRule> missionRule, double initialHour,
            Func<SamplePreservationState> createPreservation)
        {
            lastHour = initialHour;
            for (int m = 0; m < 3; m++)
            {
                var mission = missionRule((ResearchMission)m);
                if (mission == null || mission.devices == null || mission.devices.Length != 3)
                    throw new ArgumentException("Each mission needs exactly three research device definitions.");
                targets[m] = Math.Max(.001, mission.targetData);
                maximumPercent[m] = Math.Clamp(mission.maximumPercent, 0, 100);
                for (int i = 0; i < 3; i++)
                {
                    var device = mission.devices[i] ?? throw new ArgumentException("Research device definition is missing.");
                    states[m, i] = new ResearchDataState();
                    if (m == 0) states[m, i].Preservation = createPreservation?.Invoke();
                    rates[m, i] = Math.Max(0, device.unitsPerGameHour);
                    capacities[m, i] = Math.Max(0, device.maximumData);
                    weights[m, i] = Math.Max(0, device.contribution);
                }
            }
        }

        public ResearchDataState Get(ResearchMission mission, int index) => states[(int)mission, index];
        public void SetShieldAbandoned(ResearchMission mission,int index,bool value)
        {
            if(IsFinished || !value) return;
            var state=Get(mission,index);
            state.AbandonedForShield=true;
            state.Powered=false;
        }
        public bool IsComplete(ResearchMission mission) => Percent(mission)>=maximumPercent[(int)mission]-1e-6;
        public bool LostResearchToShield(ResearchMission mission)
        {
            if(IsComplete(mission)) return false;
            for(int i=0;i<3;i++)
            {
                var state=Get(mission,i);
                if(weights[(int)mission,i]>0 && state.AbandonedForShield && state.Data<capacities[(int)mission,i]-1e-9)
                    return true;
            }
            return false;
        }

        internal void SetInitialData(ResearchMission mission, int index, double data, double sampleQuality)
        {
            var state = Get(mission, index);
            if (!state.Activated) return;
            state.Data = double.IsFinite(data) ? Math.Clamp(data, 0, capacities[(int)mission, index]) : 0;
            state.Preservation?.SetInitialQuality(sampleQuality);
        }

        public void SetState(ResearchMission mission, int index, bool activated, bool powered, bool valid)
        {
            if (IsFinished) return;
            var state = Get(mission, index);
            powered &= !state.AbandonedForShield;
            if (state.Activated == activated && state.Powered == powered && state.Valid == valid) return;
            state.Activated = activated;
            state.Powered = powered;
            state.Valid = valid;
            Changed?.Invoke();
        }

        public void AdvanceTo(double hour)
        {
            if (IsFinished || !double.IsFinite(hour) || hour <= lastHour) return;
            double delta = hour - lastHour;
            lastHour = hour;
            for (int m = 0; m < 3; m++)
            for (int i = 0; i < 3; i++)
            {
                var state = states[m, i];
                if (state.Activated) state.Preservation?.Advance(delta, state.Powered);
                if (!state.Running) continue;
                state.ValidGameHours += delta;
                state.Data = Math.Min(capacities[m, i], state.Data + rates[m, i] * delta);
            }
            Changed?.Invoke();
        }

        public double Data(ResearchMission mission)
        {
            double sum = 0;
            for (int i = 0; i < 3; i++) sum += Get(mission, i).Data * weights[(int)mission, i];
            return sum;
        }

        public double Percent(ResearchMission mission) =>
            Math.Min(maximumPercent[(int)mission], Data(mission) / targets[(int)mission] * maximumPercent[(int)mission]);

        public int RunningDevices(ResearchMission mission)
        {
            int count = 0;
            for (int i = 0; i < 3; i++) if (Get(mission, i).Running) count++;
            return count;
        }

        public void Finish() => IsFinished = true;
    }
}
