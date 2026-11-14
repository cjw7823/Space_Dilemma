using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace SpaceDilemma.CustomInput
{
    [StructLayout(LayoutKind.Explicit, Size = 28)]
    public struct SerialControllerState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('S', 'C', 'T', 'L');
        [FieldOffset(0), InputControl(layout = "Vector2")] public Vector2 joystick1;
        [FieldOffset(8), InputControl(layout = "Vector2")] public Vector2 joystick2;
        [FieldOffset(16), InputControl(layout = "Axis")] public float knob;
        [FieldOffset(20), InputControl(layout = "Axis")] public float slider;
        [FieldOffset(24)]
        [InputControl(name = "button0", layout = "Button", bit = 0)]
        [InputControl(name = "button1", layout = "Button", bit = 1)]
        [InputControl(name = "button2", layout = "Button", bit = 2)]
        [InputControl(name = "button3", layout = "Button", bit = 3)]
        public uint buttons;
    }

    // Deliberately NOT a Gamepad: real HID Gamepad identities and pairing stay distinct.
    [InputControlLayout(stateType = typeof(SerialControllerState), displayName = "USB Serial Controller")]
    public sealed class SerialController : InputDevice
    {
        public Vector2Control joystick1 { get; private set; }
        public Vector2Control joystick2 { get; private set; }
        public AxisControl knob { get; private set; }
        public AxisControl slider { get; private set; }
        public ButtonControl button0 { get; private set; }
        public ButtonControl button1 { get; private set; }
        public ButtonControl button2 { get; private set; }
        public ButtonControl button3 { get; private set; }
        protected override void FinishSetup()
        {
            base.FinishSetup();
            joystick1 = GetChildControl<Vector2Control>("joystick1"); joystick2 = GetChildControl<Vector2Control>("joystick2");
            knob = GetChildControl<AxisControl>("knob"); slider = GetChildControl<AxisControl>("slider");
            button0 = GetChildControl<ButtonControl>("button0"); button1 = GetChildControl<ButtonControl>("button1");
            button2 = GetChildControl<ButtonControl>("button2"); button3 = GetChildControl<ButtonControl>("button3");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register()
        {
            if (!InputSystem.ListLayouts().Contains(nameof(SerialController))) InputSystem.RegisterLayout<SerialController>();
        }
    }
}
