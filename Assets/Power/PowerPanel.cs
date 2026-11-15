using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Survival;
using SpaceDilemma.Research;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpaceDilemma.Power
{
    [DefaultExecutionOrder(105)]
    public sealed class PowerPanel : MonoBehaviour
    {
        public BasePowerController power;
        public ActiveRadiationShield shielding;
        public ResearchProgress research;
        public MoonBasePlayer player;
        public SurvivalSession session;
        public HudGameSettings language;
        public InputContextProfile context;
        public Transform console;
        [Min(.1f), HideInInspector] public float interactionRange=2;
        public GameObject popup;
        public TMP_Text prompt,title,totals,shieldLabel,note,closeLabel;
        public Slider shield;
        public Button closeButton;
        InputHub hub;
        int token;
        GameObject previousSelection;
        bool wasVisible;
        public bool IsOpen => token!=0;
        public bool Visible => popup && popup.activeSelf;

        public bool CanReach()
        {
            if (!console || !player || player.IsDriving || player.IsObservingScenery) return false;
            var config = session.Scenario;
            bool inside = config ? session.ContainsPlayer() : session.Contains(player.transform.position + Vector3.up);
            if (!inside) return false;
            var from=player.transform.position+Vector3.up*.8f;
            int mask=Physics.DefaultRaycastLayers & ~(1<<player.gameObject.layer);
            float distance=(from-console.position).sqrMagnitude;
            float reach = Mathf.Max(.1f, config ? config.interaction.consoleRange : interactionRange);
            if(distance>reach*reach || Physics.Linecast(from,console.position,mask,QueryTriggerInteraction.Ignore)) return false;
            // Use the same target selection as E charging, including markers inside models.
            var interaction = research ? research.Interaction : null;
            var target = interaction ? interaction.FindNearby() : null;
            if (interaction && interaction.CanUse(target) && (target.InteractionPoint(from) - from).sqrMagnitude < distance) return false;
            return true;
        }
        void Update()
        {
            if(!popup || !prompt) { Close(); enabled=false; return; }
            if(!hub) hub=InputHub.Active;
            if(!hub || session.Run==null) return;
            if(session.Run.Outcome!=SurvivalOutcome.Running) Close();
            bool usable=hub.GameplayAllowed && player.ControlCaptured && session.Run.Outcome==SurvivalOutcome.Running && CanReach();
            bool en=!language || language.English;
            prompt.text=usable?(en?"E: POWER CONTROL":"E: 전력 제어 패널"):"";
            if(!IsOpen && usable && hub.WasPressed("ResearchInteract")) Open();
            bool visible=IsOpen && hub.CurrentContext==context;
            if(visible && hub.WasPressed("Cancel")) { Close(); visible=false; }
            popup.SetActive(visible);
            if(visible && !wasVisible && EventSystem.current) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            wasVisible=visible;
        }
        public void Open()
        {
            if(!hub) hub=InputHub.Active;
            if(IsOpen || !hub || !hub.GameplayAllowed || !CanReach() || session.Run.Outcome!=SurvivalOutcome.Running) return;
            previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            token=hub.PushContext(context);
        }
        public void Close()
        {
            if(hub && token!=0) hub.PopContext(token);
            token=0;
            if(popup) popup.SetActive(false);
            if(EventSystem.current && previousSelection && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            previousSelection=null;
        }
        public void ChangeShield(float value) => power.SetShield(value);
        void LateUpdate()
        {
            if(!power || power.Budget==null || !title) return;
            var b=power.Budget;
            bool en=!language || language.English;
            title.text=en?"BASE POWER CONTROL":"기지 전력 제어";
            totals.text=en?$"Available {b.Available:0.#} W   Used {b.Consumption:0.#} W   Free {b.Remaining:0.#} W\nLife support (protected) {b.EssentialSupplied:0.#}/{b.EssentialRequired:0.#} W":
                $"총 전력 {b.Available:0.#} W   소비 {b.Consumption:0.#} W   잔여 {b.Remaining:0.#} W\n생명유지 (필수 보호) {b.EssentialSupplied:0.#}/{b.EssentialRequired:0.#} W";
            if(b.EssentialShortage) totals.text+=en?"\nWARNING: Essential supply shortage":"\n경고: 필수 생명유지 전력 부족";
            shieldLabel.text=en?$"Shield  requested {b.ShieldRequestedOutput:P0}  actual {b.ShieldActualOutput:P0}  ({b.ShieldWatts:0.#} W)":
                $"능동차폐  요청 {b.ShieldRequestedOutput:P0}  실제 {b.ShieldActualOutput:P0}  ({b.ShieldWatts:0.#} W)";
            shield.SetValueWithoutNotify((float)b.ShieldRequestedOutput);
            string reduction=shielding?$"{shielding.Reduction:P0}":"0%";
            note.text=(en?"Reference budgets: one cooler / one plant habitat. Three channels share each system.\n":
                "기준 전력은 냉각기·재배시설 각 1세트를 세 채널에 나눈 값입니다.\n")+
                (en?"During SPE, interrupted experiments are permanently forfeited. Earned data stays.\n":
                "SPE 중 중단한 실험은 영구 포기됩니다. 기존 연구 데이터는 보존됩니다.\n")+
                SpeHud.SampleLine(research,en)+"\n"+
                (en?$"Indoor SPE reduction {reduction}. Life support > shield > research. Time continues.":
                    $"실내 SPE 감소율 {reduction}. 생명유지 → 차폐 → 연구 순으로 공급. 시간은 계속 흐릅니다.");
            closeLabel.text=en?"CLOSE (Backspace)":"닫기 (Backspace)";
        }
        void OnDisable() { Close(); if(prompt) prompt.text=""; }
    }
}
