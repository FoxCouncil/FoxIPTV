// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FoxIPTV.Classes;
using Newtonsoft.Json.Linq;

namespace FoxIPTV.Services
{
    public interface IService
    {
        string Title { get; }

        Dictionary<string, Type> Fields { get; }

        JObject Data { get; set; }

        bool SaveAuthentication { get; set; }

        Task<bool> IsAuthenticated();

        Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        Task<Tuple<List<Channel>, List<Programme>>> Process();
    }
}
