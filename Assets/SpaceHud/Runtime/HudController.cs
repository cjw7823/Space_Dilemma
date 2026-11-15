using System;
using HeyHeyThere.PixelUISciFiFree;
using SpaceDilemma.CustomInput;
using UnityEngine;

namespace SpaceDilemma.Hud
{
    // All visual objects are authored in the scene. This component only updates their state.
    public sealed class HudController : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] HudSettings settings;
        [SerializeField] LunarEnvironment environment;
        [SerializeField] MoonBasePlayer player;
        [SerializeField] Canvas canvas;
        [SerializeField] MinimapController minimap;
        [SerializeField] HudPauseManager pause;
        [SerializeField] GameObject pauseOverlay;
        [SerializeField] PixelButton pauseButton;
        [SerializeField] PixelButton continueButton;
        [SerializeField] PixelLabel clockLabel;
        [SerializeField] PixelLabel objectiveTitle;
        [SerializeField] PixelLabel objectiveDescription;
        [SerializeField] PixelLabel objectiveProgress;
        [SerializeField] GameObject controlsPanel;
        [SerializeField] PixelLabel controlsPrimary;
        [SerializeField] PixelLabel controlsSecondary;
        [SerializeField] HudGameSettings gameSettings;
        [Header("English example objective")]
        [SerializeField] string exampleTitleEnglish = "Restore the solar plant";
        [SerializeField, TextArea] string exampleDescriptionEnglish = "Bring the generator online";

        public static HudController Active { get; private set; }
        public ObjectiveModel Objective { get; } = new ObjectiveModel();
        public MinimapController Minimap => minimap;
        public HudPauseManager Pause => pause;
        public PixelButton PauseButton => pauseButton;
        public PixelButton ContinueButton => continueButton;
        public PixelLabel ClockLabel => clockLabel;
        public PixelLabel ObjectiveTitle => objectiveTitle;
        public PixelLabel ObjectiveDescription => objectiveDescription;
        public PixelLabel ObjectiveProgress => objectiveProgress;
        public Canvas Canvas => canvas;

        void Awake()
        {
            if (Active && Active != this) { Debug.LogError("Only one Space HUD may be active.", this); enabled = false; return; }
            Active = this;
        }
        void Start()
        {
            var hub = InputHub.Active;
            if (!settings || !hub || !hub.player || !player || !canvas || !minimap || !pause || !pauseOverlay || !pauseButton ||
                !continueButton || !clockLabel)
            { Debug.LogError("Space HUD is missing a scene reference or the Custom Input system.", this); return; }
            minimap.Initialize(hub.player.transform);
            pause.Initialize(hub, settings.pauseContext, pauseOverlay, continueButton);
            if (objectiveTitle && objectiveDescription && objectiveProgress)
            {
                Objective.Changed += RefreshObjective;
                if (gameSettings) gameSettings.LanguageChanged += RefreshObjective;
                Objective.SetLocalized(exampleTitleEnglish, exampleDescriptionEnglish, objectiveTitle.text,
                    objectiveDescription.text, settings.exampleCurrent, settings.exampleTotal, true);
            }
            RefreshClock();
        }
        void RefreshObjective()
        {
            objectiveTitle.text = Text(Objective.Title, Objective.KoreanTitle);
            objectiveDescription.text = Text(Objective.Description, Objective.KoreanDescription);
            objectiveProgress.text = (Objective.IsExample ? Text("EXAMPLE  ", "예시  ") : "") + Objective.Current + " / " + Objective.Total;
        }
        void LateUpdate()
        {
            if (clockLabel) RefreshClock();
            if (!player || !controlsPanel || !controlsPrimary || !controlsSecondary) return;
            if (controlsPanel.activeSelf != player.showControls) controlsPanel.SetActive(player.showControls);
            if (!player.showControls) return;
            string text = player.IsDriving ? Text("WASD Drive   T Exit rover", "WASD 운전   T 하차") :
                Text("WASD Move  Shift Run  Space Jump", "WASD 이동   Shift 달리기   Space 점프");
            if (player.IsDriving && player.ExitBlocked) text = Text("Exit blocked. Move rover to clear space.", "내릴 공간이 없습니다. 차량을 옮기세요.");
            else if (!player.IsDriving && player.ControlCaptured && player.rover && player.rover.CanBoard(player.transform.position)) text = Text("T Board rover", "T 차량 탑승");
            controlsPrimary.text = text;
            controlsSecondary.text = player.ControlCaptured ? Text("Tab: cursor   Esc: pause", "Tab 커서   Esc 일시정지") :
                Text("Click game view to control player", "화면을 클릭해 조작 시작");
        }
        string Text(string english, string korean) => gameSettings ? gameSettings.Text(english, korean) : english;
        void RefreshClock()
        {
            if (!environment || environment.SunTable == null) { clockLabel.text = "--:--:--"; return; }
            clockLabel.text = FormatElapsedTime(environment.sunHoursFromStart);
        }

        public static string FormatElapsedTime(double hours)
        {
            long seconds = (long)Math.Floor(Math.Max(0, hours) * 3600 + 1e-6);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                seconds / 3600, seconds / 60 % 60, seconds % 60);
        }
        void OnDestroy()
        {
            Objective.Changed -= RefreshObjective;
            if (gameSettings) gameSettings.LanguageChanged -= RefreshObjective;
            if (Active == this) Active = null;
        }
    }
}
