using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceDilemma.CustomInput.Editor
{
    [InitializeOnLoad]
    public static class CustomInputSetup
    {
        static CustomInputSetup() => SerialController.Register();
        public const string Root = "Assets/CustomInputSystem";
        [MenuItem("Tools/Custom Input/Create or Locate Default Settings")]
        public static void CreateDefaults()
        {
            Directory.CreateDirectory(Root + "/Resources"); Directory.CreateDirectory(Root + "/Profiles");
            AssetDatabase.Refresh();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/Actions/UnifiedInput.inputactions");
            if (!actions) throw new InvalidOperationException("UnifiedInput.inputactions has not imported.");
            var settings = AssetDatabase.LoadAssetAtPath<CustomInputSettings>(Root + "/Resources/CustomInputSettings.asset");
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<CustomInputSettings>();
                settings.actions = actions;
                settings.gameplay = Profile("Gameplay", "Player", 0, false);
                settings.ui = Profile("UI", "UI", 100, true);
                settings.solarGenerator = Profile("SolarGenerator", "SolarGenerator", 50, true);
                settings.oxygenGenerator = Profile("OxygenGenerator", "OxygenGenerator", 50, true);
                Profile("Facility", "Facility", 50, true);
                AssetDatabase.CreateAsset(settings, Root + "/Resources/CustomInputSettings.asset");
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = settings;
        }
        static InputContextProfile Profile(string id, string map, int priority, bool blocks)
        {
            string path = Root + "/Profiles/" + id + ".asset";
            var result = AssetDatabase.LoadAssetAtPath<InputContextProfile>(path);
            if (result) return result;
            result = ScriptableObject.CreateInstance<InputContextProfile>();
            result.contextId = id; result.actionMap = map; result.priority = priority; result.blocksGameplay = blocks;
            AssetDatabase.CreateAsset(result, path); return result;
        }
        [MenuItem("Tools/Custom Input/Documentation")]
        static void Documentation() => AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(Root + "/README.md"));
    }
}
