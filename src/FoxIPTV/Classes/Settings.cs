// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using Newtonsoft.Json;

    /// <summary>The class that contains FoxIPTV's settings and defaults</summary>
    public class Settings
    {
        public bool Fullscreen { get; set; } = false;

        /// <summary>The channel we're currently on, zero indexed</summary>
        public uint Channel { get; set; } = 0;

        /// <summary>Is Closed Captioning enabled</summary>
        public bool CCEnabled { get; set; } = false;

        public string CaptionLanguage { get; set; }

        public int? WindowLeft { get; set; }

        public int? WindowTop { get; set; }

        public double WindowWidth { get; set; }

        public double WindowHeight { get; set; }

        public string WindowOldState { get; set; }

        public bool Borders { get; set; } = true;

        public bool AlwaysOnTop { get; set; } = false;

        public bool GuideOpen { get; set; } = false;

        public double Opacity { get; set; } = 1;

        /// <summary>The current aspect ratio for the video</summary>
        public string AspectRatio { get; set; } = string.Empty;

        public int StereoMode { get; set; } = 1;

        public bool StatusBar { get; set; } = true;

        public bool CheckForUpdates { get; set; } = true;

        public string Provider { get; set; }

        public double LiveDelay { get; set; }

        public bool AdMute { get; set; }

        public bool AdLabel { get; set; } = true;

        public bool AdDim { get; set; }

        public double AdDimLevel { get; set; } = 0.7;

        public string AdMediaFolder { get; set; }

        public bool AdMediaSound { get; set; }

        public bool AdTitle { get; set; } = true;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> AdMessages { get; set; } = new List<string>
        {
            "We'll be right back",
            "Let's all goto the lobby",
            "Stay tuned",
            "Don't touch that dial",
            "Please stand by",
            "Please do not adjust your set",
            "Back in a flash",
            "Hang tight",
            "Don't go anywhere",
            "Intermission",
            "Taking five",
            "BRB",
            "The show will resume shortly",
            "Coming up next: more TV",
            "Grab a snack",
            "Snack o'clock",
            "Popcorn refill time",
            "Refill your drink",
            "Go hydrate",
            "Bathroom break!",
            "Stretch those legs",
            "Time for a quick dance break",
            "Pet your pet",
            "Feed the cat",
            "Check on the kettle",
            "Go text your mom",
            "Meanwhile, in the lobby...",
            "Be kind, rewind",
            "Rewinding the VHS",
            "Adjusting the rabbit ears",
            "Warming up the tubes",
            "Recharging the cathode rays",
            "The fox is fetching the next tape",
            "Signal's on a coffee break",
            "Loading more show...",
            "Ad-free zone",
            "Nothing to see here",
            "Smile, you're skipping the ads",
            "This break brought to you by nobody",
            "Technical difficulties (not really)"
        };

        /// <summary>The synchronizing object for thread safe access to save and load functions</summary>
        private readonly ReaderWriterLockSlim _fileLock = new ReaderWriterLockSlim();

        /// <summary>The filepath to the user settings</summary>
        private readonly string _filePath = Path.Combine(TvCore.UserStoragePath, "settings.json");

        /// <summary>The previous state of loaded settings, used to compare differences</summary>
        private string _savedJson;

        /// <summary>Save current settings if there is any differences</summary>
        public void Save()
        {
            // Convert to JSON, because
            var settingsData = JsonConvert.SerializeObject(this);

            // Don't waste the time to save if nothing has changed
            if (settingsData == _savedJson)
            {
                return;
            }

            // Lock the file for writing
            _fileLock.EnterWriteLock();

            try
            {
                File.WriteAllText(_filePath, settingsData);

                // Save the new state, using JSON for cloning
                _savedJson = settingsData;
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

                // Copy the values from the newly loaded state to this instance
                JsonConvert.PopulateObject(File.ReadAllText(_filePath), this);

                _savedJson = JsonConvert.SerializeObject(this);
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
