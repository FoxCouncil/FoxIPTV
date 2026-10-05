// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using FoxIPTV.Playback;

    public class Scte35Tests
    {
        private static byte[] AdStart(int type, double seconds, int number, int expected)
        {
            var ticks = (long)(seconds * Scte35.Hz);
            var descriptor = new List<byte> { 0x02, 0 };

            descriptor.AddRange(Encoding.ASCII.GetBytes("CUEI"));
            descriptor.AddRange(new byte[] { 0, 0, 0, 7, 0x7F, 0xFF });
            descriptor.AddRange(new[] { (byte)(ticks >> 32), (byte)(ticks >> 24), (byte)(ticks >> 16), (byte)(ticks >> 8), (byte)ticks });
            descriptor.AddRange(new byte[] { 0x00, 0x00, (byte)type, (byte)number, (byte)expected });
            descriptor[1] = (byte)(descriptor.Count - 2);

            var section = new List<byte> { 0xFC, 0, 0, 0x00, 0x00, 0, 0, 0, 0, 0x00, 0xFF, 0xF0, 0x05, 0x06, 0xFE, 0x00, 0x00, 0x00, 0x10 };

            section.Add(0);
            section.Add((byte)descriptor.Count);
            section.AddRange(descriptor);

            var length = section.Count - 3 + 4;

            section[1] = (byte)(0x30 | (length >> 8));
            section[2] = (byte)length;

            var crc = Scte35.Crc32(section.ToArray());

            section.AddRange(new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });

            return section.ToArray();
        }

        [Fact]
        public void SpliceInsert_ReadsTheBreakLength()
        {
            var signal = Scte35.Parse(Convert.FromBase64String("/DAvAAAAAAAA///wFAVIAACPf+/+c2nALv4AUsz1AAAAAAAKAAhDVUVJAAABNWLbowo="));

            Assert.Equal(0x05, signal.Command);
            Assert.True(signal.OutOfNetwork);
            Assert.Equal(0x07369C02EL, signal.Time);
            Assert.Equal(60.293567, signal.BreakSeconds, 5);
        }

        [Fact]
        public void TimeSignal_ReadsThePlacementOpportunity()
        {
            var signal = Scte35.Parse(Convert.FromBase64String("/DA0AAAAAAAA///wBQb+cr0AUAAeAhxDVUVJSAAAjn/PAAGlmbAICAAAAAAsoKGKNAIAmsnRfg=="));
            var segment = Assert.Single(signal.Segments);

            Assert.Equal(0x06, signal.Command);
            Assert.Equal(0x072BD0050L, signal.Time);
            Assert.Equal(0x34, segment.Type);
            Assert.True(segment.IsBreakStart);
            Assert.Equal(307.0, segment.Seconds, 3);
            Assert.Equal(2, segment.Number);
        }

        [Fact]
        public void AdStart_ReadsTheNumberTotalAndLength()
        {
            var segment = Assert.Single(Scte35.Parse(AdStart(0x30, 15, 2, 4)).Segments);

            Assert.True(segment.IsAdStart);
            Assert.Equal(15.0, segment.Seconds, 3);
            Assert.Equal(2, segment.Number);
            Assert.Equal(4, segment.Expected);
        }

        [Fact]
        public void DamagedSection_IsRefused()
        {
            var data = AdStart(0x30, 15, 2, 4);

            data[25] ^= 0x10;

            Assert.Null(Scte35.Parse(data));
        }

        [Fact]
        public void Id3_ListsItsFrames()
        {
            var text = Encoding.UTF8.GetBytes("\u0003adId\u0000abc123");
            var frame = new List<byte>(Encoding.ASCII.GetBytes("TXXX")) { 0, 0, 0, (byte)text.Length, 0, 0 };

            frame.AddRange(text);

            var tag = new List<byte> { (byte)'I', (byte)'D', (byte)'3', 4, 0, 0, 0, 0, 0, (byte)frame.Count };

            tag.AddRange(frame);

            Assert.Equal("TXXX adId|abc123", Scte35.DescribeId3(tag.ToArray()));
        }
    }
}
