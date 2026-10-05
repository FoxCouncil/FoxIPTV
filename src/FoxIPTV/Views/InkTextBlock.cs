// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Globalization;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;

    public class InkTextBlock : TextBlock
    {
        public static readonly StyledProperty<bool> AllCapsProperty = AvaloniaProperty.Register<InkTextBlock, bool>(nameof(AllCaps));

        static InkTextBlock()
        {
            TextProperty.OverrideMetadata<InkTextBlock>(new StyledPropertyMetadata<string>(coerce: (sender, text) => sender is InkTextBlock { AllCaps: true } ? text?.ToUpperInvariant() : text));
            AllCapsProperty.Changed.AddClassHandler<InkTextBlock>((sender, args) => sender.CoerceValue(TextProperty));
        }

        public InkTextBlock()
        {
            RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Alias);
        }

        public bool AllCaps
        {
            get => GetValue(AllCapsProperty);
            set => SetValue(AllCapsProperty, value);
        }

        private double _shift = double.NaN;

        private (FontFamily Family, double Size, FontWeight Weight, FontStyle Style, FontStretch Stretch, double Scale) _shiftFor;

        protected override Type StyleKeyOverride => typeof(TextBlock);

        protected override void RenderTextLayout(DrawingContext context, Point origin)
        {
            base.RenderTextLayout(context, new Point(origin.X, origin.Y + Shift()));
        }

        private double Shift()
        {
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            var key = (FontFamily, FontSize, FontWeight, FontStyle, FontStretch, scale);

            if (!double.IsNaN(_shift) && key == _shiftFor)
            {
                return _shift;
            }

            _shiftFor = key;
            _shift = 0;

            try
            {
                var probe = new FormattedText("H", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Brushes.White);
                var capital = probe.BuildGeometry(new Point(0, 0))?.Bounds ?? default;

                if (capital.Height > 0 && probe.Height > 0)
                {
                    var offset = probe.Height / 2 - (capital.Top + capital.Height / 2);

                    _shift = Math.Round(offset * scale) / scale;
                }
            }
            catch (Exception)
            {
                _shift = 0;
            }

            return _shift;
        }
    }
}
