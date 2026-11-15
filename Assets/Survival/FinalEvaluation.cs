using SpaceDilemma.Hud;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpaceDilemma.Results
{
    [DefaultExecutionOrder(200)]
    public sealed class FinalEvaluation : MonoBehaviour
    {
        public SurvivalSession session;
        public ResearchProgress research;
        [HideInInspector] public EvaluationRules rules;
        public HudPauseManager pause;
        public HudGameSettings language;
        public GameObject popup;
        public TMP_Text title,survivalText,shieldText,researchTitle,overallText,samplesText,note,settingsLabel,quitLabel;
        public TMP_Text[] missionLabels;
        public Image[] missionFills;
        public Button settingsButton,quitButton;
        public FinalReport Report { get; private set; }
        public int CaptureCount { get; private set; }
        public ResearchWeights Weights { get; private set; }
        bool wasVisible;
        public double CurrentResearchPercent => Report?.OverallPercent ??
            (research && research.Ledger != null ? Weights.OverallPercent(research.Ledger) : 0);

        void Awake()
        {
            var config = session.Scenario;
            Weights = config ? config.GetResearchWeights() : rules.GetResearchWeights();
        }
        void Start() => session.Run.Ended+=OnEnded;
        void OnEnded(SurvivalOutcome outcome)
        {
            if(Report!=null) return;
            research.Ledger.Finish();
            Report=new FinalReport(session.Run,research.Ledger,Weights);
            CaptureCount++;
        }
        void LateUpdate()
        {
            if(!popup) return;
            bool visible=Report!=null && (!pause || !pause.IsPaused);
            popup.SetActive(visible);
            if(!visible) { wasVisible=false; return; }
            if(!wasVisible && EventSystem.current) EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
            wasVisible=true;
            bool en=!language || language.English;
            var r=Report;
            bool survived=r.Outcome==SurvivalOutcome.Survived;
            title.text=survived?(en?"WELCOME BACK TO EARTH! CONGRATULATIONS!":"지구 무사 컴백! 축하합니다"):
                (en?"GAME OVER":"게임 오버");
            survivalText.text=(en?"Elapsed ":"경과 ")+HudController.FormatElapsedTime(r.Hours)+"\n"+
                (en?$"Final exposure {r.Dose:0.00} mSv":$"최종 누적 피폭량 {r.Dose:0.00} mSv");
            shieldText.text=en?$"SPE protection inside: {r.ShieldedSpeHours:0.00} / {r.IndoorSpeHours:0.00} game h\nMean indoor reduction: {r.MeanIndoorReduction:P0}\nExposure avoided by active shield: {r.AvoidedSpeDose:0.00} mSv":
                $"실내 SPE 보호 시간 {r.ShieldedSpeHours:0.00} / {r.IndoorSpeHours:0.00} 게임 h\n실내 평균 피폭 감소율 {r.MeanIndoorReduction:P0}\n능동차폐로 줄인 피폭량 {r.AvoidedSpeDose:0.00} mSv";
            researchTitle.text=en?"RESEARCH ACHIEVEMENT":"연구 성과";
            string[] names=en?new[]{"Sample preservation","Instruments","Plant research"}:new[]{"시료 채취·보존","측정장비","식물 재배"};
            for(int i=0;i<3;i++)
            {
                double value=r.MissionPercent((ResearchMission)i);
                missionLabels[i].text=$"{names[i]}   {value:0.0}%";
                missionFills[i].fillAmount=(float)(value/100);
            }
            overallText.text=en?$"Mission completion: {r.OverallPercent:0.0}%":$"임무 완수율: {r.OverallPercent:0.0}%";
            samplesText.text=en?$"Usable stored samples: {r.UsableSamples} / {r.StoredSamples}":$"보관 시료 중 유효 품질: {r.UsableSamples} / {r.StoredSamples}";
            note.text=survived?(en?"Thanks to you, we gained valuable research data and resources.\nThank you so much!":
                "당신 덕분에 소중한 연구 자료와 자원을 얻게 되었어요.\n정말 감사합니다!"):
                (en?"Radiation exposure reached the limit.\nGame over due to a safety rule violation.":
                "방사선 피폭량이 제한치에 도달했습니다.\n안전 수칙 위반으로 게임 오버되었습니다.");
            settingsLabel.text=en?"SETTINGS":"설정";
            quitLabel.text=en?"QUIT GAME":"게임 종료";
        }
        public void ShowSettings() { if(Report==null || !pause) return; pause.Pause(); pause.ShowSettings(); }
        public void QuitGame()
        {
            if(Report==null) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        void OnDestroy() { if(session && session.Run!=null) session.Run.Ended-=OnEnded; }
    }
}
