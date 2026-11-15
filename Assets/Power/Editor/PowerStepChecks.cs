using System;
using System.Collections;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Power;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

static class PowerStepChecks
{
    static bool Near(double a,double b)=>Math.Abs(a-b)<.0001;
    static IEnumerator Wait(float seconds) { float until=Time.realtimeSinceStartup+seconds; do { yield return null; } while(Time.realtimeSinceStartup<until); }
    static void Pure(Action<bool,string> check)
    {
        var rules=ScriptableObject.CreateInstance<PowerRules>();
        try
        {
            var b=new PowerBudget(rules);
            check(Near(b.Consumption,880) && Near(b.Remaining,120) && Enumerable.Range(0,3).All(f=>b.Running(f)==3),"STEP 4 sums actual unequal device demands in W across nine devices");
            b.SetAllocation(0,0); check(b.Running(0)==0,"Zero facility allocation switches all three devices OFF");
            b.SetAllocation(0,60); check(b.Running(0)==1 && b.Powered(0,0),"60 W supplies exactly the first 60 W device");
            b.SetAllocation(0,129); check(b.Running(0)==1,"A partial second-device demand never counts as ON");
            b.SetAllocation(0,130); check(b.Running(0)==2,"130 W supplies exactly two devices with 60/70 W demand");
            b.SetAllocation(0,210); check(b.Running(0)==3,"210 W supplies all three unequal-demand devices");
            b.SetDevice(0,0,false); check(b.Running(0)==2 && Near(b.Supplied(0),150),"Individual OFF releases its exact demand for other loads");
            b.SetShield(1); check(Near(b.EssentialSupplied,250) && Near(b.ShieldWatts,500) && b.Consumption<=b.Available,"Life support is protected before shield/research under overload");
            rules.samples.devices[2].priority=-1;
            rules.samples.devices[2].watts=110;
            var reordered=new PowerBudget(rules); reordered.SetAllocation(0,110);
            check(reordered.Powered(0,2) && !reordered.Powered(0,0) && Near(reordered.Supplied(0),110),"Changing demand and priority in rules changes allocation without source edits");
            reordered.SetAllocation(0,109);
            check(reordered.Running(0)==0,"Strict device priority stops lower devices while a higher-priority request cannot fit");
            rules.availableWatts=100; var low=new PowerBudget(rules); low.SetShield(1);
            check(low.EssentialShortage && Near(low.EssentialSupplied,100) && Near(low.ShieldWatts,0) && Enumerable.Range(0,3).All(f=>low.Running(f)==0) && Near(low.Consumption,100),"Total power below essential demand reports shortage and sheds every discretionary load");
            rules.availableWatts=400; var partialShield=new PowerBudget(rules); partialShield.SetShield(1);
            check(Near(partialShield.ShieldWatts,150) && Near(partialShield.ShieldActualOutput,.3),"Requested shield output and actually supplied output stay distinct");
            var random=new System.Random(4126); bool bounded=true;
            for(int n=0;n<1200;n++)
            {
                rules.availableWatts=random.Next(0,1500); rules.lifeSupportWatts=random.Next(0,600);
                for(int f=0;f<3;f++) for(int d=0;d<3;d++) rules.Facility(f).devices[d].watts=random.Next(0,301);
                var stress=new PowerBudget(rules); stress.SetShield(random.NextDouble());
                for(int f=0;f<3;f++) { stress.SetAllocation(f,random.Next(0,901)); for(int d=0;d<3;d++) stress.SetDevice(f,d,random.Next(2)==1); }
                double total=stress.EssentialSupplied+stress.ShieldWatts;
                for(int f=0;f<3;f++)
                {
                    double actual=Enumerable.Range(0,3).Where(d=>stress.Powered(f,d)).Sum(d=>stress.Demand(f,d));
                    bounded &= Near(actual,stress.Supplied(f)) && actual<=stress.Requested(f)+.00001;
                    total+=actual;
                }
                bounded &= Near(total,stress.Consumption) && total<=stress.Available+.00001 && stress.Remaining>=0;
            }
            check(bounded,"1,200 varying power/demand/switch scenarios keep per-facility budgets and total supply bounded");
        }
        finally { UnityEngine.Object.DestroyImmediate(rules); }
    }

