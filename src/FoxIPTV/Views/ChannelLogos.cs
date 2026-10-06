// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Media.Imaging;
    using Avalonia.Platform;
    using Classes;

    public static class ChannelLogos
    {
        private static Bitmap _placeholder;

        public static Bitmap Placeholder
        {
            get
            {
                if (_placeholder == null)
                {
                    using (var stream = AssetLoader.Open(new Uri("avares://FoxIPTV/Assets/FoxIPTV.ico")))
                    {
                        _placeholder = new Bitmap(stream);
                    }
                }

                return _placeholder;
            }
        }

        public static async Task<Bitmap> Load(Channel channel)
        {
            if (channel?.Logo == null)
            {
                return null;
            }

            var url = channel.Logo.ToString();

            try
            {
                return await Task.Run(async () =>
                {
                    var bytes = await TvCore.DownloadImageAndCache(url);

                    if (bytes == null || bytes.Length == 0)
                    {
                        return null;
                    }

                    using (var stream = new System.IO.MemoryStream(bytes))
                    using (var decoded = new Bitmap(stream))
                    {
                        return Trim(decoded);
                    }
                });
            }
            catch (Exception e)
            {
                TvCore.LogDebug($"[.NET] Logo failed for {channel.Name}: {e.Message}");

                return null;
            }
        }

        private static Bitmap Trim(Bitmap source)
        {
            var size = source.PixelSize;
            var stride = size.Width * 4;
            var pixels = new byte[stride * size.Height];

            var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);

            try
            {
                source.CopyPixels(new PixelRect(size), pinned.AddrOfPinnedObject(), pixels.Length, stride);
            }
            finally
            {
                pinned.Free();
            }

            var format = source.Format ?? PixelFormat.Bgra8888;
            var alpha = source.AlphaFormat ?? AlphaFormat.Premul;

            byte cb = pixels[0], cg = pixels[1], cr = pixels[2], ca = pixels[3];
            var flatCorner = ca == 255 && Math.Abs(cr - cg) < 8 && Math.Abs(cg - cb) < 8;

            int left = size.Width, top = size.Height, right = -1, bottom = -1;

            for (var y = 0; y < size.Height; y++)
            {
                for (var x = 0; x < size.Width; x++)
                {
                    var i = y * stride + x * 4;
                    var a = pixels[i + 3];

                    if (a < 16 || (flatCorner && a == ca && pixels[i] == cb && pixels[i + 1] == cg && pixels[i + 2] == cr))
                    {
                        continue;
                    }

                    if (x < left)
                    {
                        left = x;
                    }

                    if (x > right)
                    {
                        right = x;
                    }

                    if (y < top)
                    {
                        top = y;
                    }

                    if (y > bottom)
                    {
                        bottom = y;
                    }
                }
            }

            if (right < left || bottom < top)
            {
                return Copy(pixels, size, new PixelRect(size), format, alpha);
            }

            const int pad = 2;

            var box = new PixelRect(Math.Max(0, left - pad), Math.Max(0, top - pad), 0, 0);

            box = box.WithWidth(Math.Min(size.Width, right + 1 + pad) - box.X).WithHeight(Math.Min(size.Height, bottom + 1 + pad) - box.Y);

            return Copy(pixels, size, box, format, alpha);
        }

        private static Bitmap Copy(byte[] pixels, PixelSize size, PixelRect box, PixelFormat format, AlphaFormat alpha)
        {
            var stride = size.Width * 4;
            var result = new WriteableBitmap(box.Size, new Vector(96, 96), format, alpha);

            using (var buffer = result.Lock())
            {
                for (var y = 0; y < box.Height; y++)
                {
                    Marshal.Copy(pixels, (box.Y + y) * stride + box.X * 4, buffer.Address + y * buffer.RowBytes, box.Width * 4);
                }
            }

            return result;
        }
    }
}
