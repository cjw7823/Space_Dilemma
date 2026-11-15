using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Power;
using SpaceDilemma.Results;
using SpaceDilemma.Scenario;
using SpaceDilemma.Survival;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
static class RadLabCalibrationChecks
{
    const string Output="QA/RadLabCalibration/RemainingDose";
    static readonly Stack<IEnumerator> routines=new();
    static readonly List<string> checks=new(),errors=new();
    static int frame=-1,failures,oldLanguage;
    static float deadline;
    static bool background,hadLanguage;
    static RadLabCalibrationChecks(){EditorApplication.update+=Update;}
    [MenuItem("Tools/Survival/Validate RadLab Calibration")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
        SessionState.SetBool("RadLabCalibration.Test",true);EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string text){checks.Add((ok?"PASS ":"FAIL ")+text);if(!ok)failures++;}
    static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack);}
    static void Update()
    {
        if(!SessionState.GetBool("RadLabCalibration.Test",false)||!EditorApplication.isPlaying||Time.frameCount==frame)return;frame=Time.frameCount;
        try
        {
            if(routines.Count==0){checks.Clear();errors.Clear();failures=0;deadline=Time.realtimeSinceStartup+100;Application.logMessageReceived+=Log;routines.Push(Cases());}
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("RadLab calibration validation timed out.");
            while(routines.Count>0)
            {
                var top=routines.Peek();if(!top.MoveNext()){routines.Pop();continue;}
                if(top.Current is IEnumerator nested){routines.Push(nested);continue;}return;
            }
            Finish();
        }
        catch(Exception e){Check(false,e.ToString());Finish();}
    }
    static IEnumerator Frames(int n){while(n-->0)yield return null;}
    static IEnumerator Wait(float s){float end=Time.realtimeSinceStartup+s;while(Time.realtimeSinceStartup<end)yield return null;}
    static bool Near(double a,double b,double tolerance=.0001)=>Math.Abs(a-b)<tolerance;
    static SurvivalRun NewRun(ScenarioRules rules)=>new SurvivalRun(rules.time.durationHours,rules.time.speStartHour,
        rules.radiation.NormalRate,rules.radiation.SpeRate,rules.radiation.lethalDose,rules.radiation.indoorTransmission,
        rules.start.elapsedHours,rules.start.dose,rules.time.speDurationHours,
        fitSpeToRemainingDose:rules.radiation.fitSpeToRemainingDose,
        speRemainingDoseMultiplier:rules.radiation.speRemainingDoseMultiplier);
    sealed class EightyPercentShield : IRadiationShield
    {public double Transmission(bool inside,bool spe)=>inside && spe ? 0.2 : 1;}
    sealed class AdjustableShield : IRadiationShield
    {
        public double transmission=0.6;
        public double Transmission(bool inside,bool spe)=>inside && spe ? transmission : 1;
    }
    static void Pure(ScenarioRules rules)
    {
        Check(rules.radiation.fitSpeToRemainingDose&&Near(rules.radiation.speRemainingDoseMultiplier,1.1)&&
            rules.radiation.useRadLabAverages&&rules.radiation.gameGain==570&&rules.radiation.lethalDose==600,
            "Current scenario enables one-time SPE normalization with a 10% margin, RadLab means, k=570 and a 600 game mSv limit");
        Check(Near(rules.time.durationHours/rules.time.gameHoursPerSecond,600)&&Near(rules.time.speStartHour/rules.time.gameHoursPerSecond,480),"48 game hours map to ten play minutes and SPE starts at eight minutes");
        Check(Near(rules.radiation.NormalRate,6.535871354166667)&&Near(rules.radiation.SpeRate,37.12048802083333),
            "The configured raw rates still match the observed means multiplied by k/1000");
        Check(Near(rules.radiation.SpeRate/rules.radiation.NormalRate,5.679501019733006),
            "The source mean ratio remains approximately 5.68 before gameplay normalization");
        Check(Near(rules.radiation.NormalRate*rules.time.gameHoursPerSecond,.5228697083333333),
            "The unchanged pre-event rate adds approximately 0.5229 game mSv per play second outdoors");
        var coarse=NewRun(rules);int alerts=0;double alertHour=0;coarse.SpeStarted+=()=>{alerts++;alertHour=coarse.Hours;};
        coarse.Advance(479*rules.time.gameHoursPerSecond,false);
        Check(!coarse.SpeActive&&!coarse.SpeRateCalibrated&&coarse.Outcome==SurvivalOutcome.Running&&alerts==0,
            "At play 7:59 SPE has not calibrated or raised an alert");
        coarse.Advance(2*rules.time.gameHoursPerSecond,false);
        Check(coarse.SpeActive&&alerts==1&&Near(alertHour/rules.time.gameHoursPerSecond,480),"A frame crossing 8:00 triggers SPE once at the correct game-hour boundary");
        double outdoorStartDose=rules.start.dose+rules.radiation.NormalRate*(rules.time.speStartHour-rules.start.elapsedHours);
        double multiplier=rules.radiation.speRemainingDoseMultiplier;
        double outdoorRate=multiplier*(rules.radiation.lethalDose-outdoorStartDose)/(rules.time.durationHours-rules.time.speStartHour);
        double deathHour=rules.time.speStartHour+(rules.time.durationHours-rules.time.speStartHour)/multiplier;
        Check(coarse.SpeRateCalibrated&&Near(coarse.SpeCalibrationHour,rules.time.speStartHour)&&
            Near(coarse.SpeStartDose,outdoorStartDose)&&Near(coarse.SpeOutdoorRate,outdoorRate)&&Near(coarse.Rate,outdoorRate),
            "The crossing frame snapshots dose at the SPE boundary and fixes the remaining-dose outdoor rate");
        coarse.Advance(100,false);
        Check(coarse.Outcome==SurvivalOutcome.Died&&Near(coarse.Dose,600)&&Near(coarse.Hours,deathHour)&&
            coarse.Hours<rules.time.durationHours&&Near(coarse.Hours/rules.time.gameHoursPerSecond,589.0909,.002),
            "Continuous outdoor exposure reaches 600 at approximately 9:49.09 before mission end");
        var fine=NewRun(rules);
        double frameHours=rules.time.durationHours/(600*60.0);
        for(int i=0;i<=600*60&&fine.Outcome==SurvivalOutcome.Running;i++)fine.Advance(frameHours,false);
        Check(Near(fine.SpeStartDose,coarse.SpeStartDose)&&Near(fine.SpeOutdoorRate,coarse.SpeOutdoorRate)&&
            Near(fine.Dose,coarse.Dose)&&Near(fine.Hours,coarse.Hours)&&fine.Outcome==coarse.Outcome,
            "60 FPS integration and coarse crossing advances agree on calibration and the terminal boundary");

        var indoorFirst=NewRun(rules);indoorFirst.Advance(rules.time.speStartHour,true);
        double indoorStartDose=rules.start.dose+rules.radiation.NormalRate*rules.radiation.indoorTransmission*
            (rules.time.speStartHour-rules.start.elapsedHours);
        double indoorFirstRate=multiplier*(rules.radiation.lethalDose-indoorStartDose)/(rules.time.durationHours-rules.time.speStartHour);
        Check(Near(indoorFirst.SpeStartDose,indoorStartDose)&&Near(indoorFirst.SpeOutdoorRate,indoorFirstRate)&&
            indoorFirst.SpeOutdoorRate>outdoorRate,
            "Eight minutes indoors records the lower actual dose and computes a higher fixed SPE outdoor rate");
        indoorFirst.Advance(100,false);
        Check(indoorFirst.Outcome==SurvivalOutcome.Died&&Near(indoorFirst.Dose,600)&&Near(indoorFirst.Hours,deathHour),
            "Going outdoors after an indoor opening reaches 600 at the same pre-end safety boundary");

        var sheltered=NewRun(rules);sheltered.Advance(rules.time.speStartHour,false);
        double fixedRate=sheltered.SpeOutdoorRate,fixedHour=sheltered.SpeCalibrationHour,fixedDose=sheltered.SpeStartDose;
        sheltered.Advance(100,true,new EightyPercentShield());
        double fullyShelteredDose=fixedDose+(600-fixedDose)*multiplier*rules.radiation.indoorTransmission*.2;
        Check(sheltered.Outcome==SurvivalOutcome.Survived&&Near(sheltered.Dose,fullyShelteredDose)&&sheltered.AvoidedSpeDose>0&&
            Near(sheltered.SpeOutdoorRate,fixedRate)&&Near(sheltered.SpeStartDose,fixedDose),
            "Indoor active shielding reduces cumulative dose while the captured SPE outdoor rate remains fixed");

        var mixed=NewRun(rules);mixed.Advance(rules.time.speStartHour,false);
        var partial=new AdjustableShield();
        double remaining=rules.time.durationHours-rules.time.speStartHour;
        mixed.Advance(remaining*.25,true,partial);
        Check(Near(mixed.Rate,fixedRate*rules.radiation.indoorTransmission*.6)&&
            Near(mixed.SpeOutdoorRate,fixedRate)&&Near(mixed.SpeCalibrationHour,fixedHour)&&Near(mixed.SpeStartDose,fixedDose),
            "Partial active shielding changes the received rate without recalibrating the SPE snapshot");
        partial.transmission=.2;mixed.UpdateRate(true,partial);
        Check(Near(mixed.Rate,fixedRate*rules.radiation.indoorTransmission*.2)&&Near(mixed.SpeOutdoorRate,fixedRate),
            "Changing supplied shield output only changes the transmission multiplier");
        mixed.Advance(remaining*.25,true,partial);mixed.UpdateRate(false,partial);
        Check(Near(mixed.Rate,fixedRate)&&Near(mixed.SpeCalibrationHour,fixedHour)&&Near(mixed.SpeStartDose,fixedDose),
            "Returning outdoors restores the fixed SPE rate and preserves the original calibration dose and hour");
        mixed.Advance(100,false,partial);
        double mixedDose=fixedDose+(600-fixedDose)*multiplier*(.25*rules.radiation.indoorTransmission*(.6+.2)+.5);
        Check(mixed.Outcome==SurvivalOutcome.Survived&&Near(mixed.Dose,mixedDose)&&Near(mixed.Hours,rules.time.durationHours),
            "Dose avoided during partial and full shielding stays saved after returning outdoors");

        var copy=UnityEngine.Object.Instantiate(rules);
        try
        {
            copy.radiation.speRemainingDoseMultiplier=1;
            var unitMargin=NewRun(copy);unitMargin.Advance(100,false);
            Check(unitMargin.Outcome==SurvivalOutcome.Died&&Near(unitMargin.Dose,600)&&Near(unitMargin.Hours,copy.time.durationHours),
                "A unit multiplier reproduces exact-endpoint normalization with death priority at the tie");
            copy.radiation.speRemainingDoseMultiplier=.5f;
            var belowUnit=NewRun(copy);belowUnit.Advance(100,false);
            Check(belowUnit.Outcome==SurvivalOutcome.Died&&Near(belowUnit.Dose,600)&&Near(belowUnit.Hours,copy.time.durationHours),
                "A below-unit multiplier is clamped so full outdoor SPE exposure can still reach the limit");
            copy.radiation.speRemainingDoseMultiplier=rules.radiation.speRemainingDoseMultiplier;
            copy.start.elapsedHours=copy.time.speStartHour;copy.start.dose=0;
            var zeroAtSpe=NewRun(copy);
            Check(zeroAtSpe.SpeRateCalibrated&&Near(zeroAtSpe.SpeStartDose,0)&&
                Near(zeroAtSpe.SpeOutdoorRate,multiplier*600/(copy.time.durationHours-copy.time.speStartHour)),
                "A session initialized at SPE with zero dose calibrates its full remaining 600 allowance");
            zeroAtSpe.Advance(100,false);
            Check(zeroAtSpe.Outcome==SurvivalOutcome.Died&&Near(zeroAtSpe.Dose,600)&&Near(zeroAtSpe.Hours,deathHour),
                "A zero-dose SPE start reaches the same pre-end death boundary when outdoors");

            copy.start.elapsedHours=41;copy.start.dose=91;
            var late=NewRun(copy);
            Check(late.SpeRateCalibrated&&Near(late.SpeCalibrationHour,41)&&Near(late.SpeStartDose,91)&&
                Near(late.SpeOutdoorRate,multiplier*509/7.0),
                "A session initialized after SPE snapshots its actual starting hour and existing dose once");
            late.Advance(1,true,new EightyPercentShield());late.UpdateRate(false,null);
            Check(Near(late.SpeCalibrationHour,41)&&Near(late.SpeStartDose,91)&&Near(late.SpeOutdoorRate,multiplier*509/7.0)&&
                Near(late.Rate,multiplier*509/7.0),"A late-start session retains its first calibration after protected exposure");
            late.Advance(100,false);
            Check(late.Outcome==SurvivalOutcome.Survived&&late.Dose<600,
                "Late-start shielding remains beneficial after returning to outdoor exposure");

            copy.start.elapsedHours=copy.start.dose=0;copy.time.speStartHour=copy.time.durationHours;
            var disabled=NewRun(copy);disabled.Advance(100,false);
            Check(!disabled.SpeRateCalibrated&&disabled.Outcome==SurvivalOutcome.Survived&&
                Near(disabled.Dose,copy.radiation.NormalRate*copy.time.durationHours),
                "Disabling SPE at the mission endpoint keeps the normal rate and never calibrates");

            copy.time.speStartHour=rules.time.speStartHour;copy.time.speDurationHours=1;
            var finite=NewRun(copy);finite.Advance(copy.time.speStartHour,false);
            double finiteRate=finite.SpeOutdoorRate;finite.Advance(2,false);
            Check(finite.SpeRateCalibrated&&!finite.SpeActive&&Near(finite.SpeOutdoorRate,finiteRate)&&
                Near(finite.Rate,copy.radiation.NormalRate),
                "A finite-duration SPE restores the normal rate without changing its captured SPE rate");

            copy.time.speDurationHours=0;copy.radiation.fitSpeToRemainingDose=false;
            var fixedLegacy=NewRun(copy);fixedLegacy.Advance(100,false);
            Check(!fixedLegacy.SpeRateCalibrated&&fixedLegacy.Outcome==SurvivalOutcome.Died&&
                Near(fixedLegacy.Hours/rules.time.gameHoursPerSecond,597.5303,.002),
                "Disabling normalization reproduces the original fixed k=570 ending at approximately 9:57.53");
            copy.radiation.gameGain=200;var old=NewRun(copy);old.Advance(100,false);
            Check(old.Outcome==SurvivalOutcome.Survived&&Near(old.Dose,213.0997),"k=200 reproduces the lower 213.10 total under the same 8/2-minute timing");
            Check(Near(copy.radiation.SpeRate/copy.radiation.NormalRate,5.679501019733006)&&Near(copy.time.speStartHour,rules.time.speStartHour),"Changing k scales both rates while preserving ratio and event timing");
            double perK=(copy.radiation.preAverageUGyPerHour*38.4+copy.radiation.postAverageUGyPerHour*9.6)/1000;
            Check(Near(600/perK,563.1167007743326),"Inverse calibration requires k≈563.1167 to reach exactly 600 at ten minutes");
            copy.radiation.gameGain=563;var below=NewRun(copy);below.Advance(100,false);Check(below.Outcome==SurvivalOutcome.Survived&&below.Dose<600,"A coefficient just below the inverse boundary remains below the limit");
            copy.radiation.useRadLabAverages=false;copy.radiation.normalRate=1;copy.radiation.speRate=4;
            var manual=NewRun(copy);manual.Advance(100,false);
            Check(copy.radiation.NormalRate==1&&copy.radiation.SpeRate==4&&!manual.SpeRateCalibrated&&
                manual.Outcome==SurvivalOutcome.Survived&&Near(manual.Dose,copy.time.speStartHour+4*(copy.time.durationHours-copy.time.speStartHour)),
                "Manual legacy dose-rate mode integrates its configured fixed rates independently of observed data");
        }
        finally{UnityEngine.Object.DestroyImmediate(copy);}
    }
    static IEnumerator Capture(string label)
    {
        foreach(int width in new[]{1280,1920})
        {
            SampleDiscoveryChecks.SetGameSize(width,width==1280?720:1080);yield return Frames(8);
            string path=Path.GetFullPath(Output+$"/{label}-{width}.png");ScreenCapture.CaptureScreenshot(path);yield return Frames(5);
            Check(File.Exists(path)&&new FileInfo(path).Length>50000,$"Actual scene capture: {label} {width}");
        }
    }
    static IEnumerator Briefing(SurvivalSession session, HudGameSettings language)
    {
        HudController.Active.Pause.StartMission();
        yield return Frames(4);
        var briefing = UnityEngine.Object.FindFirstObjectByType<RadiationBriefing>();
        var overlay = HudController.Active.Canvas.transform.Find("Radiation Briefing Overlay");
        Check(briefing && briefing.IsOpen && overlay && overlay.gameObject.activeInHierarchy,
            "Start Game opens the authored radiation briefing");
        if (!briefing || !briefing.IsOpen || !overlay) yield break;

        var run = session.Run;
        HudController.Active.Pause.Pause();
        Check(Time.timeScale == 0 && !InputHub.Active.GameplayAllowed && !HudController.Active.Pause.IsPaused &&
            Near(run.Hours, session.Scenario.start.elapsedHours) && Near(run.Dose, session.Scenario.start.dose),
            "Startup holds the clock, dose and gameplay input without opening the regular pause menu");
        var labels = overlay.GetComponentsInChildren<TMP_Text>();
        var selected = EventSystem.current.currentSelectedGameObject;
        Check(selected && selected.name == "Begin Mission", "UI submit targets Begin Mission by default");

        language.UseEnglish();
        Check(labels.Any(label => label.text.Contains("11.4664") && label.text.Contains("65.1237")) &&
            labels.Any(label => label.text.Contains("0.5229") && label.text.Contains("8:00")) &&
            labels.Any(label => label.text.Contains("remaining dose") && label.text.Contains("fixed once")) &&
            labels.Any(label => label.text.Contains("10%") && label.text.Contains("before mission end")) &&
            labels.Any(label => label.text.Contains("35%") && label.text.Contains("80%") && label.text.Contains("600")),
            "English presents observed means, pre-event rate, one-time remaining-dose calibration, event time and shielding");
        yield return Capture("briefing-en");
        Check(!labels.Any(label => label.isTextOverflowing), "English briefing fits its authored text areas");

        language.UseKorean();
        Check(labels.Any(label => label.text.Contains("관측 평균")) &&
            labels.Any(label => label.text.Contains("남은") && label.text.Contains("한 번")) &&
            labels.Any(label => label.text.Contains("CRaTER-D1D2") && label.text.Contains("2022-08-27")),
            "Korean briefing explains the one-time remaining-dose rate and observation provenance");
        yield return Capture("briefing-ko");
        Check(!labels.Any(label => label.isTextOverflowing), "Korean briefing fits its authored text areas");
        Check(Near(run.Hours, session.Scenario.start.elapsedHours) && Near(run.Dose, session.Scenario.start.dose),
            "Reading and switching language leave the clock and dose at their starting values");

        if (selected) ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        double before = run.Hours;
        yield return Frames(4);
        Check(!briefing.IsOpen && !overlay.gameObject.activeSelf && Time.timeScale > 0 &&
            InputHub.Active.GameplayAllowed && run.Hours > before && run.Dose > session.Scenario.start.dose,
            "Beginning the mission closes the briefing, restores gameplay and starts dose accumulation");
        briefing.Close();
    }
    static IEnumerator Cases()
    {
        Directory.CreateDirectory(Output);
        background=Application.runInBackground;Application.runInBackground=true;yield return Frames(8);
        var session=UnityEngine.Object.FindFirstObjectByType<SurvivalSession>();var rules=session.Scenario;Pure(rules);
        var player=UnityEngine.Object.FindFirstObjectByType<MoonBasePlayer>();var power=UnityEngine.Object.FindFirstObjectByType<BasePowerController>();var shield=UnityEngine.Object.FindFirstObjectByType<ActiveRadiationShield>();
        var warning=UnityEngine.Object.FindFirstObjectByType<SpeHud>();var result=UnityEngine.Object.FindFirstObjectByType<FinalEvaluation>();
        var pause=UnityEngine.Object.FindFirstObjectByType<HudPauseManager>();var language=warning.language;
        const string key="SpaceDilemma.Language";hadLanguage=PlayerPrefs.HasKey(key);oldLanguage=PlayerPrefs.GetInt(key);
        yield return Briefing(session, language);
        language.UseKorean();
        var cc=player.GetComponent<CharacterController>();player.GetComponent<ThirdPersonController>().enabled=false;cc.enabled=false;
        Check(player.rover.TryDismount(cc,out var approach),"Actual rover provides an outdoor exposure test position");player.transform.position=approach;Physics.SyncTransforms();power.SetShield(0);yield return Frames(3);
        Check(!session.IsInside,"The actual scene identifies the exposure test position as outdoors");session.enabled=false;
        double initialHour=session.Run.Hours,initialDose=session.Run.Dose;
        double atEvent=initialDose+rules.radiation.NormalRate*(rules.time.speStartHour-initialHour);
        double predictedRate=rules.radiation.speRemainingDoseMultiplier*(rules.radiation.lethalDose-atEvent)/
            (rules.time.durationHours-rules.time.speStartHour);
        double predictedHour=rules.time.speStartHour+(rules.time.durationHours-rules.time.speStartHour)/
            (double)rules.radiation.speRemainingDoseMultiplier;
        session.Run.Advance(479*rules.time.gameHoursPerSecond-session.Run.Hours,false,shield);yield return Frames(3);
        Check(!session.Run.SpeActive&&!warning.panel.activeSelf&&session.Run.Outcome==SurvivalOutcome.Running,"Actual scene is alive with no alert at play 7:59");
        Check(Near(session.Run.Rate,rules.radiation.NormalRate),"Actual session uses the calibrated pre-event rate");
        pause.Pause();session.enabled=true;double hour=session.Run.Hours,dose=session.Run.Dose;yield return Wait(.15f);
        Check(session.Run.Hours==hour&&session.Run.Dose==dose,"Pause stops both the eight-minute timer and radiation accumulation");session.enabled=false;pause.Resume();yield return Frames(3);
        session.Run.Advance(rules.time.speStartHour-session.Run.Hours,false,shield);yield return Frames(4);
        Check(session.Run.SpeActive&&warning.AlertCount==1&&warning.panel.activeInHierarchy,"Actual 8:00 boundary raises the existing SPE warning once");
        Check(session.Run.SpeRateCalibrated&&Near(session.Run.SpeCalibrationHour,rules.time.speStartHour)&&
            Near(session.Run.SpeStartDose,atEvent)&&Near(session.Run.SpeOutdoorRate,predictedRate)&&
            Near(session.Run.Rate,predictedRate)&&Near(session.Run.Dose,atEvent),
            "The actual session snapshots exposure and fixes its remaining-dose SPE rate at the eight-minute boundary");
        Check(session.Scenario!=AssetDatabase.LoadAssetAtPath<ScenarioRules>(RadLabScenarioSetup.RulesPath),"Runtime scenario remains a copy of the source definition asset");
        yield return Capture("spe-start-ko");
        session.Run.Advance(100,false,shield);yield return Frames(5);
        Check(session.Run.Outcome==SurvivalOutcome.Died&&Near(session.Run.Dose,600)&&Near(session.Run.Hours,predictedHour)&&
            session.Run.Hours<rules.time.durationHours&&Near(session.Run.Hours/rules.time.gameHoursPerSecond,589.0909,.002),
            "Actual outdoor exposure reaches 600 at approximately 9:49.09 before mission end");
        Check(result.popup.activeInHierarchy&&result.title.text=="게임 오버"&&Time.timeScale==0&&!InputHub.Active.GameplayAllowed,"The calibrated death shows Game Over and keeps terminal pause/input blocking");
        Check(result.Report.Dose==600&&Near(result.Report.Hours,predictedHour)&&result.Report.Hours<48&&result.CaptureCount==1,
            "Final report captures the pre-end radiation death exactly once");
        File.WriteAllText(Output+"/actual-ending.txt",$"Initial hours={initialHour:R}, initial dose={initialDose:R}\nSPE game hour={rules.time.speStartHour:R}\nCalibration hour={session.Run.SpeCalibrationHour:R}\nCalibration dose={session.Run.SpeStartDose:R}\nFixed SPE outdoor rate={session.Run.SpeOutdoorRate:R}\nActual ending game hour={session.Run.Hours:R}\nActual ending play seconds={session.Run.Hours/rules.time.gameHoursPerSecond:R}\nDose={session.Run.Dose:R}\n");
        yield return Capture("game-over-ko");
        Check(errors.Count==0,"No new Unity errors: "+string.Join(" | ",errors));
    }
    static void Finish()
    {
        SessionState.SetBool("RadLabCalibration.Test",false);while(routines.Count>0)(routines.Pop() as IDisposable)?.Dispose();Application.logMessageReceived-=Log;
        Application.runInBackground=background;SampleDiscoveryChecks.RestoreGameSize();const string key="SpaceDilemma.Language";
        if(hadLanguage)PlayerPrefs.SetInt(key,oldLanguage);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
        Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/play-checks.txt",checks.Concat(new[]{$"RESULT checks={checks.Count} failures={failures}"}));EditorApplication.isPlaying=false;
    }
}
