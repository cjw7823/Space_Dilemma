using SpaceDilemma.Hud;
using SpaceDilemma.Power;
using SpaceDilemma.Research;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDilemma.Survival
{
    public sealed class SpeHud : MonoBehaviour
    {
        public SurvivalSession session;
        public ActiveRadiationShield shielding;
        public BasePowerController power;
        public ResearchProgress research;
        public MoonBasePlayer player;
        public Transform console;
        public HudGameSettings language;
        public GameObject panel;
        public Image background;
        public TMP_Text title,details;
        public int AlertCount { get; private set; }
        void Start()
        {
            AlertCount=session.Run.SpeActive?1:0;
            session.Run.SpeStarted+=OnSpe;
        }
        void OnSpe() => AlertCount++;
        void LateUpdate()
        {
            if(!panel || session.Run==null) return;
            var run=session.Run;
            bool visible=run.SpeActive && run.Outcome==SurvivalOutcome.Running;
            panel.SetActive(visible);
            if(!visible) return;
            bool inside=session.IsInside;
            bool en=!language || language.English;
            title.text=en?"SPE ALERT - SOLAR PARTICLE EVENT":"SPE 경고 — 태양 입자 이벤트";
            string action=inside?(en?"Inside base: use E at the blue power console.":"기지 내부: 파란 전력 콘솔에 접근해 E키로 조작하세요."):
                (en?"RETURN TO BASE. Active shielding works only indoors.":"기지로 복귀하세요. 능동차폐는 기지 내부에서만 작동합니다.");
            double reduction=inside?shielding.Reduction:0;
            double safeHours=run.Rate>0?System.Math.Max(0,(run.DoseLimit-run.Dose)/run.Rate):double.PositiveInfinity;
            string margin=double.IsFinite(safeHours)?safeHours.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture):"--";
            var b=power.Budget;
            float distance=console?Vector3.Distance(player.transform.position,console.position):0;
            details.text=action+"\n"+(en?$"Console: {distance:0} m straight-line  |  Exposure {run.Rate:0.00} mSv/h":$"콘솔 직선거리 {distance:0} m  |  피폭률 {run.Rate:0.00} mSv/h")+"\n"+
                (en?$"Indoor shield output {shielding.SuppliedOutput:P0}  |  Here: {reduction:P0} reduction":$"실내 차폐 출력 {shielding.SuppliedOutput:P0}  |  현재 위치 감소율 {reduction:P0}")+"\n"+
                (en?$"At this rate: {margin} game h to danger. Rate may change.":$"현재 피폭률 유지 시 위험까지 {margin} 게임 시간. 조건에 따라 변동.")+"\n"+
                (en?$"Power ON - cold {b.Running(0)}/3  instruments {b.Running(1)}/3  plants {b.Running(2)}/3":$"전원 ON — 보관 {b.Running(0)}/3  장비 {b.Running(1)}/3  재배 {b.Running(2)}/3")+"\n"+
                SampleLine(research,en)+"\n"+
                (en?"TEST MODEL. Balance shield power and research; outages stop new data.":"임시 모델. 차폐와 연구 전력을 선택하세요. 전원 OFF 시 신규 연구 중단.");
            background.color=inside?new Color32(68,48,15,245):new Color32(89,24,29,245);
        }
        public static string SampleLine(ResearchProgress research,bool english)
        {
            string result=english?"Cold-store quality:":"보관 시료 품질:";
            for(int i=0;i<3;i++)
            {
                var state=research && research.Ledger!=null?research.Ledger.Get(ResearchMission.Samples,i):null;
                string value=state==null || !state.Activated?"--":state.Preservation==null?"--":$"{state.Preservation.Quality:P0}";
                if(state?.Preservation!=null && state.Activated && !state.Preservation.Usable) value+=english?" INVALID":" 연구불가";
                result+=$"  {i+1}: {value}";
            }
            return result;
        }
        void OnDestroy() { if(session && session.Run!=null) session.Run.SpeStarted-=OnSpe; }
    }
}
