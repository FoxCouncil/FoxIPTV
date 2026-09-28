// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Globalization;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;

    /// <summary>One of the little stream tags (H264, 720P, 30FPS, STEREO...): a dark rounded box with heavy white lettering</summary>
    public sealed class StreamTag : Control
    {
        public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<StreamTag, string>(nameof(Text));

        private const double TagHeight = 32;

        private const double MinTagWidth = 56;

        private const double SidePadding = 7;

        private const double Radius = 6;

        /// <summary>One size for every tag, so "CC" and "SURROUND" read as the same lettering</summary>
        private const double FontPixels = 20;

        private static readonly Typeface Face = new Typeface(new FontFamily("Bahnschrift, Arial Black, Arial"), FontStyle.Normal, FontWeight.Bold, FontStretch.Condensed);

        private static readonly IBrush Box = new SolidColorBrush(Color.FromRgb(51, 51, 51));

        private static readonly IPen Outline = new Pen(Brushes.White, 1, lineJoin: PenLineJoin.Round);

        private Geometry _ink;

        static StreamTag()
        {
            AffectsMeasure<StreamTag>(TextProperty);
            AffectsRender<StreamTag>(TextProperty);
        }

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty)
            {
                _ink = null;
            }
        }

        private Geometry Ink()
        {
            if (_ink == null && !string.IsNullOrEmpty(Text))
            {
                var text = new FormattedText(Text.ToUpperInvariant(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, FontPixels, Brushes.White);

                _ink = text.BuildGeometry(new Point(0, 0));
            }

            return _ink;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var ink = Ink()?.Bounds ?? default;

            return new Size(Math.Max(MinTagWidth, Math.Ceiling(ink.Width) + SidePadding * 2), TagHeight);
        }

        public override void Render(DrawingContext context)
        {
            var size = Bounds.Size;

            context.DrawRectangle(Box, null, new RoundedRect(new Rect(size), Radius));

            var geometry = Ink();

            if (geometry == null)
            {
                return;
            }

            var ink = geometry.Bounds;

            // Centre the ink itself, not the font box: caps and digits have no descenders and would ride high
            using (context.PushTransform(Matrix.CreateTranslation((size.Width - ink.Width) / 2 - ink.X, (size.Height - ink.Height) / 2 - ink.Y)))
            {
                context.DrawGeometry(Brushes.White, Outline, geometry);
            }
        }
    }
}
