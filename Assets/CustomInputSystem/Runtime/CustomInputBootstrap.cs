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
            var players = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None).Where(p => p.isActiveAndEnabled && p.gameObject.scene == scene).ToArray();
            var hubs = Object.FindObjectsByType<InputHub>(FindObjectsSortMode.None).Where(h => h.isActiveAndEnabled && h.gameObject.scene == scene).ToArray();
            if (players.Length != 1 || hubs.Length != 1)
            { Debug.LogWarning("Custom Input: the scene needs exactly one active PlayerInput and one authored InputHub."); return; }
            bool allowAutomaticSerial = true;
#if UNITY_EDITOR
            // The HUD validation uses virtual input and must not open/reset the user's real COM device.
            allowAutomaticSerial = !UnityEditor.SessionState.GetBool("SpaceHud.PlayValidation", false);
#endif
            var hub = hubs[0]; hub.Initialize(settings, players[0], allowAutomaticSerial);
            var example = hub.GetComponent<CustomInputDemoPanel>();
            if (example) example.Initialize(hub);
        }
    }
}
