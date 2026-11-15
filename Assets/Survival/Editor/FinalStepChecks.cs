using System;
using System.Collections;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Power;
using SpaceDilemma.Research;
using SpaceDilemma.Results;
using SpaceDilemma.Survival;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

static class FinalStepChecks
{
    static bool Near(double a,double b)=>Math.Abs(a-b)<.00001;
    static IEnumerator Wait(float seconds) { float until=Time.realtimeSinceStartup+seconds; do {yield return null;}while(Time.realtimeSinceStartup<until); }
    sealed class BudgetShield : IRadiationShield
    {
        public PowerBudget power;
        readonly ShieldEfficiencyModel model=new ShieldEfficiencyModel(.8);
        public double Transmission(bool inside,bool spe)=>model.Transmission(inside,spe,power.ShieldActualOutput);
    }
    sealed class Scenario : IDisposable
    {
        public readonly PowerRules powerRules=ScriptableObject.CreateInstance<PowerRules>();
        public readonly ResearchDataRules dataRules=ScriptableObject.CreateInstance<ResearchDataRules>();
        public readonly SamplePreservationRules preservation=ScriptableObject.CreateInstance<SamplePreservationRules>();
        public readonly EvaluationRules weights=ScriptableObject.CreateInstance<EvaluationRules>();
        public readonly PowerBudget power;
        public readonly ResearchLedger ledger;
        public readonly SurvivalRun run;
        readonly BudgetShield shield;
        readonly bool[,] active=new bool[3,3];
        public Scenario(bool all=false,Action<PowerRules> configurePower=null,Action<ResearchDataRules> configureData=null)
        {
            configurePower?.Invoke(powerRules); configureData?.Invoke(dataRules);
            power=new PowerBudget(powerRules); ledger=new ResearchLedger(dataRules,0,preservation);
            run=new SurvivalRun(48,24,.1,3,50,.35); shield=new BudgetShield{power=power};
            run.TimeChanged+=ledger.AdvanceTo; run.Ended+=_=>ledger.Finish();
            for(int m=0;m<3;m++)for(int d=0;d<3;d++)active[m,d]=all;
        }
        public void Activate(ResearchMission mission,int device) => active[(int)mission,device]=true;
        public void Advance(double hours,bool inside=true)
        {
            for(int m=0;m<3;m++)for(int d=0;d<3;d++)ledger.SetState((ResearchMission)m,d,active[m,d],power.Powered(m,d),true);
            run.Advance(hours,inside,shield);
        }
        public FinalReport Snapshot()=>new FinalReport(run,ledger,weights);
        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(powerRules); UnityEngine.Object.DestroyImmediate(dataRules);
            UnityEngine.Object.DestroyImmediate(preservation); UnityEngine.Object.DestroyImmediate(weights);
        }
    }
    public static void Pure(Action<bool,string> check)
    {
        var configured = new ResearchWeights(40, 35, 25);
        var relative = new ResearchWeights(8, 7, 5);
        check(Near(configured.Samples + configured.Instruments + configured.Plants, 1) &&
            Near(configured.OverallPercent(50, 80, 20), 53) &&
            Near(relative.OverallPercent(50, 80, 20), 53),
            "Configured research proportions total 100% and 40/35/25 score 50/80/20 as 53%");
        check(Near(new ResearchWeights(0, 100, 0).OverallPercent(100, 37, 100), 37),
            "A zero-weight mission contributes nothing to the aggregate");
        using(var empty=new Scenario())
        {
            bool rejected=false; try { empty.Snapshot(); } catch(InvalidOperationException){rejected=true;}
            check(rejected,"STEP 6 rejects a final report while the mission is still running");
            empty.Advance(100); var r=empty.Snapshot();
            check(r.Outcome==SurvivalOutcome.Survived && Near(r.Hours,48) && Near(r.OverallPercent,0) && r.StoredSamples==0,"No missions: 48h completion reports survival and zero research");
            check(Near(r.AvoidedSpeDose,0) && Near(r.ShieldedSpeHours,0) && Near(r.MeanIndoorReduction,0),"Unpowered active shielding earns no protection history credit");
        }
        using(var full=new Scenario(true,configureData:rules=>
        { foreach(ResearchMission m in Enum.GetValues(typeof(ResearchMission)))foreach(var d in rules.For(m).devices)d.unitsPerGameHour=2; }))
        {
            full.Advance(12);
            check(!full.run.SpeActive && Enum.GetValues(typeof(ResearchMission)).Cast<ResearchMission>().All(m=>Near(full.ledger.Percent(m),100)),"All three missions can reach 100% before SPE with configurable test rates");
            full.power.SetShield(1); full.Advance(36); var r=full.Snapshot();
            check(r.Outcome==SurvivalOutcome.Survived && Near(r.OverallPercent,100),"Completed research remains complete when later power is redirected to shielding");
            check(Near(r.IndoorSpeHours,24) && Near(r.ShieldedSpeHours,24) && Near(r.MeanIndoorReduction,.8) && Near(r.AvoidedSpeDose,20.16),"Shield history reports actual indoor SPE duration, time-weighted reduction and avoided dose");
            full.power.SetShield(0); full.Advance(100);
            check(Near(r.OverallPercent,100) && Near(r.Hours,48) && Near(r.MeanIndoorReduction,.8),"A captured report never changes after later power/time operations");
        }
        using(var partial=new Scenario())
        {
            for(int i=0;i<3;i++)partial.Activate(ResearchMission.Instruments,i);
            partial.Advance(48); var r=partial.Snapshot();
            check(Near(r.MissionPercent(ResearchMission.Instruments),100) && Near(r.MissionPercent(ResearchMission.Samples),0) && Near(r.OverallPercent,100.0/3),"One completed mission uses the default equal three-mission average");
            partial.weights.samplesWeight=1; partial.weights.instrumentsWeight=2; partial.weights.plantsWeight=1;
            check(Near(partial.Snapshot().OverallPercent,50) && Near(r.OverallPercent,100.0/3),"Configurable weights change new evaluations while the previous snapshot stays immutable");
            partial.weights.samplesWeight=partial.weights.instrumentsWeight=partial.weights.plantsWeight=0;
            check(Near(partial.Snapshot().OverallPercent,100.0/3),"All-zero weights safely fall back to equal weights");
        }
        using(var shieldOnly=new Scenario(true))
        {
            for(int f=0;f<3;f++)shieldOnly.power.SetAllocation(f,0);
            shieldOnly.power.SetShield(1); shieldOnly.Advance(48); var r=shieldOnly.Snapshot();
            check(Near(r.OverallPercent,0) && r.Outcome==SurvivalOutcome.Survived && Near(r.AvoidedSpeDose,20.16),"All discretionary research cut for shielding: survival and lost new research are both reported");
            check(Near(shieldOnly.power.EssentialSupplied,250) && shieldOnly.power.Consumption<=shieldOnly.power.Available,"Shield-first scenario still protects essential life support and the total W bound");
        }
        using(var coldFirst=new Scenario(true))
        using(var plantsFirst=new Scenario(true,p=>{p.samples.priority=2;p.plants.priority=0;}))
        {
            coldFirst.power.SetShield(1); plantsFirst.power.SetShield(1); coldFirst.Advance(48); plantsFirst.Advance(48);
            var a=coldFirst.Snapshot(); var b=plantsFirst.Snapshot();
            check(a.MissionPercent(ResearchMission.Samples)>b.MissionPercent(ResearchMission.Samples) &&
                b.MissionPercent(ResearchMission.Plants)>a.MissionPercent(ResearchMission.Plants),"Different facility shutdown priorities produce different mission portfolios");
            check(a.UsableSamples==3 && b.UsableSamples==0,"Long cooling outages are reflected in final usable sample counts");
        }
        using(var outside=new Scenario(true))
        {
            outside.Advance(24,false); // Earn research before redirecting power away from its devices.
            outside.power.SetShield(1); outside.Advance(100,false); var r=outside.Snapshot();
            check(r.Outcome==SurvivalOutcome.Died && r.Hours<48 && Near(r.Dose,50) && Near(r.AvoidedSpeDose,0),"Never returning after SPE reaches danger before 48h despite indoor shield power");
            check(Near(r.OverallPercent,100),"Death is reported separately and does not erase already-earned research scores");
        }
        using(var history=new Scenario())
        {
            history.Advance(24); history.Advance(12); history.power.SetShield(1); history.Advance(12); var r=history.Snapshot();
            check(Near(r.ShieldedSpeHours,12) && Near(r.MeanIndoorReduction,.4) && Near(r.AvoidedSpeDose,10.08),"A late shield start records history rather than displaying the last 100% output as the whole run");
        }
        var rules=ScriptableObject.CreateInstance<PowerRules>();
        try
        {
            var p=new PowerBudget(rules); p.SetShield(1); var shield=new BudgetShield{power=p};
            var coarse=new SurvivalRun(48,24,.1,3,1000,.35); var fine=new SurvivalRun(48,24,.1,3,1000,.35);
            coarse.Advance(25,true,shield); for(int i=0;i<2500;i++)fine.Advance(.01,true,shield);
            check(Near(coarse.AvoidedSpeDose,fine.AvoidedSpeDose) && Near(coarse.ShieldedSpeHours,1),"Shield telemetry is frame-independent and splits the 24h boundary correctly");
            var lethal=new SurvivalRun(48,24,.1,3,1,.35); lethal.Advance(100,true,shield);
            check(Near(lethal.ShieldedSpeHours,(1-.84)/.21) && lethal.Outcome==SurvivalOutcome.Died,"Early death records only actual protected time before the danger threshold");
        }
        finally { UnityEngine.Object.DestroyImmediate(rules); }
    }

    public static IEnumerator Run(HudController hud,SurvivalSession session,bool death,Action<Key[]> keys,Action<bool,string> check,Action<HudController,int,int,string> capture)
    {
        Pure(check);
        var evaluation=session.GetComponent<FinalEvaluation>(); var power=UnityEngine.Object.FindFirstObjectByType<BasePowerController>();
        var shield=UnityEngine.Object.FindFirstObjectByType<ActiveRadiationShield>();
        check(evaluation && !evaluation.popup.activeSelf && evaluation.Report==null,"Result UI and evaluation are authored but hidden during gameplay");
        if(!evaluation)yield break;
        var language=evaluation.language; bool english=language.English;
        const string key="SpaceDilemma.Language"; bool hadPreference=PlayerPrefs.HasKey(key); int preference=PlayerPrefs.GetInt(key);
        try
        {
            language.UseEnglish(); power.SetShield(1); keys(Array.Empty<Key>());
            session.Run.Advance(100,!death,shield); yield return Wait(.2f);
            var report=evaluation.Report;
            check(report!=null && report.Outcome==(death?SurvivalOutcome.Died:SurvivalOutcome.Survived) && evaluation.CaptureCount==1,"Actual scene ending captures exactly one "+(death?"death":"survival")+" report");
            check(evaluation.popup.activeSelf && Time.timeScale==0 && !InputHub.Active.GameplayAllowed && Cursor.lockState==CursorLockMode.None,"Ending displays results, freezes time, blocks gameplay and unlocks the cursor");
            check(death?report.Hours<48:Near(report.Hours,48),"Result elapsed time matches the actual terminal boundary");
            check(!evaluation.popup.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Any(c=>c>127 && char.IsLetter(c))),"English result UI contains no non-English letters");
            check(evaluation.missionFills.Select((image,i)=>Near(image.fillAmount,report.MissionPercent((ResearchMission)i)/100)).All(b=>b),"Three authored result bars match frozen mission percentages");
            var liveGauge = hud.Canvas.transform.Find("Gameplay HUD/Research Gauge/Research Percent")?.GetComponent<TMP_Text>();
            string gaugePercent = report.OverallPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
            check(liveGauge && liveGauge.text == gaugePercent,
                "The HUD research gauge and final report agree on the ending score");
            check(Enumerable.Range(0,evaluation.quitButton.onClick.GetPersistentEventCount()).Any(i=>evaluation.quitButton.onClick.GetPersistentTarget(i)==evaluation && evaluation.quitButton.onClick.GetPersistentMethodName(i)==nameof(FinalEvaluation.QuitGame)),"Result Quit Game button is persistently wired without exiting the test process");
            string prefix=death?"result-death":"result-survival";
            capture(hud,1920,1080,prefix+"-en-1920.png"); yield return Wait(.15f); capture(hud,1280,720,prefix+"-en-1280.png"); yield return Wait(.15f);
            var rect=(RectTransform)evaluation.settingsButton.transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var hits=new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
            check(hits.Count>0 && hits[0].gameObject==evaluation.settingsButton.gameObject,"Result settings button receives the top pointer raycast");
            evaluation.settingsButton.onClick.Invoke(); yield return Wait(.15f);
            check(hud.Pause.IsPaused && language.gameObject.activeInHierarchy && !evaluation.popup.activeSelf,"Result settings button opens existing Settings without unfreezing the ended mission");
            language.UseKorean(); hud.Pause.BackFromSettings(); hud.Pause.Resume(); yield return Wait(.15f);
            check(evaluation.popup.activeSelf && evaluation.title.text==(death?"게임 오버":"지구 무사 컴백! 축하합니다") && ReferenceEquals(report,evaluation.Report) && Time.timeScale==0,"Returning from Settings localizes the same frozen result in Korean");
            capture(hud,1920,1080,prefix+"-ko-1920.png"); yield return Wait(.15f); capture(hud,1280,720,prefix+"-ko-1280.png");
            keys(new[]{Key.Escape}); yield return Wait(.15f); keys(Array.Empty<Key>()); yield return Wait(.15f);
            check(hud.Pause.IsPaused && !evaluation.popup.activeSelf,"ESC can open the menu over an ended result");
            keys(new[]{Key.Escape}); yield return Wait(.15f); keys(Array.Empty<Key>()); yield return Wait(.15f);
            check(!hud.Pause.IsPaused && evaluation.popup.activeSelf && Time.timeScale==0 && !InputHub.Active.GameplayAllowed,"Closing ESC menu restores results without resuming the mission");
            AfterEnd(check);
        }
        finally
        {
            keys(Array.Empty<Key>()); if(hud.Pause.IsPaused)hud.Pause.Resume();
            if(english)language.UseEnglish();else language.UseKorean();
            if(hadPreference)PlayerPrefs.SetInt(key,preference);else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
        }
    }
    public static void AfterEnd(Action<bool,string> check)
    {
        var evaluation=UnityEngine.Object.FindFirstObjectByType<FinalEvaluation>();
        if(!evaluation)return;
        var report=evaluation.Report;
        check(report!=null && evaluation.CaptureCount==1,"Terminal evaluation exists even when the HUD has already been destroyed");
        if(report==null)return;
        check(Near(report.OverallPercent, evaluation.Weights.OverallPercent(evaluation.research.Ledger)),
            "The frozen result uses the same weighted score as the gameplay HUD");
        double score=report.OverallPercent,dose=report.Dose,hours=report.Hours;
        evaluation.session.Run.Advance(100,true); evaluation.research.Ledger.AdvanceTo(1000);
        check(ReferenceEquals(report,evaluation.Report) && evaluation.CaptureCount==1 && Near(score,report.OverallPercent) && Near(dose,report.Dose) && Near(hours,report.Hours),"Duplicate time/ending attempts cannot alter or duplicate final results");
    }
}
