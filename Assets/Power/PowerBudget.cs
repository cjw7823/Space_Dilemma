using System;
using System.Linq;
using SpaceDilemma.Research;
using SpaceDilemma.Scenario;

namespace SpaceDilemma.Power
{
    // Independent of scene/UI. Rules are copied so play never mutates the definition asset.
    public sealed class PowerBudget
    {
        readonly double[,] demands = new double[3,3];
        readonly int[][] deviceOrder = new int[3][];
        readonly int[] facilityOrder;
        readonly double[] requested = new double[3], supplied = new double[3];
        readonly bool[,] enabled = new bool[3,3], powered = new bool[3,3];
        public double Available { get; }
        public double EssentialRequired { get; }
        public double EssentialSupplied { get; private set; }
        public bool EssentialShortage => EssentialSupplied < EssentialRequired;
        public double ShieldMaximum { get; }
        public double ShieldRequestedOutput { get; private set; }
        public double ShieldWatts { get; private set; }
        public double ShieldActualOutput => ShieldMaximum > 0 ? ShieldWatts / ShieldMaximum : 0;
        public double Consumption { get; private set; }
        public double Remaining => Math.Max(0, Available - Consumption);

        public PowerBudget(PowerRules rules) : this(rules.availableWatts, rules.lifeSupportWatts,
            rules.shieldMaximumWatts, rules.Facility, null) { }

        public PowerBudget(ScenarioRules scenario) : this(scenario.power.availableWatts, scenario.power.lifeSupportWatts,
            scenario.power.shieldMaximumWatts, f => scenario.Mission((ResearchMission)f).ToPowerRule(), scenario) { }

        PowerBudget(double available, double essential, double shieldMaximum, Func<int, PowerFacilityRule> facilityRule,
            ScenarioRules scenario)
        {
            Available=Nonnegative(available);
            EssentialRequired=Nonnegative(essential);
            ShieldMaximum=Nonnegative(shieldMaximum);
            ShieldRequestedOutput=Math.Min(1, Nonnegative(scenario?.power.initialShieldOutput ?? 0));
            var facilities = Enumerable.Range(0, 3).Select(facilityRule).ToArray();
            facilityOrder=Enumerable.Range(0,3).OrderBy(f=>facilities[f].priority).ThenBy(f=>f).ToArray();
            for(int f=0;f<3;f++)
            {
                var devices=facilities[f].devices;
                if(devices==null || devices.Length!=3 || devices.Any(d=>d==null))
                    throw new ArgumentException("Each power facility must define exactly three devices.");
                deviceOrder[f]=Enumerable.Range(0,3).OrderBy(d=>devices[d].priority).ThenBy(d=>d).ToArray();
                var mission = scenario?.Mission((ResearchMission)f);
                for (int d = 0; d < 3; d++)
                {
                    demands[f,d] = Nonnegative(devices[d].watts);
                    enabled[f,d] = mission?.Device(d).initiallyRequested ?? true;
                }
                double initialWatts = mission?.initialRequestedWatts ?? -1;
                requested[f] = initialWatts < 0 ? Maximum(f) : Math.Min(Maximum(f), Nonnegative(initialWatts));
            }
            Recalculate();
        }

        static double Nonnegative(double value) => double.IsNaN(value)||double.IsInfinity(value)?0:Math.Max(0,value);
        public double Maximum(int facility) => Enumerable.Range(0,3).Sum(d=>demands[facility,d]);
        public double Demand(int facility,int device) => demands[facility,device];
        public double Requested(int facility) => requested[facility];
        public double Supplied(int facility) => supplied[facility];
        public bool RequestedOn(int facility,int device) => enabled[facility,device];
        public bool Powered(int facility,int device) => powered[facility,device];
        public int Running(int facility) => Enumerable.Range(0,3).Count(d=>powered[facility,d]);
        public void SetAllocation(int facility,double watts) { requested[facility]=Math.Min(Maximum(facility),Nonnegative(watts)); Recalculate(); }
        public void SetDevice(int facility,int device,bool on) { enabled[facility,device]=on; Recalculate(); }
        public void SetShield(double output) { ShieldRequestedOutput=Math.Min(1,Nonnegative(output)); Recalculate(); }
        public void Recalculate()
        {
            Array.Clear(powered,0,powered.Length);
            Array.Clear(supplied,0,supplied.Length);
            EssentialSupplied=Math.Min(Available,EssentialRequired);
            double remaining=Available-EssentialSupplied;
            ShieldWatts=Math.Min(remaining,ShieldMaximum*ShieldRequestedOutput);
            remaining-=ShieldWatts;
            foreach(int f in facilityOrder)
            {
                double budget=Math.Min(requested[f],remaining);
                foreach(int d in deviceOrder[f])
                {
                    if(!enabled[f,d]) continue;
                    // Strict priority: don't supply a lower-priority device ahead of a blocked one.
                    if(demands[f,d]>budget) break;
                    powered[f,d]=true;
                    budget-=demands[f,d]; supplied[f]+=demands[f,d];
                }
                remaining-=supplied[f];
            }
            Consumption=EssentialSupplied+ShieldWatts+supplied.Sum();
        }
    }
}
