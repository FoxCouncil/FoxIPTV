// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;

    /// <summary>The kind of thing a <see cref="LibraryItem"/> is</summary>
    public enum LibraryItemKind
    {
        /// <summary>A single feature length video</summary>
        Movie,

        /// <summary>A show made of seasons of episodes; not directly playable</summary>
        Series,

        /// <summary>A single episode of a <see cref="Series"/></summary>
        Episode
    }

    /// <summary>A top level browse category of a library</summary>
    public class LibraryCategory
    {
        /// <summary>The provider specific identifier</summary>
        public string Id { get; set; }

        /// <summary>The human readable name</summary>
        public string Name { get; set; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>One page of <see cref="LibraryItem"/> results</summary>
    public class LibraryPage
    {
        /// <summary>The items on this page</summary>
        public List<LibraryItem> Items { get; set; } = new List<LibraryItem>();

        /// <summary>The one based page number</summary>
        public int Page { get; set; } = 1;

        /// <summary>The total number of pages, one if unknown</summary>
        public int TotalPages { get; set; } = 1;
    }

    /// <summary>A season of a series</summary>
    public class LibrarySeason
    {
        /// <summary>The season number</summary>
        public int Number { get; set; }

        /// <summary>The season name, falls back to "Season N"</summary>
        public string Name { get; set; }

        /// <summary>How many episodes the season has, zero if unknown</summary>
        public int EpisodeCount { get; set; }

        /// <inheritdoc/>
        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? $"Season {Number}" : Name;
    }

    /// <summary>A movie, series or episode in an on-demand library</summary>
    public class LibraryItem
    {
        /// <summary>The provider specific identifier</summary>
        public string Id { get; set; }

        /// <summary>What kind of item this is</summary>
        public LibraryItemKind Kind { get; set; }

        /// <summary>The title</summary>
        public string Title { get; set; }

        /// <summary>A secondary line, e.g. "S01E03" for episodes</summary>
        public string Subtitle { get; set; }

        /// <summary>The release year, if known</summary>
        public string Year { get; set; }

        /// <summary>The synopsis</summary>
        public string Overview { get; set; }

        /// <summary>The poster image</summary>
        public Uri Poster { get; set; }

        /// <summary>The wide backdrop image</summary>
        public Uri Backdrop { get; set; }

        /// <summary>A rating out of ten, if known</summary>
        public double? Rating { get; set; }

        /// <summary>The running time in minutes, if known</summary>
        public int? DurationMinutes { get; set; }

        /// <summary>For episodes, the <see cref="Id"/> of the parent series</summary>
        public string SeriesId { get; set; }

        /// <summary>For episodes, the season number</summary>
        public int? Season { get; set; }

        /// <summary>For episodes, the episode number</summary>
        public int? Episode { get; set; }

        /// <summary>For series, the seasons; populated by <see cref="Services.ILibraryProvider.GetDetails"/></summary>
        public List<LibrarySeason> Seasons { get; set; } = new List<LibrarySeason>();

        /// <summary>Any provider specific extra data needed to resolve the item later</summary>
        public JObject Extra { get; set; }

        /// <summary>Can this item be handed to <see cref="Services.ILibraryProvider.Resolve"/></summary>
        public bool IsPlayable => Kind != LibraryItemKind.Series;

        /// <inheritdoc/>
        public override string ToString() => string.IsNullOrWhiteSpace(Year) ? Title : $"{Title} ({Year})";
    }

    /// <summary>A single way to play a <see cref="LibraryItem"/>: a direct media URL LibVLC can open (HLS, MP4, MPEG-TS, ...)</summary>
    public class MediaSource
    {
        /// <summary>The human readable name, e.g. "1080p" or "Mirror 2"</summary>
        public string Name { get; set; }

        /// <summary>The URL to play</summary>
        public Uri Url { get; set; }

        /// <summary>Any HTTP headers the stream needs (User-Agent, Referer, ...)</summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override string ToString() => Name;
    }
}
