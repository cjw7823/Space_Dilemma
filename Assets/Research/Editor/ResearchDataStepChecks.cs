using System;
using System.Collections;
using System.Linq;
using SpaceDilemma.Hud;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
using UnityEngine;

static class ResearchDataStepChecks
{
    public static void BeforeInteractions(Action<bool,string> check)
    {
        var progress = UnityEngine.Object.FindFirstObjectByType<ResearchProgress>();
        check(progress && progress.Ledger != null && progress.Devices.Length == 9, "STEP 3 starts with 9 authored research devices");
        if (!progress || progress.Ledger == null) return;
        check(Enum.GetValues(typeof(ResearchMission)).Cast<ResearchMission>().All(m =>
            progress.Ledger.Data(m) == 0 && progress.Ledger.RunningDevices(m) == 0),
            "Uncollected, uninstalled and unmanaged sites produce no data");
        PureChecks(check);
    }

    static void PureChecks(Action<bool,string> check)
    {
        var rules = ScriptableObject.CreateInstance<ResearchDataRules>();
        try
        {
            var ledger = new ResearchLedger(rules, 0);
            ledger.AdvanceTo(5);
            check(ledger.Data(ResearchMission.Samples) == 0, "An empty powered cold store produces no sample credit");
            ledger.SetState(ResearchMission.Samples, 0, true, true, true);
            ledger.AdvanceTo(7);
            check(Near(ledger.Get(ResearchMission.Samples,0).Data,2) &&
                Near(ledger.Get(ResearchMission.Samples,0).ValidGameHours,2), "Sample credit starts at storage and records valid preservation time");
            ledger.SetState(ResearchMission.Samples,0,true,false,true);
            ledger.AdvanceTo(9);
            check(Near(ledger.Get(ResearchMission.Samples,0).Data,2) &&
                ledger.Get(ResearchMission.Samples,0).Valid, "Cooling outage pauses credit without destroying the sample or saved credit");
            ledger.SetState(ResearchMission.Samples,0,true,true,false);
            ledger.AdvanceTo(10);
            check(Near(ledger.Get(ResearchMission.Samples,0).Data,2), "The sample validity hook pauses future credit");
            ledger.SetState(ResearchMission.Samples,0,true,true,true);
            ledger.AdvanceTo(11);
            check(Near(ledger.Get(ResearchMission.Samples,0).Data,3), "Valid preservation resumes after cooling and quality recover");

            var fine = new ResearchLedger(rules,0);
            var coarse = new ResearchLedger(rules,0);
            for (int i=0;i<3;i++)
            {
                fine.SetState(ResearchMission.Instruments,i,true,true,true);
                coarse.SetState(ResearchMission.Instruments,i,true,true,true);
            }
            for(int i=1;i<=2400;i++) fine.AdvanceTo(i*.01);
            coarse.AdvanceTo(24);
            check(Near(fine.Data(ResearchMission.Instruments),72) &&
                Near(fine.Data(ResearchMission.Instruments),coarse.Data(ResearchMission.Instruments)) &&
                Near(fine.Percent(ResearchMission.Instruments),100), "Different frame sizes and time speeds produce the same mission score");
            coarse.AdvanceTo(48);
            check(Near(coarse.Data(ResearchMission.Instruments),72) && Near(coarse.Percent(ResearchMission.Instruments),100),
                "Per-device data capacity and mission achievement both cap at their configured limit");

            var early = new ResearchLedger(rules,0);
            var late = new ResearchLedger(rules,0);
            early.SetState(ResearchMission.Plants,0,true,true,true);
            early.AdvanceTo(5);
            late.AdvanceTo(4);
            late.SetState(ResearchMission.Plants,0,true,true,true);
            late.AdvanceTo(5);
            check(Near(early.Get(ResearchMission.Plants,0).Data,5) && Near(late.Get(ResearchMission.Plants,0).Data,1),
                "Starting a plant plot later accumulates less valid data");
            early.SetState(ResearchMission.Plants,0,true,false,true);
            early.AdvanceTo(6);
            early.SetState(ResearchMission.Plants,0,true,true,true);
            early.AdvanceTo(7);
            check(Near(early.Get(ResearchMission.Plants,0).Data,6), "Plant shutdown preserves valid data and restart resumes accumulation");

            rules.instruments.devices[0].unitsPerGameHour=4;
            rules.instruments.devices[0].maximumData=100;
            rules.instruments.devices[0].contribution=2;
            rules.instruments.targetData=32;
            rules.instruments.maximumPercent=80;
            var customized = new ResearchLedger(rules,0);
            customized.SetState(ResearchMission.Instruments,0,true,true,true);
            customized.AdvanceTo(2);
            check(Near(customized.Data(ResearchMission.Instruments),16) && Near(customized.Percent(ResearchMission.Instruments),40),
                "Per-device rate, contribution, mission target and percent maximum are configurable");
            customized.AdvanceTo(2);
            customized.AdvanceTo(1);
            check(Near(customized.Data(ResearchMission.Instruments),16), "Duplicate or older time callbacks never duplicate credit");
            customized.Finish();
            customized.AdvanceTo(10);
            customized.SetState(ResearchMission.Instruments,0,true,false,true);
            check(Near(customized.Data(ResearchMission.Instruments),16) && customized.Get(ResearchMission.Instruments,0).Powered,
                "Completed run freezes data and state for final evaluation");
        }
        finally { UnityEngine.Object.DestroyImmediate(rules); }
    }

