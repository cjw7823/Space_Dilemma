using SpaceDilemma.Hud;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Results;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDilemma.Research
{
    public sealed class ResearchHud : MonoBehaviour
    {
        [SerializeField] ResearchInteraction research;
        [SerializeField] HudGameSettings language;
        [SerializeField] TMP_Text summary;
        [SerializeField] TMP_Text prompt;
        [SerializeField] Image progressFill;
        [SerializeField] TMP_Text achievement;
        [SerializeField] TMP_Text achievementValue;
        [SerializeField] Image achievementFill;
        [SerializeField] FinalEvaluation evaluation;
        [SerializeField] TMP_Text boardTitle;
        [SerializeField] MoonBasePlayer player;

        void LateUpdate()
        {
            UpdateAchievement();
            if (boardTitle) boardTitle.text = Text("MISSION BOARD", "미션 보드");
            var target = research.Nearby;
            string counts = $"{Text("Collected", "채취")} {research.Count(ResearchKind.Sample)}/3   " +
                $"{Text("Carried", "소지")} {research.CarriedSampleCount}   " +
                $"{Text("Stored", "보관")} {research.Count(ResearchKind.Storage)}/3\n" +
                $"{Text("Instruments", "장비")} {research.Count(ResearchKind.Instrument)}/3   " +
                $"{Text("Plants", "재배")} {research.Count(ResearchKind.Plant)}/3";
            summary.text = counts;
            progressFill.fillAmount = research.Progress;
            if (!target)
            {
                prompt.text = NearbyAction();
                return;
            }

            string name = Text(target.englishName, target.koreanName);
            string action = research.IsComplete(target) ? Text("Complete", "완료")
                : !research.CanUse(target) ? Text("Collect a lunar sample first", "먼저 달 시료를 채취하세요")
                : research.NeedsRelease ? Text("Release E", "E키 놓기")
                : target.kind == ResearchKind.Storage ? Text("Hold E to store", "E 길게 눌러 보관")
                : Text("Hold E", "E 길게 누르기");
            prompt.text = name + "  /  " + action;
        }

        string Text(string english, string korean) => language ? language.Text(english, korean) : english;

        string NearbyAction()
        {
            if (player && player.ControlCaptured && InputHub.Active && InputHub.Active.GameplayAllowed)
            {
                if (player.IsDriving && player.ExitBlocked)
                    return Text("Exit blocked. Move the rover.", "하차 공간이 없습니다. 차량을 옮기세요.");
                if (player.IsDriving) return Text("T: Exit rover", "T: 차량 하차");
                if (player.rover && player.rover.CanBoard(player.transform.position))
                    return Text("T: Board rover", "T: 차량 탑승");
            }
            return Text("Approach a research marker", "연구 표식에 접근하세요");
        }

        void UpdateAchievement()
        {
            if (!evaluation || !achievement || !achievementValue || !achievementFill) return;
            double percent = evaluation.CurrentResearchPercent;
            achievement.text = Text("RESEARCH", "연구 달성도");
            achievementValue.text = percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            achievementFill.rectTransform.anchorMax = new Vector2(1, (float)(percent / 100));
            achievementFill.enabled = percent > 0;
        }
    }
}
