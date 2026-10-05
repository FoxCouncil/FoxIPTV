// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public sealed class Segmentation
    {
        public uint EventId { get; set; }

        public int Type { get; set; }

        public double Seconds { get; set; }

        public int Number { get; set; }

        public int Expected { get; set; }

        public bool IsAdStart => Type == 0x30 || Type == 0x32;

        public bool IsAdEnd => Type == 0x31 || Type == 0x33;

        public bool IsBreakStart => Type == 0x22 || Type == 0x34 || Type == 0x36;

        public bool IsBreakEnd => Type == 0x23 || Type == 0x35 || Type == 0x37;

        public override string ToString() => $"type 0x{Type:X2} event {EventId}{(Seconds > 0 ? $" {Seconds:0.###}s" : string.Empty)}{(Number > 0 || Expected > 0 ? $" {Number} of {Expected}" : string.Empty)}";
    }

    public sealed class SpliceSignal
    {
        public int Command { get; set; }

        public long? Time { get; set; }

        public bool OutOfNetwork { get; set; }

        public bool InToNetwork { get; set; }

        public double BreakSeconds { get; set; }

        public List<Segmentation> Segments { get; } = new List<Segmentation>();

        public double PlayAt { get; set; } = double.NaN;

        public override string ToString() => $"command 0x{Command:X2}{(Time.HasValue ? $" at {Time.Value / Scte35.Hz:0.000}s" : " now")}{(OutOfNetwork ? " out" : string.Empty)}{(InToNetwork ? " in" : string.Empty)}{(BreakSeconds > 0 ? $" break {BreakSeconds:0.###}s" : string.Empty)}{(Segments.Count > 0 ? "; " + string.Join("; ", Segments) : string.Empty)}";
    }

    public sealed class AdProgress
    {
        public string Creative { get; set; }

        public double Elapsed { get; set; }

        public double Length { get; set; }

        public override string ToString() => $"creative {Creative} at {Elapsed:0}s of {Length:0}s";
    }

    public static class Scte35
    {
        private const string PlutoTracking = "www.pluto.tv:clik:";

        public const double Hz = 90000;

        private const long PtsWrap = 1L << 33;

        public static SpliceSignal Parse(ReadOnlySpan<byte> data)
        {
            if (data.Length < 17 || data[0] != 0xFC || (data[4] & 0x80) != 0)
            {
                return null;
            }

            var sectionLength = ((data[1] & 0x0F) << 8) | data[2];

            if (sectionLength + 3 > data.Length || Crc32(data.Slice(0, sectionLength + 3)) != 0)
            {
                return null;
            }

            var reader = new Bits(data.Slice(0, sectionLength + 3 - 4));

            reader.Skip(8 + 1 + 1 + 2 + 12 + 8 + 1 + 6);

            var adjustment = reader.Read(33);

            reader.Skip(8 + 12);

            var commandLength = (int)reader.Read(12);
            var signal = new SpliceSignal { Command = (int)reader.Read(8) };
            var commandStart = reader.Position;

            switch (signal.Command)
            {
                case 0x05:
                {
                    ReadSpliceInsert(ref reader, signal);
                }
                break;

                case 0x06:
                {
                    signal.Time = ReadSpliceTime(ref reader);
                }
                break;
            }

            if (commandLength != 0xFFF)
            {
                reader.Position = commandStart + commandLength * 8;
            }

            var loopEnd = reader.Position + 16 + (int)reader.Read(16) * 8;

            while (reader.Position + 16 <= loopEnd)
            {
                var tag = (int)reader.Read(8);
                var length = (int)reader.Read(8);
                var next = reader.Position + length * 8;

                if (tag == 0x02 && length >= 9)
                {
                    var segmentation = ReadSegmentation(ref reader, next);

                    if (segmentation != null)
                    {
                        signal.Segments.Add(segmentation);
                    }
                }

                reader.Position = next;
            }

            if (signal.Time.HasValue)
            {
                signal.Time = (signal.Time.Value + adjustment) % PtsWrap;
            }

            return signal;
        }

        private static void ReadSpliceInsert(ref Bits reader, SpliceSignal signal)
        {
            reader.Skip(32);

            if (reader.Read(1) == 1)
            {
                reader.Skip(7);

                return;
            }

            reader.Skip(7);

            var outOfNetwork = reader.Read(1) == 1;
            var program = reader.Read(1) == 1;
            var hasDuration = reader.Read(1) == 1;
            var immediate = reader.Read(1) == 1;

            reader.Skip(4);

            signal.OutOfNetwork = outOfNetwork;
            signal.InToNetwork = !outOfNetwork;

            if (program && !immediate)
            {
                signal.Time = ReadSpliceTime(ref reader);
            }

            if (!program)
            {
                var components = (int)reader.Read(8);

                for (var i = 0; i < components; i++)
                {
                    reader.Skip(8);

                    if (!immediate)
                    {
                        signal.Time ??= ReadSpliceTime(ref reader);
                    }
                }
            }

            if (hasDuration)
            {
                reader.Skip(1 + 6);

                signal.BreakSeconds = reader.Read(33) / Hz;
            }
        }

        private static long? ReadSpliceTime(ref Bits reader)
        {
            if (reader.Read(1) == 1)
            {
                reader.Skip(6);

                return reader.Read(33);
            }

            reader.Skip(7);

            return null;
        }

        private static Segmentation ReadSegmentation(ref Bits reader, int end)
        {
            if (reader.Read(32) != 0x43554549)
            {
                return null;
            }

            var segmentation = new Segmentation { EventId = (uint)reader.Read(32) };

            if (reader.Read(1) == 1)
            {
                return null;
            }

            reader.Skip(7);

            var program = reader.Read(1) == 1;
            var hasDuration = reader.Read(1) == 1;

            reader.Skip(6);

            if (!program)
            {
                reader.Skip((int)reader.Read(8) * 48);
            }

            if (hasDuration)
            {
                segmentation.Seconds = reader.Read(40) / Hz;
            }

            reader.Skip(8);
            reader.Skip((int)reader.Read(8) * 8);

            if (reader.Position + 24 > end)
            {
                return null;
            }

            segmentation.Type = (int)reader.Read(8);
            segmentation.Number = (int)reader.Read(8);
            segmentation.Expected = (int)reader.Read(8);

            return segmentation;
        }

        public static uint Crc32(ReadOnlySpan<byte> data)
        {
            var crc = 0xFFFFFFFFu;

            foreach (var value in data)
            {
                crc ^= (uint)value << 24;

                for (var i = 0; i < 8; i++)
                {
                    crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
                }
            }

            return crc;
        }

        public static string DescribeId3(ReadOnlySpan<byte> data)
        {
            if (data.Length < 10 || data[0] != 'I' || data[1] != 'D' || data[2] != '3')
            {
                return null;
            }

            var version = data[3];
            var size = Math.Min(data.Length - 10, (data[6] << 21) | (data[7] << 14) | (data[8] << 7) | data[9]);
            var frames = new List<string>();
            var position = 10;

            while (position + 10 <= 10 + size)
            {
                var id = Encoding.ASCII.GetString(data.Slice(position, 4));

                if (id[0] == '\0')
                {
                    break;
                }

                var length = version >= 4 ? (data[position + 4] << 21) | (data[position + 5] << 14) | (data[position + 6] << 7) | data[position + 7] : (data[position + 4] << 24) | (data[position + 5] << 16) | (data[position + 6] << 8) | data[position + 7];

                position += 10;

                if (length < 0 || position + length > data.Length)
                {
                    break;
                }

                frames.Add($"{id} {FrameText(id, data.Slice(position, length))}");

                position += length;
            }

            return string.Join("; ", frames);
        }

        public static AdProgress ReadPlutoProgress(ReadOnlySpan<byte> data)
        {
            if (data.Length < 10 || data[0] != 'I' || data[1] != 'D' || data[2] != '3')
            {
                return null;
            }

            var text = Encoding.ASCII.GetString(data);
            var start = text.IndexOf(PlutoTracking, StringComparison.Ordinal);

            if (start < 0)
            {
                return null;
            }

            start += PlutoTracking.Length;

            var end = text.IndexOf('\0', start);
            byte[] fields;

            try
            {
                fields = Convert.FromBase64String(text.Substring(start, (end < 0 ? text.Length : end) - start));
            }
            catch (FormatException)
            {
                return null;
            }

            var progress = new AdProgress();

            for (var i = 0; i + 5 <= fields.Length;)
            {
                var key = Encoding.ASCII.GetString(fields, i, 4);
                var length = fields[i + 4];
                var value = i + 5;

                if (value + length > fields.Length)
                {
                    break;
                }

                switch (key)
                {
                    case "crid":
                    {
                        progress.Creative = Convert.ToHexString(fields, value, length).ToLowerInvariant();
                    }
                    break;

                    case "cidx":
                    {
                        progress.Elapsed = Number(fields, value, length);
                    }
                    break;

                    case "midx":
                    {
                        progress.Length = Number(fields, value, length);
                    }
                    break;
                }

                i = value + length;
            }

            return progress.Creative != null && progress.Length > 0 ? progress : null;
        }

        private static long Number(byte[] data, int start, int length)
        {
            long value = 0;

            for (var i = 0; i < length; i++)
            {
                value = (value << 8) | data[start + i];
            }

            return value;
        }

        private static string FrameText(string id, ReadOnlySpan<byte> frame)
        {
            if (frame.Length == 0)
            {
                return string.Empty;
            }

            if (id == "PRIV")
            {
                var owner = frame.IndexOf((byte)0);

                return owner < 0 ? $"{frame.Length} bytes" : $"{Encoding.ASCII.GetString(frame.Slice(0, owner))} {Printable(frame.Slice(owner + 1))}";
            }

            if (id[0] == 'T')
            {
                var encoding = frame[0] == 1 ? Encoding.Unicode : frame[0] == 2 ? Encoding.BigEndianUnicode : Encoding.UTF8;

                return encoding.GetString(frame.Slice(1)).Replace('\0', '|').Trim('|');
            }

            return $"{frame.Length} bytes";
        }

        private static string Printable(ReadOnlySpan<byte> bytes)
        {
            var text = bytes.ToArray();

            return text.All(x => x >= 0x20 && x < 0x7F) ? Encoding.ASCII.GetString(text) : Convert.ToHexString(text.Length > 48 ? text.AsSpan(0, 48) : text);
        }

        private ref struct Bits
        {
            private readonly ReadOnlySpan<byte> _data;

            public Bits(ReadOnlySpan<byte> data)
            {
                _data = data;
                Position = 0;
            }

            public int Position { get; set; }

            public void Skip(int bits)
            {
                Position += bits;
            }

            public long Read(int bits)
            {
                long value = 0;

                for (var i = 0; i < bits; i++, Position++)
                {
                    var index = Position >> 3;

                    value = (value << 1) | (uint)(index < _data.Length ? (_data[index] >> (7 - (Position & 7))) & 1 : 0);
                }

                return value;
            }
        }
    }
}
