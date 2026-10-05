// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Primitives;
    using Avalonia.Input;
    using Avalonia.Media;
    using Avalonia.Media.TextFormatting;
    using Avalonia.Styling;
    using Avalonia.Threading;
    using Classes;

    public sealed class GuideView : Control
    {
        private const double HeaderHeight = 36;

        private const double RowHeight = 56;

        private const double NumberWidth = 64;

        private const double NameWidth = 220;

        private const double LogoWidth = 64;

        private const int VisibleMinutes = 150;

        private const int StepMinutes = 30;

        private static readonly FontFamily Face = new FontFamily("Segoe UI, Inter, Helvetica, Arial");

        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };

        private Dictionary<string, List<Programme>> _byChannel;

        private int _guideVersion = -1;

        private int _topChannel;

        private DateTimeOffset? _startUtc;

        private bool _dataLoaded;

        private Programme _hoverProgramme;

        private string _search = string.Empty;

        private List<int> _shown;

        private readonly List<Tuple<Rect, object>> _hits = new List<Tuple<Rect, object>>();

        public ScrollBar ScrollBar { get; set; }

        private ScrollBar _timeBar;

        private DateTimeOffset _guideEndUtc;

        public GuideView()
        {
            Focusable = true;
            ClipToBounds = true;

            _timer.Tick += (sender, args) => InvalidateVisual();
            _timer.Start();

            _dataLoaded = TvCore.State == TvCoreState.Running;

            TvCore.StateChanged += state => Dispatcher.UIThread.Post(() =>
            {
                _dataLoaded = state == TvCoreState.Running;
                _shown = null;

                if (_dataLoaded)
                {
                    ResetView();
                }
            });

            TvCore.ChannelListChanged += () => Dispatcher.UIThread.Post(() =>
            {
                _shown = null;

                InvalidateVisual();
            });

            TvCore.ChannelChanged += channel => Dispatcher.UIThread.Post(() =>
            {
                var position = Shown.IndexOf((int)channel);

                if (position >= 0 && (position < _topChannel || position >= _topChannel + VisibleRows))
                {
                    _topChannel = position;
                }

                InvalidateVisual();
            });
        }

        public string Search
        {
            get => _search;
            set
            {
                var search = value?.Trim() ?? string.Empty;

                if (search == _search)
                {
                    return;
                }

                _search = search;
                _shown = null;
                _topChannel = 0;

                InvalidateVisual();
            }
        }

        public void AttachScrollBar(ScrollBar scrollBar)
        {
            ScrollBar = scrollBar;

            scrollBar.Scroll += (sender, args) =>
            {
                _topChannel = (int)Math.Round(scrollBar.Value);
                InvalidateVisual();
            };
        }

        public void AttachTimeBar(ScrollBar timeBar)
        {
            _timeBar = timeBar;
            _timeBar.Margin = new Thickness(NumberWidth + NameWidth, 0, 0, 0);

            timeBar.Scroll += (sender, args) => SetStart(EarliestUtc.AddMinutes(Math.Round(timeBar.Value) * StepMinutes));

            QueueTimeBar();
        }

        public void ResetView()
        {
            _startUtc = null;
            _topChannel = Math.Max(0, Shown.IndexOf((int)TvCore.CurrentChannelIndex));

            InvalidateVisual();
        }

        private int VisibleRows => Math.Max(1, (int)((Bounds.Height - HeaderHeight) / RowHeight));

        private static int TotalChannels => Math.Min(TvCore.ChannelIndexList?.Count ?? 0, TvCore.Channels?.Count ?? 0);

        private int ChannelCount => Shown.Count;

        private List<int> Shown
        {
            get
            {
                if (_shown != null)
                {
                    return _shown;
                }

                var channels = TvCore.Channels;
                var total = TotalChannels;
                var nowUtc = DateTimeOffset.UtcNow;

                EnsureIndex();

                _shown = new List<int>(total);

                for (var i = 0; i < total; i++)
                {
                    if (_search.Length == 0 || Matches(channels[i], nowUtc))
                    {
                        _shown.Add(i);
                    }
                }

                return _shown;
            }
        }

        private bool Matches(Channel channel, DateTimeOffset nowUtc)
        {
            if ((channel.Name ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 || channel.Index.ToString(CultureInfo.InvariantCulture) == _search)
            {
                return true;
            }

            return channel.Id != null && _byChannel.TryGetValue(channel.Id, out var programmes) && programmes.Any(x => x.Stop > nowUtc && (x.Title ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private DateTimeOffset StartUtc => _startUtc ?? Floor(DateTimeOffset.UtcNow, StepMinutes);

        private static DateTimeOffset EarliestUtc => Floor(DateTimeOffset.UtcNow, StepMinutes).AddHours(-2);

        private DateTimeOffset LatestStartUtc
        {
            get
            {
                var now = Floor(DateTimeOffset.UtcNow, StepMinutes);
                var end = _guideEndUtc > now ? Floor(_guideEndUtc, StepMinutes).AddMinutes(StepMinutes - VisibleMinutes) : now.AddHours(24);

                return end > now ? end : now;
            }
        }

        private static DateTimeOffset Floor(DateTimeOffset time, int minutes)
        {
            return new DateTimeOffset(time.Year, time.Month, time.Day, time.Hour, time.Minute / minutes * minutes, 0, time.Offset);
        }

        private bool IsDark => ActualThemeVariant == ThemeVariant.Dark;

        private Color Back => IsDark ? Color.FromRgb(32, 32, 32) : Color.FromRgb(240, 240, 240);

        private Color Surface => IsDark ? Color.FromRgb(43, 43, 43) : Colors.White;

        private Color Raised => IsDark ? Color.FromRgb(56, 56, 56) : Color.FromRgb(227, 227, 227);

        private Color Border => IsDark ? Color.FromRgb(70, 70, 70) : Color.FromRgb(160, 160, 160);

        private Color Text => IsDark ? Color.FromRgb(230, 230, 230) : Colors.Black;

        private Color MutedText => IsDark ? Color.FromRgb(160, 160, 160) : Color.FromRgb(109, 109, 109);

        private void EnsureIndex()
        {
            var version = TvCore.Guide?.Count ?? 0;

            if (_byChannel != null && _guideVersion == version)
            {
                return;
            }

            _byChannel = new Dictionary<string, List<Programme>>(StringComparer.Ordinal);
            _shown = null;

            foreach (var programme in TvCore.Guide ?? new List<Programme>())
            {
                if (programme.Channel == null)
                {
                    continue;
                }

                if (!_byChannel.TryGetValue(programme.Channel, out var list))
                {
                    list = new List<Programme>();
                    _byChannel[programme.Channel] = list;
                }

                list.Add(programme);
            }

            foreach (var list in _byChannel.Values)
            {
                list.Sort((a, b) => a.Start.CompareTo(b.Start));
            }

            _guideEndUtc = _byChannel.Values.Where(x => x.Count > 0).Select(x => x[x.Count - 1].Stop).DefaultIfEmpty(DateTimeOffset.MinValue).Max();

            _guideVersion = version;
        }

        private void ClampScroll()
        {
            var max = Math.Max(0, ChannelCount - VisibleRows);

            _topChannel = Math.Max(0, Math.Min(_topChannel, max));

            if (ScrollBar != null)
            {
                Dispatcher.UIThread.Post(UpdateScrollBar, DispatcherPriority.Background);
            }

            QueueTimeBar();
        }

        private void UpdateScrollBar()
        {
            var max = Math.Max(0, ChannelCount - VisibleRows);

            ScrollBar.Minimum = 0;
            ScrollBar.Maximum = max;
            ScrollBar.ViewportSize = VisibleRows;
            ScrollBar.LargeChange = VisibleRows;
            ScrollBar.SmallChange = 1;
            if (Math.Abs(ScrollBar.Value - _topChannel) >= 1)
            {
                ScrollBar.Value = _topChannel;
            }
            ScrollBar.IsEnabled = ChannelCount > VisibleRows;
        }

        private void QueueTimeBar()
        {
            if (_timeBar != null)
            {
                Dispatcher.UIThread.Post(UpdateTimeBar, DispatcherPriority.Background);
            }
        }

        private void UpdateTimeBar()
        {
            var visibleSteps = VisibleMinutes / (double)StepMinutes;

            _timeBar.Minimum = 0;
            _timeBar.Maximum = Math.Max(0, (LatestStartUtc - EarliestUtc).TotalMinutes / StepMinutes);
            _timeBar.ViewportSize = visibleSteps;
            _timeBar.LargeChange = visibleSteps;
            _timeBar.SmallChange = 1;
            _timeBar.Value = Math.Max(0, (StartUtc - EarliestUtc).TotalMinutes / StepMinutes);
        }

        private static void DrawText(DrawingContext g, string text, double size, FontWeight weight, Color colour, Rect box, TextAlignment alignment = TextAlignment.Left, bool centreVertically = true, bool wrap = false)
        {
            if (string.IsNullOrEmpty(text) || box.Width <= 0 || box.Height <= 0)
            {
                return;
            }

            using (var layout = new TextLayout(text, new Typeface(Face, FontStyle.Normal, weight), size, new SolidColorBrush(colour), alignment, wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, box.Width, box.Height))
            {
                var y = centreVertically ? box.Y + Math.Max(0, (box.Height - layout.Height) / 2) : box.Y;

                layout.Draw(g, new Point(box.X, y));
            }
        }

        public override void Render(DrawingContext g)
        {
            var width = Bounds.Width;
            var height = Bounds.Height;

            g.FillRectangle(new SolidColorBrush(Back), new Rect(0, 0, width, height));

            _hits.Clear();

            if (!_dataLoaded || TotalChannels == 0 || TvCore.Channels == null)
            {
                DrawText(g, "No guide yet", 15, FontWeight.Bold, MutedText, new Rect(0, 0, width, height), TextAlignment.Center);

                return;
            }

            EnsureIndex();
            ClampScroll();

            var gridLeft = NumberWidth + NameWidth;
            var gridWidth = Math.Max(1, width - gridLeft);
            var pixelsPerMinute = gridWidth / VisibleMinutes;
            var startUtc = StartUtc;
            var endUtc = startUtc.AddMinutes(VisibleMinutes);
            var nowUtc = DateTimeOffset.UtcNow;

            var line = new Pen(new SolidColorBrush(Border), 1);
            var nowPen = new Pen(new SolidColorBrush(Color.FromRgb(220, 60, 60)), 2);
            var headerBrush = new SolidColorBrush(Surface);
            var currentRowBrush = new SolidColorBrush(IsDark ? Color.FromRgb(24, 48, 32) : Color.FromRgb(210, 235, 215));
            var programmeBrush = new SolidColorBrush(Raised);
            var onNowBrush = new SolidColorBrush(IsDark ? Color.FromRgb(40, 80, 60) : Color.FromRgb(180, 220, 195));
            var hoverBrush = new SolidColorBrush(IsDark ? Color.FromRgb(70, 110, 85) : Color.FromRgb(150, 200, 170));
            var gapBrush = new SolidColorBrush(IsDark ? Color.FromRgb(38, 38, 38) : Color.FromRgb(235, 235, 235));

            g.FillRectangle(headerBrush, new Rect(0, 0, width, HeaderHeight));

            var dayText = _startUtc == null ? "Now" : startUtc.ToLocalTime().ToString("ddd d MMM");

            DrawText(g, dayText, 15, FontWeight.Bold, Text, new Rect(0, 0, gridLeft, HeaderHeight), TextAlignment.Center);

            for (var t = startUtc; t < endUtc; t = t.AddMinutes(StepMinutes))
            {
                var x = gridLeft + (t - startUtc).TotalMinutes * pixelsPerMinute;

                g.DrawLine(line, new Point(x, 4), new Point(x, HeaderHeight - 4));
                DrawText(g, t.ToLocalTime().ToString("h:mm tt"), 15, FontWeight.Bold, Text, new Rect(x + 6, 0, StepMinutes * pixelsPerMinute - 6, HeaderHeight));
            }

            g.DrawLine(line, new Point(0, HeaderHeight - 0.5), new Point(width, HeaderHeight - 0.5));

            // Rows
            var rows = VisibleRows;
            var shown = Shown;

            for (var row = 0; row <= rows; row++)
            {
                var position = _topChannel + row;

                if (position >= shown.Count)
                {
                    break;
                }

                var channelIndex = shown[position];
                var channel = TvCore.Channels[channelIndex];
                var y = HeaderHeight + row * RowHeight;
                var isCurrent = channelIndex == (int)TvCore.CurrentChannelIndex;

                if (isCurrent)
                {
                    g.FillRectangle(currentRowBrush, new Rect(0, y, width, RowHeight));
                }

                DrawText(g, TvCore.ChannelIndexList[channelIndex].ToString(), 19, FontWeight.Bold, isCurrent ? Colors.Lime : Text, new Rect(0, y, NumberWidth, RowHeight), TextAlignment.Center);

                DrawText(g, channel.ShortName, 13, FontWeight.Bold, Text, new Rect(NumberWidth + 4, y, NameWidth - LogoWidth - 10, RowHeight), wrap: true);

                var logo = channel.LogoImage;

                if (logo != null && logo.Size.Width > 0 && logo.Size.Height > 0)
                {
                    var box = new Rect(NumberWidth + NameWidth - LogoWidth - 4, y + 6, LogoWidth, RowHeight - 12);
                    var scale = Math.Min(box.Width / logo.Size.Width, box.Height / logo.Size.Height);
                    var size = new Size(logo.Size.Width * scale, logo.Size.Height * scale);

                    g.DrawImage(logo, new Rect(box.X + (box.Width - size.Width) / 2, box.Y + (box.Height - size.Height) / 2, size.Width, size.Height));
                }

                _hits.Add(Tuple.Create(new Rect(0, y, gridLeft, RowHeight), (object)channel));

                g.FillRectangle(gapBrush, new Rect(gridLeft, y, gridWidth, RowHeight));

                if (channel.Id != null && _byChannel.TryGetValue(channel.Id, out var programmes))
                {
                    foreach (var programme in programmes)
                    {
                        if (programme.Stop <= startUtc || programme.Start >= endUtc)
                        {
                            continue;
                        }

                        var from = programme.Start < startUtc ? startUtc : programme.Start;
                        var to = programme.Stop > endUtc ? endUtc : programme.Stop;
                        var x1 = gridLeft + (from - startUtc).TotalMinutes * pixelsPerMinute;
                        var x2 = gridLeft + (to - startUtc).TotalMinutes * pixelsPerMinute;
                        var block = new Rect(x1, y + 3, Math.Max(2, x2 - x1 - 2), RowHeight - 6);
                        var onNow = programme.Start <= nowUtc && programme.Stop > nowUtc;
                        var brush = ReferenceEquals(programme, _hoverProgramme) ? hoverBrush : onNow ? onNowBrush : programmeBrush;

                        g.FillRectangle(brush, block);

                        if (block.Width > 24)
                        {
                            var label = block.Deflate(new Thickness(6, 2));

                            DrawText(g, programme.Title, 13, FontWeight.Normal, Text, new Rect(label.X, label.Y + 3, label.Width, 20), centreVertically: false);
                            DrawText(g, $"{programme.Start.ToLocalTime():h:mm}-{programme.Stop.ToLocalTime():h:mm tt}", 11, FontWeight.Normal, MutedText, new Rect(label.X, label.Bottom - 16, label.Width, 16), centreVertically: false);
                        }

                        _hits.Add(Tuple.Create(block, (object)programme));
                    }
                }

                g.DrawLine(line, new Point(0, y + RowHeight - 0.5), new Point(width, y + RowHeight - 0.5));
            }

            g.DrawLine(line, new Point(NumberWidth, HeaderHeight), new Point(NumberWidth, height));
            g.DrawLine(line, new Point(gridLeft, HeaderHeight), new Point(gridLeft, height));

            if (nowUtc >= startUtc && nowUtc < endUtc)
            {
                var x = gridLeft + (nowUtc - startUtc).TotalMinutes * pixelsPerMinute;

                g.DrawLine(nowPen, new Point(x, 0), new Point(x, height));
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            var notches = (int)Math.Round(e.Delta.Y);

            if (notches == 0)
            {
                notches = Math.Sign(e.Delta.Y);
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                ScrollTime(-notches);
            }
            else
            {
                _topChannel -= notches * 3;
                InvalidateVisual();
            }

            e.Handled = true;
        }

        public bool Navigate(Key key)
        {
            switch (key)
            {
                case Key.Up:
                {
                    _topChannel--;
                }
                break;

                case Key.Down:
                {
                    _topChannel++;
                }
                break;

                case Key.PageUp:
                {
                    _topChannel -= VisibleRows;
                }
                break;

                case Key.PageDown:
                {
                    _topChannel += VisibleRows;
                }
                break;

                case Key.Left:
                {
                    ScrollTime(-1);
                }
                break;

                case Key.Right:
                {
                    ScrollTime(1);
                }
                break;

                case Key.Home:
                {
                    ResetView();
                }
                break;

                default:
                {
                    return false;
                }
            }

            InvalidateVisual();

            return true;
        }

        private void ScrollTime(int steps)
        {
            SetStart(StartUtc.AddMinutes(steps * StepMinutes));
        }

        private void SetStart(DateTimeOffset start)
        {
            if (start > LatestStartUtc)
            {
                start = LatestStartUtc;
            }

            if (start < EarliestUtc)
            {
                start = EarliestUtc;
            }

            _startUtc = start == Floor(DateTimeOffset.UtcNow, StepMinutes) ? (DateTimeOffset?)null : start;

            QueueTimeBar();
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            Focus();

            var point = e.GetCurrentPoint(this);
            var hit = HitTest(point.Position);

            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (hit is Channel channel)
            {
                TvCore.SetChannel((uint)TvCore.ChannelIndexList.IndexOf(channel.Index));
            }
            else if (hit is Programme programme)
            {
                var owner = TvCore.Channels.Find(x => x.Id == programme.Channel);

                if (owner != null)
                {
                    TvCore.SetChannel((uint)TvCore.ChannelIndexList.IndexOf(owner.Index));
                }
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            var hit = HitTest(e.GetPosition(this)) as Programme;

            if (ReferenceEquals(hit, _hoverProgramme))
            {
                return;
            }

            _hoverProgramme = hit;

            if (hit == null)
            {
                ToolTip.SetIsOpen(this, false);
                ToolTip.SetTip(this, null);
            }
            else
            {
                var text = $"{hit.Title}\n{hit.Start.ToLocalTime():h:mm tt} to {hit.Stop.ToLocalTime():h:mm tt}";

                if (!string.IsNullOrWhiteSpace(hit.Description))
                {
                    text += "\n\n" + hit.Description;
                }

                ToolTip.SetIsOpen(this, false);
                ToolTip.SetTip(this, new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
                ToolTip.SetPlacement(this, PlacementMode.Pointer);
                ToolTip.SetIsOpen(this, true);
            }

            InvalidateVisual();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);

            _hoverProgramme = null;

            ToolTip.SetIsOpen(this, false);

            InvalidateVisual();
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            InvalidateVisual();
        }

        private object HitTest(Point point)
        {
            foreach (var hit in _hits)
            {
                if (hit.Item1.Contains(point))
                {
                    return hit.Item2;
                }
            }

            return null;
        }
    }
}
