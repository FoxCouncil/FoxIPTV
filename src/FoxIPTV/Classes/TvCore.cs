// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using Newtonsoft.Json;
    using Services;
    using Services.Scripting;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using Avalonia.Media.Imaging;
    using Timer = System.Timers.Timer;

    /// <summary>The static god class for FoxIPTV's functionality and features</summary>
    public static class TvCore
    {
        /// <summary>The default filename for the applications logfile</summary>
        private const string LogFilename = "FoxIPTV.log";

        /// <summary>The default filename for the applications favorite channel data</summary>
        private const string ChannelFavoritesFilename = "fcdata";

        /// <summary>The default filename for the applications image blacklist data</summary>
        private const string ImageServerBlacklistFilename = "ibldata";

        /// <summary>The default filename for the streams found to be copy-protected, one file per provider</summary>
        private const string ProtectedChannelsFilename = "drmdata";

        /// <summary>The default filename for the channels the user never wants to see, one file per provider</summary>
        private const string HiddenChannelsFilename = "hidden";

        /// <summary>The stream addresses the user has hidden for the current provider</summary>
        private static readonly HashSet<string> _hiddenStreams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The stream addresses found to be copy-protected for the current provider</summary>
        private static readonly HashSet<string> _protectedStreams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The list position a copy-protected channel was just taken out of, while it is still the current channel; -1 otherwise</summary>
        private static int _protectedGapAt = -1;

        /// <summary>A non win forms timer at 100ms intervals</summary>
        private static readonly Timer _coreTimer = new Timer(100);

        /// <summary>A storage cache for loading channel logo image data</summary>
        private static readonly ConcurrentDictionary<uint, byte[]> _imageCache = new ConcurrentDictionary<uint, byte[]>();

        /// <summary>The synchronizer object for writing to the logfile</summary>
        private static readonly object _logWriterLock = new object();

        /// <summary>The stream writer for writing to the logfile</summary>
        private static readonly StreamWriter _logWriter;

        /// <summary>A in-memory log storage, limited to 1,000 items</summary>
        private static readonly FixedQueue<string> _logBuffer = new FixedQueue<string> { FixedSize = 1000 };

        /// <summary>Lines waiting to be written; callers never touch the disk, LibVLC logs from its own download and decode threads and a flush there is a stall on screen</summary>
        private static readonly BlockingCollection<string> _logQueue = new BlockingCollection<string>();

        /// <summary>The one thread that writes the log file</summary>
        private static readonly Thread _logThread = new Thread(LogWriterLoop) { IsBackground = true, Name = "Log writer" };

        /// <summary>The in-memory image server black list, to avoid hitting servers that return non 200 responses</summary>
        private static readonly List<string> _imageServerBlacklist = new List<string>();

        /// <summary>The queue of image Uris to download</summary>
        private static Queue<Tuple<uint, string>> _imageCacheQueue;

        /// <summary>The user agent string we send to the services, a current desktop Chrome; see <see cref="Web.UserAgent"/></summary>
        public const string ServiceUserAgentString = Web.UserAgent;

        /// <summary>The error event; any significant errors will be posted here for safe display to the user</summary>
        public static event Action<string> Error;

        /// <summary>The TvCore's state change event, will include the new state being changed to</summary>
        public static event Action<TvCoreState> StateChanged;

        /// <summary>A event to inform of percentage progress on channels being loaded</summary>
        public static event Action<int> ChannelLoadPercentageChanged;

        /// <summary>A event to inform of percentage progress on channel data being loaded</summary>
        public static event Action<int> GuideLoadPercentageChanged;

        /// <summary>A event to inform of percentage progress on guide data being loaded</summary>
        public static event Action<uint> ChannelChanged;

        /// <summary>Raised when channels leave the list, after a copy-protected one is found</summary>
        public static event Action ChannelListChanged;

        /// <summary>A event to inform of a chance of the programme while active</summary>
        public static event Action<Programme> ProgrammeChanged;

        /// <summary>An event raised when on-demand media is chosen for playback, with the source and a display title</summary>
        public static event Action<MediaSource, string> MediaChanged;

        /// <summary>The on-demand media currently overriding the live channel, or null when watching live TV</summary>
        public static MediaSource CurrentMedia { get; private set; }

        /// <summary>The display title of <see cref="CurrentMedia"/></summary>
        public static string CurrentMediaTitle { get; private set; }

        /// <summary>The current service being used</summary>
        public static int ServiceSelected { get; set; }

        /// <summary>A list of loaded services this IPTV client supports</summary>
        public static List<IService> Services { get; } = new List<IService>();

        /// <summary>In-memory storage of user channel favorties</summary>
        public static List<string> ChannelFavorites { get; } = new List<string>();

        /// <summary>A readonly value of the current path of the executable file</summary>
        public static string ExePath => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>The version the build was stamped with, without the source revision</summary>
        public static string Version { get; } = (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

        /// <summary>The full path of the log file currently being written</summary>
        public static string LogPath { get; private set; }

        /// <summary>The path used to store user data</summary>
        public static string UserStoragePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FoxIPTV");

        /// <summary>The path to the temporary folder used by this application</summary>
        public static string TempPath => Path.Combine(Path.GetTempPath(), "FoxIPTV");
        
        /// <summary>The path to store cached data, (data that can be re-downloaded)</summary>
        public static string CachePath => Path.Combine(TempPath, "cache");

        /// <summary>A read only access to the current service instance running, null if no providers loaded</summary>
        public static IService CurrentService => Services.Count == 0 ? null : Services[Math.Max(0, Math.Min(ServiceSelected, Services.Count - 1))];

        /// <summary>The current service as a library provider, null if it does not offer one</summary>
        public static ILibraryProvider CurrentLibrary => CurrentService != null && CurrentService.Capabilities.HasFlag(ProviderCapabilities.Library) ? CurrentService as ILibraryProvider : null;

        /// <summary>Select the current service by its id</summary>
        /// <param name="id">The <see cref="IService.Id"/></param>
        /// <returns>True if a matching service was found</returns>
        public static bool SelectService(string id)
        {
            var index = Services.FindIndex(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                return false;
            }

            ServiceSelected = index;

            return true;
        }

        /// <summary>Play on-demand media instead of the live channel</summary>
        /// <param name="source">The resolved source</param>
        /// <param name="title">A display title</param>
        public static void PlayMedia(MediaSource source, string title)
        {
            LogInfo($"[TVCore] PlayMedia({source.Url}, {title})");

            CurrentMedia = source;
            CurrentMediaTitle = title;

            MediaChanged?.Invoke(source, title);
        }

#if DEBUG
        /// <summary>During debug, all logging is turned on</summary>
        public static TvCoreLogLevel CurrentLogLevel { get; set; } = TvCoreLogLevel.All;
#else
        /// <summary>Release builds only report errors</summary>
        public static TvCoreLogLevel CurrentLogLevel { get; set; } = TvCoreLogLevel.Error;
#endif

        /// <summary>The current instance for the settings for TVCore</summary>
        public static Settings Settings { get; } = new Settings();

        /// <summary>The current TVCoreState</summary>
        public static TvCoreState State { get; private set; } = TvCoreState.None;

        /// <summary>The list of channel indexes, to convert from Channel numbers to zero based indexes, and vice-versa</summary>
        public static List<uint> ChannelIndexList { get; private set; }

        /// <summary>The currently loaded channels</summary>
        public static List<Channel> Channels { get; private set; }

        /// <summary>The currently being watched channel</summary>
        public static Channel CurrentChannel { get; private set; }

        /// <summary>The current channel non-zero index,</summary>
        public static uint CurrentChannelIndex { get; private set; }

        /// <summary>The currently loaded guide data</summary>
        public static List<Programme> Guide { get; private set; }

        /// <summary>A list of programmes for the current channel</summary>
        public static List<Programme> CurrentChannelProgrammes { get; private set; }

        /// <summary>The current programme being watched</summary>
        public static Programme CurrentProgramme { get; private set; }

        /// <summary>The static constructor for the TVCore object</summary>
        static TvCore()
        {
            // Create the log writer, shared so a restart can open it while the old process is still closing, and retried for the same reason
            var logPath = LogFilePath();

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    _logWriter = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete));

                    break;
                }
                catch (IOException) when (attempt < 10)
                {
                    System.Threading.Thread.Sleep(300);
                }
            }

            LogStart();

            LogMessage($"[TVCore] Startup: Fox IPTV {Version} on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}...");

            LogInfo("[TVCore] Startup: Binding ThreadException & UnhandledException...");

            void LogException(Exception ex)
            {
                LogError($"[Exception] Unhandled: {ex.Message}\n{ex.StackTrace}");
            }

            TaskScheduler.UnobservedTaskException += (s, a) => LogException(a.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, a) => LogException(a.ExceptionObject as Exception);

            try
            {
                // Make sure these directories exist
                var directoriesToCheck = new[] { TempPath, CachePath, UserStoragePath };

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

            var assembly = Assembly.GetExecutingAssembly();

            // Grab all built-in IService objects, the ones written in C# with a parameterless constructor
            var types = assembly.GetTypes().Where(p => typeof(IService).IsAssignableFrom(p) && p.IsClass && !p.IsAbstract && p.GetConstructor(Type.EmptyTypes) != null);

            foreach (var type in types)
            {
                try
                {
                    InstallService((IService)Activator.CreateInstance(type));
                }
                catch (Exception ex)
                {
                    LogError($"[TVCore] Startup: Unable to install {type.Name}: {ex.Message}");
                }
            }

            // Then every JavaScript plugin, built-in and user supplied
            foreach (var script in ScriptLoader.LoadAll())
            {
                if (Services.Any(x => string.Equals(x.Id, script.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    LogError($"[TVCore] Startup: Plugin id '{script.Id}' clashes with a built-in service, skipped");

                    continue;
                }

                InstallService(script);
            }

            Services.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

            // Load the settings from the disk, if they exist
            Settings.Load();

            // Load the image blacklist, if they exist
            BlacklistLoad();

            LogMessage("[TVCore] Startup: Finished Fox IPTV TVCore Startup");
        }

        /// <summary>Where the log goes: beside the executable when that folder can be written to, as it can in a build folder, otherwise the user storage folder</summary>
        private static string LogFilePath()
        {
            var besideExe = Path.Combine(ExePath, LogFilename);

            try
            {
                using (new FileStream(besideExe, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                }

                LogPath = besideExe;
            }
            catch (Exception)
            {
                Directory.CreateDirectory(UserStoragePath);

                LogPath = Path.Combine(UserStoragePath, LogFilename);
            }

            return LogPath;
        }

        /// <summary>Wire a service's progress reporting and add it to <see cref="Services"/></summary>
        /// <param name="instance">The service</param>
        private static void InstallService(IService instance)
        {
            instance.ProgressUpdater = new Tuple<IProgress<int>, IProgress<int>>(new Progress<int>(percentage => ChannelLoadPercentageChanged?.Invoke(percentage)), new Progress<int>(percentage => GuideLoadPercentageChanged?.Invoke(percentage)));

            LogDebug($"[TVCore] Startup: Installing [{instance.Title}] Service ({instance.Capabilities})");

            Services.Add(instance);
        }

        /// <summary>A shortcut method for sending a log message of type Error</summary>
        /// <param name="message">The Error message needed to be logged</param>
        public static void LogError(string message) => Log(TvCoreLogLevel.Error, message);

        /// <summary>A shortcut method for sending a log message of type Info</summary>
        /// <param name="message">The Info message needed to be logged</param>
        public static void LogInfo(string message) => Log(TvCoreLogLevel.Info, message);

        /// <summary>A shortcut method for sending a log message of type Debug</summary>
        /// <param name="message">The Debug message needed to be logged</param>
        public static void LogDebug(string message) => Log(TvCoreLogLevel.Debug, message);

        /// <summary>A shortcut method for sending a basic log message</summary>
        /// <param name="message">The message needed to be logged</param>
        public static void LogMessage(string message) => Log(TvCoreLogLevel.Message, message);

        /// <summary>Used to start the TVCore functionality, setup the user experience</summary>
        /// <returns>An awaitable task</returns>
        public static async Task Start()
        {
            if (State != TvCoreState.None)
            {
                throw new ApplicationException("TvCore already initializing or initialized...");
            }

            ChangeState(TvCoreState.Starting);

            // Load the user's favorite channels for this provider, if they exist
            FavoritesLoad();

            ProtectedLoad();

            TvCore.LogDebug($"[{CurrentService.Title}] Process(): Starting data processing...");

            // Ask the service to give us the channel and guide data
            var result = await CurrentService.Process();

            Channels = result?.Item1 ?? new List<Channel>();
            Guide = result?.Item2 ?? new List<Programme>();

            LogInfo($"[TVCore] Start(): {Channels.Count} channel(s), {Guide.Count} programme(s) from {CurrentService.Title}");

            // Build the zero index map to the actual channel numbers
            // Channels found to be copy-protected on an earlier run, and channels the user hid, never show up
            var protectedCount = Channels.RemoveAll(x => x.Stream != null && _protectedStreams.Contains(x.Stream.ToString()));
            var hiddenCount = Channels.RemoveAll(x => x.Stream != null && _hiddenStreams.Contains(x.Stream.ToString()));

            if (protectedCount + hiddenCount > 0)
            {
                LogInfo($"[TVCore] Start(): {protectedCount} copy-protected and {hiddenCount} hidden channel(s) left out");
            }

            ChannelIndexList = Channels.Select(x => x.Index).ToList();

            // Put all the logos needed to be loaded into a queue so we can load them in another thread
            _imageCacheQueue = new Queue<Tuple<uint, string>>(Channels.Select(chan => new Tuple<uint, string>(chan.Index, chan.Logo?.ToString())).ToList());

            // Threads...
            ThreadPool.QueueUserWorkItem(async x => await DownloadChannelImages());

            // We are now considered ready to be running
            ChangeState(TvCoreState.Running);

            // Start the timer
            _coreTimer.AutoReset = true;
            _coreTimer.Elapsed += CoreTimerOnElapsed;
            _coreTimer.Start();
        }

        /// <summary>Change the channel in a up or down direction</summary>
        /// <param name="direction">If true, increase channel by one, if false, decrease channel by one</param>
        public static void ChangeChannel(bool direction)
        {
            LogDebug($"[TVCore] Changing Channel in the {(direction ? "UP" : "DOWN")} direction");

            if (ChannelIndexList == null || ChannelIndexList.Count == 0)
            {
                return;
            }

            if (_protectedGapAt >= 0 && !Channels.Contains(CurrentChannel))
            {
                // The current channel left the list: up is whatever took its place, down is the one before
                var count = ChannelIndexList.Count;
                var target = direction ? _protectedGapAt : _protectedGapAt - 1;

                SetChannel((uint)((target % count + count) % count));

                return;
            }

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

        /// <summary>Set the channel, using the zero indexed number</summary>
        /// <param name="channelIndex">A zero based number to switch to</param>
        public static void SetChannel(uint channelIndex)
        {
            var totalChannels = ChannelIndexList?.Count ?? 0;

            if (totalChannels == 0)
            {
                LogDebug("[TVCore] SetChannel(): No channels loaded, ignoring");

                return;
            }

            if (channelIndex >= totalChannels)
            {
                channelIndex = (uint)totalChannels - 1;
            }

            if (CurrentChannel != null && CurrentChannelIndex == channelIndex && CurrentMedia == null && Channels.Contains(CurrentChannel))
            {
                return;
            }

            // Any on-demand media is abandoned in favour of the live channel
            CurrentMedia = null;
            CurrentMediaTitle = null;

            LogDebug($"[TVCore] Setting channelIndex to {channelIndex}");

            _protectedGapAt = -1;

            CurrentChannel = Channels.Find(x => x.Index == ChannelIndexList[(int)channelIndex]);
            CurrentChannelIndex = channelIndex;
            CurrentChannelProgrammes = Guide.Where(x => x.Channel == CurrentChannel.Id).ToList();
            CurrentProgramme = CurrentChannelProgrammes.Find(x => x.Start < DateTime.UtcNow && x.Stop > DateTime.UtcNow);

            ChannelChanged?.Invoke(channelIndex);
        }

        /// <summary>Add a favorite channel using the channel string based ID</summary>
        /// <param name="channelId">A string based key for the channel to favorite</param>
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

        /// <summary>Remove a favorite channel using the channel string based ID</summary>
        /// <param name="channelId">A string based key for the channel to un-favorite</param>
        public static void RemoveFavoriteChannel(string channelId)
        {
            LogDebug($"[TVCore] RemoveFavoriteChannel({channelId})");

            if (ChannelFavorites.Contains(channelId))
            {
                ChannelFavorites.Remove(channelId);
            }

            FavoritesSave();
        }

        /// <summary>A central place to download string based data and cache it for a specified time</summary>
        /// <param name="contentUri">The url to the string data that needs to be downloaded</param>
        /// <param name="cacheFilename">The filename of the cached string data</param>
        /// <param name="cacheTime">How long to cache the data, in hours</param>
        /// <returns>An awaitable task</returns>
        public static async Task<string> DownloadStringAndCache(string contentUri, string cacheFilename, int cacheTime)
        {
            LogDebug($"[TVCore] DownloadStringAndCache(url, {cacheFilename}, {cacheTime}) called");

            return await Web.GetStringCached(contentUri, cacheFilename, cacheTime);
        }

        /// <summary>A central place to download image based data and cache it</summary>
        /// <param name="imageUri">The URI of the image to download</param>
        /// <returns>A awaitable task</returns>
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

                contents = await Web.GetBytes(imageUri);

                // Cache
                File.Delete(cachePath);
                File.WriteAllBytes(cachePath, contents);
            }

            return contents;
        }

        /// <summary>The favorites file for the current provider, channel ids are only meaningful within one provider</summary>
        private static string FavoritesFilePath => Path.Combine(UserStoragePath, CurrentService == null ? ChannelFavoritesFilename : $"{ChannelFavoritesFilename}-{CurrentService.Id}");

        /// <summary>Load the favorite data from the user storage location</summary>
        private static void FavoritesLoad()
        {
            ChannelFavorites.Clear();

            var favoriteChannelsFilePath = FavoritesFilePath;

            if (!File.Exists(favoriteChannelsFilePath))
            {
                // Favorites saved before providers were tracked, used until this provider saves its own
                favoriteChannelsFilePath = Path.Combine(UserStoragePath, ChannelFavoritesFilename);
            }

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

        /// <summary>Load the image blacklist data from the user storage location</summary>
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

        /// <summary>The copy-protected streams file for the current provider</summary>
        private static string ProtectedFilePath => Path.Combine(UserStoragePath, $"{ProtectedChannelsFilename}-{CurrentService?.Id ?? "none"}");

        /// <summary>The hidden channels file for the current provider</summary>
        private static string HiddenFilePath => Path.Combine(UserStoragePath, $"{HiddenChannelsFilename}-{CurrentService?.Id ?? "none"}");

        /// <summary>Load the copy-protected and hidden stream lists for the current provider</summary>
        private static void ProtectedLoad()
        {
            LoadStreamList(ProtectedFilePath, _protectedStreams);
            LoadStreamList(HiddenFilePath, _hiddenStreams);
        }

        /// <summary>Read a saved list of stream addresses, a JSON array of strings</summary>
        private static void LoadStreamList(string path, HashSet<string> into)
        {
            into.Clear();

            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                foreach (var stream in JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path)) ?? new List<string>())
                {
                    into.Add(stream);
                }
            }
            catch (Exception e)
            {
                LogError($"[TVCore] LoadStreamList(): Error parsing {path}: {e.Message}");
            }
        }

        /// <summary>A channel turned out to be copy-protected: remember it and take it out of the channel list now; the channel stays current until the user moves on</summary>
        /// <param name="channel">The channel</param>
        public static void MarkProtected(Channel channel)
        {
            if (channel?.Stream == null || Channels == null || !_protectedStreams.Add(channel.Stream.ToString()))
            {
                return;
            }

            LogInfo($"[TVCore] MarkProtected({channel.Index} {channel.Name}): copy-protected, hidden from now on");

            try
            {
                File.WriteAllText(ProtectedFilePath, JsonConvert.SerializeObject(_protectedStreams.ToList()));
            }
            catch (Exception e)
            {
                LogError($"[TVCore] MarkProtected(): Unable to write {ProtectedFilePath}: {e.Message}");
            }

            var position = Channels.IndexOf(channel);

            if (position < 0)
            {
                return;
            }

            Channels.RemoveAt(position);
            ChannelIndexList = Channels.Select(x => x.Index).ToList();

            // Where the channel was, so channel up and down from it land on its old neighbours
            _protectedGapAt = position;

            if (ChannelIndexList.Count > 0)
            {
                CurrentChannelIndex = (uint)Math.Min(position, ChannelIndexList.Count - 1);
            }

            ChannelListChanged?.Invoke();
        }

        /// <summary>Saves the user favorite channels to the user storage location</summary>
        private static void FavoritesSave()
        {
            var rawJson = JsonConvert.SerializeObject(ChannelFavorites);

            var favoriteChannelsFilePath = FavoritesFilePath;

            LogInfo($"[TVCore] FavoritesSave(): Saving channelIndex favorites file: {favoriteChannelsFilePath}");

            File.WriteAllText(favoriteChannelsFilePath, rawJson);
        }

        /// <summary>Saves the image blacklist data to the user storage location</summary>
        private static void BlacklistSave()
        {
            var rawJson = JsonConvert.SerializeObject(_imageServerBlacklist);

            var imageServerBlacklistFilename = Path.Combine(UserStoragePath, ImageServerBlacklistFilename);

            LogDebug($"[TVCore] BlacklistSave(): Saving image server blacklist file: {imageServerBlacklistFilename}");

            File.WriteAllText(imageServerBlacklistFilename, rawJson);
        }

        /// <summary>Start the logging system, and insert two blank lines</summary>
        private static void LogStart()
        {
            _logQueue.Add(string.Empty);
            _logQueue.Add(string.Empty);

            _logThread.Start();

            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                // Give the writer a moment to drain what is queued so the last lines of a run are not lost
                _logQueue.CompleteAdding();
                _logThread.Join(TimeSpan.FromSeconds(2));
            };
        }

        /// <summary>Drain the queue to the file, flushing once per batch rather than once per line</summary>
        private static void LogWriterLoop()
        {
            try
            {
                foreach (var line in _logQueue.GetConsumingEnumerable())
                {
                    lock (_logWriterLock)
                    {
                        _logWriter.WriteLine(line);

                        string more;

                        while (_logQueue.TryTake(out more))
                        {
                            _logWriter.WriteLine(more);
                        }

                        _logWriter.Flush();
                    }
                }
            }
            catch (Exception)
            {
                // Shutting down, the file may already be closed
            }
        }

        /// <summary>Log a message of a certain type</summary>
        /// <param name="logLevel">The type of log this is</param>
        /// <param name="message">The log message</param>
        private static void Log(TvCoreLogLevel logLevel, string message)
        {
            if (logLevel == TvCoreLogLevel.None || logLevel == TvCoreLogLevel.All || logLevel > CurrentLogLevel)
            {
                return;
            }

            var logLine = $"[{DateTime.UtcNow:O}]-[{logLevel.ToString().ToUpper().PadLeft(7)}]: {message}";

            lock (_logBuffer)
            {
                _logBuffer.Enqueue(logLine);
            }

            try
            {
                _logQueue.Add(logLine);
            }
            catch (InvalidOperationException)
            {
                // The queue closed at process exit, a line logged after that is dropped
            }
        }

        /// <summary>Change the internal TVCore state</summary>
        /// <param name="newState">The new state to change to</param>
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

        /// <summary>Download all of the channel images in the queue</summary>
        /// <returns>An awaitable task</returns>
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

                    Bitmap logo;

                    try
                    {
                        logo = new Bitmap(new MemoryStream(imageData));
                    }
                    catch (Exception decodeFailure)
                    {
                        // Not a picture at all, treated like a missing one
                        throw new InvalidDataException($"404 not an image, {decodeFailure.Message}");
                    }

                    Channels.Find(x => x.Index == image.Item1).LogoImage = logo;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("404") || ex.Message.Contains("403"))
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

        /// <summary>The timer tick event handler</summary>
        /// <param name="sender">The object that created this event</param>
        /// <param name="e">The event data object</param>
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

    /// <summary>A TVCore state</summary>
    public enum TvCoreState
    {
        None,
        Starting,
        Running
    }

    /// <summary>The log type</summary>
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
