using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceDilemma.CustomInput.Editor
{
    public sealed class CustomInputDebugger : EditorWindow
    {
        string[] ports = Array.Empty<string>();
        string packet = "S,1,500,0,500,500,500,500,0";
        Vector2 scroll;
        [MenuItem("Tools/Custom Input/Debugger")]
        static void Open() => GetWindow<CustomInputDebugger>("Custom Input");
        void OnInspectorUpdate() => Repaint();
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Devices", EditorStyles.boldLabel);
            foreach (var device in InputSystem.devices) EditorGUILayout.LabelField(device.displayName, device.layout + " #" + device.deviceId);
            var hub = InputHub.Active;
            if (!EditorApplication.isPlaying || !hub)
            {
                EditorGUILayout.HelpBox("Enter SampleScene Play mode. Default settings are a ScriptableObject; no WPF/Arduino changes required.", MessageType.Info);
                if (GUILayout.Button("Locate / create settings")) CustomInputSetup.CreateDefaults();
                EditorGUILayout.EndScrollView(); return;
            }
            var serial = hub.Serial; var config = hub.settings.serial;
            EditorGUILayout.ObjectField("Runtime settings / calibration", hub.settings, typeof(CustomInputSettings), false);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Serial: " + serial.Status, EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh COM ports"))
            { try { ports = WindowsSerialTransport.GetPortNames(); } catch (Exception ex) { hub.AddTrace(ex.Message); } }
            if (ports.Length > 0)
            {
                int index = Array.IndexOf(ports, config.portName);
                int next = EditorGUILayout.Popup("Detected COM", index, ports);
                if (next >= 0 && next < ports.Length) config.portName = ports[next];
            }
            config.portName = EditorGUILayout.TextField("COM (manual allowed)", config.portName);
            config.baudRate = EditorGUILayout.IntField("Baud", config.baudRate);
            config.dataBits = EditorGUILayout.IntSlider("Data bits", config.dataBits, 5, 8);
            config.parity = (SerialParity)EditorGUILayout.EnumPopup("Parity", config.parity);
            config.stopBits = (SerialStopBits)EditorGUILayout.EnumPopup("Stop bits", config.stopBits);
            config.dtr = EditorGUILayout.Toggle("DTR", config.dtr); config.rts = EditorGUILayout.Toggle("RTS", config.rts);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect / reconnect")) serial.Connect();
            if (GUILayout.Button("Disconnect")) serial.Disconnect();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Context", hub.CurrentContext.contextId + " (map " + hub.CurrentContext.actionMap + ")");
            EditorGUILayout.LabelField("Active maps", string.Join(", ", hub.Actions.actionMaps.Where(m => m.enabled).Select(m => m.name)));
            foreach (var entry in hub.Contexts.Entries) EditorGUILayout.LabelField("Stack #" + entry.token, entry.profile.contextId + " priority=" + entry.profile.priority);
            if (GUILayout.Button("Toggle example menu (F2 / Start / Serial button2)")) hub.GetComponent<CustomInputDemoPanel>()?.ToggleMenu();
            var d = serial.Diagnostics;
            EditorGUILayout.LabelField("Counters", $"S={d.packets} invalid={d.invalidPackets} seq gaps={d.sequenceGaps} timeout={d.timedOut}");
            EditorGUILayout.LabelField("Last error", d.lastError);
            EditorGUILayout.SelectableLabel("Last line: " + d.lastLine, GUILayout.Height(35));
            if (d.raw != null) EditorGUILayout.LabelField("Raw", string.Join(",", d.raw.analog) + " buttons=" + d.raw.buttons);
            EditorGUILayout.LabelField("Normalized sticks", d.normalized.joystick1 + " / " + d.normalized.joystick2);
            EditorGUILayout.LabelField("Knob / slider", d.normalized.knob.ToString("F3") + " / " + d.normalized.slider.ToString("F3"));
            EditorGUILayout.LabelField("Logical button mask", d.normalized.buttons.ToString());
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Simulation (Editor tool only)", EditorStyles.boldLabel);
            if (GUILayout.Button("Start isolated serial simulation")) serial.BeginSimulation();
            packet = EditorGUILayout.TextArea(packet, GUILayout.MinHeight(45));
            using (new EditorGUI.DisabledScope(serial.Status != SerialConnectionStatus.Simulated))
                if (GUILayout.Button("Inject lines (LF appended)")) serial.InjectBytes(Encoding.ASCII.GetBytes(packet.TrimEnd('\r', '\n') + "\n"));
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Actions / context trace", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(string.Join("\n", hub.Trace.Reverse()), GUILayout.Height(300));
            EditorGUILayout.EndScrollView();
        }
    }
}
