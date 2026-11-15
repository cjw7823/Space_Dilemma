using System;
using System.Reflection;
using SpaceDilemma.Results;
using SpaceDilemma.Scenario;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScenarioRules))]
sealed class ScenarioRulesEditor : Editor
{
    static readonly string[] MissionFields = { nameof(ScenarioRules.samples), nameof(ScenarioRules.instruments), nameof(ScenarioRules.plants) };
    static readonly string[] MissionNames = { "시료 채취·보존", "측정장비", "식물 재배" };
    static readonly string[] DeviceFields = { nameof(ScenarioMission.first), nameof(ScenarioMission.second), nameof(ScenarioMission.third) };
    static readonly string[] MissionSettings = { nameof(ScenarioMission.targetData), nameof(ScenarioMission.maximumPercent),
        nameof(ScenarioMission.supplyPriority), nameof(ScenarioMission.initialRequestedWatts) };
    static readonly string[,] DeviceNames =
    {
        { "보관소 1", "보관소 2", "보관소 3" },
        { "지진계", "열류량 측정기", "자기장 측정기" },
        { "재배 구획 1", "재배 구획 2", "재배 구획 3" }
    };

    bool weightsExpanded = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("수정값은 다음 Play부터 적용됩니다. 시간·전력·선량은 게임 밸런스 수치입니다.", MessageType.Info);
        DrawSection(nameof(ScenarioRules.time));
        DrawSection(nameof(ScenarioRules.radiation));
        DrawSection(nameof(ScenarioRules.interaction));
        DrawSection(nameof(ScenarioRules.power));
        DrawSection(nameof(ScenarioRules.preservation));
        DrawWeights();
        for (int mission = 0; mission < MissionFields.Length; mission++) DrawMission(mission);
        DrawSection(nameof(ScenarioRules.start));
        serializedObject.ApplyModifiedProperties();

        var start = ((ScenarioRules)target).start;
        var first = start.firstSlot.sample;
        var second = start.secondSlot.sample;
        var third = start.thirdSlot.sample;
        if ((first != StartingSample.None && (first == second || first == third)) ||
            (second != StartingSample.None && second == third))
            EditorGUILayout.HelpBox("같은 시료는 번호가 가장 작은 보관소 한 곳에 배정됩니다.", MessageType.Warning);
    }

    void DrawSection(string field)
    {
        var info = typeof(ScenarioRules).GetField(field);
        DrawSection(serializedObject.FindProperty(field), info.FieldType, Label(info));
    }

    static void DrawSection(SerializedProperty section, Type type, GUIContent label, string hiddenField = null)
    {
        section.isExpanded = EditorGUILayout.Foldout(section.isExpanded, label, true);
        if (!section.isExpanded) return;
        using (new EditorGUI.IndentLevelScope())
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.Name == hiddenField) continue;
                var value = section.FindPropertyRelative(field.Name);
                if (field.FieldType == typeof(ScenarioSampleSlot)) DrawSection(value, field.FieldType, Label(field));
                else EditorGUILayout.PropertyField(value, Label(field));
            }
        }
    }

    static GUIContent Label(FieldInfo field) => new(
        field.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? ObjectNames.NicifyVariableName(field.Name),
        field.GetCustomAttribute<TooltipAttribute>()?.tooltip);

    void DrawWeights()
    {
        weightsExpanded = EditorGUILayout.Foldout(weightsExpanded, "전체 연구 달성도 비중", true);
        if (!weightsExpanded) return;
        using (new EditorGUI.IndentLevelScope())
        {
            var inputs = new SerializedProperty[MissionFields.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = serializedObject.FindProperty(MissionFields[i]).FindPropertyRelative(nameof(ScenarioMission.scoreWeight));
                EditorGUILayout.PropertyField(inputs[i], new GUIContent(MissionNames[i], "예를 들어 40, 35, 25는 각각 40%, 35%, 25%로 반영됩니다."));
            }
            var weights = new ResearchWeights(inputs[0].floatValue, inputs[1].floatValue, inputs[2].floatValue);
            double samples = Math.Round(weights.Samples * 100, 2);
            double firstTwo = Math.Round((weights.Samples + weights.Instruments) * 100, 2);
            EditorGUILayout.LabelField("실제 비중 (합계 100%)", $"{samples:0.##}%  /  {firstTwo - samples:0.##}%  /  {100 - firstTwo:0.##}%");
        }
    }

    void DrawMission(int index)
    {
        var mission = serializedObject.FindProperty(MissionFields[index]);
        mission.isExpanded = EditorGUILayout.Foldout(mission.isExpanded, MissionNames[index], true);
        if (!mission.isExpanded) return;
        using (new EditorGUI.IndentLevelScope())
        {
            foreach (string name in MissionSettings)
                EditorGUILayout.PropertyField(mission.FindPropertyRelative(name), Label(typeof(ScenarioMission).GetField(name)));
            for (int i = 0; i < DeviceFields.Length; i++)
                DrawSection(mission.FindPropertyRelative(DeviceFields[i]), typeof(ScenarioDevice),
                    new GUIContent(DeviceNames[index, i]), index == 0 ? nameof(ScenarioDevice.initiallyCompleted) : null);
        }
    }
}
