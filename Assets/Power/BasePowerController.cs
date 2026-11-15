using SpaceDilemma.Research;
using SpaceDilemma.Scenario;
using SpaceDilemma.Survival;
using UnityEngine;

namespace SpaceDilemma.Power
{
    [DefaultExecutionOrder(50)]
    public sealed class BasePowerController : MonoBehaviour
    {
        [SerializeField, HideInInspector] PowerRules rules;
        [SerializeField] SurvivalSession session;
        [SerializeField] ResearchDevice[] devices;
        public PowerBudget Budget { get; private set; }
        public ShieldPowerTradeoff ShieldTradeoff { get; } = new ShieldPowerTradeoff();
        public ResearchDevice[] Devices => devices;
        public ScenarioRules Scenario => session ? session.Scenario : null;
        bool DilemmaActive => session && session.Run!=null && session.Run.Outcome==SurvivalOutcome.Running && session.Run.SpeActive;
        void Awake()
        {
            var config = Scenario;
            Budget = config ? new PowerBudget(config) : new PowerBudget(rules);
            Apply();
        }
        void Update() => Apply();
        public void Apply()
        {
            if(Budget==null) return;
            ShieldTradeoff.Observe(Budget,DilemmaActive);
            Budget.LockShieldSacrifices(ShieldTradeoff);
            foreach(var device in devices)
                if(device) device.SetPowered(Budget.Powered((int)device.mission,device.index));
        }
        public void SetAllocation(int facility,float watts) { Budget.SetAllocation(facility,watts,DilemmaActive); Apply(); }
        public void SetDevice(int facility,int device,bool on) { Budget.SetDevice(facility,device,on,DilemmaActive); Apply(); }
        public void SetShield(float output) { Budget.SetShield(output); Apply(); }
    }
}
