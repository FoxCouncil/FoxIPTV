// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using FoxIPTV.Views;

    public class WindowIconArtTests
    {
        [Theory]
        [InlineData(16)]
        [InlineData(20)]
        [InlineData(24)]
        [InlineData(32)]
        [InlineData(40)]
        [InlineData(48)]
        [InlineData(64)]
        public void Whiten_KeepsTheShapeAndPaintsItWhite(int size)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var file = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Media", "FoxIPTV.ico"));
            var plain = WindowIconArt.Pixels(WindowIconArt.LoadIcon(file, size), size);
            var white = WindowIconArt.Pixels(WindowIconArt.Whiten(WindowIconArt.LoadIcon(file, size), size), size);

            Assert.NotNull(plain);
            Assert.NotNull(white);

            var drawn = Enumerable.Range(0, size * size).Where(i => plain[i * 4 + 3] != 0).ToList();

            Assert.True(drawn.Count > size, $"only {drawn.Count} drawn pixels at {size}px");
            Assert.Contains(drawn, i => plain[i * 4] < 64 && plain[i * 4 + 1] < 64 && plain[i * 4 + 2] < 64);
            Assert.All(Enumerable.Range(0, size * size), i => Assert.Equal(plain[i * 4 + 3], white[i * 4 + 3]));
            Assert.All(drawn, i => Assert.True(white[i * 4] == 255 && white[i * 4 + 1] == 255 && white[i * 4 + 2] == 255, $"pixel {i} is not white"));
        }
    }
}
