// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Threading.Tasks;
    using Classes;
    using FFmpeg.AutoGen;

    /// <summary>The app's player: one session at a time, sound through SDL3, pictures handed to the video surface</summary>
    public sealed class Player : IDisposable
    {
        private readonly object _lock = new object();

        private readonly AudioOutput _audio = new AudioOutput();

        private PlaybackSession _session;

        private HardwareDevice _hardware;

        private bool _probedHardware;

        private bool _muted;

        public event Action<PlayerState, string> StateChanged;

        public event Action<StreamInfo> InfoChanged;

        public event Action<string> CaptionChanged;

        public event Action<int> BufferChanged;

        public PlayerState State { get; private set; } = PlayerState.Idle;

        public StreamInfo Info { get; private set; } = new StreamInfo();

        public string Caption { get; private set; }

        public StereoMode StereoMode { get; set; } = StereoMode.Stereo;

        /// <summary>Set by the video surface when it draws from system memory rather than GPU textures</summary>
        public bool WantsCpuFrames { get; set; }

        /// <summary>Set by the video surface on Windows once its D3D11 device exists; elsewhere found on first use</summary>
        public HardwareDevice Hardware
        {
            get
            {
                lock (_lock)
                {
                    if (_hardware == null && !_probedHardware && !OperatingSystem.IsWindows())
                    {
                        _probedHardware = true;

                        if (OperatingSystem.IsMacOS())
                        {
                            _hardware = HardwareDevice.Create(AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX);
                        }
                        else if (OperatingSystem.IsLinux())
                        {
                            _hardware = HardwareDevice.Create(AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI);
                        }
                    }

                    return _hardware;
                }
            }
            set
            {
                lock (_lock)
                {
                    _hardware = value;
                }
            }
        }

        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                _audio.Gain = value ? 0 : 1;
            }
        }

        public bool IsActive
        {
            get
            {
                lock (_lock)
                {
                    return _session != null && !_session.IsStopped;
                }
            }
        }

        public double Clock
        {
            get
            {
                lock (_lock)
                {
                    return _session?.Clock ?? double.NaN;
                }
            }
        }

        public PlayerStats Stats
        {
            get
            {
                lock (_lock)
                {
                    return _session?.Stats;
                }
            }
        }

        public void Play(MediaRequest request)
        {
            if (!FFmpegNative.Initialize())
            {
                SetState(PlayerState.Failed, $"FFmpeg failed to load: {FFmpegNative.Failure}");

                return;
            }

            PlaybackSession old;
            PlaybackSession session;

            lock (_lock)
            {
                old = _session;
                session = new PlaybackSession(this, request, _audio);
                _session = session;
            }

            Retire(old);

            TvCore.LogInfo($"[Player] Session {session.Id}: {request.Label ?? request.Uri.ToString()} ({request.Uri})");

            Caption = null;
            CaptionChanged?.Invoke(null);

            session.Start();
        }

        public void Stop()
        {
            PlaybackSession old;

            lock (_lock)
            {
                old = _session;
                _session = null;
            }

            Retire(old);

            _audio.Clear();

            Caption = null;
            CaptionChanged?.Invoke(null);

            SetState(PlayerState.Idle, null);
        }

        private static void Retire(PlaybackSession session)
        {
            if (session == null)
            {
                return;
            }

            session.Stop();

            Task.Run(session.Close);
        }

        /// <summary>The picture due on screen now; called on the UI thread once per screen refresh</summary>
        public VideoFrame TakeFrame(double lead)
        {
            PlaybackSession session;

            lock (_lock)
            {
                session = _session;
            }

            return session?.TakeFrame(lead);
        }

        internal void Report(PlaybackSession session, PlayerState state, string detail)
        {
            if (!IsCurrent(session))
            {
                return;
            }

            SetState(state, detail);
        }

        internal void ReportInfo(PlaybackSession session, StreamInfo info)
        {
            if (!IsCurrent(session))
            {
                return;
            }

            Info = info;

            InfoChanged?.Invoke(info);
        }

        internal void ReportCaption(PlaybackSession session, string caption)
        {
            if (!IsCurrent(session))
            {
                return;
            }

            Caption = string.IsNullOrWhiteSpace(caption) ? null : caption;

            CaptionChanged?.Invoke(Caption);
        }

        internal void ReportBuffer(PlaybackSession session, int percent)
        {
            if (IsCurrent(session))
            {
                BufferChanged?.Invoke(percent);
            }
        }

        private bool IsCurrent(PlaybackSession session)
        {
            lock (_lock)
            {
                return ReferenceEquals(session, _session);
            }
        }

        private void SetState(PlayerState state, string detail)
        {
            State = state;

            StateChanged?.Invoke(state, detail);
        }

        public void Dispose()
        {
            PlaybackSession old;

            lock (_lock)
            {
                old = _session;
                _session = null;
            }

            old?.Close();

            _audio.Dispose();
            _hardware?.Dispose();
        }
    }
}
