// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>The single place FoxIPTV talks HTTP; a real browser identity, transparent gzip and an on-disk cache</summary>
    public static class Web
    {
        /// <summary>The user agent string sent with every request, a current desktop Chrome on Windows</summary>
        public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

        /// <summary>The magic bytes at the start of a gzip stream</summary>
        private static readonly byte[] GzipMagic = { 0x1f, 0x8b };

        /// <summary>The shared client; sockets are pooled per host by the handler</summary>
        private static readonly HttpClient Client = CreateClient();

        /// <summary>Build the shared <see cref="HttpClient"/></summary>
        private static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90) };

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

            return client;
        }

        /// <summary>Download a URL to bytes, sending any extra headers</summary>
        /// <param name="url">The absolute URL</param>
        /// <param name="headers">Optional extra headers; a User-Agent here overrides the default</param>
        /// <returns>The raw response body, already inflated if the server compressed it</returns>
        public static async Task<byte[]> GetBytes(string url, IDictionary<string, string> headers = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        if (string.Equals(header.Key, "User-Agent", StringComparison.OrdinalIgnoreCase))
                        {
                            request.Headers.Remove("User-Agent");
                        }

                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>Download a URL to a string; gzip payloads (.gz files) are inflated regardless of headers</summary>
        /// <param name="url">The absolute URL</param>
        /// <param name="headers">Optional extra headers</param>
        /// <returns>The response body decoded as UTF-8</returns>
        public static async Task<string> GetString(string url, IDictionary<string, string> headers = null)
        {
            var bytes = await GetBytes(url, headers).ConfigureAwait(false);

            return Decode(bytes);
        }

        /// <summary>Download a URL to a string, reusing a cached copy on disk if it is fresh enough</summary>
        /// <param name="url">The absolute URL</param>
        /// <param name="cacheKey">A filename safe key for the cache entry</param>
        /// <param name="cacheHours">How many hours a cached copy stays fresh; zero or less bypasses the cache</param>
        /// <param name="headers">Optional extra headers</param>
        /// <returns>The response body decoded as UTF-8</returns>
        public static async Task<string> GetStringCached(string url, string cacheKey, double cacheHours, IDictionary<string, string> headers = null)
        {
            if (cacheHours <= 0 || string.IsNullOrWhiteSpace(cacheKey))
            {
                return await GetString(url, headers).ConfigureAwait(false);
            }

            var cachePath = Path.Combine(TvCore.CachePath, SafeFilename(cacheKey));

            if (File.Exists(cachePath) && DateTime.Now - File.GetLastWriteTime(cachePath) < TimeSpan.FromHours(cacheHours))
            {
                TvCore.LogDebug($"[Web] Cache HIT {cacheKey}");

                return File.ReadAllText(cachePath, Encoding.UTF8);
            }

            TvCore.LogDebug($"[Web] Cache MISS {cacheKey}");

            var contents = await GetString(url, headers).ConfigureAwait(false);

            try
            {
                File.WriteAllText(cachePath, contents, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[Web] Unable to write cache entry {cacheKey}: {ex.Message}");
            }

            return contents;
        }

        /// <summary>Turn a response body into text, inflating gzip if the magic bytes are present</summary>
        /// <param name="bytes">The raw bytes</param>
        /// <returns>The UTF-8 decoded text without a byte order mark</returns>
        public static string Decode(byte[] bytes)
        {
            if (bytes.Length > 2 && bytes[0] == GzipMagic[0] && bytes[1] == GzipMagic[1])
            {
                using (var input = new MemoryStream(bytes))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip, Encoding.UTF8, true))
                {
                    return reader.ReadToEnd();
                }
            }

            using (var input = new MemoryStream(bytes))
            using (var reader = new StreamReader(input, Encoding.UTF8, true))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>Reduce any string to something safe to use as a filename</summary>
        /// <param name="value">The string to sanitize</param>
        /// <returns>A filename safe string; long keys are hashed</returns>
        public static string SafeFilename(string value)
        {
            var sb = new StringBuilder(value.Length);

            foreach (var c in value)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '_');
            }

            var safe = sb.ToString();

            return safe.Length > 64 ? safe.Substring(0, 32) + value.ToMD5() : safe;
        }
    }
}
