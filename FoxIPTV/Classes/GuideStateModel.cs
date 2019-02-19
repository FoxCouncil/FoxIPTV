// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System.Collections.Generic;
    using System.Drawing;

    /// <summary>A model to store a calculated guide state for the width and height allotments of the containing form</summary>
    public class GuideStateModel
    {
        /// <summary>The total amount rows for this model</summary>
        public int TotalRows { get; set; }

        /// <summary>The total amount of columns for this model</summary>
        public int TotalColumns { get; set; }

        /// <summary>The title for this model</summary>
        public string HeaderTitle { get; set; }

        /// <summary>A <see cref="List{T}"/> of <see cref="GuideTextModel"/> objects, used to list time "chunks" (10 minutes)</summary>
        public List<GuideTextModel> Headers { get; set; } = new List<GuideTextModel>();

        /// <summary>A <see cref="List{T}"/> of <see cref="GuideRowModel"/> objects, used to list programmes and channels</summary>
        public List<GuideRowModel> Rows { get; set; } = new List<GuideRowModel>();
    }

    /// <summary>A generic guide text model</summary>
    public class GuideTextModel
    {
        /// <summary>The text to display</summary>
        public string Text { get; set; }

        /// <summary>An object representative of the text, used for click events</summary>
        public object Tag { get; set; }

        /// <summary>The column the text should occupy</summary>
        public int Column { get; set; }

        /// <summary>How many columns to span the text node across</summary>
        public int ColSpan { get; set; }

        /// <summary>The background color for this text node</summary>
        public Color BackgroundColor { get; set; }
    }

    /// <summary>A guide row model, used to represent a single line of channel and programme information</summary>
    public class GuideRowModel
    {
        /// <summary>The channel number (Index), as a string</summary>
        public string Number { get; set; }

        /// <summary>The channel name</summary>
        public string Text { get; set; }

        /// <summary>An object to represent this channel row, used for click events</summary>
        public object Tag { get; set; }
        
        /// <summary>The image logo for the channel</summary>
        public Image Image { get; set; }

        /// <summary>A background color for the channel number and name columns</summary>
        public Color BackgroundColor { get; set; }

        /// <summary>A list of programmes for this channel row</summary>
        public List<GuideTextModel> Programmes { get; set; } = new List<GuideTextModel>();
    }
}
