using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SpaceDilemma.CustomInput
{
    public static class SerialPortPreferences
    {
        const string Key = "SpaceDilemma.SerialPort";

        public static bool IsPortName(string value) =>
            Regex.IsMatch(value ?? "", @"^COM[1-9][0-9]{0,8}$", RegexOptions.IgnoreCase);

        public static void Load(SerialPortSettings settings)
        {
            var json = PlayerPrefs.GetString(Key);
            if (string.IsNullOrEmpty(json)) return;

            SerialPortSettings saved;
            try { saved = JsonUtility.FromJson<SerialPortSettings>(json); }
            catch (ArgumentException) { return; }
            if (saved == null || !IsPortName(saved.portName) || saved.baudRate <= 0 ||
                saved.dataBits < 5 || saved.dataBits > 8 ||
                !Enum.IsDefined(typeof(SerialParity), saved.parity) ||
                !Enum.IsDefined(typeof(SerialStopBits), saved.stopBits)) return;

            settings.portName = saved.portName;
            settings.baudRate = saved.baudRate;
            settings.dataBits = saved.dataBits;
            settings.parity = saved.parity;
            settings.stopBits = saved.stopBits;
            settings.dtr = saved.dtr;
            settings.rts = saved.rts;
        }

        public static void Save(SerialPortSettings settings)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(settings));
            PlayerPrefs.Save();
        }
    }
}
