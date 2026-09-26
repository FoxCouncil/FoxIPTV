// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using System.Collections.Generic;
using System.Threading.Tasks;
using FoxIPTV.Classes;

namespace FoxIPTV.Services
{
    /// <summary>A provider that exposes a browsable on-demand library (movies, series, episodes)</summary>
    public interface ILibraryProvider
    {
        /// <summary>The top level browse categories, e.g. "Trending Movies" or "Top Rated Series"</summary>
        Task<List<LibraryCategory>> GetCategories();

        /// <summary>A page of items in a category</summary>
        /// <param name="categoryId">The <see cref="LibraryCategory.Id"/> to browse</param>
        /// <param name="page">A one based page number</param>
        Task<LibraryPage> Browse(string categoryId, int page);

        /// <summary>A page of items matching a free text query</summary>
        /// <param name="query">The user's search text</param>
        /// <param name="page">A one based page number</param>
        Task<LibraryPage> Search(string query, int page);

        /// <summary>The full details of an item, including <see cref="LibraryItem.Seasons"/> for series</summary>
        Task<LibraryItem> GetDetails(string id, LibraryItemKind kind);

        /// <summary>The episodes of a season of a series</summary>
        Task<List<LibraryItem>> GetEpisodes(string seriesId, int season);

        /// <summary>The playable sources for a movie or episode</summary>
        Task<List<MediaSource>> Resolve(LibraryItem item);
    }
}
