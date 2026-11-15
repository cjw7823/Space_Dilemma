using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SpaceDilemma.CustomInput
{
    public enum SerialConnectionStatus { Disconnected, Connecting, Connected, WaitingToReconnect, Simulated, Faulted }

    public sealed class SerialInputBackend : IDisposable
    {
        sealed class Frame { public string line, error; public double time; }
        readonly ConcurrentQueue<Frame> inbox = new ConcurrentQueue<Frame>();
        readonly SerialPortSettings settings;
        readonly SerialCalibration calibration;
        readonly Func<ISerialTransport> transportFactory;
        SerialLineDecoder simulatedDecoder;
        Thread thread;
        volatile bool stop;
        volatile bool workerRunning;
        bool requested, simulation, hasPacket, inputReset;
        int lastSequence, lastKnob;
        double openedAt, lastPacketAt, retryAt;
        public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        public SerialController Device { get; private set; }
        public SerialDiagnostics Diagnostics { get; } = new SerialDiagnostics();
        // Absolute K from the last validated packet. Disconnect and timeout clear that sample.
        public int? RawKnobPosition => calibration.knob.enabled ? Diagnostics.raw?.analog[0] : null;
        public SerialConnectionStatus Status { get; private set; }
        public event Action<SerialPacket, SerialControllerState> PacketReceived;

        public SerialInputBackend(SerialPortSettings settings, SerialCalibration calibration, Func<ISerialTransport> transportFactory = null)
        {
            this.settings = settings; this.calibration = calibration;
            this.transportFactory = transportFactory ?? (() => new WindowsSerialTransport());
            simulatedDecoder = new SerialLineDecoder(settings.maximumLineBytes);
            SerialController.Register();
            Device = InputSystem.AddDevice<SerialController>("CustomSerialController");
        }
        public void Connect()
        {
            Disconnect(); requested = true; simulation = false;
            StartWorker();
        }
        void StartWorker()
        {
            if (thread != null && thread.IsAlive) return;
            stop = false; workerRunning = true; Status = SerialConnectionStatus.Connecting;
            openedAt = Now; lastPacketAt = openedAt; hasPacket = false; inputReset = false;
            thread = new Thread(() =>
            {
                try
                {
                    using (var transport = transportFactory())
                    {
                        transport.Open(settings);
                        inbox.Enqueue(new Frame { error = "@connected", time = Now });
                        var decoder = new SerialLineDecoder(settings.maximumLineBytes);
                        var buffer = new byte[1024];
                        while (!stop)
                        {
                            int count = transport.Read(buffer);
                            decoder.Feed(buffer, count,
                                line => inbox.Enqueue(new Frame { line = line, time = Now }),
                                () => inbox.Enqueue(new Frame { error = "Invalid ASCII or overlong packet", time = Now }));
                            if (inbox.Count > 4096) throw new InvalidOperationException("Serial receive queue overflow; connection reset.");
                        }
                    }
                }
                catch (Exception ex) { if (!stop) inbox.Enqueue(new Frame { error = "@fault:" + ex.Message, time = Now }); }
                finally { workerRunning = false; }
            }) { IsBackground = true, Name = "Custom Input Serial" };
            thread.Start();
        }

        // Main thread only, called in InputSystem.onBeforeUpdate before Actions process events.
        public void Pump(double now)
        {
            while (inbox.TryDequeue(out var frame))
            {
                if (frame.error == "@connected") { Status = SerialConnectionStatus.Connected; openedAt = frame.time; }
                else if (frame.error != null)
                {
                    Diagnostics.lastError = frame.error;
                    if (frame.error.StartsWith("@fault:", StringComparison.Ordinal))
                    { ResetInput(); Status = SerialConnectionStatus.Faulted; retryAt = now + settings.reconnectSeconds; }
                    else Diagnostics.invalidPackets++;
                }
                else AcceptLine(frame.line, frame.time);
            }
            if (requested && !simulation && Status == SerialConnectionStatus.Connected &&
                now - lastPacketAt > settings.inputTimeoutSeconds && (hasPacket || now - openedAt > settings.startupGraceSeconds))
            {
                if (!inputReset) { ResetInput(); Diagnostics.timedOut = true; Diagnostics.lastError = "Input timeout"; }
                stop = true; Status = SerialConnectionStatus.WaitingToReconnect; retryAt = now + settings.reconnectSeconds;
            }
            if (requested && !simulation && !workerRunning && (Status == SerialConnectionStatus.Faulted || Status == SerialConnectionStatus.WaitingToReconnect))
            {
                if (settings.reconnect && now >= retryAt) StartWorker();
            }
        }
        void AcceptLine(string line, double time)
        {
            Diagnostics.receivedLines++; Diagnostics.lastLine = line;
            if (!SerialProtocol.TryParse(line, settings, calibration, out var packet))
            { Diagnostics.invalidPackets++; Diagnostics.lastError = "Invalid packet: " + line; return; }
            if (hasPacket && packet.sequence != (lastSequence + 1L) % (settings.maximumSequence + 1L)) Diagnostics.sequenceGaps++;
            var normalized = new SerialControllerState
            {
                joystick1 = calibration.NormalizeStick(packet.analog[2], packet.analog[3], calibration.joystick1X, calibration.joystick1Y),
                joystick2 = calibration.NormalizeStick(packet.analog[4], packet.analog[5], calibration.joystick2X, calibration.joystick2Y),
                knob = calibration.knob.Normalize(packet.analog[0], false),
                slider = calibration.slider.Normalize(packet.analog[1], false)
            };
            if (calibration.knobInterpretation == KnobInterpretation.DeltaBetweenPackets)
            {
                int range = calibration.knob.maximum - calibration.knob.minimum;
                int delta = hasPacket ? packet.analog[0] - lastKnob : 0;
                if (calibration.knobWraps && range > 0)
                { if (delta > range / 2) delta -= range; else if (delta < -range / 2) delta += range; }
                normalized.knob = calibration.knob.enabled && range > 0 ? Mathf.Clamp(delta / (float)range * calibration.knob.sensitivity * (calibration.knob.invert ? -1 : 1), -1, 1) : 0;
            }
            for (int i = 0; i < 4; i++)
                if (calibration.buttonEnabled.Length > i && calibration.buttonEnabled[i] && calibration.buttonBits.Length > i &&
                    calibration.buttonBits[i] >= 0 && calibration.buttonBits[i] <= 30 && (packet.buttons & (1 << calibration.buttonBits[i])) != 0)
                    normalized.buttons |= 1u << i;
            lastSequence = packet.sequence; lastKnob = packet.analog[0]; hasPacket = true; inputReset = false; lastPacketAt = time;
            Diagnostics.packets++; Diagnostics.timedOut = false; Diagnostics.raw = packet; Diagnostics.normalized = normalized;
            // Queue EVERY state, including press+release in one render frame. Never merge button packets.
            if (Device != null && Device.added) InputSystem.QueueStateEvent(Device, normalized);
            PacketReceived?.Invoke(packet, normalized);
        }
        public void BeginSimulation()
        {
            Disconnect(); simulation = true; Status = SerialConnectionStatus.Simulated;
            simulatedDecoder = new SerialLineDecoder(settings.maximumLineBytes);
        }
        public void InjectBytes(byte[] bytes)
        {
            if (!simulation) throw new InvalidOperationException("Begin simulation first. Real serial and simulation cannot share a device.");
            simulatedDecoder.Feed(bytes, bytes.Length,
                line => inbox.Enqueue(new Frame { line = line, time = Now }),
                () => inbox.Enqueue(new Frame { error = "Invalid simulated frame", time = Now }));
        }
        public void InjectLine(string line) => InjectBytes(Encoding.ASCII.GetBytes(line + "\n"));
        public void ResetInput()
        {
            if (Device != null && Device.added) InputSystem.QueueStateEvent(Device, new SerialControllerState());
            inputReset = true; hasPacket = false;
            Diagnostics.normalized = default; Diagnostics.raw = null;
        }
        public void Disconnect()
        {
            requested = false; simulation = false; stop = true;
            // Native Read has a 100ms timeout. No Unity object is touched on this worker.
            if (thread != null && thread.IsAlive) thread.Join(500);
            while (inbox.TryDequeue(out _)) { }
            ResetInput(); Status = SerialConnectionStatus.Disconnected;
        }
        public void Dispose()
        {
            Disconnect();
            if (Device != null && Device.added) InputSystem.RemoveDevice(Device);
            Device = null;
        }
    }
}
