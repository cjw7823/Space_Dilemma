using System;
using SpaceDilemma.Survival;
using UnityEngine;

static class SurvivalStepChecks
{
    sealed class HalfShield : IRadiationShield
    {
        public double Transmission(bool inside, bool speActive) => .5;
    }

    public static void Run(SurvivalSession session, LunarEnvironment environment, Action<bool, string> check)
    {
        var crossed = new SurvivalRun(48, 24, 1, 3, 1000, .5);
        int warnings = 0;
        double warningTime = -1;
        double warningRate = 0;
        crossed.SpeStarted += () => { warnings++; warningTime = crossed.Hours; warningRate = crossed.Rate; };
        crossed.Advance(25, false);
        check(Near(crossed.Dose, 27) && crossed.SpeActive && Near(warningRate, 3), "A frame crossing 24h uses both dose rates");
        crossed.Advance(100, false);
        check(warnings == 1 && Near(warningTime, 24) && Near(crossed.Hours, 48) && Near(crossed.Dose, 96),
            "SPE fires once at 24h and surplus time after 48h is discarded");

        var fine = new SurvivalRun(48, 24, 1, 3, 1000, .5);
        var fast = new SurvivalRun(48, 24, 1, 3, 1000, .5);
        for (int i = 0; i < 60 * 480; i++) fine.Advance(.1 / 60, false);
        for (int i = 0; i < 19 * 12; i++) fast.Advance(4.0 / 19, false);
        check(fine.Outcome == SurvivalOutcome.Survived && fast.Outcome == fine.Outcome &&
            Near(fine.Dose, crossed.Dose) && Near(fast.Dose, fine.Dose), "60 FPS and 19 FPS at different playback speeds both integrate 96 mSv over 48h");

        var shielded = new SurvivalRun(48, 24, 1, 3, 1000, .5);
        shielded.Advance(1, true, new HalfShield());
        check(Near(shielded.Dose, .25) && Near(shielded.Rate, .25), "Indoor and active shielding factors combine");

        var lethal = new SurvivalRun(48, 24, 1, 3, 30, .5);
        int deaths = 0;
        lethal.Ended += _ => deaths++;
        lethal.Advance(48, false);
        lethal.Advance(100, false);
        check(deaths == 1 && lethal.Outcome == SurvivalOutcome.Died && Near(lethal.Hours, 26) && Near(lethal.Dose, 30) &&
            Near(lethal.Rate, 0), "Lethal dose ends the run at the threshold without further exposure");

        var tied = new SurvivalRun(48, 24, 1, 1, 48, .5);
        int endings = 0;
        tied.Ended += _ => endings++;
        tied.Advance(50, false);
        check(endings == 1 && tied.Outcome == SurvivalOutcome.Died && Near(tied.Hours, 48),
            "Death takes priority in a 48h tie and fires one ending");

        var custom = new SurvivalRun(12, 3, 2, 5, 1000, .2);
        custom.Advance(12, true);
        check(custom.Outcome == SurvivalOutcome.Survived && Near(custom.Dose, 10.2), "Alternate duration, SPE and dose settings are honored");

        int sceneEnds = 0;
        session.Run.Ended += _ => sceneEnds++;
        session.Run.Advance(1000, true);
        double endedAt = session.Run.Hours, finalDose = session.Run.Dose;
        session.Run.Advance(1000, false);
        check(sceneEnds == 1 && session.Run.Outcome != SurvivalOutcome.Running && Time.timeScale == 0 &&
            Near(environment.sunHoursFromStart, endedAt) && Near(finalDose, session.Run.Dose),
            "Authored session freezes Unity and synchronizes sky once the run ends");
    }

    static bool Near(double a, double b) => Math.Abs(a - b) < .00001;
}
