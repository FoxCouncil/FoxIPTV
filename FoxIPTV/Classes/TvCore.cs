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

    /// <summary>The static god class for FoxIPTV's functionality and features</summary>
    public static class TvCore
    {
        /// <summary>The default filename for the applications logfile</summary>
        private const string LogFilename = "FoxIPTV.log";

        /// <summary>The default filename for the applications favorite channel data</summary>
        private const string ChannelFavoritesFilename = "fcdata";

        /// <summary>The default filename for the applications image blacklist data</summary>
        private const string ImageServerBlacklistFilename = "ibldata";

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

        /// <summary>The in-memory image server black list, to avoid hitting servers that return non 200 responses</summary>
        private static readonly List<string> _imageServerBlacklist = new List<string>();

        /// <summary>The queue of image Uris to download</summary>
        private static Queue<Tuple<uint, string>> _imageCacheQueue;

        /// <summary>The server agent string we send to the services (Chrome 69)</summary>
        public const string ServiceUserAgentString = "Mozilla/5.0 AppleWebKit (KHTML, like Gecko) Chrome/69.0 Safari";

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

        /// <summary>A event to inform of a chance of the programme while active</summary>
        public static event Action<Programme> ProgrammeChanged;

        /// <summary>The current service being used</summary>
        public static int ServiceSelected { get; set; }

        /// <summary>A list of loaded services this IPTV client supports</summary>
        public static List<IService> Services { get; } = new List<IService>();

        /// <summary>In-memory storage of user channel favorties</summary>
        public static List<string> ChannelFavorites { get; } = new List<string>();

        /// <summary>A readonly value of the current path of the executable file</summary>
        public static string ExePath => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>The path used to store user data</summary>
        public static string UserStoragePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FoxIPTV");

        /// <summary>The path to the temporary folder used by this application</summary>
        public static string TempPath => Path.Combine(Path.GetTempPath(), "FoxIPTV");
        
        /// <summary>The path to store cached data, (data that can be re-downloaded)</summary>
        public static string CachePath => Path.Combine(TempPath, "cache");

        /// <summary>The path to the LibVLC deposit location</summary>
        public static string LibraryPath => Path.Combine(TempPath, "libs");

        /// <summary>A read only access to the current service instance running</summary>
        public static IService CurrentService => Services[ServiceSelected];

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
            // Create the log writer
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
                // Make sure these directories exist
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

            // Extract and deposit libVLC's library files
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

            // Grab all configured IService objects
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IService).IsAssignableFrom(p) && p.IsClass);

            foreach (var type in types)
            {
                var instance = (IService)Activator.CreateInstance(type);

                instance.ProgressUpdater = new Tuple<IProgress<int>, IProgress<int>>(new Progress<int>(percentage => ChannelLoadPercentageChanged?.Invoke(percentage)), new Progress<int>(percentage => GuideLoadPercentageChanged?.Invoke(percentage)));

                LogDebug($"[TVCore] Startup: Installing [{instance.Title}] Service");

                Services.Add(instance);
            }
            
            // Load the settings from the disk, if they exist
            Settings.Load();

            // Load the user's favorite channels, if they exist
            FavoritesLoad();

            // Load the image blacklist, if they exist
            BlacklistLoad();

            LogMessage("[TVCore] Startup: Finished Fox IPTV TVCore Startup");
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

            // Ask the service to give us the channel and guide data
            var (channels, guide) = await CurrentService.Process();

            Channels = channels;
            Guide = guide;

            // Build the zero index map to the actual channel numbers
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

        /// <summary>Load the favorite data from the user storage location</summary>
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

        /// <summary>Saves the user favorite channels to the user storage location</summary>
        private static void FavoritesSave()
        {
            var rawJson = JsonConvert.SerializeObject(ChannelFavorites);

            var favoriteChannelsFilePath = Path.Combine(UserStoragePath, ChannelFavoritesFilename);

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
            lock (_logWriterLock)
            {
                _logWriter.WriteLine(string.Empty);
                _logWriter.WriteLine(string.Empty);
                _logWriter.Flush();
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

            lock (_logWriterLock)
            {
                var logLine = $"[{DateTime.UtcNow:O}]-[{logLevel.ToString().ToUpper().PadLeft(7)}]: {message}";

                _logWriter.WriteLine(logLine);
                _logWriter.Flush();

                _logBuffer.Enqueue(logLine);
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
