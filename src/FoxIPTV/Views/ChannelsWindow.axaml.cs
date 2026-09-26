// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media.Imaging;
    using Avalonia.Platform;
    using Avalonia.Threading;
    using Classes;

    /// <summary>One line of the channel list: a group heading or a channel</summary>
    public class ChannelRow
    {
        /// <summary>What the line says</summary>
        public string Text { get; set; }

        /// <summary>Channels under a heading sit further in</summary>
        public Thickness Indent { get; set; }

        /// <summary>Is this a group heading</summary>
        public bool IsHeading { get; set; }

        /// <summary>Is this channel a favourite</summary>
        public bool IsFavourite { get; set; }

        /// <summary>The heading's group, null for channels</summary>
        public string Group { get; set; }

        /// <summary>The channel's zero based position in <see cref="TvCore.ChannelIndexList"/>, -1 for headings</summary>
        public int ListIndex { get; set; } = -1;
    }

    /// <summary>The channel editor: search the channels, group them by country, favourite the one playing</summary>
    public partial class ChannelsWindow : Window
    {
        /// <summary>The current search text</summary>
        private string _allChannelsFilter = string.Empty;

        /// <summary>How the channels are grouped: None or Countries</summary>
        private string _allChannelCategoryFilter = "None";

        /// <summary>The groups the user has opened, when grouping by country</summary>
        private readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Are we changing the selection ourselves, so it is not taken as the user picking a channel</summary>
        private bool _isChannelChanging;

        /// <summary>The channel whose logo is on its way, so a slow download for a channel we have already left is dropped</summary>
        private string _logoFor;

        /// <summary>Bumped on each reload so a slower, older one is dropped</summary>
        private int _generation;

        /// <summary>Are we filtering channels based on a search term</summary>
        private bool IsAllChannelsSearchFiltered => !string.IsNullOrWhiteSpace(_allChannelsFilter);

        /// <inheritdoc/>
        public ChannelsWindow()
        {
            InitializeComponent();

            SearchTextBox.TextChanged += (sender, args) =>
            {
                _allChannelsFilter = SearchTextBox.Text?.Trim() ?? string.Empty;

                LoadAll();
            };

            FilterNoneButton.Click += ButtonFilter_Click;
            FilterCountriesButton.Click += ButtonFilter_Click;

            ChannelList.SelectionChanged += ChannelList_SelectionChanged;

            FavoriteButton.Click += (sender, args) => ToggleFavourite();

            TvCore.ChannelChanged += newChannel => Dispatcher.UIThread.Post(UpdateGui);

            Opened += (sender, args) => LoadAll();

            Closing += (sender, args) =>
            {
                if (args.CloseReason == WindowCloseReason.ApplicationShutdown || args.CloseReason == WindowCloseReason.OwnerWindowClosing || args.CloseReason == WindowCloseReason.OSShutdown)
                {
                    return;
                }

                // Hidden, not disposed, so it opens again as it was
                args.Cancel = true;

                TvCore.Settings.ChannelEditorOpen = false;
                TvCore.Settings.Save();

                Hide();
            };
        }

        /// <summary>Whether a channel is in the favourites list</summary>
        private static bool IsFavorite(Channel channel)
        {
            return channel != null && TvCore.ChannelFavorites.Contains(channel.Id);
        }

        /// <summary>Build the rows off the UI thread, then show them</summary>
        private async void LoadAll()
        {
            if (TvCore.Channels == null)
            {
                return;
            }

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

        /// <summary>The list as rows: all channels flat, or by the country before the colon in their names</summary>
        private static List<ChannelRow> BuildRows(string filter, string grouping, HashSet<string> expanded)
        {
            var rows = new List<ChannelRow>();

            var tvChannels = TvCore.Channels;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                tvChannels = TvCore.Channels.FindAll(x => x.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

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
                    ListIndex = indexOf.TryGetValue(channel.Index, out var listIndex) ? listIndex : -1
                };
            }

            if (grouping != "Countries")
            {
                rows.Add(new ChannelRow { Text = $"All Channels ({tvChannels.Count})", IsHeading = true });

                rows.AddRange(tvChannels.Select(channel => Row(channel, 12)));

                return rows;
            }

            string CountryOf(Channel channel)
            {
                var split = channel.Name.Trim().Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);

                var countryString = split.Length == 2 ? split[0] : string.Empty;

                return countryString.Contains(" ") ? string.Empty : countryString.ToUpperInvariant();
            }

            var byCountry = tvChannels.GroupBy(CountryOf).ToDictionary(x => x.Key, x => x.ToList());
            var countries = byCountry.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();

            rows.Add(new ChannelRow { Text = $"{grouping} ({countries.Count})", IsHeading = true });

            void AddGroup(string key, string title, List<Channel> channels)
            {
                var open = expanded.Contains(key);

                rows.Add(new ChannelRow { Text = $"{(open ? "▾" : "▸")} {title} ({channels.Count})", IsHeading = true, Group = key, Indent = new Thickness(12, 0, 0, 0) });

                if (open)
                {
                    rows.AddRange(channels.Select(channel => Row(channel, 28)));
                }
            }

            foreach (var country in countries)
            {
                AddGroup(country, country, byCountry[country]);
            }

            if (byCountry.TryGetValue(string.Empty, out var rest) && rest.Count > 0)
            {
                AddGroup("NA", "N/A", rest);
            }

            return rows;
        }

        /// <summary>A heading opens or closes its group and plays its first channel, a channel plays</summary>
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

        /// <summary>Rebuild after a group opens or closes; an opened group plays its first channel, as it always has</summary>
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

        /// <summary>Show the channel playing: selected in the list, with its name, logo and favourite state</summary>
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

            ChannelNameLabel.Text = channel.Index + Environment.NewLine + channel.Name.Replace(": ", Environment.NewLine);

            ShowLogo(channel);

            var favourite = IsFavorite(channel);

            FavoriteButton.Content = favourite ? "★ Unfavourite" : "☆ Favourite";
            if (favourite)
            {
                FavoriteButton.Foreground = Avalonia.Media.Brushes.Lime;
            }
            else
            {
                FavoriteButton.ClearValue(ForegroundProperty);
            }
        }

        /// <summary>One button: favourite the channel that is playing, or take it back out</summary>
        private void ToggleFavourite()
        {
            var channel = TvCore.CurrentChannel;

            if (channel == null)
            {
                return;
            }

            if (IsFavorite(channel))
            {
                TvCore.RemoveFavoriteChannel(channel.Id);
            }
            else
            {
                TvCore.AddFavoriteChannel(channel.Id);
            }

            LoadAll();
        }

        /// <summary>A button to change how the channels are grouped</summary>
        private void ButtonFilter_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!(sender is Button button))
            {
                return;
            }

            _allChannelCategoryFilter = (string)button.Tag;

            FilterNoneButton.IsEnabled = _allChannelCategoryFilter != "None";
            FilterCountriesButton.IsEnabled = _allChannelCategoryFilter != "Countries";

            LoadAll();
        }

        /// <summary>Fetch the channel logo through the image cache and show it trimmed of its empty margins, so it fills the box</summary>
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

        /// <summary>The app icon, for channels with no logo</summary>
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

        /// <summary>Cut the transparent (or single flat colour) margins off a logo</summary>
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

            // Four bytes a pixel with alpha last, in either colour order; the corner is the colour a flat margin would be
            byte cb = pixels[0], cg = pixels[1], cr = pixels[2], ca = pixels[3];
            var flatCorner = ca == 255 && Math.Abs(cr - cg) < 8 && Math.Abs(cg - cb) < 8;

            int left = size.Width, top = size.Height, right = -1, bottom = -1;

            for (var y = 0; y < size.Height; y++)
            {
                for (var x = 0; x < size.Width; x++)
                {
                    var i = y * stride + x * 4;
                    var a = pixels[i + 3];

                    // Empty means fully transparent, or the same flat colour as the corner
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

        /// <summary>A new bitmap from part of a pixel buffer</summary>
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
