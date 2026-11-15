using System;
using System.Collections;
using System.Linq;
using SpaceDilemma.Hud;
using SpaceDilemma.Power;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
using StarterAssets;
using Unity.Cinemachine;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

static class SpeStepChecks
{
    sealed class FixedShield : IRadiationShield
    {
        public double output;
        public ShieldEfficiencyModel model=new ShieldEfficiencyModel(.8);
        public double Transmission(bool inside,bool speActive)=>model.Transmission(inside,speActive,output);
    }
    static bool Near(double a,double b)=>Math.Abs(a-b)<.00001;
    static IEnumerator Wait(float seconds) { float until=Time.realtimeSinceStartup+seconds; do {yield return null;} while(Time.realtimeSinceStartup<until); }
    static void Pure(Action<bool,string> check)
    {
        var shield=new FixedShield { output=1 };
        check(Near(shield.Transmission(false,true),1) && Near(shield.Transmission(true,false),1) && Near(shield.Transmission(true,true),.2),"STEP 5 active shielding affects only indoor SPE exposure");
        shield.output=.5; check(Near(shield.Transmission(true,true),.6),"Half of supplied output gives the configured intermediate reduction");
        shield.output=0; check(Near(shield.Transmission(true,true),1),"Zero actual shield supply provides zero active protection");
        var off=new SurvivalRun(48,24,.1,3,20,.35);
        var on=new SurvivalRun(48,24,.1,3,20,.35);
        int events=0; on.SpeStarted+=()=>events++;
        shield.output=1; off.Advance(48,true); on.Advance(48,true,shield);
        check(off.Outcome==SurvivalOutcome.Died && on.Outcome==SurvivalOutcome.Survived && Near(on.Dose,5.88),"Configured danger threshold can kill an unshielded run while a powered sheltered run survives");
        on.Advance(100,true,shield); check(events==1 && Near(on.Hours,48),"SPE remains an external one-time 24h event independent of shield choice");
        var coarse=new SurvivalRun(48,24,.1,3,1000,.35); var fine=new SurvivalRun(48,24,.1,3,1000,.35);
        coarse.Advance(25,true,shield); for(int i=0;i<2500;i++)fine.Advance(.01,true,shield);
        check(Near(coarse.Dose,fine.Dose) && Near(coarse.Dose,1.05),"A frame crossing SPE onset integrates background then powered SPE at the correct boundary");
        var rules=ScriptableObject.CreateInstance<SamplePreservationRules>(); var dataRules=ScriptableObject.CreateInstance<ResearchDataRules>();
        try
        {
            var cool=new SamplePreservationState(rules); var fineCool=new SamplePreservationState(rules);
            cool.Advance(.5,false); check(Near(cool.Quality,1),"A cooling outage within the grace period does not destroy or degrade a sample");
            cool.Advance(.5,false); check(Near(cool.Quality,.925),"Quality degrades only for time beyond the cooling grace period");
            for(int i=0;i<100;i++)fineCool.Advance(.01,false);
            check(Near(cool.Quality,fineCool.Quality),"Sample quality has identical results for coarse and fine game-time steps");
            cool.Advance(2,true); check(Near(cool.Quality,.925) && Near(cool.OutageHours,0),"Restored cooling stops deterioration and resets the outage timer without healing damage");
            cool.Advance(6,false); check(!cool.Usable && cool.Quality>=0,"A long outage makes the sample unusable while quality stays bounded");
            var ledger=new ResearchLedger(dataRules,0,rules);
            ledger.SetState(ResearchMission.Samples,0,true,true,true); ledger.AdvanceTo(1);
            double earned=ledger.Data(ResearchMission.Samples);
            ledger.SetState(ResearchMission.Samples,0,true,false,true); ledger.AdvanceTo(7);
            ledger.SetState(ResearchMission.Samples,0,true,true,true); ledger.AdvanceTo(8);
            check(Near(ledger.Data(ResearchMission.Samples),earned) && ledger.RunningDevices(ResearchMission.Samples)==0,"Unusable sample stops future preservation credit and retains previously earned data");
            check(Near(ledger.Get(ResearchMission.Samples,1).Preservation.Quality,1),"An empty storage slot never loses sample quality during unrelated outages");
            double q=ledger.Get(ResearchMission.Samples,0).Preservation.Quality; ledger.Finish(); ledger.AdvanceTo(100);
            check(Near(ledger.Get(ResearchMission.Samples,0).Preservation.Quality,q),"Ending freezes preservation quality together with research totals");
        }
        finally { UnityEngine.Object.DestroyImmediate(rules); UnityEngine.Object.DestroyImmediate(dataRules); }
        var powerRules=ScriptableObject.CreateInstance<PowerRules>();
        try
        {
            powerRules.availableWatts=400; var budget=new PowerBudget(powerRules); budget.SetShield(1);
            check(Near(budget.ShieldActualOutput,.3) && Near(shield.model.Transmission(true,true,budget.ShieldActualOutput),.76),"Insufficient power uses actual 30% output rather than the requested 100% for radiation protection");
        }
        finally { UnityEngine.Object.DestroyImmediate(powerRules); }
    }

