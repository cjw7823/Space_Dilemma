using SpaceDilemma.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDilemma.Power
{
    public sealed class PowerFacilityRow : MonoBehaviour
    {
        public BasePowerController power;
        public HudGameSettings language;
        [Range(0,2)] public int facility;
        public TMP_Text heading;
        public Slider allocation;
        public Toggle[] switches;
        public TMP_Text[] deviceLabels;
        public Image[] indicators;
        public void ChangeAllocation(float value) => power.SetAllocation(facility,value);
        public void ToggleFirst(bool value) => power.SetDevice(facility,0,value);
        public void ToggleSecond(bool value) => power.SetDevice(facility,1,value);
        public void ToggleThird(bool value) => power.SetDevice(facility,2,value);
        void LateUpdate()
        {
            if(!power || power.Budget==null) return;
            var b=power.Budget;
            bool en=!language || language.English;
            string name=en?new[]{"Cold storage","Instruments","Plant plots"}[facility]:new[]{"시료 보관소","측정장비","식물 재배"}[facility];
            heading.text=$"{name}   {b.Supplied(facility):0.#} / {b.Requested(facility):0.#} W   ON {b.Running(facility)}/3";
            allocation.maxValue=(float)b.Maximum(facility);
            allocation.SetValueWithoutNotify((float)b.Requested(facility));
            for(int d=0;d<3;d++)
            {
                switches[d].SetIsOnWithoutNotify(b.RequestedOn(facility,d));
                switches[d].interactable=!b.Abandoned(facility,d);
                bool on=b.Powered(facility,d);
                string status=b.Abandoned(facility,d)?(en?"FORFEITED":"포기(영구)"):
                    on?"ON":!b.RequestedOn(facility,d)?"OFF":en?"NO POWER":"전력 부족";
                deviceLabels[d].text=$"{d+1}: {b.Demand(facility,d):0.#} W  {status}";
                indicators[d].color=on?new Color32(71,223,153,255):new Color32(227,100,83,255);
            }
        }
    }
}
