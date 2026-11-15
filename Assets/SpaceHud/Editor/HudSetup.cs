using SpaceDilemma.Hud;
using UnityEditor;

static class HudSetup
{
    [MenuItem("Tools/Space HUD/Select Settings")]
    static void SelectSettings()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<HudSettings>("Assets/SpaceHud/Resources/SpaceHudSettings.asset");
        EditorGUIUtility.PingObject(Selection.activeObject);
    }
}
