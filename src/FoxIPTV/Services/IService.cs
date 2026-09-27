// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FoxIPTV.Classes;
using Newtonsoft.Json.Linq;

namespace FoxIPTV.Services
{
    [Flags]
    public enum ProviderCapabilities
    {
        None = 0,

        LiveTv = 1,

        Library = 2
    }

    public enum ProviderFieldKind
    {
        Text,

        Password,

        Url,

        Choice
    }

    public class ProviderField
    {
        public string Key { get; set; }

        public string Label { get; set; }

        public ProviderFieldKind Kind { get; set; } = ProviderFieldKind.Text;

        public List<string> Choices { get; set; } = new List<string>();

        public string Default { get; set; }

        public bool Required { get; set; } = true;

        public static ProviderField Text(string key, string @default = null, bool required = true) => new ProviderField { Key = key, Kind = ProviderFieldKind.Text, Default = @default, Required = required };

        public static ProviderField Password(string key) => new ProviderField { Key = key, Kind = ProviderFieldKind.Password };

        public static ProviderField Url(string key, string @default = null, bool required = true) => new ProviderField { Key = key, Kind = ProviderFieldKind.Url, Default = @default, Required = required };

        public static ProviderField Choice(string key, IEnumerable<string> choices, string @default = null) => new ProviderField { Key = key, Kind = ProviderFieldKind.Choice, Choices = new List<string>(choices), Default = @default };
    }

    public interface IService
    {
        string Id { get; }

        string Title { get; }

        string Description { get; }

        ProviderCapabilities Capabilities { get; }

        List<ProviderField> Fields { get; }

        JObject Data { get; set; }

        bool SaveAuthentication { get; set; }

        Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        Task<bool> IsAuthenticated();

        Task<Tuple<List<Channel>, List<Programme>>> Process();
    }
}
