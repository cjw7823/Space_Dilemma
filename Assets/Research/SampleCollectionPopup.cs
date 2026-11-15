using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Survival;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceDilemma.Research
{
    // Visuals and information assets are authored in the scene, never created in Play.
    public sealed class SampleCollectionPopup : MonoBehaviour
    {
        public ResearchInteraction interaction;
        public SurvivalSession session;
        public HudPauseManager pause;
        public HudGameSettings language;
        public InputContextProfile context;
        public LunarSampleInformation[] samples;
        public GameObject popup;
        public TMP_Text title, details, description, source, status, closeLabel, sourceLabel;
        public Button closeButton, sourceButton;
        [Header("First plant mission introduction (once per session)")]
        public PlantExperimentInformation plantInformation;
        public GameObject plantPopup;
        public TMP_Text plantTitle, plantResearchTitle, plantResearch, plantPowerTitle, plantPower, plantAdaptation, plantSources, plantStatus, plantCloseLabel;
        public TMP_Text plantPowerSourceLabel, plantDurationSourceLabel;
        public Button plantCloseButton;
        public bool IsOpen { get; private set; }
        public LunarSampleInformation Current { get; private set; }
        public bool IsPlantIntroduction { get; private set; }
        public bool PlantIntroductionShown { get; private set; }
        GameObject ActiveWindow => IsPlantIntroduction ? plantPopup : popup;
        Button ActiveCloseButton => IsPlantIntroduction ? plantCloseButton : closeButton;
        InputHub hub;
        int token;
        float previousScale;
        bool pendingClose, selectClose;
        GameObject previousSelection;
        CursorLockMode previousLock;
        bool previousCursor;

        void OnEnable()
        {
            interaction.Completed += Collected;
            if (language) language.LanguageChanged += Refresh;
        }
        void Start()
        {
            hub = InputHub.Active;
            if (hub) hub.ActionTriggered += OnAction;
            // A scenario resumed after the mission has begun must not show the introduction later.
            PlantIntroductionShown = interaction.Count(ResearchKind.Plant) > 0;
        }
        void Collected(ResearchTarget target)
        {
            if (target.kind == ResearchKind.Plant)
            {
                if (!PlantIntroductionShown) OpenPlantIntroduction();
                return;
            }
            if (target.kind != ResearchKind.Sample || target.index < 0 || target.index >= samples.Length) return;
            Open(samples[target.index]);
        }
        public void Open(LunarSampleInformation information)
        {
            if (IsOpen || !information || !popup) return;
            Current = information; IsPlantIntroduction = false;
            Begin();
        }
        public void OpenPlantIntroduction()
        {
            if (IsOpen || PlantIntroductionShown || !plantInformation || !plantPopup) return;
            Current = null; IsPlantIntroduction = true;
            if (Begin()) PlantIntroductionShown = true;
        }
        bool Begin()
        {
            if (!context || !session ||
                session.Run.Outcome != SurvivalOutcome.Running || (pause && pause.IsPaused)) return false;
            if (!hub) hub = InputHub.Active;
            if (!hub) return false;
            previousScale = Time.timeScale;
            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            previousLock = Cursor.lockState; previousCursor = Cursor.visible;
            token = hub.PushContext(context);
            IsOpen = true;
            if (pause) pause.BlockExternalPause = true;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Refresh(); ActiveWindow.SetActive(true); selectClose = true;
            return true;
        }
        void OnAction(ActionSignal signal)
        {
            if (IsOpen && signal.phase == InputActionPhase.Performed &&
                ((signal.map == "Global" && signal.action == "Pause") ||
                 (signal.map == "UI" && signal.action == "Cancel"))) pendingClose = true;
        }
        void Update()
        {
            if (pendingClose) { pendingClose = false; Close(); }
            else if (IsOpen && selectClose)
            {
                selectClose = false;
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(ActiveCloseButton.gameObject);
            }
            if (IsOpen && session.Run.Outcome != SurvivalOutcome.Running) Close();
        }
        void Refresh()
        {
            bool en = !language || language.English;
            if (IsPlantIntroduction) { RefreshPlant(en); return; }
            if (!Current) return;
            title.text = en ? $"SAMPLE COLLECTED — APOLLO {Current.apolloMission}" : $"시료 채취 완료 — 아폴로 {Current.apolloMission}호";
            details.text = en ? $"Lunar sample {Current.sampleId}\n{Current.classificationEnglish}\nCollected: {Current.locationEnglish}" :
                $"달 시료 {Current.sampleId}\n{Current.classificationKorean}\n채취 장소: {Current.locationKorean}";
            description.text = en ? Current.descriptionEnglish : Current.descriptionKorean;
            source.text = (en ? "Source: " : "출처: ") + "NASA / ARES — Astromaterials 3D\n" + Current.sourceUrl;
            status.text = en ? "GAME PAUSED · Close this window to continue.\nHistorical Apollo sample; its placement here is a game scenario." :
                "게임 일시정지 · 창을 닫으면 시간이 다시 흐릅니다.\n실제 아폴로 시료이며, 이곳의 배치는 게임 설정입니다.";
            closeLabel.text = en ? "CONTINUE (Esc)" : "계속하기 (Esc)";
            sourceLabel.text = en ? "NASA SOURCE" : "NASA 원문 보기";
        }
        public void RefreshPlant(bool en)
        {
            var info = plantInformation;
            if (!info) return;
            plantTitle.text = en ? "LUNAR ROOT GROWTH EXPERIMENT" : "달의 뿌리 성장·방향 실험";
            plantResearchTitle.text = en ? "WHAT ARE WE INVESTIGATING?" : "무엇을 알아보는 연구일까요?";
            plantResearch.text = en ? info.objectiveEnglish : info.objectiveKorean;
            plantPowerTitle.text = en ? "POWER & OBSERVATION TIME" : "전력과 관찰 시간";
            int hours = info.observationMinutes / 60, minutes = info.observationMinutes % 60;
            plantPower.text = en ?
                $"Reference power   {info.referencePowerWatts:0.##} W\nObservation   {hours} h {minutes:00} min\nEstimated energy   {info.ReferenceEnergyKwh:0.00} kWh\n\n{info.referencePowerWatts / 1000d:0.###} kW × {info.ObservationHours:0.####} h\nOne habitat set, constant power.\nW = power used now.\nkWh = energy used over time." :
                $"기준 소비전력   {info.referencePowerWatts:0.##} W\n관찰 시간   {hours}시간 {minutes:00}분\n예상 전력량   {info.ReferenceEnergyKwh:0.00} kWh\n\n{info.referencePowerWatts / 1000d:0.###} kW × {info.ObservationHours:0.####}시간\n장치 1세트가 일정 전력을 쓸 때의 값입니다.\nW = 지금 사용하는 전력\nkWh = 시간 동안 쌓인 전력 사용량";
            plantAdaptation.text = en ? info.adaptationEnglish : info.adaptationKorean;
            plantSources.text = en ?
                "[1] POWER · B. Granath, NASA (19 Feb 2013)\nKennedy Engineers Designing Plant Habitat For Space Station\n[2] TIME · NASA/TP–2009–213146–REV A, p.94 · GRAVI-1\nFirst run: 15 h + 13 h 40 min = 28 h 40 min. Only timing adapted." :
                "[1] 전력 · B. Granath, NASA (2013.02.19)\nKennedy Engineers Designing Plant Habitat For Space Station\n[2] 시간 · NASA/TP–2009–213146–REV A, p.94 · GRAVI-1\n첫 실험 15시간 + 13시간 40분에서 관찰 시간만 차용했습니다.";
            plantStatus.text = en ? "GAME PAUSED · Close to continue." : "게임 일시정지 · 창을 닫으면 시간이 다시 흐릅니다.";
            plantCloseLabel.text = en ? "CONTINUE (Esc)" : "계속하기 (Esc)";
            plantPowerSourceLabel.text = en ? "NASA POWER SOURCE [1]" : "NASA 전력 출처 [1]";
            plantDurationSourceLabel.text = en ? "NASA TIME SOURCE [2]" : "NASA 시간 출처 [2]";
        }
        public void OpenPlantPowerSource() { if (IsOpen && IsPlantIntroduction) Application.OpenURL(plantInformation.powerSourceUrl); }
        public void OpenPlantDurationSource() { if (IsOpen && IsPlantIntroduction) Application.OpenURL(plantInformation.durationSourceUrl); }
        public void OpenSource() { if (IsOpen && Current) Application.OpenURL(Current.sourceUrl); }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; pendingClose = selectClose = false;
            if (ActiveWindow) ActiveWindow.SetActive(false);
            if (pause) pause.BlockExternalPause = false;
            if (hub && token != 0) hub.PopContext(token);
            token = 0;
            bool running = session && session.Run != null && session.Run.Outcome == SurvivalOutcome.Running;
            Time.timeScale = running ? previousScale : 0;
            Cursor.lockState = running ? previousLock : CursorLockMode.None;
            Cursor.visible = running ? previousCursor : true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(
                previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
            previousSelection = null;
        }
        void OnDisable()
        {
            if (interaction) interaction.Completed -= Collected;
            if (language) language.LanguageChanged -= Refresh;
            Close();
        }
        void OnDestroy() { if (hub) hub.ActionTriggered -= OnAction; }
    }
}
