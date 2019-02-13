// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System.Drawing;
    using System.IO;
    using System.Threading;
    using Newtonsoft.Json;

    public class Settings
    {
        public bool Fullscreen { get; set; } = false;

        public uint Channel { get; set; } = 0;

        public bool CCEnabled { get; set; } = false;

        public Point TvFormLocation { get; set; } = new Point(0, 0);

        public Size TvFormSize { get; set; } = new Size(0, 0);

        public string TvFormState { get; set; }

        public Point TvFormOldLocation { get; set; } = new Point(0,0);

        public Size TvFormOldSize { get; set; } = new Size(0, 0);

        public string TvFormOldState { get; set; }

        public bool Borders { get; set; } = true;

        public bool AlwaysOnTop { get; set; } = false;

        public bool ChannelEditorOpen { get; set; } = false;

        public bool GuideOpen { get; set; } = false;

        public double Opacity { get; set; } = 1;

        public bool Visibility { get; set; } = true;

        public int StereoMode { get; set; } = 0;

        public bool StatusBar { get; set; } = true;

        private readonly ReaderWriterLockSlim _fileLock = new ReaderWriterLockSlim();

        private readonly string _filePath = Path.Combine(TvCore.UserStoragePath, "udata");

        public void Save()
        {
            TvCore.LogDebug($"[Settings] Writing settings file {_filePath}");

            _fileLock.EnterWriteLock();

            try
            {
                var settingsData = JsonConvert.SerializeObject(this);

                File.WriteAllText(_filePath, settingsData);
            }
            finally
            {
                _fileLock.ExitWriteLock();
            }
        }

        public void Load()
        {
            TvCore.LogDebug($"[Settings] Reading settings file {_filePath}");

            _fileLock.EnterReadLock();

            try
            {
                if (!File.Exists(_filePath))
                {
                    return;
                }

                var fileContents = File.ReadAllText(_filePath);

                var settingsData = JsonConvert.DeserializeObject<Settings>(fileContents);

                MapProperties(settingsData);
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }

        private void MapProperties(Settings newSettingsData)
        {
            var settingsType = newSettingsData.GetType();

            foreach (var setting in settingsType.GetProperties())
            {
                var currentProperty = GetType().GetProperty(setting.Name);
                currentProperty?.SetValue(this, setting.GetValue(newSettingsData));
            }
        }
    }
}
