// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Media.Imaging;
    using Classes;
    using Services;

    public class LibraryTile : INotifyPropertyChanged
    {
        private Bitmap _poster;

        public LibraryItem Item { get; set; }

        public string Caption => Item.ToString();

        public Bitmap Poster
        {
            get => _poster;
            set
            {
                _poster = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Poster)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class LibraryWindow : Window
    {
        private const int ThumbnailWidth = 110;

        private ILibraryProvider _library;

        private bool _isInitialized;

        private LibraryPage _page;

        private Func<int, Task<LibraryPage>> _pageLoader;

        private LibraryItem _details;

        private LibraryItem _playable;

        private List<MediaSource> _sources;

        private int _generation;

        private bool _filling;

        public LibraryWindow()
        {
            InitializeComponent();

            Opened += (sender, args) => Initialize();

            CategoryComboBox.SelectionChanged += CategoryComboBox_SelectionChanged;
            SearchButton.Click += (sender, args) => Search();
            SearchTextBox.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    args.Handled = true;

                    Search();
                }
            };

            PreviousButton.Click += (sender, args) =>
            {
                if (_page != null && _page.Page > 1)
                {
                    LoadPage(_page.Page - 1);
                }
            };

            NextButton.Click += (sender, args) =>
            {
                if (_page != null && _page.Page < _page.TotalPages)
                {
                    LoadPage(_page.Page + 1);
                }
            };

            ItemsList.SelectionChanged += (sender, args) =>
            {
                if (!_filling)
                {
                    ShowDetails(SelectedItem);
                }
            };

            ItemsList.DoubleTapped += (sender, args) =>
            {
                if (_playable != null)
                {
                    Play();
                }
            };

            SeasonComboBox.SelectionChanged += (sender, args) =>
            {
                if (!_filling)
                {
                    LoadEpisodes();
                }
            };

            EpisodesListBox.SelectionChanged += EpisodesListBox_SelectionChanged;

            EpisodesListBox.DoubleTapped += (sender, args) =>
            {
                if (_playable != null && _playable.Kind == LibraryItemKind.Episode)
                {
                    Play();
                }
            };

            PlayButton.Click += (sender, args) => Play();

            Closing += (sender, args) =>
            {
                if (args.CloseReason == WindowCloseReason.ApplicationShutdown || args.CloseReason == WindowCloseReason.OwnerWindowClosing || args.CloseReason == WindowCloseReason.OSShutdown)
                {
                    return;
                }

                args.Cancel = true;

                TvCore.Settings.LibraryOpen = false;
                TvCore.Settings.Save();

                Hide();
            };
        }

        private LibraryItem SelectedItem => (ItemsList.SelectedItem as LibraryTile)?.Item;

        private void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            _library = TvCore.CurrentLibrary;

            Title = $"Fox IPTV - Library - {TvCore.CurrentService?.Title}";

            if (_library == null)
            {
                SetStatus("This provider does not offer a library");

                ToolBar.IsEnabled = false;

                return;
            }

            LoadCategories();
        }

        private async void LoadCategories()
        {
            SetStatus("Loading categories...");

            try
            {
                var categories = await _library.GetCategories();

                _filling = true;

                CategoryComboBox.ItemsSource = categories;

                _filling = false;

                if (categories.Count > 0)
                {
                    CategoryComboBox.SelectedIndex = 0;
                }
                else
                {
                    SetStatus("No categories, use search");
                }
            }
            catch (Exception ex)
            {
                _filling = false;

                SetStatus(ex.Message);
            }
        }

        private async void LoadPage(int page)
        {
            if (_pageLoader == null)
            {
                return;
            }

            var generation = ++_generation;

            SetStatus($"Loading page {page}...");

            ItemsList.IsEnabled = false;

            try
            {
                var result = await _pageLoader(page);

                if (generation != _generation)
                {
                    return;
                }

                _page = result;

                var tiles = FillList();

                SetStatus($"{_page.Items.Count} item(s), page {_page.Page} of {_page.TotalPages}");

                LoadPosters(tiles, generation);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
            finally
            {
                ItemsList.IsEnabled = true;
            }
        }

        private List<LibraryTile> FillList()
        {
            foreach (var old in (ItemsList.ItemsSource as List<LibraryTile>) ?? new List<LibraryTile>())
            {
                old.Poster?.Dispose();
            }

            var tiles = _page.Items.Select(item => new LibraryTile { Item = item }).ToList();

            _filling = true;

            ItemsList.ItemsSource = tiles;

            _filling = false;

            PageLabel.Text = $"{_page.Page} / {_page.TotalPages}";

            PreviousButton.IsEnabled = _page.Page > 1;
            NextButton.IsEnabled = _page.Page < _page.TotalPages;

            return tiles;
        }

        private async void LoadPosters(List<LibraryTile> tiles, int generation)
        {
            using (var throttle = new SemaphoreSlim(4))
            {
                var tasks = tiles.Where(x => x.Item.Poster != null).Select(async tile =>
                {
                    await throttle.WaitAsync();

                    try
                    {
                        if (generation != _generation)
                        {
                            return;
                        }

                        var thumbnail = await Task.Run(async () =>
                        {
                            var bytes = await TvCore.DownloadImageAndCache(tile.Item.Poster.ToString());

                            using (var stream = new MemoryStream(bytes))
                            {
                                return Bitmap.DecodeToWidth(stream, ThumbnailWidth);
                            }
                        });

                        if (generation != _generation)
                        {
                            thumbnail.Dispose();

                            return;
                        }

                        tile.Poster = thumbnail;
                    }
                    catch (Exception ex)
                    {
                        TvCore.LogDebug($"[Library] Poster failed {tile.Item.Poster}: {ex.Message}");
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks);
            }
        }

        private async void ShowDetails(LibraryItem item)
        {
            _details = null;
            _playable = null;
            _sources = null;

            _filling = true;

            SeasonComboBox.ItemsSource = null;
            SeasonComboBox.IsEnabled = false;
            EpisodesListBox.ItemsSource = null;
            EpisodesListBox.IsEnabled = false;
            SourcesComboBox.ItemsSource = null;
            SourcesComboBox.IsEnabled = false;
            PlayButton.IsEnabled = false;

            _filling = false;

            if (item == null)
            {
                TitleLabel.Text = string.Empty;
                MetaLabel.Text = string.Empty;
                OverviewTextBox.Text = string.Empty;
                SetPoster(null);

                return;
            }

            Present(item);

            LoadPoster(item);

            var details = item;

            if (item.Kind != LibraryItemKind.Episode)
            {
                SetStatus($"Loading {item.Title}...");

                try
                {
                    details = await _library.GetDetails(item.Id, item.Kind) ?? item;
                }
                catch (Exception ex)
                {
                    SetStatus(ex.Message);
                }

                if (!ReferenceEquals(SelectedItem, item))
                {
                    return;
                }
            }

            _details = details;

            Present(details);

            if (details.Kind == LibraryItemKind.Series)
            {
                var seasons = details.Seasons.OrderBy(x => x.Number).ToList();

                _filling = true;

                SeasonComboBox.ItemsSource = seasons;

                _filling = false;

                SeasonComboBox.IsEnabled = seasons.Count > 0;

                if (seasons.Count > 0)
                {
                    SeasonComboBox.SelectedIndex = 0;
                }
                else
                {
                    SetStatus("No seasons listed for this series");
                }
            }
            else
            {
                _playable = details;

                PlayButton.IsEnabled = true;

                SetStatus($"Ready to play {details.Title}");
            }
        }

        private void Present(LibraryItem item)
        {
            TitleLabel.Text = string.IsNullOrWhiteSpace(item.Subtitle) ? item.Title : $"{item.Subtitle} - {item.Title}";

            var meta = new List<string>();

            if (!string.IsNullOrWhiteSpace(item.Year))
            {
                meta.Add(item.Year);
            }

            if (item.Rating.HasValue && item.Rating.Value > 0)
            {
                meta.Add($"{item.Rating.Value:0.0}/10");
            }

            if (item.DurationMinutes.HasValue && item.DurationMinutes.Value > 0)
            {
                meta.Add($"{item.DurationMinutes.Value} min");
            }

            meta.Add(item.Kind.ToString());

            MetaLabel.Text = string.Join("  |  ", meta);

            OverviewTextBox.Text = item.Overview ?? string.Empty;
        }

        private void SetPoster(Bitmap image)
        {
            var old = PosterImage.Source as Bitmap;

            PosterImage.Source = image;

            if (old != null && !ReferenceEquals(old, image))
            {
                old.Dispose();
            }
        }

        private async void LoadPoster(LibraryItem item)
        {
            SetPoster(null);

            var url = item.Poster ?? item.Backdrop;

            if (url == null)
            {
                return;
            }

            try
            {
                var image = await Task.Run(async () =>
                {
                    var bytes = await TvCore.DownloadImageAndCache(url.ToString());

                    using (var stream = new MemoryStream(bytes))
                    {
                        return new Bitmap(stream);
                    }
                });

                var selected = SelectedItem;

                if (!IsVisible || selected != null && selected.Id != item.Id && selected.Id != item.SeriesId)
                {
                    image.Dispose();

                    return;
                }

                SetPoster(image);
            }
            catch (Exception ex)
            {
                TvCore.LogDebug($"[Library] Poster failed {url}: {ex.Message}");
            }
        }

        private async void LoadEpisodes()
        {
            var season = SeasonComboBox.SelectedItem as LibrarySeason;

            if (season == null || _details == null)
            {
                return;
            }

            _playable = null;
            _sources = null;
            PlayButton.IsEnabled = false;

            EpisodesListBox.ItemsSource = null;
            EpisodesListBox.IsEnabled = false;

            SetStatus($"Loading {season}...");

            try
            {
                var episodes = await _library.GetEpisodes(_details.Id, season.Number);

                if (!ReferenceEquals(SeasonComboBox.SelectedItem, season))
                {
                    return;
                }

                foreach (var episode in episodes)
                {
                    if (string.IsNullOrEmpty(episode.SeriesId))
                    {
                        episode.SeriesId = _details.Id;
                    }

                    if (!episode.Season.HasValue)
                    {
                        episode.Season = season.Number;
                    }
                }

                _filling = true;

                EpisodesListBox.ItemsSource = episodes;

                _filling = false;

                EpisodesListBox.IsEnabled = episodes.Count > 0;

                SetStatus(episodes.Count == 0 ? "No episodes listed" : $"{episodes.Count} episode(s), pick one");
            }
            catch (Exception ex)
            {
                _filling = false;

                SetStatus(ex.Message);
            }
        }

        private void EpisodesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_filling || !(EpisodesListBox.SelectedItem is LibraryItem episode))
            {
                return;
            }

            _playable = episode;
            _sources = null;

            _filling = true;

            SourcesComboBox.ItemsSource = null;
            SourcesComboBox.IsEnabled = false;

            _filling = false;

            OverviewTextBox.Text = string.IsNullOrWhiteSpace(episode.Overview) ? _details?.Overview ?? string.Empty : episode.Overview;

            MetaLabel.Text = string.Join("  |  ", new[] { episode.Subtitle, episode.Year, episode.DurationMinutes.HasValue ? $"{episode.DurationMinutes} min" : null }.Where(x => !string.IsNullOrWhiteSpace(x)));

            if (episode.Poster != null)
            {
                LoadPoster(episode);
            }

            PlayButton.IsEnabled = true;

            SetStatus($"Ready to play {episode.Subtitle} {episode.Title}");
        }

        private async void Play()
        {
            if (_playable == null)
            {
                return;
            }

            if (_sources == null)
            {
                PlayButton.IsEnabled = false;

                SetStatus($"Finding sources for {_playable.Title}...");

                try
                {
                    _sources = await _library.Resolve(_playable);
                }
                catch (Exception ex)
                {
                    SetStatus(ex.Message);

                    PlayButton.IsEnabled = true;

                    return;
                }

                _filling = true;

                SourcesComboBox.ItemsSource = _sources;

                _filling = false;

                SourcesComboBox.IsEnabled = _sources.Count > 1;

                if (_sources.Count > 0)
                {
                    SourcesComboBox.SelectedIndex = 0;
                }

                PlayButton.IsEnabled = true;

                if (_sources.Count == 0)
                {
                    SetStatus("No sources found for this item");

                    return;
                }
            }

            var chosen = SourcesComboBox.SelectedItem as MediaSource ?? _sources.FirstOrDefault();

            if (chosen == null)
            {
                return;
            }

            var title = _playable.Kind == LibraryItemKind.Episode && _details != null ? $"{_details.Title} {_playable.Subtitle} - {_playable.Title}" : _playable.ToString();

            SetStatus($"Playing {chosen.Name}");

            TvCore.PlayMedia(chosen, title);
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_filling || !(CategoryComboBox.SelectedItem is LibraryCategory category))
            {
                return;
            }

            SearchTextBox.Text = string.Empty;

            _pageLoader = page => _library.Browse(category.Id, page);

            LoadPage(1);
        }

        private void Search()
        {
            var query = SearchTextBox.Text?.Trim() ?? string.Empty;

            if (query.Length == 0)
            {
                if (CategoryComboBox.SelectedItem is LibraryCategory category)
                {
                    _pageLoader = page => _library.Browse(category.Id, page);

                    LoadPage(1);
                }

                return;
            }

            _pageLoader = page => _library.Search(query, page);

            LoadPage(1);
        }

        private void SetStatus(string text)
        {
            StatusLabel.Text = text;
        }
    }
}
