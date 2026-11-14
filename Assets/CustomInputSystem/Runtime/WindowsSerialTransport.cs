using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace SpaceDilemma.CustomInput
{
    // Windows native serial API: no paid asset, unmanaged plug-in or extra DLL.
    // Other OS backends can implement this small interface without changing Actions.
    public interface ISerialTransport : IDisposable
    {
        void Open(SerialPortSettings settings);
        int Read(byte[] buffer);
    }

    public sealed class WindowsSerialTransport : ISerialTransport
    {
        SafeFileHandle handle;
        public static string[] GetPortNames()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var chars = new char[65536];
            uint count = QueryDosDevice(null, chars, chars.Length);
            if (count == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            var names = new List<string>();
            foreach (string name in new string(chars, 0, (int)count).Split('\0'))
                if (Regex.IsMatch(name, @"^COM[1-9]\d*$", RegexOptions.IgnoreCase)) names.Add(name);
            names.Sort((a, b) => int.Parse(a.Substring(3)).CompareTo(int.Parse(b.Substring(3))));
            return names.ToArray();
#else
            return Array.Empty<string>();
#endif
        }

        public void Open(SerialPortSettings settings)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!Regex.IsMatch(settings.portName ?? "", @"^COM[1-9]\d*$", RegexOptions.IgnoreCase))
                throw new ArgumentException("Choose a COM port, e.g. COM3.");
            if (settings.baudRate <= 0 || settings.dataBits < 5 || settings.dataBits > 8)
                throw new ArgumentException("Invalid baud rate or data bits.");
            handle = CreateFile(@"\\.\" + settings.portName, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) ThrowLastError();
            try
            {
                var dcb = new Dcb { length = (uint)Marshal.SizeOf<Dcb>() };
                if (!GetCommState(handle, ref dcb)) ThrowLastError();
                dcb.baud = (uint)settings.baudRate;
                // fBinary, fParity, DTR_CONTROL_ENABLE, RTS_CONTROL_ENABLE. No flow control.
                dcb.flags = 1u | (settings.parity != SerialParity.None ? 2u : 0u) |
                    (settings.dtr ? 1u << 4 : 0) | (settings.rts ? 1u << 12 : 0);
                dcb.byteSize = (byte)settings.dataBits;
                dcb.parity = (byte)settings.parity;
                dcb.stopBits = (byte)settings.stopBits;
                if (!SetCommState(handle, ref dcb)) ThrowLastError();
                var timeouts = new Timeouts { readInterval = uint.MaxValue, readMultiplier = uint.MaxValue, readConstant = 100, writeConstant = 200 };
                if (!SetCommTimeouts(handle, ref timeouts)) ThrowLastError();
                if (!PurgeComm(handle, 0x0008)) ThrowLastError(); // discard pre-connection input
            }
            catch { Dispose(); throw; }
#else
            throw new PlatformNotSupportedException("USB Serial backend currently supports Windows Editor/Standalone. HID, keyboard and simulation are cross-platform.");
#endif
        }
        public int Read(byte[] buffer)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!ReadFile(handle, buffer, (uint)buffer.Length, out uint count, IntPtr.Zero)) ThrowLastError();
            return (int)count;
#else
            throw new PlatformNotSupportedException();
#endif
        }
        public void Dispose() { handle?.Dispose(); handle = null; }
        static void ThrowLastError() => throw new Win32Exception(Marshal.GetLastWin32Error());

        [StructLayout(LayoutKind.Sequential)]
        struct Dcb
        {
            public uint length, baud, flags;
            public ushort reserved, xonLimit, xoffLimit;
            public byte byteSize, parity, stopBits;
            public byte xon, xoff, error, eof, evt;
            public ushort reserved1;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Timeouts { public uint readInterval, readMultiplier, readConstant, writeMultiplier, writeConstant; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint QueryDosDevice(string name, [Out] char[] targets, int length);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetCommState(SafeFileHandle file, ref Dcb state);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetCommState(SafeFileHandle file, ref Dcb state);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetCommTimeouts(SafeFileHandle file, ref Timeouts timeouts);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadFile(SafeFileHandle file, byte[] buffer, uint count, out uint read, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool PurgeComm(SafeFileHandle file, uint flags);
    }
}
