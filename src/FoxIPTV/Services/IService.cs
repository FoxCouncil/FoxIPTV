// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FoxIPTV.Classes;
using Newtonsoft.Json.Linq;

namespace FoxIPTV.Services
{
    /// <summary>What a provider is able to supply to the player</summary>
    [Flags]
    public enum ProviderCapabilities
    {
        /// <summary>Nothing, the provider is useless</summary>
        None = 0,

        /// <summary>A flat list of live channels, optionally with an electronic programme guide</summary>
        LiveTv = 1,

        /// <summary>A browsable, searchable on-demand library of movies and series</summary>
        Library = 2
    }

    /// <summary>The kind of input a provider field requires</summary>
    public enum ProviderFieldKind
    {
        /// <summary>A free form single line of text</summary>
        Text,

        /// <summary>A masked single line of text</summary>
        Password,

        /// <summary>An absolute URL</summary>
        Url,

        /// <summary>A single selection from a fixed list of choices</summary>
        Choice
    }

    /// <summary>A single configuration input a provider needs from the user before it can start</summary>
    public class ProviderField
    {
        /// <summary>The key used to store the value in the provider's <see cref="IService.Data"/></summary>
        public string Key { get; set; }

        /// <summary>The human readable label, falls back to <see cref="Key"/></summary>
        public string Label { get; set; }

        /// <summary>The kind of input to render</summary>
        public ProviderFieldKind Kind { get; set; } = ProviderFieldKind.Text;

        /// <summary>The choices for a <see cref="ProviderFieldKind.Choice"/> field</summary>
        public List<string> Choices { get; set; } = new List<string>();

        /// <summary>The default value, used when the user has not entered anything</summary>
        public string Default { get; set; }

        /// <summary>Must the user supply a value</summary>
        public bool Required { get; set; } = true;

        /// <summary>Create a text field</summary>
        public static ProviderField Text(string key, string @default = null, bool required = true) => new ProviderField { Key = key, Kind = ProviderFieldKind.Text, Default = @default, Required = required };

        /// <summary>Create a password field</summary>
        public static ProviderField Password(string key) => new ProviderField { Key = key, Kind = ProviderFieldKind.Password };

        /// <summary>Create a URL field</summary>
        public static ProviderField Url(string key, string @default = null, bool required = true) => new ProviderField { Key = key, Kind = ProviderFieldKind.Url, Default = @default, Required = required };

        /// <summary>Create a choice field</summary>
        public static ProviderField Choice(string key, IEnumerable<string> choices, string @default = null) => new ProviderField { Key = key, Kind = ProviderFieldKind.Choice, Choices = new List<string>(choices), Default = @default };
    }

    /// <summary>A content provider; the thing that gives the player channels, a guide and/or an on-demand library</summary>
    public interface IService
    {
        /// <summary>A stable, unique, filename safe identifier for this provider</summary>
        string Id { get; }

        /// <summary>The human readable title</summary>
        string Title { get; }

        /// <summary>A one line description shown in the provider picker</summary>
        string Description { get; }

        /// <summary>What this provider is able to supply</summary>
        ProviderCapabilities Capabilities { get; }

        /// <summary>The inputs this provider needs from the user</summary>
        List<ProviderField> Fields { get; }

        /// <summary>The values the user entered for <see cref="Fields"/>, keyed by <see cref="ProviderField.Key"/></summary>
        JObject Data { get; set; }

        /// <summary>Should the provider persist any authentication state it creates</summary>
        bool SaveAuthentication { get; set; }

        /// <summary>Progress reporters for channel loading (Item1) and guide loading (Item2)</summary>
        Tuple<IProgress<int>, IProgress<int>> ProgressUpdater { get; set; }

        /// <summary>Validate <see cref="Data"/> against the provider; providers with no credentials return true</summary>
        Task<bool> IsAuthenticated();

        /// <summary>Produce the live channel list and guide; providers without <see cref="ProviderCapabilities.LiveTv"/> return empty lists</summary>
        Task<Tuple<List<Channel>, List<Programme>>> Process();
    }
}
