using System;
using System.Globalization;
using SpaceDilemma.CustomInput;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDilemma.Hud
{
    public sealed class HudGameSettings : MonoBehaviour
    {
        const string LanguageKey = "SpaceDilemma.Language";
        static readonly string[] StopLabels = { "1", "1.5", "2" };

        [Serializable]
        struct MenuText
        {
            public TMP_Text label;
            public string english;
            public string korean;
        }

        [SerializeField] MenuText[] menuTexts;
        [SerializeField] Button englishButton, koreanButton;
        [SerializeField] TMP_InputField portField, baudField;
        [SerializeField] TMP_Text detectedLabel, dataBitsLabel, parityLabel, stopBitsLabel, dtrLabel, rtsLabel, diagnosticsLabel;

        InputHub hub;
        string[] ports = Array.Empty<string>();
        string feedbackEnglish, feedbackKorean;
        int portIndex = -1, dataBits;
        SerialParity parity;
        SerialStopBits stopBits;
        bool dtr, rts;
        float nextRefresh;
        public Button FirstControl => English ? koreanButton : englishButton;
        public bool English { get; private set; } = true;
        public event Action LanguageChanged;

        public void Initialize(InputHub input)
        {
            hub = input;
            diagnosticsLabel.richText = false;
            English = PlayerPrefs.GetInt(LanguageKey, 0) == 0;
            ApplyLanguage();
        }

        public string Text(string english, string korean) => English ? english : korean;

        public void UseEnglish() => SelectLanguage(true);
        public void UseKorean() => SelectLanguage(false);

        void SelectLanguage(bool english)
        {
            English = english;
            PlayerPrefs.SetInt(LanguageKey, english ? 0 : 1);
            PlayerPrefs.Save();
            ApplyLanguage();
            LanguageChanged?.Invoke();
        }

        void ApplyLanguage()
        {
            foreach (var entry in menuTexts)
                entry.label.text = Text(entry.english, entry.korean);
            // Keep both language buttons available for keyboard and controller navigation.
            englishButton.GetComponentInChildren<TMP_Text>().text = English ? "English (Active)" : "English";
            koreanButton.GetComponentInChildren<TMP_Text>().text = English ? "Korean" : "한국어 (선택)";
            if (isActiveAndEnabled) UpdateFields();
        }

        public void Open()
        {
            var config = hub.settings.serial;
            portField.SetTextWithoutNotify(SerialPortPreferences.IsPortName(config.portName) ? config.portName : "");
            baudField.SetTextWithoutNotify(config.baudRate.ToString(CultureInfo.InvariantCulture));
            dataBits = Mathf.Clamp(config.dataBits, 5, 8);
            parity = config.parity;
            stopBits = config.stopBits;
            dtr = config.dtr;
            rts = config.rts;
            gameObject.SetActive(true);
            RefreshPorts();
            UpdateFields();
        }

        public void RefreshPorts()
        {
            feedbackEnglish = feedbackKorean = null;
            try { ports = WindowsSerialTransport.GetPortNames(); }
            catch (Exception error)
            {
                ports = Array.Empty<string>();
                feedbackEnglish = "Could not list COM ports.";
                feedbackKorean = error.Message;
            }
            portIndex = Array.FindIndex(ports, p => string.Equals(p, portField.text, StringComparison.OrdinalIgnoreCase));
            UpdateFields();
        }

        public void SelectNextPort()
        {
            if (ports.Length == 0) return;
            portIndex = (portIndex + 1) % ports.Length;
            portField.SetTextWithoutNotify(ports[portIndex]);
            UpdateFields();
        }

        public void CycleDataBits() { dataBits = dataBits == 8 ? 5 : dataBits + 1; UpdateFields(); }
        public void CycleParity() { parity = (SerialParity)(((int)parity + 1) % 5); UpdateFields(); }
        public void CycleStopBits() { stopBits = (SerialStopBits)(((int)stopBits + 1) % StopLabels.Length); UpdateFields(); }
        public void ToggleDtr() { dtr = !dtr; UpdateFields(); }
        public void ToggleRts() { rts = !rts; UpdateFields(); }

        public void Connect()
        {
            string port = portField.text.Trim().ToUpperInvariant();
            if (!SerialPortPreferences.IsPortName(port) ||
                !int.TryParse(baudField.text, NumberStyles.None, CultureInfo.InvariantCulture, out int baud) || baud <= 0)
            {
                feedbackEnglish = "Enter a valid COM port (e.g. COM3) and baud rate.";
                feedbackKorean = "COM 포트(예: COM3)와 전송 속도를 확인하세요.";
                RefreshDiagnostics();
                return;
            }
            if ((dataBits == 5 && stopBits == SerialStopBits.Two) ||
                (dataBits != 5 && stopBits == SerialStopBits.OnePointFive))
            {
                feedbackEnglish = "Use 1.5 stop bits with 5 data bits only, and 2 with 6–8.";
                feedbackKorean = "정지 비트 1.5는 데이터 5비트, 2는 데이터 6~8비트에서 사용하세요.";
                RefreshDiagnostics();
                return;
            }

            hub.Serial.Disconnect();
            var config = hub.settings.serial;
            config.portName = port;
            config.baudRate = baud;
            config.dataBits = dataBits;
            config.parity = parity;
            config.stopBits = stopBits;
            config.dtr = dtr;
            config.rts = rts;
            SerialPortPreferences.Save(config);
            feedbackEnglish = feedbackKorean = null;
            hub.Serial.Connect();
            UpdateFields();
        }

        public void Disconnect()
        {
            hub.Serial.Disconnect();
            feedbackEnglish = feedbackKorean = null;
            RefreshDiagnostics();
        }

        void UpdateFields()
        {
            detectedLabel.text = ports.Length == 0 ? Text("No ports found", "감지된 포트 없음") :
                portIndex >= 0 ? $"{ports[portIndex]}  ({portIndex + 1}/{ports.Length})" : Text("Select port", "포트 선택");
            dataBitsLabel.text = dataBits.ToString(CultureInfo.InvariantCulture);
            parityLabel.text = parity.ToString();
            stopBitsLabel.text = StopLabels[(int)stopBits];
            dtrLabel.text = dtr ? "DTR ON" : "DTR OFF";
            rtsLabel.text = rts ? "RTS ON" : "RTS OFF";
            RefreshDiagnostics();
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            RefreshDiagnostics();
        }

        void RefreshDiagnostics()
        {
            if (!hub || hub.Serial == null) return;
            var serial = hub.Serial;
            var d = serial.Diagnostics;
            var n = d.normalized;
            string status = serial.Status switch
            {
                SerialConnectionStatus.Connected => Text("Connected", "연결됨"),
                SerialConnectionStatus.Connecting => Text("Connecting", "연결 중"),
                SerialConnectionStatus.WaitingToReconnect => Text("Waiting to retry", "재연결 대기"),
                SerialConnectionStatus.Faulted => Text("Connection error", "연결 오류"),
                SerialConnectionStatus.Simulated => Text("Simulated", "모의 입력"),
                _ => Text("Disconnected", "연결 안 됨")
            };
            string error = Text(feedbackEnglish, feedbackKorean);
            if (string.IsNullOrEmpty(error))
            {
                if (d.timedOut) error = Text("Input timeout", "입력 시간 초과");
                else if (!string.IsNullOrEmpty(d.lastError))
                    error = Text(EnglishSerialError(d.lastError), d.lastError);
            }
            string lastLine = Clip(d.lastLine);
            string raw = d.raw == null ? "—" : string.Join(",", d.raw.analog) + $" / B={d.raw.buttons}";
            string port = SerialPortPreferences.IsPortName(hub.settings.serial.portName) ? hub.settings.serial.portName : "—";
            diagnosticsLabel.text =
                $"{status}  |  {port}  |  S={d.packets}  {Text("Invalid", "오류 패킷")}={d.invalidPackets}  {Text("Seq gaps", "순번 누락")}={d.sequenceGaps}\n" +
                $"{Text("Last line", "마지막 수신")}  {lastLine}\n{Text("Raw", "원시 값")}  {raw}\n" +
                $"J1 {n.joystick1:F2}   J2 {n.joystick2:F2}   K/S {n.knob:F2}/{n.slider:F2}   B {n.buttons}\n" +
                $"{Text("Message", "메시지")}  {Clip(error)}";
        }

        static string Clip(string value) => string.IsNullOrEmpty(value) ? "—" :
            value.Length > 100 ? value.Substring(0, 97) + "…" : value;

        static string EnglishSerialError(string error)
        {
            if (error.StartsWith("@fault:", StringComparison.Ordinal))
                return "Could not open or read the COM port. Check the connection and port number.";
            if (error.StartsWith("Invalid packet:", StringComparison.Ordinal))
                return "Invalid serial packet. See Last line.";
            if (error == "Invalid ASCII or overlong packet")
                return error;
            return "Serial input error.";
        }
    }
}
