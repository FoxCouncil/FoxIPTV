// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media.Imaging;
    using Avalonia.Platform;
    using Avalonia.Threading;
    using Classes;

    public class ChannelRow : INotifyPropertyChanged
    {
        private bool _isFavourite;

        private string _nowTitle;

        public string Text { get; set; }

        public Thickness Indent { get; set; }

        public bool IsHeading { get; set; }

        public bool IsFavourite
        {
            get => _isFavourite;
            set
            {
                _isFavourite = value;

                Changed(nameof(IsFavourite));
                Changed(nameof(Star));
            }
        }

        public string Star => IsFavourite ? "★" : "☆";

        public string NowTitle
        {
            get => _nowTitle;
            set
            {
                _nowTitle = value;

                Changed(nameof(NowTitle));
            }
        }

        public Channel Channel { get; set; }

        public string Group { get; set; }

        public int ListIndex { get; set; } = -1;

        public event PropertyChangedEventHandler PropertyChanged;

        private void Changed(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public partial class ChannelsWindow : Window
    {
        private const string FavoriteGroup = "★";

        private const string OtherGroup = "☆";

        private string _allChannelsFilter = string.Empty;

        private string _allChannelCategoryFilter = "None";

        private readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.Ordinal);

        private bool _isChannelChanging;

        private string _logoFor;

        private int _generation;

        private readonly DispatcherTimer _programmeClock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };

        /// <summary>Are we filtering channels based on a search term</summary>
        private bool IsAllChannelsSearchFiltered => !string.IsNullOrWhiteSpace(_allChannelsFilter);

        public ChannelsWindow()
        {
            InitializeComponent();

            SearchTextBox.TextChanged += (sender, args) =>
            {
                _allChannelsFilter = SearchTextBox.Text?.Trim() ?? string.Empty;

                LoadAll();
            };

            FilterNoneButton.Click += ButtonFilter_Click;
            FilterCategoriesButton.Click += ButtonFilter_Click;
            FilterFavoritesButton.Click += ButtonFilter_Click;

            ChannelList.SelectionChanged += ChannelList_SelectionChanged;

            ChannelList.AddHandler(Button.ClickEvent, StarButton_Click);

            TvCore.ChannelChanged += newChannel => Dispatcher.UIThread.Post(UpdateGui);

            TvCore.ProgrammeChanged += programme => Dispatcher.UIThread.Post(UpdateProgramme);

            TvCore.ChannelListChanged += () => Dispatcher.UIThread.Post(LoadAll);

            _programmeClock.Tick += (sender, args) =>
            {
                if (IsVisible)
                {
                    UpdateProgramme();
                    RefreshOnNow();
                }
            };

            _programmeClock.Start();

            Opened += (sender, args) => LoadAll();

            Closing += (sender, args) =>
            {
                if (args.CloseReason == WindowCloseReason.ApplicationShutdown || args.CloseReason == WindowCloseReason.OwnerWindowClosing || args.CloseReason == WindowCloseReason.OSShutdown)
                {
                    return;
                }

                args.Cancel = true;

                TvCore.Settings.ChannelEditorOpen = false;
                TvCore.Settings.Save();

                Hide();
            };
        }

        private static bool IsFavorite(Channel channel)
        {
            return channel != null && TvCore.ChannelFavorites.Contains(channel.Id);
        }

        private async void LoadAll()
        {
            if (TvCore.Channels == null)
            {
                return;
            }

            UpdateGroupRow();

            var generation = ++_generation;
            var filter = _allChannelsFilter;
            var grouping = _allChannelCategoryFilter;
            var expanded = new HashSet<string>(_expanded);

            var rows = await Task.Run(() => BuildRows(filter, grouping, expanded));

            if (generation != _generation)
            {
                return;
            }

            _isChannelChanging = true;

            ChannelList.ItemsSource = rows;

            _isChannelChanging = false;

            UpdateGui();
        }

        private static List<ChannelRow> BuildRows(string filter, string grouping, HashSet<string> expanded)
        {
            var rows = new List<ChannelRow>();

            var tvChannels = TvCore.Channels;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                tvChannels = TvCore.Channels.FindAll(x => x.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var onNow = OnNowTitles();
            var indexOf = new Dictionary<uint, int>();

            for (var i = 0; i < TvCore.ChannelIndexList.Count; i++)
            {
                indexOf[TvCore.ChannelIndexList[i]] = i;
            }

            ChannelRow Row(Channel channel, double indent)
            {
                return new ChannelRow
                {
                    Text = $"{channel.Index} {channel.Name}",
                    Indent = new Thickness(indent, 0, 0, 0),
                    IsFavourite = IsFavorite(channel),
                    NowTitle = OnNowFor(channel, onNow),
                    Channel = channel,
                    ListIndex = indexOf.TryGetValue(channel.Index, out var listIndex) ? listIndex : -1
                };
            }

            if (grouping != "Categories" && grouping != "Favorites")
            {
                rows.Add(new ChannelRow { Text = $"All Channels ({tvChannels.Count})", IsHeading = true });

                rows.AddRange(tvChannels.Select(channel => Row(channel, 0)));

                return rows;
            }

            void AddGroup(string key, string title, List<Channel> channels)
            {
                var open = expanded.Contains(key);

                rows.Add(new ChannelRow { Text = $"{(open ? "▾" : "▸")} {title} ({channels.Count})", IsHeading = true, Group = key, Indent = new Thickness(12, 0, 0, 0) });

                if (open)
                {
                    rows.AddRange(channels.Select(channel => Row(channel, 16)));
                }
            }

            if (grouping == "Favorites")
            {
                var favorites = tvChannels.Where(IsFavorite).ToList();
                var others = tvChannels.Where(x => !IsFavorite(x)).ToList();

                rows.Add(new ChannelRow { Text = $"All Channels ({tvChannels.Count})", IsHeading = true });

                if (favorites.Count > 0)
                {
                    AddGroup(FavoriteGroup, "Favorite Channels", favorites);
                }

                if (others.Count > 0)
                {
                    AddGroup(OtherGroup, "Other Channels", others);
                }

                return rows;
            }

            var byCategory = tvChannels.GroupBy(x => x.Group?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
            var categories = byCategory.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();

            rows.Add(new ChannelRow { Text = $"{grouping} ({categories.Count})", IsHeading = true });

            foreach (var category in categories)
            {
                AddGroup(category, category, byCategory[category]);
            }

            if (byCategory.TryGetValue(string.Empty, out var rest) && rest.Count > 0)
            {
                AddGroup(string.Empty, "N/A", rest);
            }

            return rows;
        }

        private void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isChannelChanging || !(ChannelList.SelectedItem is ChannelRow row))
            {
                return;
            }

            if (row.IsHeading)
            {
                if (row.Group == null)
                {
                    UpdateGui();

                    return;
                }

                var opening = _expanded.Add(row.Group);

                if (!opening)
                {
                    _expanded.Remove(row.Group);
                }

                _ = ReloadAndPlayFirst(row.Group, opening);

                return;
            }

            if (row.ListIndex >= 0)
            {
                TvCore.SetChannel((uint)row.ListIndex);
            }
        }

        private async Task ReloadAndPlayFirst(string group, bool opened)
        {
            var rows = await Task.Run(() => BuildRows(_allChannelsFilter, _allChannelCategoryFilter, new HashSet<string>(_expanded)));

            _isChannelChanging = true;

            ChannelList.ItemsSource = rows;

            _isChannelChanging = false;

            if (!opened)
            {
                UpdateGui();

                return;
            }

            var headingAt = rows.FindIndex(x => x.Group == group);
            var first = headingAt >= 0 && headingAt + 1 < rows.Count ? rows[headingAt + 1] : null;

            if (first != null && !first.IsHeading && first.ListIndex >= 0)
            {
                TvCore.SetChannel((uint)first.ListIndex);
            }

            UpdateGui();
        }

        private void UpdateGui()
        {
            var channel = TvCore.CurrentChannel;

            if (channel == null)
            {
                return;
            }

            if (!IsAllChannelsSearchFiltered && ChannelList.ItemsSource is List<ChannelRow> rows)
            {
                var current = rows.FirstOrDefault(x => !x.IsHeading && x.ListIndex == (int)TvCore.CurrentChannelIndex);

                if (current != null && !ReferenceEquals(ChannelList.SelectedItem, current))
                {
                    _isChannelChanging = true;

                    ChannelList.SelectedItem = current;
                    ChannelList.ScrollIntoView(current);

                    _isChannelChanging = false;
                }
            }

            ChannelNameLabel.Text = $"{channel.Index} {channel.Name}";

            UpdateProgramme();

            ShowLogo(channel);
        }

        private void UpdateProgramme()
        {
            var programme = TvCore.CurrentProgramme;

            if (programme == null || string.IsNullOrWhiteSpace(programme.Title))
            {
                ProgrammePanel.IsVisible = false;

                return;
            }

            var now = DateTimeOffset.UtcNow;
            var length = (programme.Stop - programme.Start).TotalSeconds;
            var done = length <= 0 ? 0 : Math.Max(0, Math.Min(1, (now - programme.Start).TotalSeconds / length));
            var left = programme.Stop - now;

            ProgrammeTitleLabel.Text = programme.Title;
            ProgrammeTimeLabel.Text = $"{programme.Start.ToLocalTime():t} – {programme.Stop.ToLocalTime():t}{(left > TimeSpan.Zero ? $" · {Remaining(left)} left" : string.Empty)}";
            ProgrammeProgressBar.Value = done * 100;

            if (ProgrammeDescriptionLabel.Text != (programme.Description ?? string.Empty))
            {
                ProgrammeDescriptionScroll.Offset = default;
            }

            ProgrammeDescriptionLabel.Text = programme.Description ?? string.Empty;
            ProgrammeDescriptionLabel.IsVisible = !string.IsNullOrWhiteSpace(programme.Description);

            ToolTip.SetTip(ProgrammeDescriptionLabel, programme.Description);

            ProgrammePanel.IsVisible = true;
        }

        private static string Remaining(TimeSpan left)
        {
            var minutes = (int)Math.Ceiling(left.TotalMinutes);

            return minutes >= 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} min";
        }

        private void StarButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!(e.Source is Button button) || !button.Classes.Contains("star") || !(button.DataContext is ChannelRow row) || row.Channel == null)
            {
                return;
            }

            e.Handled = true;

            if (IsFavorite(row.Channel))
            {
                TvCore.RemoveFavoriteChannel(row.Channel.Id);
            }
            else
            {
                TvCore.AddFavoriteChannel(row.Channel.Id);
            }

            row.IsFavourite = IsFavorite(row.Channel);

            if (_allChannelCategoryFilter == "Favorites")
            {
                LoadAll();
            }
            else
            {
                UpdateGroupRow();
            }
        }

        private static Dictionary<string, string> OnNowTitles()
        {
            var now = DateTimeOffset.UtcNow;
            var titles = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var programme in TvCore.Guide ?? new List<Programme>())
            {
                if (programme.Channel != null && programme.Start <= now && programme.Stop > now && !titles.ContainsKey(programme.Channel))
                {
                    titles[programme.Channel] = programme.Title;
                }
            }

            return titles;
        }

        private static string OnNowFor(Channel channel, Dictionary<string, string> titles)
        {
            if (channel?.Id == null || !titles.TryGetValue(channel.Id, out var title) || string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            title = title.Trim();

            var name = channel.Name.Contains(':') ? channel.Name.Split(new[] { ':' }, 2)[1].Trim() : channel.Name.Trim();

            return string.Equals(title, name, StringComparison.OrdinalIgnoreCase) || string.Equals(title, channel.Name.Trim(), StringComparison.OrdinalIgnoreCase) ? string.Empty : title;
        }

        private void RefreshOnNow()
        {
            if (!(ChannelList.ItemsSource is List<ChannelRow> rows))
            {
                return;
            }

            var titles = OnNowTitles();

            foreach (var row in rows.Where(x => x.Channel != null))
            {
                var title = OnNowFor(row.Channel, titles);

                if (row.NowTitle != title)
                {
                    row.NowTitle = title;
                }
            }
        }

        private void ButtonFilter_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!(sender is Button button))
            {
                return;
            }

            _allChannelCategoryFilter = (string)button.Tag;

            LoadAll();
        }

        private void UpdateGroupRow()
        {
            var total = TvCore.Channels?.Count ?? 0;
            var favorites = TvCore.Channels?.Count(IsFavorite) ?? 0;
            var canGroupFavorites = favorites > 0 && favorites < total;

            if (_allChannelCategoryFilter == "Favorites" && !canGroupFavorites)
            {
                _allChannelCategoryFilter = "None";
            }

            FilterNoneButton.IsEnabled = _allChannelCategoryFilter != "None";
            FilterCategoriesButton.IsEnabled = _allChannelCategoryFilter != "Categories";
            FilterFavoritesButton.IsEnabled = _allChannelCategoryFilter != "Favorites" && canGroupFavorites;

            StationCountLabel.Text = total == 1 ? "1 station" : $"{total} stations";
        }

        private async void ShowLogo(Channel channel)
        {
            _logoFor = channel.Id;

            if (channel.Logo == null)
            {
                ChannelLogo.Source = Placeholder();

                return;
            }

            var url = channel.Logo.ToString();

            Bitmap image = null;

            try
            {
                image = await Task.Run(async () =>
                {
                    var bytes = await TvCore.DownloadImageAndCache(url);

                    if (bytes == null || bytes.Length == 0)
                    {
                        return null;
                    }

                    using (var stream = new System.IO.MemoryStream(bytes))
                    using (var decoded = new Bitmap(stream))
                    {
                        return Trim(decoded);
                    }
                });
            }
            catch (Exception e)
            {
                TvCore.LogDebug($"[.NET] Logo failed for {channel.Name}: {e.Message}");
            }

            if (_logoFor != channel.Id)
            {
                image?.Dispose();

                return;
            }

            var old = ChannelLogo.Source as Bitmap;

            ChannelLogo.Source = image ?? Placeholder();

            if (old != null && !ReferenceEquals(old, ChannelLogo.Source) && !ReferenceEquals(old, _placeholder))
            {
                old.Dispose();
            }
        }

        private static Bitmap _placeholder;

        private static Bitmap Placeholder()
        {
            if (_placeholder == null)
            {
                using (var stream = AssetLoader.Open(new Uri("avares://FoxIPTV/Assets/FoxIPTV.ico")))
                {
                    _placeholder = new Bitmap(stream);
                }
            }

            return _placeholder;
        }

        private static Bitmap Trim(Bitmap source)
        {
            var size = source.PixelSize;
            var stride = size.Width * 4;
            var pixels = new byte[stride * size.Height];

            var pinned = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);

            try
            {
                source.CopyPixels(new PixelRect(size), pinned.AddrOfPinnedObject(), pixels.Length, stride);
            }
            finally
            {
                pinned.Free();
            }

            var format = source.Format ?? PixelFormat.Bgra8888;
            var alpha = source.AlphaFormat ?? AlphaFormat.Premul;

            byte cb = pixels[0], cg = pixels[1], cr = pixels[2], ca = pixels[3];
            var flatCorner = ca == 255 && Math.Abs(cr - cg) < 8 && Math.Abs(cg - cb) < 8;

            int left = size.Width, top = size.Height, right = -1, bottom = -1;

            for (var y = 0; y < size.Height; y++)
            {
                for (var x = 0; x < size.Width; x++)
                {
                    var i = y * stride + x * 4;
                    var a = pixels[i + 3];

                    if (a < 16 || (flatCorner && a == ca && pixels[i] == cb && pixels[i + 1] == cg && pixels[i + 2] == cr))
                    {
                        continue;
                    }

                    if (x < left)
                    {
                        left = x;
                    }

                    if (x > right)
                    {
                        right = x;
                    }

                    if (y < top)
                    {
                        top = y;
                    }

                    if (y > bottom)
                    {
                        bottom = y;
                    }
                }
            }

            if (right < left || bottom < top)
            {
                return Copy(pixels, size, new PixelRect(size), format, alpha);
            }

            const int pad = 2;

            var box = new PixelRect(Math.Max(0, left - pad), Math.Max(0, top - pad), 0, 0);

            box = box.WithWidth(Math.Min(size.Width, right + 1 + pad) - box.X).WithHeight(Math.Min(size.Height, bottom + 1 + pad) - box.Y);

            return Copy(pixels, size, box, format, alpha);
        }

        private static Bitmap Copy(byte[] pixels, PixelSize size, PixelRect box, PixelFormat format, AlphaFormat alpha)
        {
            var stride = size.Width * 4;
            var result = new WriteableBitmap(box.Size, new Vector(96, 96), format, alpha);

            using (var buffer = result.Lock())
            {
                for (var y = 0; y < box.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixels, (box.Y + y) * stride + box.X * 4, buffer.Address + y * buffer.RowBytes, box.Width * 4);
                }
            }

            return result;
        }
    }
}
