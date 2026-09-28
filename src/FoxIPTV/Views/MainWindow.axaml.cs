// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Threading;
    using Classes;

    public partial class MainWindow : Window
    {
        private static readonly Dictionary<string, double> AspectRatioConversionTable = new Dictionary<string, double>
        {
            { string.Empty, 0.5625 },
            { "16:9", 0.5625 },
            { "4:3", 0.75 },
            { "1:1", 1.0 },
            { "16:10", 0.625 },
            { "2.21:1", 0.4524886877828054 },
            { "2.35:1", 0.425531914893617 },
            { "2.39:1", 0.4184100418410042 },
            { "5:4", 0.8 }
        };

        /// <summary>The time out in 100ms chunks to wait before retrying the media stream</summary>
        private int _isErrorRetryTimeout = 100;

        private bool _isInitialized;

        /// <summary>Used to determine if currently in a retry error state</summary>
        private bool _isErrorState;

        private bool _isClosing;

        /// <summary>The icons for the current media loaded</summary>
        private TvIconData _currentTvIconData;

        private int _uiFadeoutTime;

        /// <summary>Is the UI currently in channel entry mode</summary>
        private bool _numberEntryMode;

        /// <summary>The time out before submitting the entry to change the channel</summary>
        private int _numberEntryModeTimeout;

        /// <summary>The list of digits entered by the user</summary>
        private readonly List<int> _numberEntryDigits = new List<int>();

        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

        private readonly DispatcherTimer _resizeSettle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        private readonly DispatcherTimer _moveSettle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };

        private GuideWindow _guideWindow;

        private ChannelsWindow _channelsWindow;

        private LibraryWindow _libraryWindow;

        public bool IsFullscreen { get; private set; }

        public MainWindow()
        {
            TvCore.LogDebug("[.NET] MainWindow(): Starting");

            InitializeComponent();

            TvCore.ChannelChanged += TvCoreOnChannelChanged;
            TvCore.MediaChanged += TvCoreOnMediaChanged;
            TvCore.ProgrammeChanged += programme => Ui(GuiShow);

            InitializeContextMenu();

            InitializeTrayIcon();

            InitializeOverlay();

            InitializePlayer();

            InitializeFormDefaults();

            _timer.Tick += Timer_Tick;
            _timer.Start();

            _resizeSettle.Tick += (sender, args) =>
            {
                _resizeSettle.Stop();
                AspectRatioResize();
            };

            _moveSettle.Tick += (sender, args) =>
            {
                _moveSettle.Stop();

                if (WindowState == WindowState.Normal)
                {
                    TvCore.Settings.WindowLeft = Position.X;
                    TvCore.Settings.WindowTop = Position.Y;
                    TvCore.Settings.Save();
                }
            };

            SizeChanged += (sender, args) =>
            {
                CaptionLabel.FontSize = Math.Max(18, Math.Min(64, args.NewSize.Height / 18));

                _resizeSettle.Stop();
                _resizeSettle.Start();
            };

            PositionChanged += (sender, args) =>
            {
                _moveSettle.Stop();
                _moveSettle.Start();
            };

            Closing += MainWindow_Closing;

            KeyUp += (sender, args) => HotKey(args);

            TvCore.LogDebug("[.NET] MainWindow(): Finished starting");
        }

        private static void Ui(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.UIThread.Post(action);
            }
        }

        public async void Start()
        {
            Show();

            TvCore.Settings.Visibility = true;
            TvCore.Settings.Save();

            AspectRatioResizeLater();

            TvCore.ChannelLoadPercentageChanged += percentage => Ui(() =>
            {
                ChannelStatusProgressBar.Value = percentage;
                StatusMessage.Text = LoadingMessage();
            });

            TvCore.GuideLoadPercentageChanged += percentage => Ui(() =>
            {
                GuideStatusProgressBar.Value = percentage;
                StatusMessage.Text = LoadingMessage();
            });

            try
            {
                await TvCore.Start();
            }
            catch (Exception failure)
            {
                TvCore.LogError($"[.NET] Provider failed to load: {failure.Message}");

                StatusMessage.Text = "Provider failed to load";

                if (await Dialogs.YesNo(this, $"{TvCore.CurrentService?.Title} could not load.\n\n{failure.Message}\n\nPick another provider?", "Fox IPTV"))
                {
                    SwitchProvider();
                }

                return;
            }

            ChannelStatusLabel.IsVisible = false;
            ChannelStatusProgressBar.IsVisible = false;
            GuideStatusLabel.IsVisible = false;
            GuideStatusProgressBar.IsVisible = false;

            StatusMessageBox.IsVisible = false;

            _isInitialized = true;

            var hasChannels = TvCore.Channels.Count > 0;

            if (hasChannels && TvCore.Settings.ChannelEditorOpen)
            {
                ChannelsWindowInstance.Show(this);
            }

            if (hasChannels && TvCore.Settings.GuideOpen)
            {
                GuideWindowInstance.Show(this);
            }

            if (hasChannels)
            {
                TvCore.SetChannel(TvCore.Settings.Channel);
            }
            else
            {
                PlayerStatusLabel.Text = "No channels";

                UpdateFormTitle();
            }

            if (TvCore.CurrentLibrary != null && (TvCore.Settings.LibraryOpen || !hasChannels))
            {
                TvCore.Settings.LibraryOpen = true;

                LibraryWindowInstance.Show(this);
            }
        }

        private string LoadingMessage()
        {
            return $"Loading | Channels {ChannelStatusProgressBar.Value:0}% | Guide {GuideStatusProgressBar.Value:0}% | Please Wait...";
        }

        private GuideWindow GuideWindowInstance => _guideWindow ??= new GuideWindow();

        private ChannelsWindow ChannelsWindowInstance => _channelsWindow ??= new ChannelsWindow();

        private LibraryWindow LibraryWindowInstance => _libraryWindow ??= new LibraryWindow();

        private void InitializeFormDefaults()
        {
            if (TvCore.Settings.WindowWidth > 0 && TvCore.Settings.WindowHeight > 0)
            {
                Width = TvCore.Settings.WindowWidth;
                Height = TvCore.Settings.WindowHeight;
            }

            if (TvCore.Settings.WindowLeft.HasValue && TvCore.Settings.WindowTop.HasValue)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint(TvCore.Settings.WindowLeft.Value, TvCore.Settings.WindowTop.Value);
            }

            StatusBar.IsVisible = TvCore.Settings.StatusBar;

            SystemDecorations = TvCore.Settings.Borders ? SystemDecorations.Full : SystemDecorations.None;

            Topmost = TvCore.Settings.AlwaysOnTop;

            CcStatusLabel.Opacity = TvCore.Settings.CCEnabled ? 1 : 0.35;

            Opened += (sender, args) =>
            {
                WindowOpacity.Apply(this, TvCore.Settings.Opacity);

                if (TvCore.Settings.Fullscreen)
                {
                    FullscreenSet(true);
                }
            };
        }

        private void InitializeOverlay()
        {
            OverlayPanel.DoubleTapped += (sender, args) => FullscreenSet(!TvCore.Settings.Fullscreen);

            OverlayPanel.PointerPressed += (sender, args) =>
            {
                if (IsFullscreen || TvCore.Settings.Borders || !args.GetCurrentPoint(OverlayPanel).Properties.IsLeftButtonPressed || args.ClickCount > 1)
                {
                    return;
                }

                BeginMoveDrag(args);
            };
        }

        private void MainWindow_Closing(object sender, WindowClosingEventArgs e)
        {
            if (_isClosing || e.CloseReason == WindowCloseReason.ApplicationShutdown || e.CloseReason == WindowCloseReason.OSShutdown)
            {
                TvCore.LogDebug("[.NET] Closing main window");

                return;
            }

            e.Cancel = true;

            ToggleVisibility();
        }

        private void UpdateFormTitle()
        {
            if (TvCore.CurrentMedia != null)
            {
                Title = TitleOf(TvCore.CurrentMediaTitle, ProviderLabel(), "Fox IPTV");

                return;
            }

            var channelObj = TvCore.CurrentChannel;

            if (channelObj == null)
            {
                Title = TitleOf(ProviderLabel(), "Fox IPTV");

                return;
            }

            var chanName = channelObj.Name.Contains(':') ? channelObj.Name.Split(new[] { ':' }, 2).Skip(1).FirstOrDefault()?.TrimStart() : channelObj.Name;
            var programme = TvCore.CurrentProgramme?.Title?.Trim();
            var channel = $"CH: {channelObj.Index} [ {chanName} ]";

            if (!string.IsNullOrEmpty(programme) && !string.Equals(programme, chanName?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                channel += $" - [ {programme} ]";
            }

            Title = TitleOf(channel, ProviderLabel(), "Fox IPTV");
        }

        private static string TitleOf(params string[] parts)
        {
            return string.Join(" - ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        /// <summary>The source a provider plays from, "Samsung TV Plus" rather than "Free TV Playlists / Samsung TV Plus"</summary>
        private static string ProviderLabel()
        {
            var service = TvCore.CurrentService;

            if (service == null)
            {
                return string.Empty;
            }

            var source = service.Data?["Source"]?.ToString();

            return string.IsNullOrWhiteSpace(source) ? service.Title : source;
        }

        private void GuiShow()
        {
            if (!_isInitialized)
            {
                return;
            }

            if (_numberEntryMode)
            {
                SetOsd(ChannelLabelBox, ChannelLabel, string.Join(string.Empty, _numberEntryDigits.Select(x => x.ToString())));
                SetOsd(ChannelNameLabelBox, ChannelNameLabel, string.Empty);

                _uiFadeoutTime = 0;

                return;
            }

            UpdateFormTitle();

            TagPanel.Children.Clear();

            if (_currentTvIconData?.ClosedCaptioning ?? false)
            {
                AddTag("CC_CC");
            }

            AddTag(_currentTvIconData?.VideoCodec);
            AddTag(_currentTvIconData?.VideoSize);
            AddTag(_currentTvIconData?.FrameRate);
            AddTag(_currentTvIconData?.AudioCodec);
            AddTag(_currentTvIconData?.AudioChannel);
            AddTag(_currentTvIconData?.AudioRate);

            if (TvCore.CurrentMedia != null)
            {
                SetOsd(ChannelLabelBox, ChannelLabel, string.Empty);
                SetOsd(ChannelNameLabelBox, ChannelNameLabel, TvCore.CurrentMediaTitle);
            }
            else
            {
                SetOsd(ChannelLabelBox, ChannelLabel, TvCore.CurrentChannel?.Index.ToString() ?? string.Empty);
                SetOsd(ChannelNameLabelBox, ChannelNameLabel, OverlayLine());
            }

            _uiFadeoutTime = 50;
        }

        private static string OverlayLine()
        {
            var name = (TvCore.CurrentChannel?.Name ?? string.Empty).ToUpperInvariant();
            var programme = TvCore.CurrentProgramme;

            return programme == null || string.IsNullOrWhiteSpace(programme.Title) ? name : $"{programme.Start.ToLocalTime():h:mmtt} - {name}: {programme.Title}";
        }

        private void GuiHide()
        {
            SetOsd(ChannelLabelBox, ChannelLabel, string.Empty);
            SetOsd(ChannelNameLabelBox, ChannelNameLabel, string.Empty);

            TagPanel.Children.Clear();
        }

        private static void SetOsd(Border box, TextBlock label, string text)
        {
            label.Text = text;
            box.IsVisible = !string.IsNullOrEmpty(text);
        }

        private void AddTag(string iconStringKey)
        {
            if (string.IsNullOrWhiteSpace(iconStringKey))
            {
                return;
            }

            iconStringKey = iconStringKey.Trim();

            var underscore = iconStringKey.IndexOf('_');
            var text = underscore >= 0 ? iconStringKey.Substring(underscore + 1) : iconStringKey;

            TagPanel.Children.Add(new StreamTag { Text = text });
        }

        /// <summary>Set the window size to always match the media's aspect ratio</summary>
        private void AspectRatioResize()
        {
            if (WindowState == WindowState.Normal && !IsFullscreen)
            {
                var heightAdjust = StatusBar.IsVisible ? StatusBar.Bounds.Height : 0;

                if (!AspectRatioConversionTable.TryGetValue(TvCore.Settings.AspectRatio ?? string.Empty, out var aspectRatio))
                {
                    aspectRatio = AspectRatioConversionTable[string.Empty];
                }

                double wantedWidth;
                double wantedHeight;

                if (Math.Abs(aspectRatio - 1) > double.Epsilon)
                {
                    wantedWidth = Width;
                    wantedHeight = Math.Round(aspectRatio * Width) + heightAdjust;
                }
                else
                {
                    var maxVal = Math.Min(Width, Height + heightAdjust);

                    wantedWidth = maxVal;
                    wantedHeight = maxVal + heightAdjust;
                }

                if (Math.Abs(wantedWidth - Width) >= 1 || Math.Abs(wantedHeight - Height) >= 1)
                {
                    Width = wantedWidth;
                    Height = wantedHeight;
                }

                TvCore.Settings.WindowWidth = Width;
                TvCore.Settings.WindowHeight = Height;
                TvCore.Settings.Save();
            }
        }

        /// <summary>Set the media error state</summary>
        private void SetErrorState()
        {
            _isErrorState = true;

            _isErrorRetryTimeout = 100;

            StatusMessage.Text = ErrorRetryText();

            StatusMessageBox.IsVisible = true;
        }

        /// <summary>Remove the media error state</summary>
        private void RemoveErrorState()
        {
            _isErrorState = false;

            StatusMessageBox.IsVisible = false;

            StatusMessage.Text = string.Empty;
        }

        private string ErrorRetryText()
        {
            return $"Stream Error: Retrying in {_isErrorRetryTimeout / 10.0:F1} second{(_isErrorRetryTimeout != 10 ? "s" : " ")}";
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Watchdog();

            if (!_isInitialized)
            {
                // Never Run Uninitialized...
                return;
            }

            if (_isErrorState)
            {
                _isErrorRetryTimeout--;

                StatusMessage.Text = ErrorRetryText();

                if (_isErrorRetryTimeout <= 0)
                {
                    RemoveErrorState();

                    PlayCurrent();
                }
            }

            TimerTvIcons();

            TimerAdLabel();

            TimerUiFade();

            if (_isPlaying)
            {
                VideoView.Wake();
            }

            TimerKeyboardEntry();

            MuteLabel.IsVisible = _muted;
        }

        private void TimerAdLabel()
        {
            if (!AdDetector.InAd || !_isPlaying)
            {
                AdLabelBox.IsVisible = false;

                return;
            }

            var text = AdDetector.AdNumber > 1 ? $"(Ad) #{AdDetector.AdNumber}" : "(Ad)";
            var left = AdDetector.SecondsLeft;

            if (left != null)
            {
                var span = TimeSpan.FromSeconds(left.Value);

                text += $" · {(int)span.TotalMinutes}:{span.Seconds:00}";
            }

            AdLabel.Text = text;
            AdLabelBox.IsVisible = true;
        }

        /// <summary>This is used to check for keyboard channel input</summary>
        private void TimerKeyboardEntry()
        {
            if (_numberEntryMode && _numberEntryModeTimeout == 1)
            {
                SetOsd(ChannelLabelBox, ChannelLabel, string.Empty);
                SetOsd(ChannelNameLabelBox, ChannelNameLabel, string.Empty);

                var newChannelNumber = uint.Parse(string.Join(string.Empty, _numberEntryDigits.Select(x => x.ToString())));

                _numberEntryMode = false;
                _numberEntryModeTimeout = 0;
                _numberEntryDigits.Clear();

                if (TvCore.ChannelIndexList == null || !TvCore.ChannelIndexList.Contains(newChannelNumber))
                {
                    return;
                }

                TvCore.SetChannel((uint)TvCore.ChannelIndexList.IndexOf(newChannelNumber));
            }
            else if (_numberEntryMode && _numberEntryModeTimeout > 0)
            {
                _numberEntryModeTimeout--;
            }
        }

        private void TimerUiFade()
        {
            if (_uiFadeoutTime == 1)
            {
                GuiHide();

                _uiFadeoutTime = 0;
            }
            else if (_uiFadeoutTime > 0 && _isPlaying)
            {
                _uiFadeoutTime--;
            }
        }

        private void TimerTvIcons()
        {
            var info = _player.Info;

            if (_uiFadeoutTime == 0 || info == null || info.VideoCodec == null && info.AudioCodec == null)
            {
                return;
            }

            var tmpTvIcons = TvIconData.CreateData(info);

            if (_currentTvIconData != null && _currentTvIconData == tmpTvIcons)
            {
                return;
            }

            _currentTvIconData = tmpTvIcons;

            GuiShow();
        }
    }
}
