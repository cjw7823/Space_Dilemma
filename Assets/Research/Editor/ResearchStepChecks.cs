using System;
using System.Collections;
using System.Linq;
using SpaceDilemma.CustomInput;
using SpaceDilemma.Hud;
using SpaceDilemma.Power;
using SpaceDilemma.Research;
using SpaceDilemma.Survival;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

static class ResearchStepChecks
{
    public static IEnumerator Run(MoonBasePlayer player, InputHub hub, HudPauseManager pause, Action<Key[]> sendKeys,
        Action<bool, string> check)
    {
        var research = UnityEngine.Object.FindFirstObjectByType<ResearchInteraction>();
        check(research && research.Targets.Count == 12, "Step 2 has nine research objects and three authored storage slots");
        if (!research || research.Targets.Count != 12) yield break;
        var rules = (ResearchRules)new SerializedObject(research).FindProperty("rules").objectReferenceValue;
        var scenario = UnityEngine.Object.FindFirstObjectByType<SurvivalSession>().Scenario?.interaction;
        void SetReset(bool value)
        {
            if (scenario != null) scenario.resetWhenInterrupted = value;
            else rules.resetWhenInterrupted = value;
        }
        var gameplay = hub.Actions.FindActionMap("Player");
        check(gameplay.FindAction("Interact").bindings.Any(b => b.path == "<Keyboard>/t") &&
            gameplay.FindAction("ResearchInteract").bindings.Any(b => b.path == "<Keyboard>/e"),
            "Research E and rover T remain distinct gameplay bindings");

        var targets = research.Targets;
        var firstStorage = targets.Where(t => t.kind == ResearchKind.Storage).OrderBy(t => t.index).First();
        var firstInstrument = targets.First(t => t.kind == ResearchKind.Instrument);
        var capsule = player.GetComponent<CharacterController>();
        var hudData = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<ResearchHud>());
        var summary = (TMP_Text)hudData.FindProperty("summary").objectReferenceValue;
        var prompt = (TMP_Text)hudData.FindProperty("prompt").objectReferenceValue;
        var language = (HudGameSettings)hudData.FindProperty("language").objectReferenceValue;
        Vector3 origin = player.transform.position;
        bool resetAtStart = research.ResetWhenInterrupted;
        int completions = 0;
        research.Completed += _ => completions++;
        player.SetControlCaptured(true);
        try
        {
            Place(player, capsule, StorageApproach(player, capsule, firstStorage));
            yield return Wait(.15f);
            sendKeys(new[] { Key.E });
            yield return Wait(research.HoldSeconds * .35f);
            check(research.Nearby == firstStorage && research.CarriedSampleCount == 0 && research.Progress == 0 &&
                !research.IsComplete(firstStorage), "Holding E at a reachable cold storage slot with no carried sample leaves it empty");
            prompt.ForceMeshUpdate();
            summary.ForceMeshUpdate();
            check(summary.text.Contains(language.Text("Carried 0", "소지 0")) &&
                prompt.text.Contains(language.Text("Collect a lunar sample first", "먼저 달 시료를 채취하세요")) &&
                !summary.isTextTruncated && !prompt.isTextTruncated,
                "The mission board fits the carried sample count and the reason storage cannot charge");
            sendKeys(Array.Empty<Key>());

            Place(player, capsule, firstInstrument.transform.position - Vector3.up * .77f);
            yield return Wait(.15f);
            sendKeys(new[] { Key.E });
            yield return Wait(research.HoldSeconds * .35f);
            check(research.Progress > 0 && !research.IsComplete(firstInstrument), "Holding E increases progress before completion");
            sendKeys(Array.Empty<Key>());
            yield return Wait(.15f);
            check(research.Progress == 0 && !research.IsComplete(firstInstrument), "Releasing E clears progress in reset mode");

            SetReset(false);
            sendKeys(new[] { Key.E });
            yield return Wait(research.HoldSeconds * .28f);
            sendKeys(Array.Empty<Key>());
            yield return Wait(.15f);
            check(research.Progress > 0 && !research.IsComplete(firstInstrument), "Rules can retain interrupted progress");
            SetReset(true);
            yield return Wait(.15f);

            sendKeys(new[] { Key.E });
            yield return Wait(research.HoldSeconds * .25f);
            Place(player, capsule, firstInstrument.transform.position - Vector3.up * .77f + Vector3.right * 5);
            yield return Wait(.2f);
            check(!research.Nearby && !research.IsComplete(firstInstrument), "Leaving interaction range cancels the hold");
            sendKeys(Array.Empty<Key>());
            Place(player, capsule, firstInstrument.transform.position - Vector3.up * .77f);
            pause.Pause();
            sendKeys(new[] { Key.E });
            yield return Wait(.25f);
            check(!research.Nearby && research.Progress == 0, "A held E cannot interact while the pause menu is open");
            sendKeys(Array.Empty<Key>());
            yield return Wait(.1f);
            pause.Resume();
            yield return Wait(.2f);
            player.SetControlCaptured(true);

            yield return CheckEmbeddedTarget(player, capsule, research, firstInstrument, sendKeys, check);
            var order = targets.Where(t => t.kind == ResearchKind.Instrument || t.kind == ResearchKind.Plant)
                .Concat(targets.Where(t => t.kind == ResearchKind.Sample || t.kind == ResearchKind.Storage)
                    .OrderBy(t => t.index).ThenBy(t => t.kind));
            foreach (var target in order)
            {
                bool storage = target.kind == ResearchKind.Storage;
                Vector3 approach = storage || target.kind == ResearchKind.Sample
                    ? StorageApproach(player, capsule, target) : target.transform.position - Vector3.up * .77f;
                Place(player, capsule, approach);
                yield return Wait(.15f);
                bool selected = research.Nearby == target;
                if (storage) check(research.CarriedSampleCount == 1 && research.CanUse(target) &&
                    prompt.text.Contains(language.Text("Hold E to store", "E 길게 눌러 보관")),
                    "One collected sample enables the correct storage slot: " + target.englishName);
                sendKeys(new[] { Key.E });
                yield return Wait(research.HoldSeconds + .15f);
                sendKeys(Array.Empty<Key>());
                yield return Wait(.1f);
                check(selected && research.IsComplete(target), "E hold completes independently: " + target.englishName);
                if (target.kind == ResearchKind.Sample) check(!target.gameObject.activeSelf && research.CarriedSampleCount == 1,
                    "Collecting hides the sample hierarchy and leaves it in inventory: " + target.englishName);
                if (target.kind == ResearchKind.Sample)
                {
                    var samplePopup = UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();
                    check(samplePopup && samplePopup.IsOpen && Time.timeScale == 0, "Collection opens the authored information popup and pauses time");
                    if (samplePopup) samplePopup.Close();
                    yield return Wait(.15f);
                }
                if (target.kind == ResearchKind.Plant)
                {
                    var popup = UnityEngine.Object.FindFirstObjectByType<SampleCollectionPopup>();
                    if (popup && popup.IsOpen)
                    {
                        check(popup.IsPlantIntroduction && Time.timeScale == 0, "First plant completion opens its paused introduction");
                        popup.Close();
                        yield return Wait(.15f);
                    }
                }
                if (storage) check(research.StoredSampleBySlot.TryGetValue(target.index, out int sample) &&
                    sample == target.index && research.CarriedSampleCount == 0,
                    "Storage consumes exactly one carried sample: " + target.englishName);
                if (target == firstInstrument)
                    check(research.Count(ResearchKind.Instrument) == 1 && research.Count(ResearchKind.Sample) == 0 &&
                        research.Count(ResearchKind.Plant) == 0, "One instrument completes before any sample or planting work");
            }

            sendKeys(new[] { Key.E });
            yield return Wait(research.HoldSeconds + .15f);
            sendKeys(Array.Empty<Key>());
            check(completions == 12 && research.StoredSampleBySlot.Count == 3 &&
                research.StoredSampleBySlot.Values.Distinct().Count() == 3, "Each slot stores one distinct sample and repeat holds never complete twice");
        }
        finally
        {
            SetReset(resetAtStart);
            sendKeys(Array.Empty<Key>());
            Place(player, capsule, origin);
        }
    }

    static IEnumerator CheckEmbeddedTarget(MoonBasePlayer player, CharacterController capsule,
        ResearchInteraction research, ResearchTarget target, Action<Key[]> sendKeys, Action<bool, string> check)
    {
        var panel = UnityEngine.Object.FindFirstObjectByType<PowerPanel>();
        var session = UnityEngine.Object.FindFirstObjectByType<SurvivalSession>();
        var motor = player.GetComponent<StarterAssets.ThirdPersonController>();
        Vector3 playerPosition = player.transform.position;
        Vector3 targetPosition = target.transform.position;
        var other = research.Targets.First(candidate => candidate.kind == ResearchKind.Instrument && candidate != target);
        Vector3 otherPosition = other.transform.position;
        Vector3 consolePosition = panel.console.position;
        bool motorEnabled = motor.enabled;
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var room = new GameObject("Temporary indoor area for research reach validation");
        var originalVolumes = session.IndoorVolumes;
        var sessionData = new SerializedObject(session);
        try
        {
            motor.enabled = false;
            var area = room.AddComponent<BoxCollider>();
            area.isTrigger = true;
            area.size = Vector3.one * 10;
            room.transform.position = new Vector3(0, 10000, 0);
            var volumes = sessionData.FindProperty("indoorVolumes");
            volumes.arraySize = 1;
            volumes.GetArrayElementAtIndex(0).objectReferenceValue = area;
            sessionData.ApplyModifiedPropertiesWithoutUndo();

            target.transform.position = room.transform.position;
            other.transform.position = target.transform.position + Vector3.back * .25f;
            model.name = "Solid model enclosing a hidden research target";
            model.transform.position = target.transform.position;
            model.GetComponent<MeshRenderer>().enabled = false;
            Place(player, capsule, target.transform.position + new Vector3(0, -.8f, 1.5f));
            // Keep the power console reachable, farther away, and outside the model's shadow.
            panel.console.position = player.transform.position + Vector3.up * .8f + Vector3.right * 1.8f;
            player.SetControlCaptured(true);
            yield return Wait(.15f);
            var from = player.transform.position + Vector3.up * .8f;
            int mask = Physics.DefaultRaycastLayers & ~(1 << player.gameObject.layer);
            bool oldCastBlocked = Physics.Linecast(from, target.transform.position, mask, QueryTriggerInteraction.Ignore);
            bool consoleClear = !Physics.Linecast(from, panel.console.position, mask, QueryTriggerInteraction.Ignore);
            check(oldCastBlocked && model.GetComponent<Collider>().enabled && research.Nearby == target,
                "The nearer of two hidden targets inside a solid model is selected");
            check(session.ContainsPlayer() && consoleClear && !panel.CanReach(),
                "A nearer embedded research target keeps E priority over a reachable power console");
            sendKeys(new[] { Key.E });
            yield return Wait(research.HoldSeconds * .35f);
            check(research.Progress > 0 && !research.IsComplete(target) && !panel.IsOpen,
                "Holding virtual E charges the embedded target without opening the power panel");
            sendKeys(Array.Empty<Key>());
            yield return Wait(.15f);
            check(research.Progress == 0, "Releasing E resets the embedded target's charge");

            other.transform.position = target.transform.position;
            yield return Wait(.1f);
            check(research.Nearby == target, "Two research targets at exactly the same point are ordered by index");

            // Both targets remain in range, and the second now becomes the closest.
            other.transform.position = target.transform.position + Vector3.forward * .25f;
            yield return Wait(.1f);
            check(research.Nearby == other && !research.IsComplete(other) && research.Progress == 0,
                "Changing which target is closest switches selection without transferring charge");
            other.transform.position = target.transform.position + Vector3.back * .25f;
            yield return Wait(.1f);
            sendKeys(new[] { Key.E });
            yield return Wait(research.HoldSeconds + .15f);
            check(research.IsComplete(target) && !research.IsComplete(other) && research.Nearby == other && research.NeedsRelease,
                "One E hold completes only the closest target and selects the next unfinished target");
            yield return Wait(research.HoldSeconds + .15f);
            check(!research.IsComplete(other) && research.Progress == 0 && !panel.IsOpen,
                "Continuing to hold E cannot chain-complete the neighboring target");
            sendKeys(Array.Empty<Key>());
            yield return Wait(.15f);
            sendKeys(new[] { Key.E });
            yield return Wait(research.HoldSeconds * .35f);
            check(research.Nearby == other && research.Progress > 0 && !research.IsComplete(other),
                "A fresh E hold charges the nearest remaining target even beside a completed target");
            sendKeys(Array.Empty<Key>());
            yield return Wait(.15f);
        }
        finally
        {
            sendKeys(Array.Empty<Key>());
            panel.Close();
            target.transform.position = targetPosition;
            other.transform.position = otherPosition;
            panel.console.position = consolePosition;
            sessionData.Update();
            var volumes = sessionData.FindProperty("indoorVolumes");
            volumes.arraySize = originalVolumes.Length;
            for (int i = 0; i < originalVolumes.Length; i++) volumes.GetArrayElementAtIndex(i).objectReferenceValue = originalVolumes[i];
            sessionData.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(model);
            UnityEngine.Object.DestroyImmediate(room);
            Place(player, capsule, playerPosition);
            motor.enabled = motorEnabled;
        }
    }

    // Storage markers can sit inside the model. Stand on a clear surface beside the marker.
    static Vector3 StorageApproach(MoonBasePlayer player, CharacterController capsule, ResearchTarget target)
    {
        Vector3 center = target.InteractionPoint(target.transform.position);
        Vector3 position = center - Vector3.up * .77f;
        float bestDistance = float.PositiveInfinity;
        float radius = capsule.radius;
        int mask = Physics.DefaultRaycastLayers & ~(1 << player.gameObject.layer);
        for (int x = -8; x <= 8; x++)
        for (int z = -8; z <= 8; z++)
        {
            Vector3 from = center + new Vector3(x * .25f, .5f, z * .25f);
            if (!Physics.Raycast(from, Vector3.down, out var hit, 4f, mask, QueryTriggerInteraction.Ignore) || hit.normal.y < .75f) continue;
            Vector3 foot = hit.point + Vector3.up * .03f;
            if (Physics.CheckCapsule(foot + Vector3.up * radius, foot + Vector3.up * (capsule.height - radius), radius, mask,
                QueryTriggerInteraction.Ignore)) continue;
            Vector3 candidate = foot - capsule.center + Vector3.up * (capsule.height / 2);
            Vector3 reach = candidate + Vector3.up * .8f;
            float distance = (reach - target.InteractionPoint(reach)).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            position = candidate;
        }
        return position;
    }

    static void Place(MoonBasePlayer player, CharacterController capsule, Vector3 position)
    {
        capsule.enabled = false;
        player.transform.position = position;
        capsule.enabled = true;
        Physics.SyncTransforms();
    }

    static IEnumerator Wait(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        do { yield return null; } while (Time.realtimeSinceStartup < until);
    }
}
