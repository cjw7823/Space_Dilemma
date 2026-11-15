using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Scenario;
using UnityEngine;

namespace SpaceDilemma.Survival
{
    [DefaultExecutionOrder(100)]
    public sealed class SurvivalSession : MonoBehaviour
    {
        [SerializeField] ScenarioRules scenario;
        [SerializeField, HideInInspector] SurvivalRules rules;
        [SerializeField] LunarEnvironment environment;
        [SerializeField] MoonBasePlayer player;
        [SerializeField] HudPauseManager pause;
        [SerializeField] InputContextProfile terminalInputContext;
        [SerializeField] BoxCollider[] indoorVolumes;
        [SerializeField] MonoBehaviour activeShield;
        [SerializeField, HideInInspector] float sampleHeight = 1f;
        ScenarioRules runtimeScenario;
        public ScenarioRules Scenario
        {
            get
            {
                if (!scenario) return null;
                if (!Application.isPlaying) return scenario;
                // Awake callers such as the power controller share the same session copy.
                if (!runtimeScenario)
                {
                    runtimeScenario = Instantiate(scenario);
                    runtimeScenario.hideFlags = HideFlags.DontSave;
                }
                return runtimeScenario;
            }
        }
        float ClockSpeed => Mathf.Max(.0001f, Scenario?.time.gameHoursPerSecond ?? rules.gameHoursPerSecond);
        public SurvivalRun Run { get; private set; }
        public bool IsInside { get; private set; }
        public bool IsPaused => pause && pause.IsPaused && Run.Outcome == SurvivalOutcome.Running;
        public BoxCollider[] IndoorVolumes => indoorVolumes;
        float timeScaleBeforeEnd;
        InputHub hub;
        int terminalToken;
        bool froze;

        void Awake()
        {
            var config = Scenario;
            if (config)
                Run = new SurvivalRun(config.time.durationHours, config.time.speStartHour, config.radiation.NormalRate,
                    config.radiation.SpeRate, config.radiation.lethalDose, config.radiation.indoorTransmission,
                    config.start.elapsedHours, config.start.dose, config.time.speDurationHours);
            else
                Run = new SurvivalRun(rules.durationHours, rules.speStartHour, rules.normalRate,
                    rules.speRate, rules.lethalDose, rules.indoorTransmission);
            IsInside = ContainsPlayer();
            SyncClock(Run.Hours);
            Run.TimeChanged += SyncClock;
            Run.Ended += FreezeSession;
        }

        void Update()
        {
            if (Run.Outcome != SurvivalOutcome.Running) return;
            IsInside = ContainsPlayer();
            var shield = activeShield as IRadiationShield;
            Run.UpdateRate(IsInside, shield);
            if (IsPaused || Time.timeScale == 0 || Time.deltaTime == 0) return;
            Run.Advance(Time.deltaTime * (double)ClockSpeed, IsInside, shield);
        }

        void SyncClock(double hours) => environment.SetSessionClock(hours, ClockSpeed, Scenario?.time.sunUpdateSeconds);

        public bool ContainsPlayer() => player && Contains(player.transform.position +
            Vector3.up * Mathf.Max(0, Scenario?.interaction.indoorSampleHeight ?? sampleHeight));

        public bool Contains(Vector3 point)
        {
            foreach (var volume in indoorVolumes)
            {
                if (!volume || !volume.gameObject.activeInHierarchy) continue;
                Vector3 delta = volume.transform.InverseTransformPoint(point) - volume.center;
                Vector3 half = volume.size * .5f;
                if (Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.y) <= half.y && Mathf.Abs(delta.z) <= half.z)
                    return true;
            }
            return false;
        }

        void FreezeSession(SurvivalOutcome outcome)
        {
            hub = InputHub.Active;
            if (hub && terminalInputContext) terminalToken = hub.PushContext(terminalInputContext);
            player.SetControlCaptured(false);
            timeScaleBeforeEnd = Time.timeScale;
            froze = true;
            Time.timeScale = 0;
        }

        void OnDestroy()
        {
            if (hub && terminalToken != 0) hub.PopContext(terminalToken);
            if (froze) Time.timeScale = timeScaleBeforeEnd;
            if (environment) environment.ReleaseSessionClock();
            if (runtimeScenario) Destroy(runtimeScenario);
        }
    }
}
