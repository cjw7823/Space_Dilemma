using UnityEngine;
using UnityEngine.InputSystem;
namespace SpaceDilemma.CustomInput
{
    [CreateAssetMenu(menuName = "Custom Input/Settings")]
    public sealed class CustomInputSettings : ScriptableObject
    {
        public bool installAutomatically = true;
        public string[] sceneNames = { "SampleScene" };
        public InputActionAsset actions;
        public InputContextProfile gameplay;
        public InputContextProfile ui;
        public InputContextProfile solarGenerator;
        public InputContextProfile oxygenGenerator;
        public SerialPortSettings serial = new SerialPortSettings();
        public SerialCalibration calibration = new SerialCalibration();
        [Min(0)] public float lookDegreesPerSecond = 120;
    }
}
