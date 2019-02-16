// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using Newtonsoft.Json;
    using Services;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using System.Windows.Forms;
    using Timer = System.Timers.Timer;

    public static class TvCore
    {
        private const string LogFilename = "FoxIPTV.log";

        private const string ChannelFavoritesFilename = "fcdata";

        private const string ImageServerBlacklistFilename = "ibldata";

        private static readonly Timer _coreTimer = new Timer(100);

        private static readonly ConcurrentDictionary<uint, byte[]> _imageCache = new ConcurrentDictionary<uint, byte[]>();

        private static readonly object _logWriterLock = new object();

        private static readonly StreamWriter _logWriter;

        private static readonly FixedQueue<string> _logBuffer = new FixedQueue<string> { FixedSize = 1000 };

        private static readonly List<string> _imageServerBlacklist = new List<string>();

        private static Queue<Tuple<uint, string>> _imageCacheQueue;

        public const string ServiceUserAgentString = "Mozilla/5.0 AppleWebKit (KHTML, like Gecko) Chrome/69.0 Safari";

        public static event Action<string> Error;

        public static event Action<TvCoreState> StateChanged;

        public static event Action<int> ChannelLoadPercentageChanged;

        public static event Action<int> GuideLoadPercentageChanged;

        public static event Action<uint> ChannelChanged;

        public static event Action<Programme> ProgrammeChanged;

        public static int ServiceSelected { get; set; }

        public static List<IService> Services { get; } = new List<IService>();

        public static List<string> ChannelFavorites { get; } = new List<string>();

        public static string ExePath => AppDomain.CurrentDomain.BaseDirectory;

        public static string UserStoragePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FoxIPTV"); 

        public static string TempPath => Path.Combine(Path.GetTempPath(), "FoxIPTV");

        public static string CachePath => Path.Combine(TempPath, "cache");

        public static string LibraryPath => Path.Combine(TempPath, "libs");

        public static IService CurrentService => Services[ServiceSelected];

#if DEBUG
        public static TvCoreLogLevel CurrentLogLevel { get; set; } = TvCoreLogLevel.All;
#else
        public static TvCoreLogLevel CurrentLogLevel { get; set; } = TvCoreLogLevel.Error;
#endif

        public static Settings Settings { get; } = new Settings();

        public static TvCoreState State { get; private set; } = TvCoreState.None;

        public static List<uint> ChannelIndexList { get; private set; }

        public static List<Channel> Channels { get; private set; }

        public static Channel CurrentChannel { get; private set; }

        public static uint CurrentChannelIndex { get; private set; }

        public static List<Programme> Guide { get; private set; }

        public static List<Programme> CurrentChannelProgrammes { get; private set; }

        public static Programme CurrentProgramme { get; private set; }

        static TvCore()
        {
            _logWriter = new StreamWriter(Path.Combine(ExePath, LogFilename), append: true);

            LogStart();

            LogMessage($"[TVCore] Startup: Fox IPTV {Assembly.GetEntryAssembly().GetName().Version}...");

            LogInfo("[TVCore] Startup: Binding ThreadException & UnhandledException...");

            void LogException(Exception ex)
            {
                LogError($"[Exception] Unhandled: {ex.Message}\n{ex.StackTrace}");
            }

            Application.ThreadException += (s, a) => LogException(a.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, a) => LogException(a.ExceptionObject as Exception);

            try
            {
                var directoriesToCheck = new[] { TempPath, CachePath, LibraryPath, UserStoragePath };

                foreach (var dir in directoriesToCheck)
                {
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
            }
            catch (Exception)
            {
                Error?.Invoke("DIRECTORY_CREATION_ERROR");
                throw;
            }

            LogDebug("[TVCore] Startup: Application directories created");

            const string namespaceValue = "FoxIPTV.LibVLC.";

            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames().Where(x => x.StartsWith(namespaceValue));

            foreach (var rsc in resources)
            {
                var libName = rsc.Replace(namespaceValue, string.Empty);
                var libNameSub = libName.Substring(0, libName.IndexOf(".dll", StringComparison.Ordinal));
                var libNamePath = libNameSub.Replace('.', '\\');

                libName = libName.Replace(libNameSub, libNamePath);

                var pathSepIdx = libNamePath.LastIndexOf('\\');

                string libFilename;
                string dir;

                if (pathSepIdx != -1)
                {
                    libNamePath = libNamePath.Substring(0, pathSepIdx);

                    dir = Path.Combine(LibraryPath, libNamePath);

                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    libFilename = libName.Substring(pathSepIdx + 1);
                }
                else
                {
                    dir = LibraryPath;
                    libFilename = libName;
                }

                var libFullPath = Path.Combine(dir, libFilename);

                if (File.Exists(libFullPath))
                {
                    continue;
                }

                using (var input = assembly.GetManifestResourceStream(rsc))
                using (var output = File.Create(libFullPath))
                {
                    input?.CopyTo(output);
                }

                LogDebug($"[TVCore] Writing resrouce to disk: {libFilename}");
            }

            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IService).IsAssignableFrom(p) && p.IsClass);

            foreach (var type in types)
            {
                var instance = (IService)Activator.CreateInstance(type);

                instance.ProgressUpdater = new Tuple<IProgress<int>, IProgress<int>>(new Progress<int>(percentage => ChannelLoadPercentageChanged?.Invoke(percentage)), new Progress<int>(percentage => GuideLoadPercentageChanged?.Invoke(percentage)));

                LogDebug($"[TVCore] Startup: Installing [{instance.Title}] Service");

                Services.Add(instance);
            }

            Settings.Load();

            FavoritesLoad();

            BlacklistLoad();

            LogMessage("[TVCore] Startup: Finished Fox IPTV TVCore Startup");
        }

        public static void LogError(string message) => Log(TvCoreLogLevel.Error, message);

        public static void LogInfo(string message) => Log(TvCoreLogLevel.Info, message);

        public static void LogDebug(string message) => Log(TvCoreLogLevel.Debug, message);

        public static void LogMessage(string message) => Log(TvCoreLogLevel.Message, message);

        public static async Task Start()
        {
            if (State != TvCoreState.None)
            {
                throw new ApplicationException("TvCore already initializing or initialized...");
            }

            ChangeState(TvCoreState.Starting);

            var (channels, guide) = await CurrentService.Process();

            Channels = channels;
            Guide = guide;

            ChannelIndexList = Channels.Select(x => x.Index).ToList();

            _imageCacheQueue = new Queue<Tuple<uint, string>>(Channels.Select(chan => new Tuple<uint, string>(chan.Index, chan.Logo?.ToString())).ToList());

            ThreadPool.QueueUserWorkItem(async x => await DownloadChannelImages());

            ChangeState(TvCoreState.Running);

            _coreTimer.AutoReset = true;
            _coreTimer.Elapsed += CoreTimerOnElapsed;
            _coreTimer.Start();
        }

        public static void ChangeChannel(bool direction)
        {
            LogDebug($"[TVCore] Changing Channel in the {(direction ? "UP" : "DOWN")} direction");

            if (direction)
            {
                var newIdx = CurrentChannelIndex + 1;

                SetChannel(newIdx >= ChannelIndexList.Count ? 0 : newIdx);
            }
            else
            {
                if (CurrentChannelIndex == 0)
                {
                    SetChannel((uint)ChannelIndexList.Count - 1);
                }
                else
                {
                    SetChannel(CurrentChannelIndex - 1);
                }
            }
        }

        public static void SetChannel(uint channelIndex)
        {
            var totalChannels = ChannelIndexList.Count;

            if (channelIndex > totalChannels)
            {
                channelIndex = (uint)totalChannels;
            }

            if (CurrentChannel != null && CurrentChannelIndex == channelIndex)
            {
                return;
            }

            LogDebug($"[TVCore] Setting channelIndex to {channelIndex}");

            CurrentChannel = Channels.Find(x => x.Index == ChannelIndexList[(int)channelIndex]);
            CurrentChannelIndex = channelIndex;
            CurrentChannelProgrammes = Guide.Where(x => x.Channel == CurrentChannel.Id).ToList();
            CurrentProgramme = CurrentChannelProgrammes.Find(x => x.Start < DateTime.UtcNow && x.Stop > DateTime.UtcNow);

            ChannelChanged?.Invoke(channelIndex);
        }

        public static void AddFavoriteChannel(string channelId)
        {
            LogDebug($"[TVCore] AddFavoriteChannel({channelId})");

            if (!ChannelFavorites.Contains(channelId))
            {
                ChannelFavorites.Add(channelId);
            }

            FavoritesSave();
        }

