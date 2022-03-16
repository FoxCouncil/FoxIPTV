// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library.Utils
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class ApiException : Exception
    {
        public int StatusCode { get; set; }

        public string Content { get; set; }
    }

    public static class ApiClient
    {
        private static bool _isInitialized;

        private static string _cacheDirectory;

        private static string _hashSalt;

        private static Dictionary<string, Tuple<TimeSpan, DateTime, object>> _cache = new Dictionary<string, Tuple<TimeSpan, DateTime, object>>();

        public static void Initialize(string tempDirectory)
        {
            if (_isInitialized)
            {
                throw new Exception("ApiClient is already initialized.");
            }

            _isInitialized = true;

            if (!Directory.Exists(tempDirectory))
            {
                throw new Exception("A core directory needed doesn't exist.");
            }

            _cacheDirectory = Path.Combine(tempDirectory, "cache");

            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }

        public static async Task<T> DeserializeAndMemoryCacheAync<T>(Uri url, TimeSpan cacheTime)
        {
            var urlHash = HashString(url.ToString());

            if (_cache.ContainsKey(urlHash) && DateTime.UtcNow - _cache[urlHash].Item2 < _cache[urlHash].Item1)
            {
                return (T)Convert.ChangeType(_cache[urlHash].Item3, typeof(T));
            }

            var data = await GetAndDeserializeAync<T>(url);

            if (_cache.ContainsKey(urlHash))
            {
                _cache.Remove(urlHash);
            }

            _cache.Add(urlHash, new Tuple<TimeSpan, DateTime, object>(cacheTime, DateTime.UtcNow, data));

            return data;
        }

        public static async Task<string> GetStringAndFileCacheAsync(Uri url, TimeSpan cacheTime)
        {
            var urlHash = HashString(url.ToString());

            var files = Directory.GetFiles(_cacheDirectory);

            var cacheFilePath = files.FirstOrDefault(x => Path.GetFileName(x).StartsWith(urlHash));

            if (cacheFilePath != null)
            {
                var cacheFile = Path.GetFileName(cacheFilePath);

                var cacheEntry = cacheFile.Split('-');

                if (double.TryParse(cacheEntry[1], out var fileCacheTimeDouble))
                {
                    var fileCacheTime = TimeSpan.FromSeconds(fileCacheTimeDouble);

                    var fileDatetime = File.GetLastWriteTimeUtc(cacheFilePath);

                    var fileCacheTimeDiff = DateTime.UtcNow - fileDatetime;

                    if (fileCacheTimeDiff > fileCacheTime)
                    {
                        File.Delete(cacheFilePath);
                    }
                    else
                    {
                        return File.ReadAllText(cacheFilePath);
                    }
                }
            }

            var data = await GetAndDeserializeAync<string>(url);

            var filename = Path.Combine(_cacheDirectory, $"{urlHash}-{cacheTime.TotalSeconds}");

            File.WriteAllText(filename, data);

            return data;
        }

        public static async Task<T> GetAndDeserializeAync<T>(Uri url)
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                return await ParseHttpResponse<T>(response);
            }
        }

        public static async Task<T> CancellableGetAndDeserializeAync<T>(Uri url, CancellationToken cancellationToken)
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                return await ParseHttpResponse<T>(response);
            }
        }

        private static async Task<T> ParseHttpResponse<T>(HttpResponseMessage response)
        {
            var stream = await response.Content.ReadAsStreamAsync();

            if (typeof(T) == typeof(string))
            {
                var value = await StreamToStringAsync(stream);

                return (T)Convert.ChangeType(value, typeof(T));
            }

            if (response.IsSuccessStatusCode)
            {
                return DeserializeJsonFromStream<T>(stream);
            }

            // Error Handling
            var content = await StreamToStringAsync(stream);

            throw new ApiException
            {
                StatusCode = (int)response.StatusCode,
                Content = content
            };
        }

        private static T DeserializeJsonFromStream<T>(Stream stream)
        {
            if (stream == null || stream.CanRead == false)
            {
                return default;
            }

            using (var sr = new StreamReader(stream))
            using (var jtr = new JsonTextReader(sr))
            {
                var js = new JsonSerializer();
                var searchResult = js.Deserialize<T>(jtr);

                return searchResult;
            }
        }

        private static async Task<string> StreamToStringAsync(Stream stream)
        {
            string content = null;

            if (stream != null)
            {
                using (var sr = new StreamReader(stream))
                {
                    content = await sr.ReadToEndAsync();
                }
            }

            return content;
        }

        private static string HashString(string inputString)
        {
            if (string.IsNullOrWhiteSpace(_hashSalt))
            {
                _hashSalt = Core.SettingGet<string>("e_s_guid");
            }

            var stringBuilder = new StringBuilder();

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(inputString + _hashSalt));

                foreach (var @byte in hash)
                {
                    stringBuilder.Append(@byte.ToString("X2"));
                }
            }

            return stringBuilder.ToString();
        }
    }
}
