// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Classes;

    public static class MediaHttp
    {
        public static readonly HttpClient Client = Create();

        private static HttpClient Create()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30)
            };

            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        }

        public static HttpRequestMessage Request(Uri uri, IReadOnlyDictionary<string, string> headers, long? offset = null, long? length = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var hasAgent = false;

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    var name = string.Equals(header.Key, "Referrer", StringComparison.OrdinalIgnoreCase) ? "Referer" : header.Key;

                    if (string.Equals(name, "User-Agent", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAgent = true;
                    }

                    request.Headers.TryAddWithoutValidation(name, header.Value);
                }
            }

            if (!hasAgent)
            {
                request.Headers.TryAddWithoutValidation("User-Agent", Web.UserAgent);
            }

            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

            if (offset.HasValue)
            {
                request.Headers.Range = new RangeHeaderValue(offset.Value, length.HasValue ? offset.Value + length.Value - 1 : (long?)null);
            }

            return request;
        }

        public static async Task<HttpResponseMessage> Send(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken token, long? offset = null, long? length = null)
        {
            using (var request = Request(uri, headers, offset, length))
            {
                var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var status = response.StatusCode;
                    var reason = response.ReasonPhrase;

                    response.Dispose();

                    throw new HttpRequestException($"HTTP {(int)status} {reason}", null, status);
                }

                return response;
            }
        }

        public static async Task<(byte[] Data, Uri FinalUri)> GetBytes(Uri uri, IReadOnlyDictionary<string, string> headers, TimeSpan timeout, CancellationToken token, long? offset = null, long? length = null)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(timeout);

                try
                {
                    using (var response = await Send(uri, headers, cts.Token, offset, length).ConfigureAwait(false))
                    {
                        var data = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);

                        return (data, response.RequestMessage?.RequestUri ?? uri);
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    throw new TimeoutException($"No answer within {timeout.TotalSeconds:0}s");
                }
            }
        }
    }
}