    public static IEnumerator Run(HudController hud,MoonBasePlayer player,SurvivalSession session,Action<Key[]> keys,Action<bool,string> check,Action<HudController,int,int,string> capture)
    {
        Pure(check);
        var power=UnityEngine.Object.FindFirstObjectByType<BasePowerController>();
        var panel=UnityEngine.Object.FindFirstObjectByType<PowerPanel>();
        check(power && panel && power.Devices.Length==9 && !panel.IsOpen,"STEP 4 controller, console and nine device links are authored in SampleScene");
        if(!power || !panel) yield break;
        var hub=InputHub.Active; var progress=UnityEngine.Object.FindFirstObjectByType<ResearchProgress>();
        var controller=player.GetComponent<CharacterController>(); var motor=player.GetComponent<ThirdPersonController>();
        Vector3 position=player.transform.position; Quaternion rotation=player.transform.rotation;
        bool oldController=controller.enabled, oldMotor=motor.enabled, captured=player.ControlCaptured;
        var menu=panel.language; bool english=menu.English;
        const string languageKey="SpaceDilemma.Language"; bool hadPreference=PlayerPrefs.HasKey(languageKey); int oldPreference=PlayerPrefs.GetInt(languageKey);
        double[] allocations=Enumerable.Range(0,3).Select(f=>power.Budget.Requested(f)).ToArray();
        double shield=power.Budget.ShieldRequestedOutput;
        try
        {
            motor.enabled=false; controller.enabled=false;
            player.transform.position=panel.console.position-Vector3.up*.8f;
            Physics.SyncTransforms(); player.SetControlCaptured(true);
            yield return Wait(.1f);
            check(panel.CanReach() && panel.prompt.text.Length>0 && session.Contains(panel.console.position),"Power console is reachable on an indoor floor and shows its E prompt");
            keys(new[]{Key.E}); yield return Wait(.15f); keys(Array.Empty<Key>()); yield return Wait(.15f);
            check(panel.IsOpen && panel.Visible && hub.CurrentContext==panel.context && !hub.GameplayAllowed && !player.ControlCaptured,"Virtual E opens authored power UI, blocks movement and releases cursor");
            double hours=session.Run.Hours; yield return Wait(.12f);
            check(session.Run.Hours>hours && Time.timeScale>0,"Power panel keeps mission time running");
            var rows=panel.popup.GetComponentsInChildren<PowerFacilityRow>(true).OrderBy(r=>r.facility).ToArray();
            check(rows.Length==3 && rows.All(r=>r.switches.Length==3),"Scene contains three allocation sliders and nine editable device toggles");
            menu.UseEnglish(); yield return Wait(.1f);
            check(!panel.popup.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Any(c=>c>127 && char.IsLetter(c))),"Power UI in English contains only English text");
            rows[1].allocation.value=0; yield return Wait(.1f);
            double before=progress.Ledger.Data(ResearchMission.Instruments);
            session.Run.Advance(.25,true);
            check(power.Budget.Running(1)==0 && power.Devices.Where(d=>d.mission==ResearchMission.Instruments).All(d=>!d.powerAvailable) && Near(before,progress.Ledger.Data(ResearchMission.Instruments)),"Actual allocation slider stops three instruments and their research accumulation");
            rows[1].allocation.value=(float)power.Budget.Maximum(1); yield return Wait(.1f);
            session.Run.Advance(.25,true);
            check(power.Budget.Running(1)==3 && progress.Ledger.Data(ResearchMission.Instruments)>before,"Restoring slider allocation resumes the three activated instruments");
            rows[1].switches[1].isOn=false; yield return Wait(.1f);
            check(!power.Budget.Powered(1,1) && progress.Ledger.RunningDevices(ResearchMission.Instruments)==2,"Authored device toggle immediately reaches research ON/OFF state");
            rows[1].switches[1].isOn=true;
            panel.shield.value=1; yield return Wait(.1f);
            check(Near(power.Budget.ShieldActualOutput,1) && power.Budget.Consumption<=power.Budget.Available && power.Budget.Running(2)<3,"Shield slider consumes W and sheds lower-priority research within total supply");
            check(panel.shieldLabel.text.Contains("requested") && rows[2].deviceLabels.Any(t=>t.text.Contains("NO POWER")),"UI distinguishes requested shield output and device supply shortage");
            capture(hud,1280,720,"power-en-1280.png"); capture(hud,1920,1080,"power-en-1920.png");
            yield return Wait(.2f); Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)panel.closeButton.transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var hits=new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
            check(hits.Count>0 && hits[0].gameObject==panel.closeButton.gameObject,"Power close button receives the top UI pointer raycast");
            keys(new[]{Key.Escape}); yield return Wait(.15f); keys(Array.Empty<Key>()); yield return Wait(.15f);
            check(hud.Pause.IsPaused && panel.IsOpen && !panel.Visible,"ESC Pause overrides and temporarily hides the power modal");
            hud.Pause.ShowSettings(); menu.UseKorean(); hud.Pause.BackFromSettings();
            keys(new[]{Key.Escape}); yield return Wait(.15f); keys(Array.Empty<Key>()); yield return Wait(.15f);
            check(!hud.Pause.IsPaused && panel.Visible && hub.CurrentContext==panel.context && panel.title.text.Contains("기지 전력"),"Resume restores power context and reflects Korean selected in Settings");
            capture(hud,1920,1080,"power-ko-1920.png");
            yield return Wait(.2f);
            capture(hud,1280,720,"power-ko-1280.png");
            keys(new[]{Key.Backspace}); yield return Wait(.15f); keys(Array.Empty<Key>()); yield return Wait(.15f);
            check(!panel.IsOpen && !panel.Visible && hub.GameplayAllowed,"Backspace closes power UI and restores gameplay without pausing");
            player.SetControlCaptured(true); keys(new[]{Key.E}); yield return Wait(.15f); keys(Array.Empty<Key>()); yield return Wait(.15f);
            panel.closeButton.onClick.Invoke(); yield return Wait(.15f);
            check(!panel.IsOpen && hub.GameplayAllowed,"Authored Close button event closes and restores input context");
            player.transform.position=panel.console.position+Vector3.right*5; Physics.SyncTransforms(); panel.Open();
            check(!panel.IsOpen && !panel.CanReach(),"The power UI cannot open from outside interaction range");
        }
        finally
        {
            keys(Array.Empty<Key>()); if(hud.Pause.IsPaused) hud.Pause.Resume(); panel.Close();
            power.SetShield((float)shield);
            for(int f=0;f<3;f++) { power.SetAllocation(f,(float)allocations[f]); for(int d=0;d<3;d++) power.SetDevice(f,d,true); }
            if(english) menu.UseEnglish(); else menu.UseKorean();
            if(hadPreference) PlayerPrefs.SetInt(languageKey,oldPreference); else PlayerPrefs.DeleteKey(languageKey); PlayerPrefs.Save();
            player.transform.SetPositionAndRotation(position,rotation); controller.enabled=oldController; motor.enabled=oldMotor;
            player.SetControlCaptured(captured); Physics.SyncTransforms();
        }
    }
}
