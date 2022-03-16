// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library
{
    using FoxIPTV.Library.Services;
    using FoxIPTV.Library.Utils;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Bson;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public static class Core
    {
        public const string UsernameKey = "Username";

        public const string PasswordKey = "Password";

        private static Regex _commandLineParsingRegex = new Regex(@"("".*?""|[^ ""]+)+", RegexOptions.Compiled);
        private static JObject _settings;
        private static CoreState _currentState = CoreState.Initializing;
        private static bool _isReady;

        private static List<IService> _services = new List<IService>();
        private static List<Account> _accounts = new List<Account>();

        private static Account _activeAccount;

        private static Provider _provider;

        private static uint _currentChannelIdx;

        private static string TemporaryFilesPath;

        private static string LocalFilesPath;

        private static string AccountsStorageFilePath;

        public static CoreState State => _currentState;

        public static uint CurrentChannel => _currentChannelIdx;

        public static event Action<CoreState> StateChange;

        public static void Initialize(string arguments, string tempPath, string localPath)
        {
            if (!Directory.Exists(tempPath) || !Directory.Exists(localPath))
            {
                throw new Exception("A core directory needed doesn't exist.");
            }

            TemporaryFilesPath = tempPath;
            LocalFilesPath = localPath;

            AccountsStorageFilePath = Path.Combine(LocalFilesPath, "foxiptv.dat");

            ApiClient.Initialize(TemporaryFilesPath);

            Log.FilePath = LocalFilesPath;

            Log.Message($"=====================[ FoxIPTV Core on ({Environment.MachineName}) machine, PID: ({Process.GetCurrentProcess().Id}) ]=====================");

            // Grab all configured IService objects
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IService).IsAssignableFrom(p) && p.IsClass);

            foreach (var type in types)
            {
                var instance = (IService)Activator.CreateInstance(type);

                Log.Debug($"Installing [{instance.Title}] Service");

                _services.Add(instance);
            }

            // Load the settings from disk
            SettingsLoad();

            // Load the saved accounts from disk
            AccountsLoad();

            // Load the settings from
            ProcessCommandLineSwitches(arguments);

            // Check and/or create salt
            SettingGet("e_s_guid", Guid.NewGuid().ToString());

            // Tick up launch count
            SettingSet("launch_count", SettingGet("launch_count", 0) + 1);
        }

        #region The Control System

        public static void ControlReady()
        {
            if (_isReady)
            {
                throw new ApplicationException("Whoops, double ready?");
            }

            _isReady = true;

            _currentState = CoreState.Login;

            StateChange?.Invoke(_currentState);
        }

        public static Channel ControlGetChannel()
        {
            if (State == CoreState.Playback)
            {
                return _provider.Channels.Find(x => x.Index == _currentChannelIdx);
            }

            return null;
        }

        public static void ControlChannelUp()
        {
            var newChannelIdx = _currentChannelIdx + 1;

            var newChannel = _provider.Channels.Find(x => x.Index == newChannelIdx);

            if (newChannel == null)
            {
                newChannelIdx = 1;

                newChannel = _provider.Channels.Find(x => x.Index == newChannelIdx);
            }

            SetChannel(newChannelIdx);
        }

        public static void ControlChannelDown()
        {
            var newChannelIdx = _currentChannelIdx - 1;

            if (newChannelIdx == 0)
            {
                newChannelIdx = _provider.Channels.Max(x => x.Index);
            }

            SetChannel(newChannelIdx);
        }

        #endregion

        #region The Account System

        public static IReadOnlyList<IService> Services => _services;

        public static IReadOnlyList<Account> GetAccounts()
        {
            return _accounts;
        }

        public static Result AddAccount(Guid serviceGuid, JObject data)
        {
            var newAccount = new Account(serviceGuid, data);

            _accounts.Add(newAccount);

            AccountsSave();

            return Result.Success();
        }

        public static async Task AccountsLogin()
        {
            if (!_accounts.Any())
            {
                return;
            }

            var firstAccountId = _accounts[0].Id;

            await AccountsLogin(firstAccountId);
        }

        public static async Task AccountsLogin(Guid accountId)
        {
            _activeAccount = _accounts.Find(x => x.Id == accountId);

            var service = _services.Find(x => x.Id == _activeAccount.ProviderId);

            var isAuthenticated = await service.IsAuthenticated(_activeAccount.Data);

            if (isAuthenticated == Result.Success())
            {
                SetState(CoreState.Load);

                _provider = new Provider
                {
                    Channels = await service.GetChannels(_activeAccount)
                };

                _currentChannelIdx = SettingGet($"{_activeAccount.Id}_channel", 1u);

                SetState(CoreState.Playback);
            }
            else
            {
                _activeAccount = null;

                SetState(CoreState.Login);
            }
        }

        private static void AccountsSave()
        {
            var accountsJsonString = JArray.FromObject(_accounts).ToString().Encrypt();

            File.WriteAllText(AccountsStorageFilePath, accountsJsonString);
        }

        private static void AccountsLoad()
        {
            if (File.Exists(AccountsStorageFilePath))
            {
                var accountsPayload = File.ReadAllText(AccountsStorageFilePath);

                _accounts = JsonConvert.DeserializeObject<List<Account>>(accountsPayload.Decrypt());
            }
        }

        #endregion

        #region The Settings System

        public static void SettingSet<T>(string settingKey, T settingValue)
        {
            _settings[settingKey] = JToken.FromObject(settingValue);

            SettingsSave();
        }

        public static T SettingGet<T>(string settingKey, T defaultValue = default)
        {
            if (_settings.ContainsKey(settingKey))
            {
                var rawSetting = _settings[settingKey];

                return rawSetting.ToObject<T>();
            }

            SettingSet(settingKey, defaultValue);

            return defaultValue;
        }

        private static void SettingsLoad()
        {
            var settingsFileLocation = Path.Combine(LocalFilesPath, "settings.dat");

            if (!File.Exists(settingsFileLocation))
            {
                Log.Message($"No settings file found at: {settingsFileLocation}");

                _settings = new JObject();
            }
            else
            {
                Log.Message($"Settings file found at: {settingsFileLocation}, attempting to parse...");

                var settingsFileRawData = File.ReadAllText(settingsFileLocation);

                try
                {
                    _settings = JObject.Parse(settingsFileRawData);
                }
                catch (JsonReaderException)
                {
                    Log.Error($"Could not read settings file located at: {settingsFileLocation}");

                    _settings = new JObject();

                    return;
                }

                Log.Message("Successfully parsed settings file!");
            }
        }

        private static void SettingsSave()
        {
            var settingsFileLocation = Path.Combine(LocalFilesPath, "settings.dat");

            Log.Message($"Saving settings: {settingsFileLocation}");

            var settingsData = JsonConvert.SerializeObject(_settings);

            File.WriteAllText(settingsFileLocation, settingsData);
        }

        private static void SettingsClear()
        {
            var settingsFileLocation = Path.Combine(LocalFilesPath, "settings.dat");

            Log.Message($"Clearning settings: {settingsFileLocation}");

            File.Delete(settingsFileLocation);

            _settings = new JObject();
        }

        #endregion

        private static void SetChannel(uint newChannelIdx)
        {
            _currentChannelIdx = newChannelIdx;

            SettingSet($"{_activeAccount.Id}_channel", _currentChannelIdx);

            SetState(CoreState.Playback);
        }

        private static void SetState(CoreState newState)
        {
            _currentState = newState;

            StateChange?.Invoke(_currentState);
        }

        private static void ProcessCommandLineSwitches(string arguments)
        {
            var commandLineArgs = _commandLineParsingRegex.Matches(arguments).OfType<Match>().Select(m => m.Groups[0]).ToArray();

            foreach (var arg in commandLineArgs)
            {
                var entryData = arg.ToString().Split('=');

                if (entryData.Length > 0 && entryData[0].Substring(0, 2) == "--")
                {
                    var settingKey = entryData[0].Substring(2);
                    dynamic settingValue = true;

                    if (entryData.Length != 1)
                    {
                        var rawSettingValue = entryData[1];

                        if (int.TryParse(rawSettingValue, out var intValue))
                        {
                            settingValue = intValue;
                        }
                        else if (rawSettingValue.Substring(0, 1) == "\"" && rawSettingValue.Substring(rawSettingValue.Length - 1, 1) == "\"")
                        {
                            // TODO: Investigate if better string handling is needed
                            settingValue = rawSettingValue.Substring(1).Substring(0, rawSettingValue.Length - 2);
                        }
                    }

                    Log.Debug($"Command Line Arg Found: {settingKey}, with value of ({settingValue.GetType()})[{settingValue.ToString()}]");

                    SettingSet(settingKey, settingValue);
                }
            }
        }
    }
}
