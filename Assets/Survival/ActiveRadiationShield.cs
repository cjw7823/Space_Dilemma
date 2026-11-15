using SpaceDilemma.Power;
using UnityEngine;
namespace SpaceDilemma.Survival
{
    public sealed class ActiveRadiationShield : MonoBehaviour,IRadiationShield
    {
        [HideInInspector] public ShieldRules rules;
        public BasePowerController power;
        ShieldEfficiencyModel model;
        public double SuppliedOutput => isActiveAndEnabled && power && power.isActiveAndEnabled && power.Budget!=null?power.Budget.ShieldActualOutput:0;
        public double Reduction => model?.Reduction(SuppliedOutput) ?? 0;
        void Awake()
        {
            var config = power ? power.Scenario : null;
            model = new ShieldEfficiencyModel(config ? config.radiation.maximumReduction : rules ? rules.maximumReduction : 0);
        }
        public double Transmission(bool inside,bool speActive) => model?.Transmission(inside,speActive,SuppliedOutput) ?? 1;
    }
}
