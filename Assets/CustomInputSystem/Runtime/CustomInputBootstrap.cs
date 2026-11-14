using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SpaceDilemma.CustomInput
{
    public static class CustomInputBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Subscribe()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var settings = Resources.Load<CustomInputSettings>("CustomInputSettings");
            if (!settings || !settings.installAutomatically || !settings.sceneNames.Contains(scene.name) || InputHub.Active) return;
            var players = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None).Where(p => p.isActiveAndEnabled).ToArray();
            if (players.Length != 1)
            { Debug.LogWarning("Custom Input: automatic installation requires one active PlayerInput. Install InputHub explicitly for other scenes."); return; }
            var go = new GameObject("Custom Input System");
            SceneManager.MoveGameObjectToScene(go, scene);
            var hub = go.AddComponent<InputHub>();
            hub.Initialize(settings, players[0]);
            go.AddComponent<CustomInputDemoPanel>().Initialize(hub);
            go.AddComponent<UiSelectionScroll>();
        }
    }
}
