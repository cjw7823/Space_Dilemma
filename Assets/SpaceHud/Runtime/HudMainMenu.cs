using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDilemma.Hud
{
    public sealed class HudMainMenu : MonoBehaviour
    {
        [SerializeField] GameObject controls;
        [SerializeField] Button startButton, optionsButton;
        [SerializeField] TMP_Text startLabel, optionsLabel, quitLabel;

        public Selectable StartButton => startButton;
        public Selectable OptionsButton => optionsButton;
        public bool ShowsControls => controls.activeSelf;

        public void ShowControls(bool visible) => controls.SetActive(visible);

        public void Localize(HudGameSettings language)
        {
            startLabel.text = language.Text("START GAME", "게임 시작");
            optionsLabel.text = language.Text("OPTIONS", "옵션");
            quitLabel.text = language.Text("QUIT GAME", "게임 종료");
        }
    }
}
