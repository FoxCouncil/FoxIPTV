// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using System.Collections.Generic;
using System.Threading.Tasks;
using FoxIPTV.Classes;

namespace FoxIPTV.Services
{
    public interface ILibraryProvider
    {
        Task<List<LibraryCategory>> GetCategories();

        Task<LibraryPage> Browse(string categoryId, int page);

        Task<LibraryPage> Search(string query, int page);

        Task<LibraryItem> GetDetails(string id, LibraryItemKind kind);

        Task<List<LibraryItem>> GetEpisodes(string seriesId, int season);

        Task<List<MediaSource>> Resolve(LibraryItem item);
    }
}
