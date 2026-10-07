// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Media.Imaging;
    using Avalonia.Threading;
    using Classes;

    public partial class GuideWindow : Window
    {
        private readonly DispatcherTimer _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        private List<string> _filters = new List<string>();

        private Channel _detailChannel;

        private Programme _detailProgramme;

        private string _logoFor;

        public GuideWindow()
        {
            InitializeComponent();

            Guide.AttachScrollBar(GuideScrollBar);
            Guide.AttachTimeBar(GuideTimeBar);

            AddHandler(KeyDownEvent, (sender, args) =>
            {
                if (!(args.Source is TextBox) && Guide.Navigate(args.Key))
                {
                    args.Handled = true;
                }
            }, RoutingStrategies.Tunnel);

            ResetViewButton.Click += (sender, args) => Guide.ResetView();

            SearchBox.TextChanged += (sender, args) =>
            {
                ClearSearchButton.IsVisible = !string.IsNullOrEmpty(SearchBox.Text);

                Guide.Search = SearchBox.Text;
            };

            ClearSearchButton.Click += (sender, args) =>
            {
                SearchBox.Text = string.Empty;

                SearchBox.Focus();
            };

            FilterCombo.SelectionChanged += (sender, args) =>
            {
                var index = FilterCombo.SelectedIndex;

                Guide.Filter = index > 0 && index < _filters.Count ? _filters[index] : null;

                ShowStationCount();
            };

            Guide.Selected += ShowDetails;
            Guide.FavouritesChanged += BuildFilters;

            TvCore.ChannelChanged += channel => Dispatcher.UIThread.Post(ShowCurrent);
            TvCore.ProgrammeChanged += programme => Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_detailChannel, TvCore.CurrentChannel))
                {
                    ShowCurrent();
                }
            });
            TvCore.ChannelListChanged += () => Dispatcher.UIThread.Post(BuildFilters);
            TvCore.StateChanged += state => Dispatcher.UIThread.Post(BuildFilters);

            _clock.Tick += (sender, args) =>
            {
                DateTimeLabel.Text = $"{DateTime.Now:F}";

                ShowProgress();
            };
            _clock.Start();

            DateTimeLabel.Text = $"{DateTime.Now:F}";

            Opened += (sender, args) =>
            {
                BuildFilters();
                Guide.ResetView();
                ShowCurrent();

                Guide.Focus();
            };

            Dialogs.HideOnClose(this, () => TvCore.Settings.GuideOpen = false);
        }

        private static string Remaining(TimeSpan left)
        {
            var minutes = (int)Math.Ceiling(left.TotalMinutes);

            return minutes >= 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} min";
        }

        private void BuildFilters()
        {
            var channels = TvCore.Channels ?? new List<Channel>();
            var labels = new List<string> { "All Channels" };
            var current = Guide.Filter;

            _filters = new List<string> { null };

            if (channels.Any(x => TvCore.ChannelFavorites.Contains(x.Id)))
            {
                _filters.Add(GuideView.FavoritesFilter);
                labels.Add("Favorite Channels");
            }

            foreach (var group in channels.Select(x => x.Group?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                _filters.Add(group);
                labels.Add(group);
            }

            FilterCombo.ItemsSource = labels;
            FilterCombo.SelectedIndex = Math.Max(0, _filters.IndexOf(current));

            ShowStationCount();
        }

        private void ShowStationCount()
        {
            var total = Guide.ShownCount;

            StationCountLabel.Text = total == 1 ? "1 station" : $"{total} stations";
        }

        private void ShowCurrent()
        {
            var channel = TvCore.CurrentChannel;

            ShowDetails(channel, Guide.OnNow(channel));
        }

        private void ShowDetails(Channel channel, Programme programme)
        {
            _detailChannel = channel;
            _detailProgramme = programme;

            ChannelNameLabel.Text = channel == null ? string.Empty : $"{channel.Index} {channel.Name}";

            if (programme == null || string.IsNullOrWhiteSpace(programme.Title))
            {
                ProgrammePanel.IsVisible = false;
            }
            else
            {
                ProgrammeTitleLabel.Text = programme.Title;

                if (ProgrammeDescriptionLabel.Text != (programme.Description ?? string.Empty))
                {
                    ProgrammeDescriptionScroll.Offset = default;
                }

                ProgrammeDescriptionLabel.Text = programme.Description ?? string.Empty;
                ProgrammeDescriptionLabel.IsVisible = !string.IsNullOrWhiteSpace(programme.Description);
                ProgrammePanel.IsVisible = true;

                ShowProgress();
            }

            ShowLogo(channel);
        }

        private void ShowProgress()
        {
            var programme = _detailProgramme;

            if (programme == null || !ProgrammePanel.IsVisible)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var length = (programme.Stop - programme.Start).TotalSeconds;
            var done = length <= 0 ? 0 : Math.Max(0, Math.Min(1, (now - programme.Start).TotalSeconds / length));
            var left = programme.Stop - now;
            var started = programme.Start <= now;

            ProgrammeTimeLabel.Text = $"{programme.Start.ToLocalTime():ddd} {programme.Start.ToLocalTime():t} – {programme.Stop.ToLocalTime():t}{(started && left > TimeSpan.Zero ? $" · {Remaining(left)} left" : string.Empty)}";
            ProgrammeProgressBar.Value = done * 100;
        }

        private async void ShowLogo(Channel channel)
        {
            _logoFor = channel?.Id;

            var image = await ChannelLogos.Load(channel);

            if (_logoFor != channel?.Id)
            {
                image?.Dispose();

                return;
            }

            var old = ChannelLogo.Source as Bitmap;

            ChannelLogo.Source = image ?? ChannelLogos.Placeholder;

            if (old != null && !ReferenceEquals(old, ChannelLogo.Source) && !ReferenceEquals(old, ChannelLogos.Placeholder))
            {
                old.Dispose();
            }
        }
    }
}
