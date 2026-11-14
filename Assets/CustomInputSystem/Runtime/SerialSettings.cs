using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceDilemma.CustomInput
{
    public enum SerialParity { None, Odd, Even, Mark, Space }
    public enum SerialStopBits { One, OnePointFive, Two }
    public enum KnobInterpretation { AbsolutePosition, DeltaBetweenPackets }

    [Serializable]
    public sealed class SerialPortSettings
    {
        public bool connectOnStart;
        public string portName = "";
        public int baudRate = 115200;
        [Range(5, 8)] public int dataBits = 8;
        public SerialParity parity;
        public SerialStopBits stopBits;
        public bool dtr = true;
        public bool rts;
        public bool reconnect = true;
        [Min(.1f)] public float reconnectSeconds = 1f;
        [Min(.1f)] public float inputTimeoutSeconds = 1f;
        [Min(0)] public float startupGraceSeconds = 3f;
        [Range(32, 4096)] public int maximumLineBytes = 96;
        [Min(1)] public int maximumSequence = 65535;
        [Min(1)] public int maximumButtonMask = 15;
    }

    [Serializable]
    public sealed class AxisCalibration
    {
        public bool enabled = true;
        public int minimum;
        public int center = 500;
        public int maximum = 1000;
        public bool invert;
        [Range(0, .95f)] public float deadZone;
        [Min(0)] public float sensitivity = 1;

        public bool Accepts(int raw) => minimum < maximum && raw >= minimum && raw <= maximum;
        public float Normalize(int raw, bool bipolar)
        {
            if (!enabled || minimum >= maximum || bipolar && maximum - minimum < 2) return 0;
            float value;
            if (bipolar)
            {
                int neutral = Mathf.Clamp(center, minimum + 1, maximum - 1);
                value = raw >= neutral ? (raw - neutral) / (float)(maximum - neutral) : (raw - neutral) / (float)(neutral - minimum);
                if (invert) value = -value;
                value = Mathf.Sign(value) * Mathf.Max(0, Mathf.Abs(value) - deadZone) / (1 - deadZone);
            }
            else
            {
                value = Mathf.InverseLerp(minimum, maximum, raw);
                if (invert) value = 1 - value;
                value = Mathf.Max(0, value - deadZone) / (1 - deadZone);
            }
            return bipolar ? Mathf.Clamp(value * sensitivity, -1, 1) : Mathf.Clamp01(value * sensitivity);
        }
    }

    [Serializable]
    public sealed class SerialCalibration
    {
        public AxisCalibration knob = new AxisCalibration();
        public AxisCalibration slider = new AxisCalibration();
        public AxisCalibration joystick1X = new AxisCalibration();
        public AxisCalibration joystick1Y = new AxisCalibration();
        public AxisCalibration joystick2X = new AxisCalibration();
        public AxisCalibration joystick2Y = new AxisCalibration();
        [Range(0, .95f)] public float stickDeadZone = .12f;
        public KnobInterpretation knobInterpretation;
        public bool knobWraps;
        public bool[] buttonEnabled = { true, true, true, true };
        public int[] buttonBits = { 0, 1, 2, 3 };
        public AxisCalibration[] Axes => new[] { knob, slider, joystick1X, joystick1Y, joystick2X, joystick2Y };
        public Vector2 NormalizeStick(int x, int y, AxisCalibration xAxis, AxisCalibration yAxis)
        {
            var value = new Vector2(xAxis.Normalize(x, true), yAxis.Normalize(y, true));
            float magnitude = value.magnitude;
            if (magnitude <= stickDeadZone) return Vector2.zero;
            return value.normalized * Mathf.Clamp01((magnitude - stickDeadZone) / (1 - stickDeadZone));
        }
    }

    [Serializable]
    public sealed class BindingOverride
    {
        public string action;
        [Tooltip("Input Actions binding GUID. Empty applies to the supplied binding index instead.")]
        public string bindingId;
        public int bindingIndex;
        public string path;
        public string processors;
    }

}
