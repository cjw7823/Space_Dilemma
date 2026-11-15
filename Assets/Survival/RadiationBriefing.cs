using System;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Scenario;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceDilemma.Survival
{
    // Open after the HUD and the survival session have initialized, before the first Update.
    [DefaultExecutionOrder(150)]
    public sealed class RadiationBriefing : MonoBehaviour
    {
        [SerializeField] SurvivalSession session;
        [SerializeField] HudPauseManager pause;
        [SerializeField] HudGameSettings language;
        [SerializeField] InputContextProfile context;
        [SerializeField] GameObject overlay;
        [SerializeField] TMP_Text title, intro, observationsHeading, observations, calculationHeading, calculation;
        [SerializeField] TMP_Text protectionHeading, protection, sources, radLabLabel, donkiLabel, continueLabel;
        [SerializeField] Button continueButton, radLabButton, donkiButton;

        const string RadLabUrl = "https://visualization.osdr.nasa.gov/radlab/gui/knowledgebase/";
        const string DonkiUrl = "https://ccmc.gsfc.nasa.gov/tools/DONKI/";

        InputHub hub;
        int contextToken;
        float previousTimeScale;
        bool previousPauseBlock, previousCursor, selectContinue, pendingClose;
        CursorLockMode previousLock;
        GameObject previousSelection;

        public bool IsOpen => contextToken != 0;

        void Start()
        {
            if (!pause || !pause.IsMainMenuOpen) Open();
        }

        public void Open()
        {
            hub = InputHub.Active;
            if (IsOpen || !hub || !context || !overlay || !session || !session.Scenario ||
                session.Run.Outcome != SurvivalOutcome.Running ||
                (pause && (pause.IsPaused || pause.IsMainMenuOpen))) return;

            previousTimeScale = Time.timeScale;
            previousCursor = Cursor.visible;
            previousLock = Cursor.lockState;
            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (pause)
            {
                previousPauseBlock = pause.BlockExternalPause;
                pause.BlockExternalPause = true;
            }

            contextToken = hub.PushContext(context);
            hub.ActionTriggered += OnAction;
            if (language) language.LanguageChanged += Refresh;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
            overlay.SetActive(true);
            selectContinue = true;
        }

        public void Refresh()
        {
            var scenario = session.Scenario;
            var time = scenario.time;
            var radiation = scenario.radiation;
            bool english = !language || language.English;
            double speed = Math.Max(0.0001f, time.gameHoursPerSecond);
            double normal = radiation.NormalRate;
            double spe = radiation.SpeRate;
            double indoor = Math.Clamp(radiation.indoorTransmission, 0, 1);
            double maximumReduction = Math.Clamp(radiation.maximumReduction, 0, 1);
            double limit = session.Run?.DoseLimit ?? radiation.lethalDose;
            bool hasSpe = Math.Max(0, time.speStartHour) < time.durationHours;
            // Use whole seconds in the briefing rather than exposing float clock rounding.
            var eventTime = TimeSpan.FromSeconds(Math.Round(Math.Max(0, time.speStartHour) / speed));
            string timestamp = $"{(long)eventTime.TotalMinutes}:{eventTime.Seconds:00}";
            double ratio = normal > 0 ? spe / normal : 0;
            string comparison = ratio > 1 ? (english ? $" ({ratio:0.00}× as fast)" : $" (약 {ratio:0.00}배)") : "";

            title.text = english ? "RADIATION BRIEFING" : "임무 브리핑 · 방사선 피폭";
            intro.text = english ? "Your exposure meter adds up the radiation you receive as you explore." :
                "달을 탐사하는 동안 받은 방사선량이 피폭 게이지에 쌓입니다.";
            observationsHeading.text = english ? "What was measured?" : "무엇을 관측했나요?";
            calculationHeading.text = english ? "How does the meter rise?" : "피폭량은 어떻게 올라가나요?";
            protectionHeading.text = english ? "How do you stay safe?" : "어떻게 피폭을 줄이나요?";

            radLabButton.gameObject.SetActive(radiation.useRadLabAverages);
            donkiButton.gameObject.SetActive(radiation.useRadLabAverages);
            if (radiation.useRadLabAverages)
            {
                observations.text = english
                    ? "CRaTER aboard NASA’s lunar orbiter measured how much energy radiation deposited in its detector. " +
                      "A solar energetic particle event (SPE) brings high-energy particles from the Sun.\n\n" +
                      $"Mean absorbed dose rate for each 48-hour window\nBefore  {radiation.preAverageUGyPerHour:0.0000} μGy/h\n" +
                      $"After      {radiation.postAverageUGyPerHour:0.0000} μGy/h"
                    : "NASA의 달 궤도선에 실린 CRaTER 검출기는 방사선에서 얼마나 많은 에너지를 흡수했는지 잽니다. " +
                      "태양 고에너지 입자 사건(SPE) 때는 태양에서 에너지가 큰 입자가 날아옵니다.\n\n" +
                      $"사건 기준 전후 48시간의 평균 흡수선량률\n사건 전  {radiation.preAverageUGyPerHour:0.0000} μGy/h\n" +
                      $"사건 후  {radiation.postAverageUGyPerHour:0.0000} μGy/h";
                sources.text = english ? "NASA RadLab · LRO / CRaTER-D1D2   |   NASA DONKI · SPE record, 27 Aug 2022, 11:12 UTC" :
                    "NASA RadLab · 달 궤도선 LRO / CRaTER-D1D2   |   NASA DONKI · SPE 기록, 2022-08-27 11:12 UTC";
            }
            else
            {
                observations.text = english ? "This scenario specifies its own outdoor dose rates for normal conditions and the solar particle event." :
                    "이 시나리오는 평상시와 태양 입자 사건의 실외 선량률을 게임 설정에서 직접 지정합니다.";
                sources.text = english ? "Source · Scenario Rules" : "출처 · 시나리오 설정";
            }

            string conversion = radiation.useRadLabAverages ? (english
                ? $"Outdoor rate = observed mean × k ÷ 1,000 (k = {radiation.gameGain:0.##})\n"
                : $"실외 선량률 = 관측 평균 × k ÷ 1,000 (k = {radiation.gameGain:0.##})\n") : "";
            string eventCopy = hasSpe ? (english
                ? $"At {timestamp} of play, the meter starts rising at the SPE rate{comparison}."
                : $"플레이 {timestamp}에 SPE가 시작되면 피폭량이 사건 중 속도로 쌓입니다{comparison}.")
                : (english ? "This mission stays at the normal rate." : "이 임무에서는 평상시 속도로 쌓입니다.");
            calculation.text = english
                ? $"{conversion}{normal:0.0000} → {spe:0.0000} game mSv per game hour\n" +
                  $"1 play second = {speed:0.####} game hours\n" +
                  $"+{normal * speed:0.0000} → +{spe * speed:0.0000} game mSv per outdoor second\n\n{eventCopy}\n" +
                  "Game mSv is a simulation unit. The scale is set for gameplay."
                : $"{conversion}{normal:0.0000} → {spe:0.0000} game mSv/게임시간\n" +
                  $"플레이 1초 = 게임시간 {speed:0.####}시간\n" +
                  $"실외에서 1초마다 +{normal * speed:0.0000} → +{spe * speed:0.0000} game mSv\n\n{eventCopy}\n" +
                  "game mSv는 게임용 단위입니다. 환산계수는 게임 난이도에 맞춰 정했습니다.";
            if (hasSpe && radiation.fitSpeToRemainingDose)
            {
                double margin = float.IsFinite(radiation.speRemainingDoseMultiplier)
                    ? Math.Max(1, radiation.speRemainingDoseMultiplier) : 1;
                bool endsEarly = time.speDurationHours > 0 && time.speStartHour + time.speDurationHours < time.durationHours;
                string outdoorCopy = endsEarly
                    ? (english ? "After SPE ends, the normal rate resumes." : "SPE가 끝나면 평상시 증가율로 돌아갑니다.")
                    : (english ? (margin > 1 ? "Stay outside → 100% before mission end." : "Stay outside until mission end → 100%.")
                        : (margin > 1 ? "계속 실외에 있으면 임무 종료 전 100%." : "계속 실외에 있으면 임무 종료 때 100%."));
                string baseConversion = radiation.useRadLabAverages
                    ? (english ? $" (observed mean × k ÷ 1,000; k = {radiation.gameGain:0.##})"
                        : $" (관측 평균 × k ÷ 1,000; k = {radiation.gameGain:0.##})") : "";
                calculation.text = english
                    ? $"Before SPE: +{normal * speed:0.0000} game mSv per play second{baseConversion}\n\n" +
                      $"At {timestamp}, the outdoor rate is fixed once:\nremaining dose limit ÷ remaining game time × {margin:0.##}.\n" +
                      $"Gameplay margin: +{margin - 1:0%}.\n{outdoorCopy}\nShelter reduces exposure; the rate stays fixed.\n" +
                      "Game mSv is a gameplay unit."
                    : $"SPE 전: 실외에서 1초마다 +{normal * speed:0.0000} game mSv{baseConversion}\n\n" +
                      $"플레이 {timestamp}에 한 번 계산합니다.\n실외 증가율 = 남은 피폭 한도 ÷ 남은 게임시간 × {margin:0.##}\n" +
                      $"게임 여유: +{margin - 1:0%}.\n{outdoorCopy}\n대피·차폐로 줄어도 증가율은 다시 올리지 않습니다.\n" +
                      "game mSv는 게임용 단위입니다.";
            }
            protection.text = english
                ? $"Inside the base, the dose builds at {indoor:0%} of the outdoor rate. During an SPE, powered shielding reduces that indoor rate by up to {maximumReduction:0%}. " +
                  $"At {limit:0.##} game mSv of total exposure, the mission ends."
                : $"기지 안에서는 바깥 선량률의 {indoor:0%}만 쌓입니다. SPE 중에는 전력을 공급한 차폐 장치가 실내 증가율을 최대 {maximumReduction:0%} 더 낮춥니다. " +
                  $"누적 피폭량이 {limit:0.##} game mSv에 도달하면 게임 오버입니다.";
            radLabLabel.text = "NASA RadLab";
            donkiLabel.text = "NASA DONKI";
            continueLabel.text = english ? "BEGIN MISSION" : "임무 시작";
        }

        void OnAction(ActionSignal signal)
        {
            if (signal.phase == InputActionPhase.Performed &&
                ((signal.map == "UI" && signal.action == "Cancel") ||
                 (signal.map == "Global" && signal.action == "Pause")))
                pendingClose = true;
        }

        void Update()
        {
            if (!IsOpen) return;
            if (pendingClose || session.Run.Outcome != SurvivalOutcome.Running)
            {
                Close();
                return;
            }
            if (selectContinue && EventSystem.current)
            {
                selectContinue = false;
                EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
            }
        }

        public void OpenRadLab() { if (IsOpen) Application.OpenURL(RadLabUrl); }
        public void OpenDonki() { if (IsOpen) Application.OpenURL(DonkiUrl); }

        public void Close()
        {
            if (!IsOpen) return;
            if (hub)
            {
                hub.ActionTriggered -= OnAction;
                hub.PopContext(contextToken);
            }
            contextToken = 0;
            selectContinue = pendingClose = false;
            if (language) language.LanguageChanged -= Refresh;
            if (overlay) overlay.SetActive(false);
            if (pause) pause.BlockExternalPause = previousPauseBlock;
            if (!session || session.Run.Outcome == SurvivalOutcome.Running)
            {
                Time.timeScale = previousTimeScale;
                Cursor.lockState = previousLock;
                Cursor.visible = previousCursor;
            }
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(
                    previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
        }

        void OnDisable() => Close();
    }
}
