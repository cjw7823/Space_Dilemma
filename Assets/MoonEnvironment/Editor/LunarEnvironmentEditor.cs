using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(LunarEnvironment))]
public sealed class LunarEnvironmentEditor : Editor
{
    int handleMode;
    bool dragging;
    Vector3 dragPoint;
    int dragUndoGroup;
    float step = 15;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "showStars", "starTint", "illustrativeStars", "starSizeScale", "starCount", "starSeed");
        if (serializedObject.ApplyModifiedProperties()) Apply();
        var env = (LunarEnvironment)target;
        DrawStarDesign(env);
        var table = env.SunTable;
        if (env.useSunAngleData)
        {
            if (table == null) EditorGUILayout.HelpBox(env.SunDataError ?? "Sun Angle CSV를 연결하세요. 현재 수동 태양 각도를 사용합니다.", MessageType.Error);
            else
            {
                EditorGUI.BeginChangeCheck();
                float hours = EditorGUILayout.Slider("CSV 시간 (시작 후 h)", env.sunHoursFromStart, 0, (float)table.DurationHours);
                if (EditorGUI.EndChangeCheck()) Change("Change Sun Angle time", () => env.sunHoursFromStart = hours);
                var sample = table.Evaluate(env.sunHoursFromStart);
                EditorGUILayout.HelpBox($"{sample.utc:yyyy-MM-dd HH:mm:ss} UTC\n방위각 {sample.azimuth:F6}° / 고도 {sample.elevation:F6}°\n{table.Count}개 기록, 범위 밖은 첫/끝 기록에 고정", MessageType.Info);
            }
        }
        if (env.useCelestialEphemeris)
        {
            var celestial = env.CelestialTable;
            if (celestial == null) EditorGUILayout.HelpBox(env.CelestialDataError ?? "천체력 데이터를 연결하세요. 현재 수동 지구 배치를 사용합니다.", MessageType.Error);
            else
            {
                var frame = env.CelestialFrame;
                EditorGUILayout.HelpBox($"SPICE 지구: 방위각 {frame.earthAzimuth:F4}° / 고도 {frame.earthElevation:F4}°\n각지름 {frame.earthDiameter:F4}° / 밝은 면 {frame.illuminatedFraction:P1}\n지형 중심 기준. 실제 지형에 가려질 수 있습니다.", MessageType.Info);
                EditorGUILayout.HelpBox("천체력 모드에서는 아래 수동 회전값을 적용하지 않습니다. 자유 회전하려면 Use Celestial Ephemeris를 끄세요.", MessageType.None);
            }
        }
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(env.EphemerisActive))
            DrawManualRotation(env);
    }

    void DrawStarDesign(LunarEnvironment env)
    {
        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("별 디자인",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("실제 별자리와 무관한 디자인 요소입니다. 표시·밝기·색은 즉시 반영됩니다.",MessageType.None);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showStars"),new GUIContent("별 표시"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("illustrativeStars"),new GUIContent("밝기"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("starTint"),new GUIContent("색"));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("starSizeScale"),new GUIContent("크기 배율"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("starCount"),new GUIContent("별 개수"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("starSeed"),new GUIContent("배치 시드"));
        if(serializedObject.ApplyModifiedProperties()) Apply();
        EditorGUILayout.HelpBox("크기·개수·시드는 아래 적용 버튼으로 별 지도를 갱신합니다. 같은 시드에서는 별 위치를 유지합니다. 생성한 지도는 모든 참조가 공유합니다.",MessageType.None);
        if(env.StarDesignNeedsBake) EditorGUILayout.HelpBox("별 지도에 아직 반영하지 않은 설정이 있습니다.",MessageType.Warning);
        using(new EditorGUI.DisabledScope(Application.isPlaying))
            if(GUILayout.Button("별 디자인 적용 / 지도 갱신")) LunarStarBaker.ApplyTo(env);
        if(Application.isPlaying) EditorGUILayout.HelpBox("지도 갱신은 Play 모드를 종료한 뒤 실행하세요. 밝기·색·표시는 Play 중에도 바꿀 수 있습니다.",MessageType.None);
    }

    void DrawManualRotation(LunarEnvironment env)
    {
        EditorGUILayout.LabelField("지구 자유 회전", EditorStyles.boldLabel);
        handleMode = GUILayout.Toolbar(handleMode, new[] { "지구 표면", "하늘 위치" });
        EditorGUILayout.HelpBox("아래 원을 드래그하거나 Scene의 회전 고리를 사용하세요. 회전은 Quaternion으로 누적됩니다. Ctrl+Z로 되돌릴 수 있습니다.", MessageType.None);
        Rect rect = GUILayoutUtility.GetRect(160, 160, GUILayout.ExpandWidth(true));
        GUI.Box(rect, GUIContent.none);
        float radius = Mathf.Min(rect.width, rect.height) * 0.43f;
        Vector2 center = rect.center;
        Handles.BeginGUI();
        Handles.color = new Color(0.3f, 0.7f, 1);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
        Quaternion q = handleMode == 0 ? env.EarthOrientation : env.EarthPlacement;
        DrawAxis(q * Vector3.right, Color.red, center, radius);
        DrawAxis(q * Vector3.up, Color.green, center, radius);
        DrawAxis(q * Vector3.forward, Color.cyan, center, radius);
        Handles.EndGUI();
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && (e.mousePosition-center).sqrMagnitude < radius*radius)
        {
            GUIUtility.hotControl = id;
            dragging = true;
            dragPoint = Arcball(e.mousePosition, center, radius);
            Undo.IncrementCurrentGroup();
            dragUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rotate Earth");
            e.Use();
        }
        if (dragging && e.type == EventType.MouseDrag)
        {
            Vector3 point = Arcball(e.mousePosition, center, radius);
            Quaternion delta = Quaternion.FromToRotation(dragPoint, point);
            Undo.RecordObject(env, "Rotate Earth");
            if (handleMode == 0) env.EarthOrientation = delta * env.EarthOrientation;
            else env.EarthPlacement = delta * env.EarthPlacement;
            dragPoint = point;
            Apply();
            e.Use();
        }
        if (dragging && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
        { dragging = false; GUIUtility.hotControl = 0; Undo.CollapseUndoOperations(dragUndoGroup); e.Use(); }
        step = EditorGUILayout.Slider("로컬 축 회전량 (°)", step, 1, 90);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("X +")) Increment(Vector3.right);
            if (GUILayout.Button("Y +")) Increment(Vector3.up);
            if (GUILayout.Button("Z +")) Increment(Vector3.forward);
            if (GUILayout.Button("회전 초기화")) Change("Reset Earth rotation", () =>
            { if (handleMode == 0) env.EarthOrientation = Quaternion.identity; else env.EarthPlacement = Quaternion.identity; });
        }
    }

    static Vector3 Arcball(Vector2 mouse, Vector2 center, float radius)
    {
        Vector2 p = (mouse-center)/radius;
        return new Vector3(p.x, -p.y, Mathf.Sqrt(Mathf.Max(0, 1-p.sqrMagnitude))).normalized;
    }

    static void DrawAxis(Vector3 axis, Color color, Vector2 center, float radius)
    {
        Handles.color = color;
        Handles.DrawLine(center, center + new Vector2(axis.x, -axis.y)*radius);
    }

    void Increment(Vector3 axis)
    {
        var env = (LunarEnvironment)target;
        if (env.EphemerisActive) return;
        Change("Rotate Earth local axis", () =>
        {
            Quaternion delta = Quaternion.AngleAxis(step, axis);
            if (handleMode == 0) env.EarthOrientation *= delta; else env.EarthPlacement *= delta;
        });
    }

    void Change(string label, System.Action action)
    { Undo.RecordObject(target, label); action(); Apply(); }

    void Apply()
    {
        var env = (LunarEnvironment)target;
        env.Apply();
        EditorUtility.SetDirty(env);
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
        SceneView.RepaintAll();
        Repaint();
    }

    void OnSceneGUI()
    {
        var env = (LunarEnvironment)target;
        if (env.EphemerisActive) return;
        Vector3 origin = env.transform.position;
        float size = HandleUtility.GetHandleSize(origin);
        Vector3 anchor = origin + env.EarthDirection * size * 2;
        Handles.color = Color.cyan;
        Handles.DrawDottedLine(origin, anchor, 4);
        Handles.Label(anchor + Vector3.up*size, handleMode == 0 ? "Earth surface rotation" : "Earth sky placement");
        Quaternion before = handleMode == 0 ? env.EarthOrientation : env.EarthPlacement;
        EditorGUI.BeginChangeCheck();
        Quaternion after = Handles.RotationHandle(before, anchor);
        if (EditorGUI.EndChangeCheck()) Change("Rotate Earth", () =>
        { if (handleMode == 0) env.EarthOrientation = after; else env.EarthPlacement = after; });
    }
}

