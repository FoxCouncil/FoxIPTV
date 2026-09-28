// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests.Engine
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    public sealed class MediaServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();

        private readonly ConcurrentDictionary<string, Func<Reply>> _routes = new ConcurrentDictionary<string, Func<Reply>>(StringComparer.Ordinal);

        private readonly List<ServedRequest> _requests = new List<ServedRequest>();

        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public MediaServer()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);

            probe.Start();

            var port = ((IPEndPoint)probe.LocalEndpoint).Port;

            probe.Stop();

            Root = new Uri($"http://127.0.0.1:{port}/");

            _listener.Prefixes.Add(Root.ToString());
            _listener.Start();

            _ = Task.Run(Listen);
        }

        public Uri Root { get; }

        public double Now => _clock.Elapsed.TotalSeconds;

        public IReadOnlyList<ServedRequest> Requests
        {
            get
            {
                lock (_requests)
                {
                    return _requests.ToList();
                }
            }
        }

        public Uri Url(string path) => new Uri(Root, path.TrimStart('/'));

        public void Serve(string path, Func<Reply> reply) => _routes[path] = reply;

        public void Serve(string path, byte[] data) => Serve(path, () => new Reply { Data = data });

        public void Serve(string path, string text) => Serve(path, Encoding.UTF8.GetBytes(text));

        private async Task Listen()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => Answer(context));
            }
        }

        private async Task Answer(HttpListenerContext context)
        {
            var served = new ServedRequest { Path = context.Request.Url.AbsolutePath, UserAgent = context.Request.UserAgent, Started = Now };

            lock (_requests)
            {
                _requests.Add(served);
            }

            try
            {
                var reply = _routes.TryGetValue(served.Path, out var route) ? route() : Reply.NotFound();

                if (reply.Delay > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(reply.Delay)).ConfigureAwait(false);
                }

                context.Response.StatusCode = reply.Status;
                context.Response.SendChunked = reply.Chunked;

                if (!reply.Chunked)
                {
                    context.Response.ContentLength64 = reply.Data.Length;
                }

                await context.Response.OutputStream.WriteAsync(reply.Data).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
            finally
            {
                served.Finished = Now;

                try
                {
                    context.Response.Close();
                }
                catch (Exception)
                {
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception)
            {
            }
        }
    }

    public sealed class Reply
    {
        public int Status { get; set; } = 200;

        public byte[] Data { get; set; } = Array.Empty<byte>();

        public double Delay { get; set; }

        public bool Chunked { get; set; }

        public static Reply NotFound() => new Reply { Status = 404 };
    }

    public sealed class ServedRequest
    {
        public string Path { get; set; }

        public string UserAgent { get; set; }

        public double Started { get; set; }

        public double Finished { get; set; } = double.NaN;
    }
}
