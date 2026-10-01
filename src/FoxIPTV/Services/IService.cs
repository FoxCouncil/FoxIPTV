// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using FoxIPTV.Classes;
using Newtonsoft.Json.Linq;

namespace FoxIPTV.Services
{
    public enum ProviderFieldKind
    {
        Text,

        Url,

        Choice
    }

    public class ProviderField
    {
        public string Key { get; set; }

        public ProviderFieldKind Kind { get; set; } = ProviderFieldKind.Text;

        public List<string> Choices { get; set; } = new List<string>();

        public string Default { get; set; }

        public bool Required { get; set; } = true;

        public static ProviderField Text(string key, string @default = null, bool required = true) => new ProviderField { Key = key, Kind = ProviderFieldKind.Text, Default = @default, Required = required };

        public static ProviderField Url(string key, string @default = null, bool required = true) => new ProviderField { Key = key, Kind = ProviderFieldKind.Url, Default = @default, Required = required };

        public static ProviderField Choice(string key, IEnumerable<string> choices, string @default = null) => new ProviderField { Key = key, Kind = ProviderFieldKind.Choice, Choices = new List<string>(choices), Default = @default };
    }

    public interface IService
    {
        string Id { get; }

        string Title { get; }

        List<ProviderField> Fields { get; }

        JObject Data { get; set; }

        Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        Task<Tuple<List<Channel>, List<Programme>>> Process();
    }
}
