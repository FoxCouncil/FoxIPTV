// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Avalonia;

    public static class MosaicLayout
    {
        public static List<Rect> Arrange(IReadOnlyList<double> aspects, double width, double height)
        {
            var cells = new List<Rect>();

            if (aspects.Count == 0 || width <= 0 || height <= 0)
            {
                return cells;
            }

            var total = aspects.Sum();
            var rows = Math.Clamp((int)Math.Round(Math.Sqrt(total * height / width)), 1, aspects.Count);
            var target = total / rows;
            var groups = new List<List<int>>();
            var current = new List<int>();
            var sum = 0.0;

            for (var i = 0; i < aspects.Count; i++)
            {
                var left = aspects.Count - i;
                var rowsAfter = rows - groups.Count - 1;
                var mustBreak = current.Count > 0 && left == rowsAfter;
                var wantBreak = current.Count > 0 && rowsAfter > 0 && (sum >= target || Math.Abs(sum + aspects[i] - target) > Math.Abs(sum - target));

                if (mustBreak || wantBreak)
                {
                    groups.Add(current);
                    current = new List<int>();
                    sum = 0;
                }

                current.Add(i);
                sum += aspects[i];
            }

            groups.Add(current);

            var natural = groups.Select(g => width / g.Sum(i => aspects[i])).ToList();
            var scale = height / natural.Sum();
            var top = 0.0;

            foreach (var (group, index) in groups.Select((g, i) => (g, i)))
            {
                var rowHeight = natural[index] * scale;
                var rowSum = group.Sum(i => aspects[i]);
                var x = 0.0;

                foreach (var item in group)
                {
                    var cellWidth = width * aspects[item] / rowSum;

                    cells.Add(new Rect(x, top, cellWidth, rowHeight));

                    x += cellWidth;
                }

                top += rowHeight;
            }

            return cells;
        }
    }
}
