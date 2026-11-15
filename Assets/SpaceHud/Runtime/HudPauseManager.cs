using SpaceDilemma.CustomInput;
using SpaceDilemma.Survival;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpaceDilemma.Hud
{
    public sealed class HudPauseManager : MonoBehaviour
    {
        public bool IsPaused { get; private set; }
        public bool IsMainMenuOpen { get; private set; }
        public bool BlockExternalPause { get; set; }
        public GameObject Popup { get; private set; }
        InputHub hub;
        InputContextProfile profile;
        Selectable continueButton;
        [SerializeField] GameObject pauseWindow;
        [SerializeField] HudGameSettings settingsWindow;
        [SerializeField] Selectable settingsButton;
        [SerializeField] HudMainMenu mainMenu;
        [SerializeField] RadiationBriefing radiationBriefing;
        GameObject previousSelection;
        int contextToken;
        float previousTimeScale;
        bool pendingToggle, selectContinue;
        bool returningToMainMenu;

        public void Initialize(InputHub input, InputContextProfile pauseProfile, GameObject popup, Selectable resume)
        {
            hub = input; profile = pauseProfile; Popup = popup; continueButton = resume;
            Popup.SetActive(false);
            if (settingsWindow)
            {
                settingsWindow.gameObject.SetActive(false);
                settingsWindow.Initialize(input);
            }
            if (hub) hub.ActionTriggered += OnAction;
            if (mainMenu && hub && profile)
            {
                IsMainMenuOpen = true;
                SuspendGameplay();
                if (previousTimeScale <= 0) previousTimeScale = 1;
                mainMenu.gameObject.SetActive(true);
                mainMenu.ShowControls(true);
                LocalizeMainMenu();
                if (settingsWindow) settingsWindow.LanguageChanged += LocalizeMainMenu;
                selectContinue = true;
            }
        }
        void OnAction(ActionSignal signal)
        {
            if (signal.phase != InputActionPhase.Performed || BlockExternalPause || returningToMainMenu) return;
            bool pauseAction = signal.map == "Global" && signal.action == "Pause";
            if (IsMainMenuOpen)
            {
                if (settingsWindow && settingsWindow.gameObject.activeInHierarchy &&
                    (pauseAction || (signal.map == "UI" && signal.action == "Cancel")))
                    pendingToggle = true;
            }
            else if (pauseAction)
                pendingToggle = true;
        }
        void Update()
        {
            if (pendingToggle)
            {
                pendingToggle = false;
                if (settingsWindow && settingsWindow.gameObject.activeInHierarchy) BackFromSettings();
                else Toggle();
            }
            // InputHub applies context changes at the start of the next frame.
            else if (selectContinue)
            {
                selectContinue = false;
                if (IsMainMenuOpen && mainMenu.ShowsControls) Select(mainMenu.StartButton);
                else if (IsPaused && (!pauseWindow || pauseWindow.activeInHierarchy)) Select(continueButton);
            }
        }
        public void Toggle() { if (IsPaused) Resume(); else Pause(); }

        public void ShowSettings()
        {
            if ((!IsPaused && !IsMainMenuOpen) || !settingsWindow || !pauseWindow || returningToMainMenu) return;
            Select(null);
            pauseWindow.SetActive(false);
            if (IsMainMenuOpen)
            {
                mainMenu.ShowControls(false);
                Popup.SetActive(true);
            }
            settingsWindow.Open();
            Select(settingsWindow.FirstControl);
        }

        public void BackFromSettings()
        {
            if ((!IsPaused && !IsMainMenuOpen) || !settingsWindow || !pauseWindow) return;
            Select(null);
            settingsWindow.gameObject.SetActive(false);
            if (IsMainMenuOpen)
            {
                Popup.SetActive(false);
                mainMenu.ShowControls(true);
                Select(mainMenu.OptionsButton);
            }
            else
            {
                pauseWindow.SetActive(true);
                Select(settingsButton);
            }
        }

        public void StartMission()
        {
            if (!IsMainMenuOpen || !mainMenu.ShowsControls || returningToMainMenu) return;
            IsMainMenuOpen = false;
            mainMenu.gameObject.SetActive(false);
            // Hand off to the briefing in the same frame, before the simulation can advance.
            ReleaseGameplay();
            if (radiationBriefing) radiationBriefing.Open();
        }

        public void ReturnToMainMenu()
        {
            if (!IsPaused || !mainMenu || returningToMainMenu) return;
            returningToMainMenu = true;
            Popup.SetActive(false);
            mainMenu.gameObject.SetActive(true);
            mainMenu.ShowControls(false);
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        public void QuitGame()
        {
            if (!IsMainMenuOpen || !mainMenu.ShowsControls || returningToMainMenu) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Pause()
        {
            if (IsPaused || IsMainMenuOpen || BlockExternalPause || returningToMainMenu) return;
            if (!hub || !profile) { Debug.LogWarning("HUD pause requires the Custom Input system and pause context."); return; }
            IsPaused = true;
            SuspendGameplay();
            if (pauseWindow) pauseWindow.SetActive(true);
            Popup.SetActive(true); selectContinue = true;
        }

        void SuspendGameplay()
        {
            previousTimeScale = Time.timeScale;
            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            contextToken = hub.PushContext(profile);
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void Resume()
        {
            if (!IsPaused || returningToMainMenu) return;
            if (settingsWindow && settingsWindow.gameObject.activeSelf) BackFromSettings();
            IsPaused = false;
            if (Popup) Popup.SetActive(false);
            ReleaseGameplay();
        }

        void ReleaseGameplay()
        {
            Time.timeScale = previousTimeScale;
            if (hub && contextToken != 0) hub.PopContext(contextToken);
            contextToken = 0; selectContinue = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(
                previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
        }

        void LocalizeMainMenu() { if (mainMenu && settingsWindow) mainMenu.Localize(settingsWindow); }

        static void Select(Selectable target)
        {
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(target ? target.gameObject : null);
        }

        void OnDestroy()
        {
            if (hub) hub.ActionTriggered -= OnAction;
            if (settingsWindow) settingsWindow.LanguageChanged -= LocalizeMainMenu;
            if (contextToken != 0) ReleaseGameplay();
        }
    }
}
