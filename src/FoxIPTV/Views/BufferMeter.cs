// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;

    public sealed class BufferMeter : Control
    {
        public const int Segments = 12;

        private const double SegmentWidth = 6;

        private const double SegmentGap = 2;

        private const double SegmentHeight = 10;

        public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<BufferMeter, double>(nameof(Value));

        private static readonly Color Red = Color.FromRgb(0xFF, 0x3B, 0x3B);

        private static readonly Color Amber = Color.FromRgb(0xFF, 0xC8, 0x57);

        private static readonly Color Green = Color.FromRgb(0x3D, 0xDC, 0x84);

        static BufferMeter()
        {
            AffectsRender<BufferMeter>(ValueProperty);
        }

        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static Color LevelColor(double percent)
        {
            return percent < 34 ? Red : percent < 67 ? Amber : Green;
        }

        public static int LitSegments(double percent)
        {
            return (int)Math.Round(Math.Clamp(percent, 0, 100) / 100 * Segments);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Segments * SegmentWidth + (Segments - 1) * SegmentGap, SegmentHeight);
        }

        public override void Render(DrawingContext context)
        {
            var lit = LitSegments(Value);
            var top = Math.Round((Bounds.Height - SegmentHeight) / 2);

            for (var i = 0; i < Segments; i++)
            {
                var color = LevelColor((i + 0.5) * 100.0 / Segments);
                var brush = new SolidColorBrush(i < lit ? color : Color.FromArgb(0x38, color.R, color.G, color.B));

                context.FillRectangle(brush, new Rect(i * (SegmentWidth + SegmentGap), top, SegmentWidth, SegmentHeight));
            }
        }
    }
}
