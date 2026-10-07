// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public static class MediaFolder
    {
        private static readonly HashSet<string> PictureTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

        private static readonly HashSet<string> VideoTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".m4v", ".mkv", ".mov", ".webm", ".avi", ".wmv", ".ts" };

        public static bool IsPicture(string path) => PictureTypes.Contains(Path.GetExtension(path));

        public static bool IsVideo(string path) => VideoTypes.Contains(Path.GetExtension(path));

        public static List<string> Files(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return new List<string>();
            }

            try
            {
                var files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Where(x => IsPicture(x) || IsVideo(x)).ToList();

                TvCore.LogInfo($"[Ads] Media folder {folder}: {files.Count(IsPicture)} pictures, {files.Count(IsVideo)} videos");

                return files;
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Ads] Reading the media folder failed: {ex.Message}");

                return new List<string>();
            }
        }
    }
}
