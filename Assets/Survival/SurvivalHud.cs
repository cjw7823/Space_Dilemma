using System.Globalization;
using SpaceDilemma.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDilemma.Survival
{
    public sealed class SurvivalHud : MonoBehaviour
    {
        [SerializeField] SurvivalSession session;
        [SerializeField] HudGameSettings language;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text heading;
        [SerializeField] TMP_Text dosePercent;
        [SerializeField] Image doseFill;

        void LateUpdate()
        {
            var run = session.Run;
            if (run == null) return;
            double percent = System.Math.Clamp(run.Dose / run.DoseLimit * 100, 0, 100);
            if (heading) heading.text = Text("RADIATION", "방사선 피폭도");
            if (dosePercent) dosePercent.text = percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            if (doseFill)
            {
                doseFill.rectTransform.anchorMax = new Vector2(1, (float)(percent / 100));
                doseFill.enabled = percent > 0;
            }
            label.text = System.FormattableString.Invariant($"{run.Dose:F2} /\n{run.DoseLimit:0.##} mSv");
        }

        string Text(string english, string korean) => language ? language.Text(english, korean) : english;

    }
}
