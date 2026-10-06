// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Avalonia.Controls;
    using Avalonia.Platform.Storage;
    using Avalonia.Threading;
    using Classes;

    public partial class SettingsWindow : Window
    {
        private static readonly (string Label, double Seconds)[] LiveDelays = { ("Default", 0), ("10 Seconds", 10), ("20 Seconds", 20), ("30 Seconds", 30), ("60 Seconds", 60) };

        private static readonly double[] Opacities = { 1.0, .9, .8, .7, .6, .5, .4, .3, .2, .1 };

        private readonly DispatcherTimer _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        private List<string> _captionChoices = new List<string>();

        private bool _loading;

        public SettingsWindow()
        {
            InitializeComponent();

            TransparencyCombo.ItemsSource = Opacities.Select(x => $"{Math.Round((1 - x) * 100):0}%").ToList();
            LiveDelayCombo.ItemsSource = LiveDelays.Select(x => x.Label).ToList();

            StatusBarCheck.IsCheckedChanged += (sender, args) => Apply(() =>
            {
                if (StatusBarCheck.IsChecked != Host.StatusBarShown)
                {
                    Host.ToggleStatusStrip();
                }
            });

            CaptionsCheck.IsCheckedChanged += (sender, args) => Apply(() =>
            {
                if (CaptionsCheck.IsChecked != TvCore.Settings.CCEnabled)
                {
                    Host.ToggleClosedCaptioning();
                }
            });

            BordersCheck.IsCheckedChanged += (sender, args) => Apply(() =>
            {
                if (BordersCheck.IsChecked != TvCore.Settings.Borders)
                {
                    Host.ToggleBorders();
                }
            });

            AlwaysOnTopCheck.IsCheckedChanged += (sender, args) => Apply(() =>
            {
                if (AlwaysOnTopCheck.IsChecked != Host.Topmost)
                {
                    Host.ToggleAlwaysOnTop();
                }
            });

            TransparencyCombo.SelectionChanged += (sender, args) => Apply(() =>
            {
                if (TransparencyCombo.SelectedIndex >= 0)
                {
                    Host.SetOpacity(Opacities[TransparencyCombo.SelectedIndex]);
                }
            });

            CaptionLanguageCombo.SelectionChanged += (sender, args) => Apply(() =>
            {
                if (CaptionLanguageCombo.SelectedIndex >= 0 && CaptionLanguageCombo.SelectedIndex < _captionChoices.Count)
                {
                    Host.SetCaptionLanguage(_captionChoices[CaptionLanguageCombo.SelectedIndex]);
                }
            });

            LiveDelayCombo.SelectionChanged += (sender, args) => Save(() =>
            {
                if (LiveDelayCombo.SelectedIndex >= 0)
                {
                    TvCore.Settings.LiveDelay = LiveDelays[LiveDelayCombo.SelectedIndex].Seconds;
                }
            });

            UpdatesCheck.IsCheckedChanged += (sender, args) => Save(() => TvCore.Settings.CheckForUpdates = UpdatesCheck.IsChecked == true);
            QuitOnCloseCheck.IsCheckedChanged += (sender, args) => Save(() => TvCore.Settings.QuitOnClose = QuitOnCloseCheck.IsChecked == true);

            AdMuteCheck.IsCheckedChanged += (sender, args) => Save(() => TvCore.Settings.AdMute = AdMuteCheck.IsChecked == true);
            AdLabelCheck.IsCheckedChanged += (sender, args) => Save(() => TvCore.Settings.AdLabel = AdLabelCheck.IsChecked == true);
            AdDimCheck.IsCheckedChanged += (sender, args) => Save(() => TvCore.Settings.AdDim = AdDimCheck.IsChecked == true);
            AdDimSlider.ValueChanged += (sender, args) => Save(() => TvCore.Settings.AdDimLevel = Math.Round(AdDimSlider.Value, 2));
            AdSoundCheck.IsCheckedChanged += (sender, args) => Save(() => TvCore.Settings.AdMediaSound = AdSoundCheck.IsChecked == true);
            AdTitleCheck.IsCheckedChanged += (sender, args) => Save(() => TvCore.Settings.AdTitle = AdTitleCheck.IsChecked == true);

            AdFolderBrowseButton.Click += async (sender, args) =>
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
                var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;

                if (path == null)
                {
                    return;
                }

                TvCore.Settings.AdMediaFolder = path;
                TvCore.Settings.Save();

                AdFolderText.Text = path;
            };

            AdFolderClearButton.Click += (sender, args) =>
            {
                TvCore.Settings.AdMediaFolder = null;
                TvCore.Settings.Save();

                AdFolderText.Text = string.Empty;
            };

            AdTitleAddButton.Click += (sender, args) => AddTitle();

            AdTitleText.KeyDown += (sender, args) =>
            {
                if (args.Key == Avalonia.Input.Key.Enter)
                {
                    AddTitle();

                    args.Handled = true;
                }
            };

            AdTitleRemoveButton.Click += (sender, args) =>
            {
                if (AdTitlesList.SelectedItem is string title)
                {
                    TvCore.Settings.AdMessages.Remove(title);
                    TvCore.Settings.Save();

                    ShowTitles();
                }
            };

            StatsResetButton.Click += (sender, args) =>
            {
                AdStats.Reset();
                TuneStats.Reset();

                ShowStats();
            };

            OpenLogButton.Click += (sender, args) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(TvCore.LogPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    TvCore.LogError($"[Settings] Unable to open the log file: {ex.Message}");
                }
            };

            _refresh.Tick += (sender, args) => ShowStats();

            PropertyChanged += (sender, args) =>
            {
                if (args.Property != IsVisibleProperty)
                {
                    return;
                }

                if (IsVisible)
                {
                    LoadState();

                    _refresh.Start();
                }
                else
                {
                    _refresh.Stop();
                }
            };

            Dialogs.HideOnClose(this, () => { });
        }

        public MainWindow Host { get; set; }

        private static string Time(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Round(seconds));

            return span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes}:{span.Seconds:00}";
        }

        private static string Seconds(long milliseconds)
        {
            return $"{milliseconds / 1000.0:0.00}s";
        }

        private void LoadState()
        {
            _loading = true;

            try
            {
                StatusBarCheck.IsChecked = Host?.StatusBarShown ?? TvCore.Settings.StatusBar;
                CaptionsCheck.IsChecked = TvCore.Settings.CCEnabled;
                BordersCheck.IsChecked = TvCore.Settings.Borders;
                BordersCheck.IsEnabled = Host == null || !Host.IsFullscreen;
                AlwaysOnTopCheck.IsChecked = Host?.Topmost ?? TvCore.Settings.AlwaysOnTop;
                QuitOnCloseCheck.IsChecked = TvCore.Settings.QuitOnClose;

                TransparencyCombo.IsEnabled = WindowOpacity.IsSupported;
                TransparencyCombo.SelectedIndex = Array.FindIndex(Opacities, x => Math.Abs(x - TvCore.Settings.Opacity) < 0.001);

                ShowCaptionLanguages();

                LiveDelayCombo.SelectedIndex = Math.Max(0, Array.FindIndex(LiveDelays, x => Math.Abs(x.Seconds - TvCore.Settings.LiveDelay) < 0.001));

                UpdatesGroup.IsVisible = Updater.IsEnabled;
                UpdatesCheck.IsChecked = TvCore.Settings.CheckForUpdates;

                AdMuteCheck.IsChecked = TvCore.Settings.AdMute;
                AdLabelCheck.IsChecked = TvCore.Settings.AdLabel;
                AdDimCheck.IsChecked = TvCore.Settings.AdDim;
                AdDimSlider.Value = TvCore.Settings.AdDimLevel;
                AdFolderText.Text = TvCore.Settings.AdMediaFolder ?? string.Empty;
                AdSoundCheck.IsChecked = TvCore.Settings.AdMediaSound;
                AdTitleCheck.IsChecked = TvCore.Settings.AdTitle;

                ShowTitles();
                ShowStats();
            }
            finally
            {
                _loading = false;
            }
        }

        private void Apply(Action action)
        {
            if (!_loading && Host != null)
            {
                action();
            }
        }

        private void Save(Action change)
        {
            if (_loading)
            {
                return;
            }

            change();

            TvCore.Settings.Save();
        }

        private void ShowCaptionLanguages()
        {
            var labels = new List<string> { "Automatic" };

            _captionChoices = new List<string> { null };

            foreach (var track in Host?.CaptionTracks ?? Array.Empty<Playback.CaptionTrack>())
            {
                var value = track.Language ?? track.Id;

                if (_captionChoices.Contains(value))
                {
                    continue;
                }

                _captionChoices.Add(value);
                labels.Add(track.Name);
            }

            var saved = TvCore.Settings.CaptionLanguage;

            if (saved != null && !_captionChoices.Contains(saved))
            {
                _captionChoices.Add(saved);
                labels.Add(saved);
            }

            CaptionLanguageCombo.ItemsSource = labels;
            CaptionLanguageCombo.SelectedIndex = Math.Max(0, _captionChoices.IndexOf(saved));
        }

        private void ShowTitles()
        {
            AdTitlesList.ItemsSource = TvCore.Settings.AdMessages.ToList();
        }

        private void AddTitle()
        {
            var title = AdTitleText.Text?.Trim();

            if (string.IsNullOrEmpty(title) || TvCore.Settings.AdMessages.Contains(title))
            {
                return;
            }

            TvCore.Settings.AdMessages.Add(title);
            TvCore.Settings.Save();

            AdTitleText.Text = string.Empty;

            ShowTitles();
        }

        private void ShowStats()
        {
            var total = AdStats.Total();

            AdStatsTotal.Text = $"{total.Breaks} breaks, {total.Ads} ads, {Time(total.Seconds)} of ads";
            AdStatsChannels.ItemsSource = AdStats.PerChannel().Select(x => $"{x.Channel}: {x.Breaks} breaks, {x.Ads} ads, {Time(x.Seconds)}").ToList();

            var tunes = TuneStats.Summary();

            TuneStatsTotal.Text = tunes.Count == 0 ? "0 changes" : $"{tunes.Count} changes, average {Seconds(tunes.Average)}, fastest {Seconds(tunes.Fastest)}, slowest {Seconds(tunes.Slowest)}";
            TuneStatsRecent.ItemsSource = TuneStats.Latest().Select(x => $"{x.What}: {Seconds(x.Milliseconds)}, slowest step {x.SlowestStage} {x.SlowestMilliseconds}ms").ToList();
        }
    }
}
