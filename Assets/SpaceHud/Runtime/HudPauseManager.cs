using SpaceDilemma.CustomInput;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceDilemma.Hud
{
    public sealed class HudPauseManager : MonoBehaviour
    {
        public bool IsPaused { get; private set; }
        public GameObject Popup { get; private set; }
        InputHub hub;
        InputContextProfile profile;
        Selectable continueButton;
        [SerializeField] GameObject pauseWindow;
        [SerializeField] HudGameSettings settingsWindow;
        [SerializeField] Selectable settingsButton;
        GameObject previousSelection;
        int contextToken;
        float previousTimeScale;
        bool pendingToggle, selectContinue;

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
        }
        void OnAction(ActionSignal signal)
        {
            if (signal.map == "Global" && signal.action == "Pause" && signal.phase == InputActionPhase.Performed)
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
            else if (selectContinue && IsPaused)
            {
                selectContinue = false;
                if (EventSystem.current && continueButton) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
            }
        }
        public void Toggle() { if (IsPaused) Resume(); else Pause(); }

        public void ShowSettings()
        {
            if (!IsPaused || !settingsWindow || !pauseWindow) return;
            EventSystem.current?.SetSelectedGameObject(null);
            pauseWindow.SetActive(false);
            settingsWindow.Open();
            EventSystem.current?.SetSelectedGameObject(settingsWindow.FirstControl.gameObject);
        }

        public void BackFromSettings()
        {
            if (!IsPaused || !settingsWindow || !pauseWindow) return;
            EventSystem.current?.SetSelectedGameObject(null);
            settingsWindow.gameObject.SetActive(false);
            pauseWindow.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(settingsButton.gameObject);
        }

        public void QuitGame()
        {
            if (!IsPaused) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Pause()
        {
            if (IsPaused) return;
            if (!hub || !profile) { Debug.LogWarning("HUD pause requires the Custom Input system and pause context."); return; }
            previousTimeScale = Time.timeScale;
            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            contextToken = hub.PushContext(profile);
            IsPaused = true; Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Popup.SetActive(true); selectContinue = true;
        }
        public void Resume()
        {
            if (!IsPaused) return;
            if (settingsWindow && settingsWindow.gameObject.activeSelf) BackFromSettings();
            IsPaused = false; if (Popup) Popup.SetActive(false); Time.timeScale = previousTimeScale;
            if (hub && contextToken != 0) hub.PopContext(contextToken);
            contextToken = 0; selectContinue = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        void OnDestroy()
        {
            if (hub) hub.ActionTriggered -= OnAction;
            if (IsPaused) Resume();
        }
    }
}
