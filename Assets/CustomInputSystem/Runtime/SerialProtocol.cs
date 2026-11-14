using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SpaceDilemma.CustomInput
{
    public sealed class SerialPacket
    {
        public int sequence;
        // K, S1, JX1, JY1, JX2, JY2: the new protocol, not the old WPF order.
        public int[] analog;
        public int buttons;
    }

    public static class SerialProtocol
    {
        public static bool TryParse(string line, SerialPortSettings settings, SerialCalibration calibration, out SerialPacket packet)
        {
            packet = null;
            if (string.IsNullOrEmpty(line)) return false;
            var fields = line.Split(',');
            if (fields.Length != 9 || fields[0] != "S") return false;
            int[] values = new int[8];
            for (int i = 0; i < 8; i++)
                if (!int.TryParse(fields[i + 1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i])) return false;
            if (values[0] < 0 || values[7] < 0 || values[0] > settings.maximumSequence || values[7] > settings.maximumButtonMask) return false;
            var axes = calibration.Axes;
            for (int i = 0; i < 6; i++) if (!axes[i].Accepts(values[i + 1])) return false;
            packet = new SerialPacket { sequence = values[0], analog = new int[6], buttons = values[7] };
            Array.Copy(values, 1, packet.analog, 0, 6);
            return true;
        }
    }

    public sealed class SerialLineDecoder
    {
        readonly int maximum;
        readonly StringBuilder pending = new StringBuilder();
        bool invalid;
        public SerialLineDecoder(int maximumBytes) { maximum = Math.Max(2, maximumBytes); }
        public void Feed(byte[] bytes, int count, Action<string> onLine, Action onInvalid)
        {
            for (int i = 0; i < count; i++)
            {
                byte b = bytes[i];
                if (b == 10)
                {
                    if (invalid) onInvalid(); else onLine(pending.ToString().TrimEnd('\r'));
                    pending.Clear(); invalid = false;
                }
                else if (!invalid)
                {
                    if (b > 127 || b < 32 && b != 13 || pending.Length >= maximum - 1) { invalid = true; pending.Clear(); }
                    else pending.Append((char)b);
                }
            }
        }
    }

    public sealed class SerialDiagnostics
    {
        public long packets, invalidPackets, sequenceGaps, receivedLines;
        public bool timedOut;
        public string lastError = "";
        public string lastLine = "";
        public SerialPacket raw;
        public SerialControllerState normalized;
    }
}
