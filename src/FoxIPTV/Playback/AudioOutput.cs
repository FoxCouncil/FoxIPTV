// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Diagnostics;
    using Classes;
    using SDL;
    using static SDL.SDL3;

    public sealed unsafe class AudioOutput : IDisposable
    {
        public const int Rate = 48000;

        private static bool _sdlReady;

        private readonly object _lock = new object();

        private readonly Stopwatch _watch = Stopwatch.StartNew();

        private SDL_AudioStream* _stream;

        private int _channels;

        private double _endTime = double.NaN;

        private double _deviceLatency;

        private bool _paused = true;

        private double _smoothBase = double.NaN;

        private double _smoothAt;

        private float _gain = 1;

        private double _speed = 1;

        private int _owner;

        public static bool Initialize()
        {
            if (_sdlReady)
            {
                return true;
            }

            SDL_SetAppMetadata("Fox IPTV", TvCore.Version, "com.foxcouncil.foxiptv");

            if (!SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO))
            {
                TvCore.LogError($"[Player] SDL audio failed to start: {SDL_GetError()}");

                return false;
            }

            _sdlReady = true;

            TvCore.LogInfo($"[Player] SDL audio started, driver {SDL_GetCurrentAudioDriver()}");

            return true;
        }

        public int Channels => _channels;

        public bool Open(int channels)
        {
            lock (_lock)
            {
                if (_stream != null && channels == _channels)
                {
                    return true;
                }

                if (_stream != null)
                {
                    var spec = new SDL_AudioSpec { format = SDL_AudioFormat.SDL_AUDIO_F32LE, channels = channels, freq = Rate };

                    if (SDL_SetAudioStreamFormat(_stream, &spec, null))
                    {
                        _channels = channels;

                        return true;
                    }

                    TvCore.LogError($"[Player] Changing to {channels} sound channels failed: {SDL_GetError()}");

                    return false;
                }

                if (!Initialize())
                {
                    return false;
                }

                var wanted = new SDL_AudioSpec { format = SDL_AudioFormat.SDL_AUDIO_F32LE, channels = channels, freq = Rate };

                _stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &wanted, null, IntPtr.Zero);

                if (_stream == null)
                {
                    TvCore.LogError($"[Player] No sound output: {SDL_GetError()}");

                    return false;
                }

                _channels = channels;
                _paused = true;

                SDL_SetAudioStreamGain(_stream, _gain);
                SDL_SetAudioStreamFrequencyRatio(_stream, (float)_speed);

                SDL_AudioSpec device;
                var frames = 0;

                if (SDL_GetAudioDeviceFormat(SDL_GetAudioStreamDevice(_stream), &device, &frames) && device.freq > 0)
                {
                    _deviceLatency = frames / (double)device.freq + 0.01;

                    TvCore.LogInfo($"[Player] Sound output open: device {device.freq}Hz {device.channels}ch, buffer {frames} frames, latency about {_deviceLatency * 1000:0}ms");
                }
                else
                {
                    _deviceLatency = 0.03;
                }

                return true;
            }
        }

        public float Gain
        {
            set
            {
                lock (_lock)
                {
                    _gain = value;

                    if (_stream != null)
                    {
                        SDL_SetAudioStreamGain(_stream, value);
                    }
                }
            }
        }

        public void SetSpeed(int session, double speed)
        {
            lock (_lock)
            {
                if (session != _owner || speed == _speed)
                {
                    return;
                }

                if (_stream != null)
                {
                    SDL_SetAudioStreamFrequencyRatio(_stream, (float)speed);
                }

                _speed = speed;
            }
        }

        public double QueuedSeconds
        {
            get
            {
                lock (_lock)
                {
                    return QueuedSecondsLocked();
                }
            }
        }

        private double QueuedSecondsLocked()
        {
            if (_stream == null || _channels == 0)
            {
                return 0;
            }

            var bytes = SDL_GetAudioStreamQueued(_stream);

            return bytes <= 0 ? 0 : bytes / (double)(Rate * _channels * sizeof(float));
        }

        public int Claim(int session)
        {
            lock (_lock)
            {
                _owner = session;

                ClearLocked();

                return session;
            }
        }

        public bool Write(int session, float[] samples, int frames, double startTime)
        {
            if (frames <= 0)
            {
                return true;
            }

            lock (_lock)
            {
                if (_stream == null || session != _owner)
                {
                    return false;
                }

                fixed (float* data = samples)
                {
                    SDL_PutAudioStreamData(_stream, (IntPtr)data, frames * _channels * sizeof(float));
                }

                _endTime = startTime + frames / (double)Rate;

                return true;
            }
        }

        public double Clock
        {
            get
            {
                lock (_lock)
                {
                    if (double.IsNaN(_endTime))
                    {
                        return double.NaN;
                    }

                    var raw = _endTime - QueuedSecondsLocked() - (_paused ? 0 : _deviceLatency);
                    var now = _watch.Elapsed.TotalSeconds;

                    if (_paused || double.IsNaN(_smoothBase))
                    {
                        _smoothBase = raw;
                        _smoothAt = now;

                        return raw;
                    }

                    var predicted = _smoothBase + (now - _smoothAt) * _speed;
                    var error = raw - predicted;

                    if (Math.Abs(error) > 0.05)
                    {
                        _smoothBase = raw;
                        _smoothAt = now;

                        return raw;
                    }

                    _smoothBase = predicted + error * 0.05;
                    _smoothAt = now;

                    return _smoothBase;
                }
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                if (_stream != null && _paused)
                {
                    SDL_ResumeAudioStreamDevice(_stream);

                    _paused = false;
                    _smoothBase = double.NaN;
                }
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_stream != null && !_paused)
                {
                    SDL_PauseAudioStreamDevice(_stream);

                    _paused = true;
                    _smoothBase = double.NaN;
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _owner = 0;

                ClearLocked();
            }
        }

        private void ClearLocked()
        {
            if (_stream != null)
            {
                SDL_PauseAudioStreamDevice(_stream);
                SDL_ClearAudioStream(_stream);
                SDL_SetAudioStreamFrequencyRatio(_stream, 1);
            }

            _speed = 1;
            _paused = true;
            _endTime = double.NaN;
            _smoothBase = double.NaN;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_stream != null)
                {
                    SDL_DestroyAudioStream(_stream);

                    _stream = null;
                }
            }
        }
    }

    public sealed class WallClock
    {
        private readonly object _lock = new object();

        private readonly Stopwatch _watch = new Stopwatch();

        private double _base = double.NaN;

        private double _speed = 1;

        public double Now
        {
            get
            {
                lock (_lock)
                {
                    return double.IsNaN(_base) ? double.NaN : _base + _watch.Elapsed.TotalSeconds * _speed;
                }
            }
        }

        public double Speed
        {
            get
            {
                lock (_lock)
                {
                    return _speed;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (value == _speed)
                    {
                        return;
                    }

                    if (!double.IsNaN(_base))
                    {
                        _base += _watch.Elapsed.TotalSeconds * _speed;

                        if (_watch.IsRunning)
                        {
                            _watch.Restart();
                        }
                        else
                        {
                            _watch.Reset();
                        }
                    }

                    _speed = value;
                }
            }
        }

        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _watch.IsRunning;
                }
            }
        }

        public void Start(double from)
        {
            lock (_lock)
            {
                if (double.IsNaN(_base))
                {
                    _base = from;
                }

                _watch.Start();
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                _watch.Stop();
            }
        }
    }
}
