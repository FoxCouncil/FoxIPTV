// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using Newtonsoft.Json;
    using System.IO;
    using System.Threading;

    /// <summary>The class that contains FoxIPTV's settings and defaults</summary>
    public class Settings
    {
        /// <summary>Is the main window currently displaying fullscreen</summary>
        public bool Fullscreen { get; set; } = false;

        /// <summary>The channel we're currently on, zero indexed</summary>
        public uint Channel { get; set; } = 0;

        /// <summary>Is Closed Captioning enabled</summary>
        public bool CCEnabled { get; set; } = false;

        /// <summary>The left edge of the main window in screen pixels, null until it has been placed once</summary>
        public int? WindowLeft { get; set; }

        /// <summary>The top edge of the main window in screen pixels, null until it has been placed once</summary>
        public int? WindowTop { get; set; }

        /// <summary>The width of the main window's inside, 0 for the default</summary>
        public double WindowWidth { get; set; }

        /// <summary>The height of the main window's inside, status bar included, 0 for the default</summary>
        public double WindowHeight { get; set; }

        /// <summary>The window state to return to when coming out of fullscreen mode</summary>
        public string WindowOldState { get; set; }

        /// <summary>Is the main window displaying a border</summary>
        public bool Borders { get; set; } = true;

        /// <summary>Is the main window always on top of other windows</summary>
        public bool AlwaysOnTop { get; set; } = false;

        /// <summary>Is the channel editor currently open (visible)</summary>
        public bool ChannelEditorOpen { get; set; } = false;

        /// <summary>Is the guide currently open (visible)</summary>
        public bool GuideOpen { get; set; } = false;

        /// <summary>Is the main window open (visible)</summary>
        public bool Visibility { get; set; } = true;

        /// <summary>The current level of opacity for the main window</summary>
        public double Opacity { get; set; } = 1;

        /// <summary>The current aspect ratio for the video</summary>
        public string AspectRatio { get; set; } = string.Empty;

        /// <summary>The current LibVLC audio mode</summary>
        public int StereoMode { get; set; } = 1;

        /// <summary>Is the main window status bar visible</summary>
        public bool StatusBar { get; set; } = true;

        /// <summary>The id of the provider used last time, preselected in the provider picker</summary>
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>Is the library currently open (visible)</summary>
        public bool LibraryOpen { get; set; } = false;

        /// <summary>The synchronizing object for thread safe access to save and load functions</summary>
        private readonly ReaderWriterLockSlim _fileLock = new ReaderWriterLockSlim();
        
        /// <summary>The filepath to the user settings</summary>
        private readonly string _filePath = Path.Combine(TvCore.UserStoragePath, "settings.json");

        /// <summary>The previous state of loaded settings, used to compare differences</summary>
        private Settings _loadedSettingsData;

        /// <summary>Save current settings if there is any differences</summary>
        public void Save()
        {
            // Null check, or clean state
            if (_loadedSettingsData != null)
            {
                var differences = _loadedSettingsData.Difference(this);

                // Don't waste the time to save if nothing has changed
                if (differences.Count == 0)
                {
                    return;
                }
#if DEBUG
                // Only log the differences in debug mode only
                foreach (var diff in differences)
                {
                    TvCore.LogDebug($"[Settings] {diff}");
                }
#endif
            }

            // Lock the file for writing
            _fileLock.EnterWriteLock();

            try
            {
                // Convert to JSON, because
                var settingsData = JsonConvert.SerializeObject(this);

                File.WriteAllText(_filePath, settingsData);

                // Save the new state, using JSON for cloning
                _loadedSettingsData = JsonConvert.DeserializeObject<Settings>(settingsData);
            }
            catch (Exception e)
            {
                TvCore.LogError($"[Settings] ERROR Writing settings file {_filePath}, {e.Message}");
            }
            finally
            {
                // ALWAYS exit the write lock!
                _fileLock.ExitWriteLock();
            }
        }

        /// <summary>Load the saved settings from the disk</summary>
        public void Load()
        {
            TvCore.LogDebug($"[Settings] Reading settings file {_filePath}");

            // Lock the file for reading
            _fileLock.EnterReadLock();

            try
            {
                if (!File.Exists(_filePath))
                {
                    // There is nothing to load
                    return;
                }

       
                var fileContents = File.ReadAllText(_filePath);

                _loadedSettingsData = JsonConvert.DeserializeObject<Settings>(fileContents);

                var settingsType = _loadedSettingsData.GetType();
                
                // Copy the values from the newly loaded state to this instance
                foreach (var setting in settingsType.GetProperties())
                {
                    var currentProperty = GetType().GetProperty(setting.Name);
                    currentProperty?.SetValue(this, setting.GetValue(_loadedSettingsData));
                }
            }
            catch (Exception e)
            {
                TvCore.LogError($"[Settings] ERROR Reading settings file {_filePath}, {e.Message}");
            }
            finally
            {
                // ALWAYS release the read lock!
                _fileLock.ExitReadLock();
            }
        }
    }
}
