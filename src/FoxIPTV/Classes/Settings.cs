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
        public bool Fullscreen { get; set; } = false;

        /// <summary>The channel we're currently on, zero indexed</summary>
        public uint Channel { get; set; } = 0;

        /// <summary>Is Closed Captioning enabled</summary>
        public bool CCEnabled { get; set; } = false;

        public int? WindowLeft { get; set; }

        public int? WindowTop { get; set; }

        public double WindowWidth { get; set; }

        public double WindowHeight { get; set; }

        public string WindowOldState { get; set; }

        public bool Borders { get; set; } = true;

        public bool AlwaysOnTop { get; set; } = false;

        public bool ChannelEditorOpen { get; set; } = false;

        public bool GuideOpen { get; set; } = false;

        public bool Visibility { get; set; } = true;

        public double Opacity { get; set; } = 1;

        /// <summary>The current aspect ratio for the video</summary>
        public string AspectRatio { get; set; } = string.Empty;

        /// <summary>The audio channel mode, see Playback.StereoMode</summary>
        public int StereoMode { get; set; } = 1;

        public bool StatusBar { get; set; } = true;

        public string ProviderId { get; set; } = string.Empty;

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
