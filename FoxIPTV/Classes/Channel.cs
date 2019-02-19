// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Drawing;

    /// <summary>A class to contain the data that represents a logical channel</summary>
    public class Channel
    {
        /// <summary>A channel index, unsigned integer generally never begins with 0</summary>
        public uint Index { get; set; }

        /// <summary>A string based key to match this channel with other data from the provider</summary>
        public string Id { get; set; }

        /// <summary>The name of the channel</summary>
        public string Name { get; set; }

        /// <summary>A Uri to a resource that is the channel's logo</summary>
        public Uri Logo { get; set; }

        /// <summary>A GDI+ <see cref="Image"/> of the channel's logo</summary>
        public Image LogoImage { get; set; }

        /// <summary>A string key to group this and other channels by</summary>
        public string Group { get; set; }

        /// <summary>A Uri to a resource that streams the channel's content</summary>
        public Uri Stream { get; set; }
    }
}
