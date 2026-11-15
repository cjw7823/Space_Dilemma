using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Power;
using SpaceDilemma.Research;
using SpaceDilemma.Results;
using SpaceDilemma.Survival;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
static class EndingSacrificeChecks
{
    const string Output="QA/EndingSacrifice";
    static readonly Stack<IEnumerator> routines=new();
    static readonly List<string> checks=new(),errors=new();
    static int lastFrame=-1,failures;
    static float deadline;
    static bool background;
    static EndingSacrificeChecks(){EditorApplication.update+=Update;}
    [MenuItem("Tools/Space HUD/Validate Irreversible Research Ending")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        SessionState.SetBool("EndingSacrifice.Test",true);EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
    static void Log(string text,string trace,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+trace);}
    static void Update()
    {
        if(!SessionState.GetBool("EndingSacrifice.Test",false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;
        lastFrame=Time.frameCount;
        try
        {
            if(routines.Count==0)
            {checks.Clear();errors.Clear();failures=0;deadline=Time.realtimeSinceStartup+90;Application.logMessageReceived+=Log;routines.Push(Cases());}
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Ending validation timed out.");
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
    static FinalReport MakeReport(ResearchDataRules rules,params (int mission,int device)[] abandoned)
    {
        var ledger=new ResearchLedger(rules,0);
        foreach(var item in abandoned)ledger.SetShieldAbandoned((ResearchMission)item.mission,item.device,true);
        var run=new SurvivalRun(48,24,0,0,50,.35);run.Advance(48,true);ledger.Finish();
        return new FinalReport(run,ledger,1,1,1);
    }
    static void Pure()
    {
        FinalStepChecks.Pure(Check);
        var p=ScriptableObject.CreateInstance<PowerRules>();
        var data=ScriptableObject.CreateInstance<ResearchDataRules>();
        try
        {
            p.availableWatts=10000;
            for(int m=0;m<3;m++)for(int d=0;d<3;d++)
            {
                var b=new PowerBudget(p);var history=new ShieldPowerTradeoff();history.Observe(b,false);
                b.SetShield(1);b.SetDevice(m,d,false);history.Observe(b,true);b.LockShieldSacrifices(history);
                Check(b.Abandoned(m,d),$"SPE/shield shutdown records device {m}/{d}");
                b.SetShield(0);b.SetAllocation(m,b.Maximum(m));b.SetDevice(m,d,true);
                Check(!b.Powered(m,d)&&!b.RequestedOn(m,d)&&b.Abandoned(m,d),$"Restoring allocation, switch and shield power cannot restart device {m}/{d}");
                Check(b.Powered(m,(d+1)%3)&&!b.Abandoned(m,(d+1)%3),$"Other devices in mission {m} keep operating");
            }
            var ordinary=new PowerBudget(p);var h=new ShieldPowerTradeoff();h.Observe(ordinary,false);
            ordinary.SetDevice(2,0,false);h.Observe(ordinary,false);ordinary.LockShieldSacrifices(h);ordinary.SetDevice(2,0,true);
            Check(!ordinary.Abandoned(2,0)&&ordinary.Powered(2,0),"Ordinary shutdown outside SPE remains reversible");
            var pending=new PowerBudget(p);var pendingHistory=new ShieldPowerTradeoff();pendingHistory.Observe(pending,false);
            pending.SetDevice(1,0,false);pendingHistory.Observe(pending,true);pending.LockShieldSacrifices(pendingHistory);pending.SetDevice(1,0,true);
            Check(pending.Abandoned(1,0)&&!pending.Powered(1,0),"Manual dilemma shutdown is irreversible before increasing the shield slider");
            ordinary.SetDevice(2,0,false);h.Observe(ordinary,false);ordinary.SetShield(1);h.Observe(ordinary,true);ordinary.LockShieldSacrifices(h);
            Check(!ordinary.Abandoned(2,0),"A device already off before SPE is not falsely attributed to the dilemma");
            ordinary.SetDevice(2,0,true);ordinary.SetDevice(2,0,false,true);ordinary.SetDevice(2,0,true);
            Check(ordinary.Abandoned(2,0)&&!ordinary.Powered(2,0),"A device enabled during SPE also locks on its later explicit shutdown");
            var allocationCut=new PowerBudget(p);allocationCut.SetAllocation(1,0,true);allocationCut.SetAllocation(1,allocationCut.Maximum(1));
            Check(Enumerable.Range(0,3).All(d=>allocationCut.Abandoned(1,d)&&!allocationCut.Powered(1,d)),"SPE allocation cuts irreversibly lock exactly the devices losing supply");
            var ledger=new ResearchLedger(data,0);ledger.SetState(ResearchMission.Plants,1,true,true,true);ledger.AdvanceTo(2);
            ledger.SetShieldAbandoned(ResearchMission.Plants,1,true);ledger.SetState(ResearchMission.Plants,1,true,true,true);ledger.AdvanceTo(10);
            Check(ledger.Get(ResearchMission.Plants,1).Data==2&&!ledger.Get(ResearchMission.Plants,1).Powered,"Forfeited research retains earned data and rejects direct power-state restoration");
            var single=MakeReport(data,(2,1));var text=EndingMessage.Survival(single,false);
            Check(text.Contains("식물 재배 장치 2번")&&text.Contains("식물 재배 미션")&&!text.Contains("측정장비"),"Single-device template names only the abandoned source, device and mission");
            var multiple=MakeReport(data,(0,0),(0,2),(1,1),(2,2));text=EndingMessage.Survival(multiple,false);
            Check(text.Contains("냉각 장치 1·3번")&&text.Contains("측정장비 2번")&&text.Contains("식물 재배 장치 3번"),"Multiple sacrifices group device numbers and include all affected missions");
            Check(!EndingMessage.Survival(MakeReport(data),false).Contains("완수할 수 없었습니다"),"No sacrifice omits the failure paragraph, even with unfinished missions");
            var completed=new ResearchLedger(data,0);
            for(int d=0;d<3;d++)completed.SetState(ResearchMission.Plants,d,true,true,true);
            completed.AdvanceTo(24);completed.SetShieldAbandoned(ResearchMission.Plants,0,true);completed.Finish();
            var ended=new SurvivalRun(48,24,0,0,50,.35);ended.Advance(48,true);
            var report=new FinalReport(ended,completed,1,1,1);
            Check(report.MissionPercent(ResearchMission.Plants)==100&&!report.ShieldInterrupted(ResearchMission.Plants),"Research completed before forfeiture is never called an incomplete mission");
            Check(!EndingMessage.Survival(multiple,true).Any(c=>c>127&&char.IsLetter(c)),"English sacrifice message contains no Korean text");
            var fresh=new PowerBudget(p);Check(!fresh.Abandoned(2,1)&&fresh.Powered(2,1),"A new run resets forfeitures without modifying rule assets");
        }
        finally{UnityEngine.Object.DestroyImmediate(p);UnityEngine.Object.DestroyImmediate(data);}
    }
    static IEnumerator Capture(FinalEvaluation evaluation,string label)
    {
        foreach(int width in new[]{1280,1920})
        {
            SampleDiscoveryChecks.SetGameSize(width,width==1280?720:1080);yield return Frames(8);
            Canvas.ForceUpdateCanvases();evaluation.note.ForceMeshUpdate();
            Check(!evaluation.note.isTextOverflowing&&evaluation.note.preferredHeight<=evaluation.note.rectTransform.rect.height+.1f,$"{label} note fits at {width}");
            string path=Path.GetFullPath(Output+$"/{label}-{width}.png");
            ScreenCapture.CaptureScreenshot(path);yield return Frames(5);
            Check(File.Exists(path),$"Actual Game view captured: {label}-{width}");
        }
    }
    static IEnumerator Cases()
    {
        background=Application.runInBackground;Application.runInBackground=true;
        Directory.CreateDirectory(Output);yield return Frames(8);Pure();
        HudPlayValidation.EnterGameplay();
        var evaluation=UnityEngine.Object.FindFirstObjectByType<FinalEvaluation>();
        var session=evaluation.session;var power=UnityEngine.Object.FindFirstObjectByType<BasePowerController>();
        var shield=UnityEngine.Object.FindFirstObjectByType<ActiveRadiationShield>();
        session.enabled=false;session.Run.Advance(session.Scenario.time.speStartHour-session.Run.Hours,true,shield);
        power.Apply();power.SetShield(1);
        for(int m=0;m<3;m++)power.SetAllocation(m,0);
        Check(Enumerable.Range(0,3).All(m=>Enumerable.Range(0,3).All(d=>power.Budget.Abandoned(m,d))),"Actual scene locks each deliberately sacrificed device");
        power.SetShield(0);
        for(int m=0;m<3;m++){power.SetAllocation(m,(float)power.Budget.Maximum(m));for(int d=0;d<3;d++)power.SetDevice(m,d,true);}
        yield return Frames(4);
        Check(power.Devices.All(d=>!d.powerAvailable),"Actual ResearchDevices remain unpowered after attempted restoration");
        var rows=UnityEngine.Object.FindObjectsByType<PowerFacilityRow>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var row in rows)typeof(PowerFacilityRow).GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(row,null);
        Check(rows.All(row=>row.switches.All(toggle=>!toggle.interactable)),"Power panel disables switches for permanently forfeited devices");
        session.Run.Advance(48-session.Run.Hours,true,shield);yield return Frames(5);
        var report=evaluation.Report;
        Check(report!=null&&report.Outcome==SurvivalOutcome.Survived&&evaluation.CaptureCount==1,"Actual scene captures one normal ending");
        Check(Enumerable.Range(0,3).All(m=>report.ShieldInterrupted((ResearchMission)m)),"Final report snapshots all three incomplete affected missions");
        Check(Time.timeScale==0&&!InputHub.Active.GameplayAllowed&&evaluation.popup.activeSelf,"Result keeps the terminal pause and gameplay block");
        bool english=evaluation.language.English;
        try
        {
            evaluation.language.UseKorean();yield return Frames(3);
            Check(evaluation.note.text.Contains("태양풍으로부터 안전하기 위해")&&evaluation.note.text.EndsWith("정말 감사합니다!"),"Korean ending combines requested template and gratitude");
            yield return Capture(evaluation,"sacrifice-ko");
            evaluation.ShowSettings();yield return Frames(2);evaluation.language.UseEnglish();evaluation.pause.BackFromSettings();evaluation.pause.Resume();yield return Frames(3);
            Check(ReferenceEquals(report,evaluation.Report)&&evaluation.CaptureCount==1&&Time.timeScale==0,"Settings and language reuse the same frozen forfeiture report");
            yield return Capture(evaluation,"sacrifice-en");
            var rules=ScriptableObject.CreateInstance<ResearchDataRules>();
            typeof(FinalEvaluation).GetProperty(nameof(FinalEvaluation.Report)).SetValue(evaluation,MakeReport(rules));
            evaluation.language.UseKorean();yield return Frames(3);yield return Capture(evaluation,"no-sacrifice-ko");
            typeof(FinalEvaluation).GetProperty(nameof(FinalEvaluation.Report)).SetValue(evaluation,report);UnityEngine.Object.DestroyImmediate(rules);
        }
        finally{if(english)evaluation.language.UseEnglish();else evaluation.language.UseKorean();}
        Check(errors.Count==0,"No new Unity errors during ending validation: "+string.Join(" | ",errors));
    }
    static void Finish()
    {
        SessionState.SetBool("EndingSacrifice.Test",false);while(routines.Count>0)(routines.Pop() as IDisposable)?.Dispose();
        Application.logMessageReceived-=Log;Application.runInBackground=background;SampleDiscoveryChecks.RestoreGameSize();
        Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/play-checks.txt",checks.Concat(new[]{$"RESULT checks={checks.Count} failures={failures}"}));
        EditorApplication.isPlaying=false;
    }
}
