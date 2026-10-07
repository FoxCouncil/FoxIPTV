// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using Avalonia.Media.Imaging;
    using Newtonsoft.Json;
    using Services;
    using Timer = System.Timers.Timer;

    /// <summary>The static god class for FoxIPTV's functionality and features</summary>
    public static class TvCore
    {
        /// <summary>The default filename for the applications logfile</summary>
        private const string LogFilename = "FoxIPTV.log";

        private const long LogRollBytes = 20L * 1024 * 1024;

        /// <summary>The default filename for the applications favorite channel data</summary>
        private const string ChannelFavoritesFilename = "fcdata";

        public const string MediaChannelId = "foxiptv-my-media";

        /// <summary>The default filename for the applications image blacklist data</summary>
        private const string ImageServerBlacklistFilename = "ibldata";

        private const string ProtectedChannelsFilename = "drmdata";

        private const string HiddenChannelsFilename = "hidden";

        public const string ChannelIdKey = "id:";

        private static readonly HashSet<string> _hiddenStreams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> _protectedStreams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> _unwritableLists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static int _protectedGapAt = -1;

        /// <summary>A non win forms timer at 100ms intervals</summary>
        private static readonly Timer _coreTimer = new Timer(100);

        /// <summary>The stream writer for writing to the logfile</summary>
        private static readonly StreamWriter _logWriter;

        private static readonly BlockingCollection<string> _logQueue = new BlockingCollection<string>();

        private static readonly Thread _logThread = new Thread(LogWriterLoop) { IsBackground = true, Name = "Log writer" };

        /// <summary>The in-memory image server black list, to avoid hitting servers that return non 200 responses</summary>
        private static readonly List<string> _imageServerBlacklist = new List<string>();

        /// <summary>The queue of image Uris to download</summary>
        private static Queue<Tuple<uint, string>> _imageCacheQueue;

        /// <summary>The TvCore's state change event, will include the new state being changed to</summary>
        public static event Action<TvCoreState> StateChanged;

        /// <summary>A event to inform of percentage progress on channels being loaded</summary>
        public static event Action<int> ChannelLoadPercentageChanged;

        /// <summary>A event to inform of percentage progress on channel data being loaded</summary>
        public static event Action<int> GuideLoadPercentageChanged;

        /// <summary>A event to inform of percentage progress on guide data being loaded</summary>
        public static event Action<uint> ChannelChanged;

        public static event Action ChannelListChanged;

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

        public static string Version { get; } = (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

        public static string LogPath { get; private set; }

        /// <summary>The path used to store user data</summary>
        public static string UserStoragePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "FoxIPTV");

        /// <summary>The path to the temporary folder used by this application</summary>
        public static string TempPath => Path.Combine(Path.GetTempPath(), "FoxIPTV");

        /// <summary>The path to store cached data, (data that can be re-downloaded)</summary>
        public static string CachePath => Path.Combine(TempPath, "cache");

        public static IService CurrentService => Services.Count == 0 ? null : Services[Math.Max(0, Math.Min(ServiceSelected, Services.Count - 1))];

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
            var logPath = LogFilePath();

            RollLog(logPath);

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
                LogError($"[Exception] Unhandled: {ex}");
            }

            TaskScheduler.UnobservedTaskException += (s, a) => LogException(a.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, a) => LogException(a.ExceptionObject as Exception);

            // Make sure these directories exist
            foreach (var dir in new[] { TempPath, CachePath, UserStoragePath })
            {
                Directory.CreateDirectory(dir);
            }

            LogDebug("[TVCore] Startup: Application directories created");

            foreach (var service in new IService[] { new PlutoTv(), new SamsungTvPlus(), new PlexTv(), new RokuTv(), new IPTVDotOrg(), new FreeTv(), new M3uPlaylist() })
            {
                InstallService(service);
            }

            // Load the settings from the disk, if they exist
            Settings.Load();

            // Load the image blacklist, if they exist
            BlacklistLoad();

            LogMessage("[TVCore] Startup: Finished Fox IPTV TVCore Startup");
        }

        private static string LogFilePath()
        {
            var besideExe = Path.Combine(ExePath, LogFilename);

            if (CanAppend(besideExe))
            {
                LogPath = besideExe;
            }
            else
            {
                Directory.CreateDirectory(UserStoragePath);

                LogPath = Path.Combine(UserStoragePath, LogFilename);
            }

            return LogPath;
        }

        private static bool CanAppend(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void RollLog(string path)
        {
            try
            {
                if (new FileInfo(path).Length > LogRollBytes)
                {
                    File.Move(path, Path.ChangeExtension(path, ".old.log"), true);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void InstallService(IService instance)
        {
            instance.ProgressUpdater = new Tuple<IProgress<int>, IProgress<int>>(new Progress<int>(percentage => ChannelLoadPercentageChanged?.Invoke(percentage)), new Progress<int>(percentage => GuideLoadPercentageChanged?.Invoke(percentage)));

            LogDebug($"[TVCore] Startup: Installing [{instance.Title}] Service");

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

            FavoritesLoad();

            ProtectedLoad();

            TvCore.LogDebug($"[{CurrentService.Title}] Process(): Starting data processing...");

            // Ask the service to give us the channel and guide data
            var result = await CurrentService.Process();

            Channels = result?.Item1 ?? new List<Channel>();
            Guide = result?.Item2 ?? new List<Programme>();

            LogInfo($"[TVCore] Start(): {Channels.Count} channel(s), {Guide.Count} programme(s) from {CurrentService.Title}");

            // Build the zero index map to the actual channel numbers
            var uniqueIds = UniqueIds(Channels);
            var protectedCount = Channels.RemoveAll(x => IsListed(_protectedStreams, x, uniqueIds));
            var hiddenCount = Channels.RemoveAll(x => IsListed(_hiddenStreams, x, uniqueIds));

            if (protectedCount + hiddenCount > 0)
            {
                LogInfo($"[TVCore] Start(): {protectedCount} copy-protected and {hiddenCount} hidden channel(s) left out");
            }

            Channels.Insert(0, MediaChannel());

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

            if (CurrentChannel != null && CurrentChannelIndex == channelIndex && Channels.Contains(CurrentChannel))
            {
                return;
            }

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
                File.WriteAllBytes(cachePath, contents);
            }

            return contents;
        }

        private static string FavoritesFilePath => Path.Combine(UserStoragePath, CurrentService == null ? ChannelFavoritesFilename : $"{ChannelFavoritesFilename}-{CurrentService.Id}");

        private static string BlacklistFilePath => Path.Combine(UserStoragePath, ImageServerBlacklistFilename);

        private static string ProtectedFilePath => Path.Combine(UserStoragePath, $"{ProtectedChannelsFilename}-{CurrentService?.Id ?? "none"}");

        private static string HiddenFilePath => Path.Combine(UserStoragePath, $"{HiddenChannelsFilename}-{CurrentService?.Id ?? "none"}");

        /// <summary>Load the favorite data from the user storage location</summary>
        private static void FavoritesLoad()
        {
            ChannelFavorites.Clear();
            ChannelFavorites.AddRange(ReadList(File.Exists(FavoritesFilePath) ? FavoritesFilePath : Path.Combine(UserStoragePath, ChannelFavoritesFilename)));
        }

        /// <summary>Load the image blacklist data from the user storage location</summary>
        private static void BlacklistLoad()
        {
            _imageServerBlacklist.Clear();
            _imageServerBlacklist.AddRange(ReadList(BlacklistFilePath));
        }

        private static void ProtectedLoad()
        {
            _protectedStreams.Clear();
            _protectedStreams.UnionWith(ReadList(ProtectedFilePath));

            _hiddenStreams.Clear();
            _hiddenStreams.UnionWith(ReadList(HiddenFilePath));
        }

        private static List<string> ReadList(string path)
        {
            if (!File.Exists(path))
            {
                return new List<string>();
            }

            LogInfo($"[TVCore] Reading {path}");

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path)) ?? new List<string>();
            }
            catch (Exception e)
            {
                LogError($"[TVCore] Unable to read {path}: {e.Message}");

                SetAside(path);

                return new List<string>();
            }
        }

        private static void SetAside(string path)
        {
            var aside = $"{path}.unreadable-{DateTime.Now:yyyyMMddHHmmss}";

            try
            {
                File.Move(path, aside);

                LogError($"[TVCore] Kept the unreadable file as {aside}");
            }
            catch (Exception e)
            {
                lock (_unwritableLists)
                {
                    _unwritableLists.Add(path);
                }

                LogError($"[TVCore] {path} will not be saved over this session: {e.Message}");
            }
        }

        private static void WriteList(string path, IEnumerable<string> items)
        {
            lock (_unwritableLists)
            {
                if (_unwritableLists.Contains(path))
                {
                    return;
                }
            }

            LogDebug($"[TVCore] Saving {path}");

            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(items));
            }
            catch (Exception e)
            {
                LogError($"[TVCore] Unable to write {path}: {e.Message}");
            }
        }

        public static HashSet<string> UniqueIds(IEnumerable<Channel> channels)
        {
            return new HashSet<string>(channels.Where(x => !string.IsNullOrEmpty(x.Id)).GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() == 1).Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsListed(HashSet<string> list, Channel channel, HashSet<string> uniqueIds)
        {
            return channel.Stream != null && list.Contains(channel.Stream.ToString()) || !string.IsNullOrEmpty(channel.Id) && uniqueIds.Contains(channel.Id) && list.Contains(ChannelIdKey + channel.Id);
        }

        public static bool IsMediaChannel(Channel channel) => channel?.Id == MediaChannelId;

        private static Channel MediaChannel() => new Channel { Index = 0, Id = MediaChannelId, Name = "My Media" };

        public static void MarkProtected(Channel channel)
        {
            if (channel?.Stream == null || Channels == null)
            {
                return;
            }

            var added = _protectedStreams.Add(channel.Stream.ToString());

            if (!string.IsNullOrEmpty(channel.Id) && _protectedStreams.Add(ChannelIdKey + channel.Id))
            {
                added = true;
            }

            if (!added)
            {
                return;
            }

            LogInfo($"[TVCore] MarkProtected({channel.Index} {channel.Name}): copy-protected, hidden from now on");

            WriteList(ProtectedFilePath, _protectedStreams);

            var position = Channels.IndexOf(channel);

            if (position < 0)
            {
                return;
            }

            Channels.RemoveAt(position);
            ChannelIndexList = Channels.Select(x => x.Index).ToList();

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
            WriteList(FavoritesFilePath, ChannelFavorites);
        }

        /// <summary>Saves the image blacklist data to the user storage location</summary>
        private static void BlacklistSave()
        {
            WriteList(BlacklistFilePath, _imageServerBlacklist);
        }

        /// <summary>Start the logging system, and insert two blank lines</summary>
        private static void LogStart()
        {
            _logQueue.Add(string.Empty);
            _logQueue.Add(string.Empty);

            _logThread.Start();

            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                _logQueue.CompleteAdding();
                _logThread.Join(TimeSpan.FromSeconds(2));
            };
        }

        private static void LogWriterLoop()
        {
            try
            {
                foreach (var line in _logQueue.GetConsumingEnumerable())
                {
                    _logWriter.WriteLine(line);

                    while (_logQueue.TryTake(out var more))
                    {
                        _logWriter.WriteLine(more);
                    }

                    _logWriter.Flush();
                }
            }
            catch (Exception)
            {
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

            try
            {
                _logQueue.Add(logLine);
            }
            catch (InvalidOperationException)
            {
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

                    Bitmap logo;

                    try
                    {
                        logo = new Bitmap(new MemoryStream(imageData));
                    }
                    catch (Exception decodeFailure)
                    {
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