    public static IEnumerator Run(HudController hud,MoonBasePlayer player,SurvivalSession session,Action<Key[]> keys,Action<bool,string> check,Action<HudController,int,int,string> capture)
    {
        Pure(check);
        var guidance=session.GetComponent<SpeHud>(); var shield=UnityEngine.Object.FindFirstObjectByType<ActiveRadiationShield>();
        var power=UnityEngine.Object.FindFirstObjectByType<BasePowerController>(); var research=UnityEngine.Object.FindFirstObjectByType<ResearchProgress>();
        var panel=UnityEngine.Object.FindFirstObjectByType<PowerPanel>();
        check(guidance && shield && power && research && research.Ledger.Get(ResearchMission.Samples,0).Preservation!=null,"STEP 5 shield, preservation and warning UI are authored and connected in SampleScene");
        if(!guidance || !shield || !power || !research) yield break;
        var motor=player.GetComponent<ThirdPersonController>(); var body=player.GetComponent<CharacterController>();
        bool oldMotor=motor.enabled,oldBody=body.enabled,captured=player.ControlCaptured;
        var position=player.transform.position; var rotation=player.transform.rotation;
        float timeScale=Time.timeScale; bool english=guidance.language.English;
        const string key="SpaceDilemma.Language"; bool hadPreference=PlayerPrefs.HasKey(key); int preference=PlayerPrefs.GetInt(key);
        double[] allocation=Enumerable.Range(0,3).Select(f=>power.Budget.Requested(f)).ToArray(); double oldShield=power.Budget.ShieldRequestedOutput;
        try
        {
            keys(Array.Empty<Key>()); Time.timeScale=1; motor.enabled=false; body.enabled=false;
            Place(player,player.rover.transform.position+Vector3.right*2);
            power.SetShield(0); guidance.language.UseEnglish(); yield return Wait(.1f);
            check(!session.IsInside && !guidance.panel.activeSelf && guidance.AlertCount==0,"Before SPE the authored warning is hidden and the player can remain outdoors");
            session.Run.Advance(Math.Max(0,24-session.Run.Hours-.01),false,shield);
            check(!session.Run.SpeActive,"SPE does not start before game hour 24");
            session.Run.Advance(.02,false,shield); yield return Wait(.1f);
            check(session.Run.SpeActive && guidance.AlertCount==1 && guidance.panel.activeSelf && guidance.details.text.Contains("RETURN TO BASE"),"Crossing 24h once shows the outdoor return-to-base warning independently of player actions");
            double outdoorRate=session.Run.Rate; power.SetShield(1); yield return Wait(.1f);
            check(Near(outdoorRate,session.Run.Rate) && guidance.details.text.Contains("Here: 0"),"A fully powered indoor shield does not protect a player outside");
            capture(hud,1920,1080,"spe-outside-en-1920.png"); yield return Wait(.15f); capture(hud,1280,720,"spe-outside-en-1280.png");
            check(!guidance.panel.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Any(c=>c>127 && char.IsLetter(c))),"SPE warning in English contains no Korean or other non-English letters");
            Place(player,panel.console.position-Vector3.up*.8f); yield return Wait(.1f);
            double protectedRate=session.Run.Rate;
            check(session.IsInside && protectedRate<outdoorRate && guidance.details.text.Contains("Inside base"),"Entering the authored interior changes the guidance and applies actual shield protection");
            power.SetShield(0); yield return Wait(.1f); double indoorRate=session.Run.Rate;
            check(protectedRate<indoorRate && indoorRate<outdoorRate,"Scene exposure distinguishes outdoors, passive indoor protection and powered active shielding");
            power.SetShield(.5f); yield return Wait(.1f);
            check(session.Run.Rate<indoorRate && session.Run.Rate>protectedRate,"Scene dose rate responds to an intermediate actual shield output");
            double pausedDose=session.Run.Dose; hud.Pause.Pause(); yield return Wait(.15f);
            check(Near(pausedDose,session.Run.Dose),"Pause keeps SPE dose and quality time frozen");
            hud.Pause.ShowSettings(); guidance.language.UseKorean(); hud.Pause.BackFromSettings(); hud.Pause.Resume();
            power.SetShield(1); yield return Wait(.15f);
            check(guidance.title.text.Contains("태양 입자") && guidance.details.text.Contains("기지 내부"),"SPE alert and indoor guidance switch to Korean via Settings");
            capture(hud,1920,1080,"spe-inside-ko-1920.png"); yield return Wait(.15f); capture(hud,1280,720,"spe-inside-ko-1280.png");
            var sample=research.Ledger.Get(ResearchMission.Samples,0); double initialQuality=sample.Preservation.Quality;
            power.SetAllocation(0,0); double validHours=sample.ValidGameHours; double savedData=sample.Data;
            session.Run.Advance(.25,true,shield);
            check(Near(sample.Preservation.Quality,initialQuality) && Near(sample.Data,savedData) && Near(sample.ValidGameHours,validHours),"Actual cold-store power cut immediately pauses credit but preserves quality through the grace period");
            session.Run.Advance(.75,true,shield);
            check(sample.Preservation.Quality<initialQuality && sample.Preservation.Usable,"Continued outage degrades actual stored sample quality without immediate destruction");
            power.SetAllocation(0,(float)power.Budget.Maximum(0)); double damagedQuality=sample.Preservation.Quality;
            session.Run.Advance(.25,true,shield);
            check(Near(sample.Preservation.Quality,damagedQuality) && sample.ValidGameHours>validHours,"Reallocating power to cold storage stabilizes quality and resumes valid preservation time");
            power.SetDevice(0,0,false); session.Run.Advance(6,true,shield);
            check(!sample.Preservation.Usable && research.Ledger.Get(ResearchMission.Samples,1).Preservation.Usable,"One long individual outage invalidates that sample independently of the other slots");
            savedData=sample.Data; validHours=sample.ValidGameHours; power.SetDevice(0,0,true); session.Run.Advance(.25,true,shield); yield return Wait(.1f);
            check(Near(sample.Data,savedData) && Near(sample.ValidGameHours,validHours) && panel.note.text.Contains("연구불가"),"Re-powering an unusable sample retains past data but cannot resume new credit; power UI explains its quality");
            guidance.language.UseEnglish(); player.SetControlCaptured(true); panel.Open(); yield return Wait(.15f);
            check(panel.Visible && panel.note.text.Contains("INVALID") && panel.note.text.Contains("Indoor SPE dose reduction"),"Power UI displays actual shield effectiveness and invalid sample quality in English");
            capture(hud,1920,1080,"spe-power-en-1920.png"); yield return Wait(.15f); capture(hud,1280,720,"spe-power-en-1280.png");
            panel.Close();
            check(guidance.AlertCount==1,"Moving inside, changing power and resuming never repeat the SPE onset alert");
        }
        finally
        {
            panel.Close(); if(hud.Pause.IsPaused)hud.Pause.Resume();
            power.SetShield((float)oldShield);
            for(int f=0;f<3;f++) { power.SetAllocation(f,(float)allocation[f]); for(int d=0;d<3;d++)power.SetDevice(f,d,true); }
            if(english)guidance.language.UseEnglish();else guidance.language.UseKorean();
            if(hadPreference)PlayerPrefs.SetInt(key,preference);else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
            Place(player,position); player.transform.rotation=rotation; body.enabled=oldBody; motor.enabled=oldMotor; player.SetControlCaptured(captured);
            Physics.SyncTransforms(); Time.timeScale=timeScale;
        }
    }
    static void Place(MoonBasePlayer player,Vector3 position)
    {
        Vector3 delta=position-player.transform.position;
        player.transform.position=position;
        Physics.SyncTransforms();
        var camera=player.thirdPersonCamera;
        if(!camera || !camera.Follow) return;
        camera.OnTargetObjectWarped(camera.Follow,delta);
        camera.InternalUpdateCameraState(Vector3.up,-1);
        var state=camera.State;
        if(Camera.main) Camera.main.transform.SetPositionAndRotation(state.GetFinalPosition(),state.GetFinalOrientation());
    }
}
