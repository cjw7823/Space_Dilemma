using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Survival;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
static class HudMainMenuChecks
{
    const string Running = "SpaceHud.MainMenuValidation";
    const string Output = "QA/MainMenu";
    const string LanguageKey = "SpaceDilemma.Language";
    static readonly List<string> results = new();
    static IEnumerator cases;
    static int frame = -1, failures, previousLanguage;
    static double deadline;
    static bool previousBackground, hadLanguage;

    static HudMainMenuChecks() => EditorApplication.update += Update;

    [MenuItem("Tools/Space HUD/Validate Main Menu")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    static void Update()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || Time.frameCount == frame) return;
        frame = Time.frameCount;
        try
        {
            if (cases == null)
            {
                results.Clear();
                failures = 0;
                previousBackground = Application.runInBackground;
                hadLanguage = PlayerPrefs.HasKey(LanguageKey);
                previousLanguage = PlayerPrefs.GetInt(LanguageKey);
                Application.runInBackground = true;
                deadline = EditorApplication.timeSinceStartup + 90;
                Directory.CreateDirectory(Output);
                cases = Cases();
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Main menu validation timed out.");
            if (!cases.MoveNext()) Finish();
        }
        catch (Exception e)
        {
            Check(false, e.ToString());
            Finish();
        }
    }

