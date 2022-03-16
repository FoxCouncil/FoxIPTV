// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library.Services
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IService
    {
        /// <summary>This must be unique and constant!</summary>
        Guid Id { get; }

        string Title { get; }

        IReadOnlyList<ServiceField> Fields { get; }

        Task<Result> IsAuthenticated(JObject data);

        Task<List<Channel>> GetChannels(Account providerAccount);
    }
}
