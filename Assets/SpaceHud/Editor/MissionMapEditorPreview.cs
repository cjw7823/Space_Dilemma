using SpaceDilemma.Hud;
using UnityEditor;
using UnityEngine;

// Live scene editing support, separate from setup and validation tools.
[InitializeOnLoad]
static class MissionMapEditorPreview
{
    static double next;
    static MissionMapEditorPreview(){EditorApplication.update+=Update;}
    static void Update()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.05;
        RefreshNow();
    }
    internal static void RefreshNow()
    {
        var markers=Object.FindObjectsByType<HudMapMarker>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var map in Object.FindObjectsByType<MinimapController>(FindObjectsSortMode.None))
            if(map.isActiveAndEnabled)map.RefreshPreview(markers);
    }
}
