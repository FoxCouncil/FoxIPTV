// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Classes;

    public sealed class MuteSwitch
    {
        public bool AppMuted { get; private set; }

        public bool SystemMuted { get; set; }

        public bool Muted => AppMuted || SystemMuted;

        public void Toggle()
        {
            AppMuted = !AppMuted;
        }
    }

    public static class SystemMute
    {
        private const int PollMilliseconds = 500;

        private static readonly object _lock = new object();

        private static Thread _thread;

        private static volatile bool _muted;

        public static bool IsMuted => _muted;

        public static void Start()
        {
            lock (_lock)
            {
                if (_thread != null)
                {
                    return;
                }

                _thread = new Thread(Poll) { IsBackground = true, Name = "System mute" };
                _thread.Start();
            }
        }

        private static void Poll()
        {
            while (true)
            {
                bool muted;

                try
                {
                    muted = Read();
                }
                catch (Exception ex)
                {
                    _muted = false;

                    TvCore.LogError($"[Audio] Reading the system mute failed, it is no longer watched: {ex.Message.Split('\n')[0].Trim()}");

                    return;
                }

                if (muted != _muted)
                {
                    _muted = muted;

                    TvCore.LogInfo($"[Audio] System sound {(muted ? "muted" : "unmuted")}");
                }

                Thread.Sleep(PollMilliseconds);
            }
        }

        private static bool Read()
        {
            if (OperatingSystem.IsWindows())
            {
                return WindowsSound.Muted();
            }

            if (OperatingSystem.IsMacOS())
            {
                return MacSound.Muted();
            }

            if (OperatingSystem.IsLinux())
            {
                return PulseSound.Muted();
            }

            return false;
        }
    }

    internal static unsafe class WindowsSound
    {
        private const uint AllContexts = 23;

        private const int RenderFlow = 0;

        private const int ConsoleRole = 0;

        private const int ReleaseSlot = 2;

        private const int ActivateSlot = 3;

        private const int GetDefaultAudioEndpointSlot = 4;

        private const int GetMuteSlot = 15;

        private static readonly Guid EnumeratorClass = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");

        private static readonly Guid EnumeratorId = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");

        private static readonly Guid EndpointVolumeId = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");

        private static IntPtr _enumerator;

        public static bool Muted()
        {
            if (_enumerator == IntPtr.Zero)
            {
                CoInitializeEx(IntPtr.Zero, 0);

                var enumeratorClass = EnumeratorClass;
                var enumeratorId = EnumeratorId;
                IntPtr enumerator;

                if (CoCreateInstance(&enumeratorClass, IntPtr.Zero, AllContexts, &enumeratorId, &enumerator) < 0)
                {
                    return false;
                }

                _enumerator = enumerator;
            }

            var device = IntPtr.Zero;
            var volume = IntPtr.Zero;

            try
            {
                if (((delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr*, int>)Slot(_enumerator, GetDefaultAudioEndpointSlot))(_enumerator, RenderFlow, ConsoleRole, &device) < 0)
                {
                    return false;
                }

                var volumeId = EndpointVolumeId;

                if (((delegate* unmanaged[Stdcall]<IntPtr, Guid*, uint, IntPtr, IntPtr*, int>)Slot(device, ActivateSlot))(device, &volumeId, AllContexts, IntPtr.Zero, &volume) < 0)
                {
                    return false;
                }

                var muted = 0;

                return ((delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Slot(volume, GetMuteSlot))(volume, &muted) >= 0 && muted != 0;
            }
            finally
            {
                Release(volume);
                Release(device);
            }
        }

        private static void Release(IntPtr unknown)
        {
            if (unknown != IntPtr.Zero)
            {
                ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(unknown, ReleaseSlot))(unknown);
            }
        }

        private static void* Slot(IntPtr unknown, int index)
        {
            return (*(void***)unknown)[index];
        }

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern int CoInitializeEx(IntPtr reserved, uint model);

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern int CoCreateInstance(Guid* classId, IntPtr outer, uint context, Guid* interfaceId, IntPtr* instance);
    }

    internal static unsafe class MacSound
    {
        private const string CoreAudio = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";

        private const uint SystemObject = 1;

        private const uint DefaultOutputDevice = 0x644F7574;

        private const uint Mute = 0x6D757465;

        private const uint GlobalScope = 0x676C6F62;

        private const uint OutputScope = 0x6F757470;

        public static bool Muted()
        {
            var address = new PropertyAddress { Selector = DefaultOutputDevice, Scope = GlobalScope };
            uint device = 0;
            var size = (uint)sizeof(uint);

            if (AudioObjectGetPropertyData(SystemObject, &address, 0, IntPtr.Zero, &size, &device) != 0 || device == 0)
            {
                return false;
            }

            address = new PropertyAddress { Selector = Mute, Scope = OutputScope };
            uint muted = 0;
            size = sizeof(uint);

            return AudioObjectGetPropertyData(device, &address, 0, IntPtr.Zero, &size, &muted) == 0 && muted != 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyAddress
        {
            public uint Selector;

            public uint Scope;

            public uint Element;
        }

        [DllImport(CoreAudio)]
        private static extern int AudioObjectGetPropertyData(uint objectId, PropertyAddress* address, uint qualifierSize, IntPtr qualifier, uint* size, void* data);
    }

    internal static unsafe class PulseSound
    {
        private const string Pulse = "libpulse.so.0";

        private const string DefaultSink = "@DEFAULT_SINK@";

        private const int NoAutoSpawn = 1;

        private const int ContextReady = 4;

        private const int ContextFailed = 5;

        private const int OperationRunning = 0;

        private static readonly int SinkMuteOffset = IntPtr.Size * 3 + 280;

        private static IntPtr _loop;

        private static IntPtr _context;

        private static bool _sinkMuted;

        public static bool Muted()
        {
            if (_context == IntPtr.Zero && !Connect())
            {
                return false;
            }

            _sinkMuted = false;

            var operation = pa_context_get_sink_info_by_name(_context, DefaultSink, &OnSink, IntPtr.Zero);

            if (operation == IntPtr.Zero)
            {
                Disconnect();

                return false;
            }

            while (pa_operation_get_state(operation) == OperationRunning)
            {
                if (pa_mainloop_iterate(_loop, 1, null) < 0 || pa_context_get_state(_context) != ContextReady)
                {
                    pa_operation_unref(operation);

                    Disconnect();

                    return false;
                }
            }

            pa_operation_unref(operation);

            return _sinkMuted;
        }

        private static bool Connect()
        {
            _loop = pa_mainloop_new();
            _context = pa_context_new(pa_mainloop_get_api(_loop), "Fox IPTV");

            if (pa_context_connect(_context, null, NoAutoSpawn, IntPtr.Zero) < 0)
            {
                Disconnect();

                return false;
            }

            while (true)
            {
                var state = pa_context_get_state(_context);

                if (state == ContextReady)
                {
                    return true;
                }

                if (state >= ContextFailed || pa_mainloop_iterate(_loop, 1, null) < 0)
                {
                    Disconnect();

                    return false;
                }
            }
        }

        private static void Disconnect()
        {
            if (_context != IntPtr.Zero)
            {
                pa_context_disconnect(_context);
                pa_context_unref(_context);

                _context = IntPtr.Zero;
            }

            if (_loop != IntPtr.Zero)
            {
                pa_mainloop_free(_loop);

                _loop = IntPtr.Zero;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void OnSink(IntPtr context, IntPtr sink, int end, IntPtr data)
        {
            if (end == 0 && sink != IntPtr.Zero)
            {
                _sinkMuted = *(int*)(sink + SinkMuteOffset) != 0;
            }
        }

        [DllImport(Pulse)]
        private static extern IntPtr pa_mainloop_new();

        [DllImport(Pulse)]
        private static extern IntPtr pa_mainloop_get_api(IntPtr loop);

        [DllImport(Pulse)]
        private static extern int pa_mainloop_iterate(IntPtr loop, int block, int* result);

        [DllImport(Pulse)]
        private static extern void pa_mainloop_free(IntPtr loop);

        [DllImport(Pulse)]
        private static extern IntPtr pa_context_new(IntPtr api, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport(Pulse)]
        private static extern int pa_context_connect(IntPtr context, [MarshalAs(UnmanagedType.LPUTF8Str)] string server, int flags, IntPtr spawn);

        [DllImport(Pulse)]
        private static extern int pa_context_get_state(IntPtr context);

        [DllImport(Pulse)]
        private static extern void pa_context_disconnect(IntPtr context);

        [DllImport(Pulse)]
        private static extern void pa_context_unref(IntPtr context);

        [DllImport(Pulse)]
        private static extern IntPtr pa_context_get_sink_info_by_name(IntPtr context, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, IntPtr, void> callback, IntPtr data);

        [DllImport(Pulse)]
        private static extern int pa_operation_get_state(IntPtr operation);

        [DllImport(Pulse)]
        private static extern void pa_operation_unref(IntPtr operation);
    }
}
