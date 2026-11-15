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
        DrawRealTime();
        DrawRadiation();
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

    void DrawRealTime()
    {
        var time=serializedObject.FindProperty(nameof(ScenarioRules.time));
        double speed=Math.Max(.0001,time.FindPropertyRelative(nameof(ScenarioTime.gameHoursPerSecond)).floatValue);
        string RealTime(double hours){long seconds=(long)Math.Round(Math.Max(0,hours)/speed);return $"{seconds/60}분 {seconds%60:00}초";}
        EditorGUILayout.LabelField("실제 플레이 기준 (일시정지 제외)","전체 "+RealTime(time.FindPropertyRelative(nameof(ScenarioTime.durationHours)).floatValue)+
            " / SPE "+RealTime(time.FindPropertyRelative(nameof(ScenarioTime.speStartHour)).floatValue));
    }
    void DrawRadiation()
    {
        var section=serializedObject.FindProperty(nameof(ScenarioRules.radiation));
        section.isExpanded=EditorGUILayout.Foldout(section.isExpanded,"방사선과 차폐",true);if(!section.isExpanded)return;
        using(new EditorGUI.IndentLevelScope())
        {
            var use=section.FindPropertyRelative(nameof(ScenarioRadiation.useRadLabAverages));EditorGUILayout.PropertyField(use,Label(typeof(ScenarioRadiation).GetField(nameof(ScenarioRadiation.useRadLabAverages))));
            foreach(var field in typeof(ScenarioRadiation).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.Name==nameof(ScenarioRadiation.useRadLabAverages))continue;
                bool data=field.Name==nameof(ScenarioRadiation.gameGain)||field.Name==nameof(ScenarioRadiation.preAverageUGyPerHour)||field.Name==nameof(ScenarioRadiation.postAverageUGyPerHour);
                bool manual=field.Name==nameof(ScenarioRadiation.normalRate)||field.Name==nameof(ScenarioRadiation.speRate);
                if((data&&!use.boolValue)||(manual&&use.boolValue))continue;
                EditorGUILayout.PropertyField(section.FindPropertyRelative(field.Name),Label(field));
            }
            if(use.boolValue)
            {
                double gain=section.FindPropertyRelative(nameof(ScenarioRadiation.gameGain)).floatValue;
                double before=section.FindPropertyRelative(nameof(ScenarioRadiation.preAverageUGyPerHour)).doubleValue*gain/1000;
                double after=section.FindPropertyRelative(nameof(ScenarioRadiation.postAverageUGyPerHour)).doubleValue*gain/1000;
                double speed=serializedObject.FindProperty(nameof(ScenarioRules.time)).FindPropertyRelative(nameof(ScenarioTime.gameHoursPerSecond)).floatValue;
                bool fit=section.FindPropertyRelative(nameof(ScenarioRadiation.fitSpeToRemainingDose)).boolValue;
                EditorGUILayout.LabelField(fit?"SPE 전 game mSv/게임시간":"실외 game mSv/게임시간",fit?$"{before:0.0000}":$"{before:0.0000} → {after:0.0000}");
                EditorGUILayout.LabelField(fit?"SPE 전 game mSv/플레이 초":"실외 game mSv/플레이 초",fit?$"{before*speed:0.0000}":$"{before*speed:0.0000} → {after*speed:0.0000}");
                EditorGUILayout.HelpBox(fit
                    ? "SPE 전에는 관측 평균 × k를 사용합니다. SPE 실외 선량률은 사건 순간의 남은 피폭 한도 ÷ 남은 게임시간 × 여유 배율로 한 번 고정하며 관측 전후 비율과 달라질 수 있습니다."
                    : "RadLab의 전후 평균을 두 상수 구간으로 적용합니다. k는 두 구간을 함께 확대하며 SPE 시작 시각은 변경하지 않습니다.",MessageType.None);
            }
            if(section.FindPropertyRelative(nameof(ScenarioRadiation.fitSpeToRemainingDose)).boolValue)
                EditorGUILayout.HelpBox("여유 배율 1은 종료 순간, 1.1은 남은 시간의 약 91%에 실외 피폭이 100%가 됩니다. 이후 실내·차폐 효과는 유지되고 선량률은 다시 올리지 않습니다. SPE가 일찍 끝나면 평상시 선량률로 돌아갑니다.",MessageType.Info);
        }
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
