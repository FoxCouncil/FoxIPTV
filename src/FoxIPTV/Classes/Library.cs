// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;

    public enum LibraryItemKind
    {
        Movie,

        Series,

        Episode
    }

    public class LibraryCategory
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public override string ToString() => Name;
    }

    public class LibraryPage
    {
        public List<LibraryItem> Items { get; set; } = new List<LibraryItem>();

        public int Page { get; set; } = 1;

        public int TotalPages { get; set; } = 1;
    }

    public class LibrarySeason
    {
        public int Number { get; set; }

        public string Name { get; set; }

        public int EpisodeCount { get; set; }

        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? $"Season {Number}" : Name;
    }

    public class LibraryItem
    {
        public string Id { get; set; }

        public LibraryItemKind Kind { get; set; }

        public string Title { get; set; }

        public string Subtitle { get; set; }

        public string Year { get; set; }

        public string Overview { get; set; }

        public Uri Poster { get; set; }

        public Uri Backdrop { get; set; }

        public double? Rating { get; set; }

        public int? DurationMinutes { get; set; }

        public string SeriesId { get; set; }

        public int? Season { get; set; }

        public int? Episode { get; set; }

        public List<LibrarySeason> Seasons { get; set; } = new List<LibrarySeason>();

        public JObject Extra { get; set; }

        public bool IsPlayable => Kind != LibraryItemKind.Series;

        public override string ToString() => string.IsNullOrWhiteSpace(Year) ? Title : $"{Title} ({Year})";
    }

    public class MediaSource
    {
        public string Name { get; set; }

        public Uri Url { get; set; }

        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public override string ToString() => Name;
    }
}
