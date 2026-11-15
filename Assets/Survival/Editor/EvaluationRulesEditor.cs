using System;
using SpaceDilemma.Results;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EvaluationRules))]
sealed class EvaluationRulesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var samples = serializedObject.FindProperty(nameof(EvaluationRules.samplesWeight));
        var instruments = serializedObject.FindProperty(nameof(EvaluationRules.instrumentsWeight));
        var plants = serializedObject.FindProperty(nameof(EvaluationRules.plantsWeight));

        EditorGUILayout.LabelField("연구 달성도 비중", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("세 미션의 비율을 입력하세요. 예를 들어 40, 35, 25는 각각 40%, 35%, 25%입니다. Play 시작 시 반영됩니다.", MessageType.Info);
        EditorGUILayout.PropertyField(samples, new GUIContent("시료 채취·보존"));
        EditorGUILayout.PropertyField(instruments, new GUIContent("측정장비"));
        EditorGUILayout.PropertyField(plants, new GUIContent("식물 재배"));

        var weights = new ResearchWeights(samples.floatValue, instruments.floatValue, plants.floatValue);
        // Round the cumulative shares so the displayed percentages also sum to 100.
        double sampleShare = Math.Round(weights.Samples * 100, 2);
        double firstTwoShares = Math.Round((weights.Samples + weights.Instruments) * 100, 2);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("실제 반영 비율", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("시료 채취·보존", $"{sampleShare:0.##}%");
        EditorGUILayout.LabelField("측정장비", $"{firstTwoShares - sampleShare:0.##}%");
        EditorGUILayout.LabelField("식물 재배", $"{100 - firstTwoShares:0.##}%");
        EditorGUILayout.LabelField("합계", "100%");
        if (samples.floatValue <= 0 && instruments.floatValue <= 0 && plants.floatValue <= 0)
            EditorGUILayout.HelpBox("세 값이 모두 0이면 같은 비중을 적용합니다.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}