/*
        public static void AddFavoriteChannels(IEnumerable<string> channelIds)
        {
            LogDebug("[TVCore] AddFavoriteChannels()");

            ChannelFavorites.Clear();
            ChannelFavorites.AddRange(channelIds);

            FavoritesSave();
        }
*/

        public static void RemoveFavoriteChannel(string channelId)
        {
            LogDebug($"[TVCore] RemoveFavoriteChannel({channelId})");

            if (ChannelFavorites.Contains(channelId))
            {
                ChannelFavorites.Remove(channelId);
            }

            FavoritesSave();
        }

        public static async Task<string> DownloadStringAndCache(string contentUri, string cacheFilename, int cacheTime)
        {
            LogDebug($"[TVCore] DownloadStringAndCache(url, {cacheFilename}, {cacheTime}) called");

            string contents;

            var cachePath = Path.Combine(CachePath, cacheFilename);

            if (File.Exists(cachePath) && DateTime.Now - File.GetLastWriteTime(cachePath) < TimeSpan.FromHours(cacheTime))
            {
                contents = File.ReadAllText(cachePath);
            }
            else
            {
            LogDebug("[TVCore] DownloadStringAndCache CACHE MISS");

                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add("user-agent", ServiceUserAgentString);

                    contents = await webClient.DownloadStringTaskAsync(contentUri);
                }

                // Cache
                File.Delete(cachePath);
                File.WriteAllText(cachePath, contents);
            }

            return contents;
        }

        public static async Task<byte[]> DownloadImageAndCache(string imageUri)
        {
            byte[] contents;

            var cachePath = Path.Combine(CachePath, $"idata{imageUri.ToMD5()}.jpg");

            if (File.Exists(cachePath))
            {
                contents = File.ReadAllBytes(cachePath);
            }
            else
            {
                LogDebug($"[TVCore] DownloadImageAndCache({imageUri}) CACHE MISS");

                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add("user-agent", ServiceUserAgentString);

                    contents = await webClient.DownloadDataTaskAsync(imageUri);
                }

                // Cache
                File.Delete(cachePath);
                File.WriteAllBytes(cachePath, contents);
            }

            return contents;
        }

        private static void FavoritesLoad()
        {
            var favoriteChannelsFilePath = Path.Combine(UserStoragePath, ChannelFavoritesFilename);

            if (!File.Exists(favoriteChannelsFilePath))
            {
                return;
            }

            LogInfo($"[TVCore] FavoritesLoad(): Loading channelIndex favorites file: {favoriteChannelsFilePath}");

            var rawJson = File.ReadAllText(favoriteChannelsFilePath);

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return;
            }

            ChannelFavorites.Clear();

            try
            {
                ChannelFavorites.AddRange(JsonConvert.DeserializeObject<List<string>>(rawJson));
            }
            catch (Exception e)
            {
                LogError($"[TVCore] FavoritesLoad(): Error parsing channelIndex favorites file... {e.Message}");
                return;
            }

            LogDebug($"[TVCore] FavoritesLoad(): Loaded {ChannelFavorites.Count} channelIndex favorites");
        }

        private static void BlacklistLoad()
        {
            var imageServerBlacklistFilename = Path.Combine(UserStoragePath, ImageServerBlacklistFilename);

            if (!File.Exists(imageServerBlacklistFilename))
            {
                return;
            }

            LogInfo($"[TVCore] BlacklistLoad(): Loading image server blacklist file: {imageServerBlacklistFilename}");

            var rawJson = File.ReadAllText(imageServerBlacklistFilename);

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return;
            }

            _imageServerBlacklist.Clear();

            try
            {
                _imageServerBlacklist.AddRange(JsonConvert.DeserializeObject<List<string>>(rawJson));
            }
            catch (Exception e)
            {
                LogError($"[TVCore] FavoritesLoad(): Error parsing channelIndex favorites file... {e.Message}");
                return;
            }

            LogDebug($"[TVCore] FavoritesLoad(): Loaded {ChannelFavorites.Count} blacklist images");
        }

        private static void FavoritesSave()
        {
            var rawJson = JsonConvert.SerializeObject(ChannelFavorites);

            var favoriteChannelsFilePath = Path.Combine(UserStoragePath, ChannelFavoritesFilename);

            LogInfo($"[TVCore] FavoritesSave(): Saving channelIndex favorites file: {favoriteChannelsFilePath}");

            File.WriteAllText(favoriteChannelsFilePath, rawJson);
        }

        private static void BlacklistSave()
        {
            var rawJson = JsonConvert.SerializeObject(_imageServerBlacklist);

            var imageServerBlacklistFilename = Path.Combine(UserStoragePath, ImageServerBlacklistFilename);

            LogDebug($"[TVCore] BlacklistSave(): Saving image server blacklist file: {imageServerBlacklistFilename}");

            File.WriteAllText(imageServerBlacklistFilename, rawJson);
        }

        private static void LogStart()
        {
            lock (_logWriterLock)
            {
                _logWriter.WriteLine(string.Empty);
                _logWriter.WriteLine(string.Empty);
                _logWriter.Flush();
            }
        }

        private static void Log(TvCoreLogLevel logLevel, string message)
        {
            if (logLevel == TvCoreLogLevel.None || logLevel == TvCoreLogLevel.All || logLevel > CurrentLogLevel)
            {
                return;
            }

            lock (_logWriterLock)
            {
                var logLine = $"[{DateTime.UtcNow:O}]-[{logLevel.ToString().ToUpper().PadLeft(7)}]: {message}";

                _logWriter.WriteLine(logLine);
                _logWriter.Flush();

                _logBuffer.Enqueue(logLine);
            }
        }

        private static void ChangeState(TvCoreState newState)
        {
            if (State == newState)
            {
                throw new ApplicationException("New state is exactly same as old state, unknown behaviour expected...");
            }

            if (newState < State)
            {
                throw new ApplicationException("State cannot be reversed!");
            }

            LogDebug($"[TVCore] ChangeState({newState})");

            State = newState;

            StateChanged?.Invoke(newState);
        }

        private static async Task DownloadChannelImages()
        {
            LogDebug("[TVCore] DownloadChannelImages()");

            while (_imageCacheQueue.Count > 0)
            {
                var image = _imageCacheQueue.Dequeue();

                if (image.Item2 == null)
                {
                    continue;
                }

                if (_imageServerBlacklist.Contains(image.Item2))
                {
                    LogDebug($"[TVCore] DownloadChannelImages(): Image is blacklisted; {image.Item2}");
                    continue;
                }

                try
                {
                    var imageData = await DownloadImageAndCache(image.Item2);
                    _imageCache.TryAdd(image.Item1, imageData);
                    Channels.Find(x => x.Index == image.Item1).LogoImage = Image.FromStream(new MemoryStream(imageData));
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("(404) Not Found") || ex.Message.Contains("(403) Forbidden") || ex.Message.Contains("Parameter is not valid."))
                    {
                        _imageServerBlacklist.Add(image.Item2);

                        LogError($"[TVCore] DownloadChannelImages(): Blacklisted Image Server: {image.Item2}");

                        BlacklistSave();
                    }
                    else
                    {
                        LogError($"[TVCore] DownloadChannelImages({image.Item2}): Exception: {ex.Message}");
                        // Ignored
                    }
                }
            }
        }

        private static void CoreTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (CurrentChannelProgrammes == null)
            {
                return;
            }

            var newProgramme = CurrentChannelProgrammes.Find(x => x.Start < DateTime.UtcNow && x.Stop > DateTime.UtcNow);

            if (Equals(CurrentProgramme, newProgramme))
            {
                return;
            }

            LogDebug("[TVCore] CoreTimerOnElapsed(): New programme detected!");

            CurrentProgramme = newProgramme;

            ProgrammeChanged?.Invoke(newProgramme);
        }
    }

    public enum TvCoreState
    {
        None,
        Starting,
        Running
    }

    public enum TvCoreLogLevel
    {
        None,
        Error,
        Info,
        Debug,
        Message,
        All
    }
}
