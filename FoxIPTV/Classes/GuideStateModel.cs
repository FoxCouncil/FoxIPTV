// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System.Collections.Generic;
    using System.Drawing;

    public class GuideStateModel
    {
        public int TotalRows { get; set; }

        public int TotalColumns { get; set; }

        public string HeaderTitle { get; set; }

        public List<GuideTextModel> Headers { get; set; } = new List<GuideTextModel>();

        public List<GuideRowModel> Rows { get; set; } = new List<GuideRowModel>();
    }

    public class GuideTextModel
    {
        public string Text { get; set; }

        public object Tag { get; set; }

        public int Column { get; set; }

        public int ColSpan { get; set; }

        public Color BackgroundColor { get; set; }
    }

    public class GuideRowModel
    {
        public string Number { get; set; }

        public string Text { get; set; }

        public object Tag { get; set; }

        public Image Image { get; set; }

        public Color BackgroundColor { get; set; }

        public List<GuideTextModel> Programmes { get; set; } = new List<GuideTextModel>();
    }
}