    public static IEnumerator Run(SurvivalSession session, HudPauseManager pause, Action<bool,string> check)
    {
        var progress = UnityEngine.Object.FindFirstObjectByType<ResearchProgress>();
        if (!progress || progress.Ledger == null) yield break;
        var ledger = progress.Ledger;
        check(Enum.GetValues(typeof(ResearchMission)).Cast<ResearchMission>().All(m => ledger.RunningDevices(m)==3),
            "E-key completions activate all three devices in each mission");
        var device = progress.Devices.Single(d => d.mission==ResearchMission.Instruments && d.index==0);
        var power=UnityEngine.Object.FindFirstObjectByType<SpaceDilemma.Power.BasePowerController>();
        bool powerWasEnabled=power && power.enabled;
        if(power) power.enabled=false; // Isolate the STEP 3 device API from STEP 4 ownership.
        bool initialPower=device.powerAvailable;
        try
        {
            double before=ledger.Get(ResearchMission.Instruments,0).Data;
            double otherBefore=ledger.Get(ResearchMission.Instruments,1).Data;
            device.SetPowered(false);
            session.Run.Advance(1,true);
            check(Near(ledger.Get(ResearchMission.Instruments,0).Data,before) &&
                ledger.Get(ResearchMission.Instruments,1).Data>otherBefore &&
                ledger.RunningDevices(ResearchMission.Instruments)==2,
                "One authored device can switch OFF while the other two keep collecting at full rate");
            device.SetPowered(true);
            session.Run.Advance(.5,true);
            check(ledger.Get(ResearchMission.Instruments,0).Data>before && ledger.RunningDevices(ResearchMission.Instruments)==3,
                "Switching power ON resumes that device through the session clock");
            var ui = UnityEngine.Object.FindFirstObjectByType<ResearchHud>();
            pause.Pause();
            double score=ledger.Data(ResearchMission.Instruments);
            yield return Wait(.25f);
            check(Near(ledger.Data(ResearchMission.Instruments),score), "Pause stops research accumulation as well as radiation");
            var menu = pause.Popup.GetComponentInChildren<HudGameSettings>(true);
            bool english=menu.English;
            const string languageKey="SpaceDilemma.Language";
            bool hadPreference=PlayerPrefs.HasKey(languageKey);
            int oldPreference=PlayerPrefs.GetInt(languageKey);
            try
            {
                pause.ShowSettings();
                menu.UseKorean();
                yield return Wait(.1f);
                check(ui.GetComponentInParent<Canvas>().GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Any(t=>t.name=="Research Gauge Title" && t.text=="연구 달성도"), "Achievement HUD switches to Korean");
                menu.UseEnglish();
                yield return Wait(.1f);
                check(ui.GetComponentInParent<Canvas>().GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Any(t=>t.name=="Research Gauge Title" && t.text=="RESEARCH"),
                    "Achievement HUD switches back to English without Korean text");
            }
            finally
            {
                if(english) menu.UseEnglish(); else menu.UseKorean();
                if(hadPreference) PlayerPrefs.SetInt(languageKey,oldPreference); else PlayerPrefs.DeleteKey(languageKey);
                PlayerPrefs.Save();
                pause.Resume();
            }
        }
        finally { device.SetPowered(initialPower); if(power) { power.enabled=powerWasEnabled; power.Apply(); } }
    }

    public static void AfterEnd(Action<bool,string> check)
    {
        var progress=UnityEngine.Object.FindFirstObjectByType<ResearchProgress>();
        if(!progress || progress.Ledger==null)return;
        double data=progress.Ledger.Data(ResearchMission.Samples);
        progress.Ledger.AdvanceTo(1000);
        check(progress.Ledger.IsFinished && Near(data,progress.Ledger.Data(ResearchMission.Samples)),
            "Actual survival ending freezes research totals for evaluation");
    }

    static bool Near(double a,double b)=>Math.Abs(a-b)<.00001;
    static IEnumerator Wait(float seconds)
    {
        float until=Time.realtimeSinceStartup+seconds;
        do {yield return null;}while(Time.realtimeSinceStartup<until);
    }
}