    static IEnumerator Cases()
    {
        for (int i = 0; i < 8; i++) yield return null;
        var hud = HudController.Active;
        var pause = hud.Pause;
        var session = UnityEngine.Object.FindFirstObjectByType<SurvivalSession>();
        var briefing = UnityEngine.Object.FindFirstObjectByType<RadiationBriefing>();
        var run = session.Run;
        var menu = hud.Canvas.GetComponentInChildren<HudMainMenu>(true);
        var settings = pause.Popup.GetComponentInChildren<HudGameSettings>(true);
        var button = pause.Popup.transform.Find("Pause Window/Main Menu Button").GetComponent<Button>();
        var start = menu.StartButton.gameObject;
        var quit = menu.transform.Find("Title and Actions/Quit Game").GetComponent<Button>();
        Check(pause.IsMainMenuOpen && menu.ShowsControls && menu.gameObject.activeInHierarchy && !briefing.IsOpen &&
            menu.GetComponent<Image>().color == Color.black, "Main menu covers the game with an opaque black overlay before briefing");
        Check(!InputHub.Active.GameplayAllowed && Time.timeScale == 0 && !pause.IsPaused &&
            run.Hours == session.Scenario.start.elapsedHours && run.Dose == session.Scenario.start.dose,
            "Title screen leaves clock and dose at their starting values and blocks gameplay");
        Check(EventSystem.current.currentSelectedGameObject == start &&
            menu.GetComponentsInChildren<Button>().Length == 3, "Start Game is preselected among exactly three authored buttons");
        Check(HasClick(quit, pause, nameof(HudPauseManager.QuitGame)) &&
            HasClick(button, pause, nameof(HudPauseManager.ReturnToMainMenu)) &&
            !HasClick(button, pause, nameof(HudPauseManager.QuitGame)),
            "Quit belongs to the title and the pause button returns to the main menu");

        settings.UseKorean();
        Check(menu.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "게임 시작") &&
            menu.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "옵션"), "Title buttons follow the selected language");
        var sizes = new[] { 1280, 1920 };
        foreach (int width in sizes)
        {
            SampleDiscoveryChecks.SetGameSize(width, width == 1280 ? 720 : 1080);
            for (int i = 0; i < 7; i++) yield return null;
            Capture($"main-ko-{width}");
            for (int i = 0; i < 7; i++) yield return null;
        }

        Submit(menu.OptionsButton.gameObject);
        for (int i = 0; i < 5; i++) yield return null;
        Check(settings.gameObject.activeInHierarchy && menu.gameObject.activeInHierarchy && !menu.ShowsControls &&
            EventSystem.current.currentSelectedGameObject == settings.FirstControl.gameObject &&
            Time.timeScale == 0 && !InputHub.Active.GameplayAllowed, "Options focuses the existing settings on the black title background");
        Capture("options-ko-1920");
        for (int i = 0; i < 7; i++) yield return null;
        settings.UseEnglish();
        var back = settings.GetComponentsInChildren<Button>().Single(item => HasClick(item, pause, nameof(HudPauseManager.BackFromSettings)));
        Submit(back.gameObject);
        Check(!settings.gameObject.activeSelf && menu.ShowsControls && Time.timeScale == 0 &&
            EventSystem.current.currentSelectedGameObject == menu.OptionsButton.gameObject,
            "Settings Back returns to the title, keeps the mission paused and restores focus");
        Capture("main-en-1920");
        for (int i = 0; i < 7; i++) yield return null;

        Submit(start);
        for (int i = 0; i < 6; i++) yield return null;
        Check(!pause.IsMainMenuOpen && !menu.gameObject.activeSelf && briefing.IsOpen && Time.timeScale == 0 &&
            run.Hours == session.Scenario.start.elapsedHours && run.Dose == session.Scenario.start.dose,
            "Start Game hands off to radiation briefing without advancing the clock or dose");
        Submit(EventSystem.current.currentSelectedGameObject);
        for (int i = 0; i < 6; i++) yield return null;
        Check(!briefing.IsOpen && InputHub.Active.GameplayAllowed && Time.timeScale == 1 &&
            run.Hours > session.Scenario.start.elapsedHours && run.Dose > session.Scenario.start.dose,
            "Closing briefing starts the clock, dose and controls");

        pause.Pause();
        Check(pause.IsPaused && button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>().text == "MAIN MENU",
            "Pause exposes the Main Menu button in English");
        Submit(button.gameObject);
        for (int i = 0; i < 120 && (session || !HudController.Active || !HudController.Active.Pause.IsMainMenuOpen); i++)
            yield return null;
        var restarted = UnityEngine.Object.FindFirstObjectByType<SurvivalSession>();
        var restartedHud = HudController.Active;
        Check(!session && restarted && restarted.gameObject.scene.path == "Assets/Scenes/SampleScene.unity" &&
            restartedHud && restartedHud.Pause.IsMainMenuOpen && !restartedHud.Pause.IsPaused && Time.timeScale == 0 &&
            restarted.Run.Hours == restarted.Scenario.start.elapsedHours &&
            restarted.Run.Dose == restarted.Scenario.start.dose,
            "Main Menu reloads the same scene to a fresh paused mission with no previous dose or time");

        if (restartedHud && restartedHud.Pause.IsMainMenuOpen)
        {
            Submit(restartedHud.Canvas.GetComponentInChildren<HudMainMenu>().StartButton.gameObject);
            for (int i = 0; i < 5; i++) yield return null;
            Check(UnityEngine.Object.FindFirstObjectByType<RadiationBriefing>().IsOpen && Time.timeScale == 0,
                "A new mission from the returned main menu shows the briefing again");
        }
        foreach (string name in new[] { "main-ko-1280", "main-ko-1920", "main-en-1920", "options-ko-1920" })
            Check(File.Exists(Path.Combine(Output, name + ".png")), $"Game view capture: {name}");
    }

    static bool HasClick(Button button, HudPauseManager pause, string method) =>
        Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(index =>
            button.onClick.GetPersistentTarget(index) == pause && button.onClick.GetPersistentMethodName(index) == method);

    static void Submit(GameObject button) =>
        ExecuteEvents.Execute(button, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);

    static void Capture(string name)
    {
        string path = Path.GetFullPath(Path.Combine(Output, name + ".png"));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
    }

    static void Check(bool passed, string description)
    {
        results.Add((passed ? "PASS " : "FAIL ") + description);
        if (!passed) failures++;
    }

    static void Finish()
    {
        SessionState.SetBool(Running, false);
        (cases as IDisposable)?.Dispose();
        cases = null;
        SampleDiscoveryChecks.RestoreGameSize();
        Application.runInBackground = previousBackground;
        if (hadLanguage) PlayerPrefs.SetInt(LanguageKey, previousLanguage);
        else PlayerPrefs.DeleteKey(LanguageKey);
        PlayerPrefs.Save();
        File.WriteAllLines(Path.Combine(Output, "play-checks.txt"),
            results.Append($"RESULT checks={results.Count} failures={failures}"));
        EditorApplication.isPlaying = false;
    }
}
