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

    public static class Web
    {
        public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

        private static readonly byte[] GzipMagic = { 0x1f, 0x8b };

        private static readonly HttpClient Client = CreateClient();

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

        public static async Task<string> PostString(string url, string body, string contentType, IDictionary<string, string> headers = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
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

                request.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, contentType ?? "application/json");

                using (var response = await Client.SendAsync(request).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    return Decode(await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
                }
            }
        }

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

        public static async Task<string> GetString(string url, IDictionary<string, string> headers = null)
        {
            var bytes = await GetBytes(url, headers).ConfigureAwait(false);

            return Decode(bytes);
        }

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