// Keep a persistent template in the serialized scene, with a private preview material in memory.
[InitializeOnLoad]
static class LunarSkySceneSaving
{
    static LunarSkySceneSaving()
    {
        EditorSceneManager.sceneSaving += (scene, path) =>
        {
            foreach (var env in Object.FindObjectsByType<LunarEnvironment>(FindObjectsSortMode.None))
                if (env.gameObject.scene == scene && env.isActiveAndEnabled) RenderSettings.skybox = env.skyTemplate;
        };
        EditorSceneManager.sceneSaved += scene =>
        {
            foreach (var env in Object.FindObjectsByType<LunarEnvironment>(FindObjectsSortMode.None))
                if (env.gameObject.scene == scene && env.isActiveAndEnabled) env.Apply();
        };
        Undo.undoRedoPerformed += () =>
        {
            foreach (var env in Object.FindObjectsByType<LunarEnvironment>(FindObjectsSortMode.None)) env.Apply();
            SceneView.RepaintAll();
        };
    }
}

// Refresh the parsed data on an Editor reimport without reading TextAsset.text in every game frame.
sealed class LunarDataReimport : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
    {
        foreach(var env in Object.FindObjectsByType<LunarEnvironment>(FindObjectsSortMode.None))
        {
            string solar=AssetDatabase.GetAssetPath(env.sunAngleCsv);
            string celestial=AssetDatabase.GetAssetPath(env.celestialEphemeris);
            foreach(string path in imported)
                if(path==solar || path==celestial) { env.InvalidateDataCaches(); break; }
        }
    }
}
