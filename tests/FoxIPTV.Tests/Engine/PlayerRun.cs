// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests.Engine
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using FoxIPTV.Classes;
    using FoxIPTV.Playback;
    using SDL;

    public sealed class PlayerRun : IDisposable
    {
        private readonly object _lock = new object();

        private readonly List<PlayerState> _states = new List<PlayerState>();

        private readonly List<ShownFrame> _frames = new List<ShownFrame>();

        private readonly Thread _pump;

        private volatile bool _stopping;

        private bool _adSeen;

        static PlayerRun()
        {
            SDL3.SDL_SetHint(SDL3.SDL_HINT_AUDIO_DRIVER, "dummy");

            if (!AudioOutput.Initialize() || SDL3.SDL_GetCurrentAudioDriver() != "dummy")
            {
                throw new InvalidOperationException($"Engine tests must play into the silent dummy sound driver, not {SDL3.SDL_GetCurrentAudioDriver() ?? "nothing"}");
            }
        }

        public PlayerRun(MediaRequest request)
        {
            Player = new Player { WantsCpuFrames = true };

            Player.StateChanged += (state, detail) =>
            {
                lock (_lock)
                {
                    _states.Add(state);

                    if (state == PlayerState.Failed)
                    {
                        Failure = detail;
                    }
                }
            };

            Player.Play(request);

            _pump = new Thread(Pump) { IsBackground = true, Name = "Test picture pump" };
            _pump.Start();
        }

        public Player Player { get; }

        public string Failure { get; private set; }

        public PlayerStats Stats { get; private set; }

        public bool AdSeen
        {
            get
            {
                lock (_lock)
                {
                    return _adSeen;
                }
            }
        }

        public IReadOnlyList<PlayerState> States
        {
            get
            {
                lock (_lock)
                {
                    return _states.ToList();
                }
            }
        }

        public IReadOnlyList<ShownFrame> Frames
        {
            get
            {
                lock (_lock)
                {
                    return _frames.ToList();
                }
            }
        }

        public bool WaitFor(PlayerState state, double seconds)
        {
            var watch = Stopwatch.StartNew();

            while (watch.Elapsed.TotalSeconds < seconds)
            {
                lock (_lock)
                {
                    if (_states.Contains(state))
                    {
                        Stats = Player.Stats;

                        return true;
                    }

                    if (_states.Contains(PlayerState.Failed))
                    {
                        return false;
                    }
                }

                Thread.Sleep(20);
            }

            return false;
        }

        public string Describe()
        {
            return $"states {string.Join(", ", States)}; {Frames.Count} frames; failure {Failure ?? "none"}; {Player.Stats}";
        }

        private void Pump()
        {
            while (!_stopping)
            {
                var frame = Player.TakeFrame(0.01);

                if (frame != null)
                {
                    var clock = Player.Clock;

                    lock (_lock)
                    {
                        _frames.Add(new ShownFrame(frame.Time, clock, frame.Width, frame.Height));
                    }

                    frame.Free();
                }

                if (AdDetector.InAd)
                {
                    lock (_lock)
                    {
                        _adSeen = true;
                    }
                }

                Thread.Sleep(5);
            }
        }

        public void Dispose()
        {
            _stopping = true;
            _pump.Join();

            Player.Stop();
            Player.Dispose();
        }
    }

    public readonly record struct ShownFrame(double Time, double Clock, int Width, int Height);
}
