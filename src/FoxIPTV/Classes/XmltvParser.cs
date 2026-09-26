// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Xml;

    /// <summary>A streaming parser for XMLTV guide documents; guides can be hundreds of megabytes so nothing is held but the result</summary>
    public static class XmltvParser
    {
        /// <summary>The XMLTV timestamp formats seen in the wild, in order of likelihood</summary>
        private static readonly string[] TimeFormats =
        {
            "yyyyMMddHHmmss zzz",
            "yyyyMMddHHmmss",
            "yyyyMMddHHmm zzz",
            "yyyyMMddHHmm",
            "yyyyMMdd"
        };

        /// <summary>Parse XMLTV text into programmes</summary>
        /// <param name="text">The full XMLTV document</param>
        /// <param name="progress">An optional percentage reporter, driven by how far through the text the reader is</param>
        /// <returns>The programmes, in document order</returns>
        public static List<Programme> Parse(string text, IProgress<int> progress = null)
        {
            var guide = new List<Programme>();

            if (string.IsNullOrWhiteSpace(text))
            {
                progress?.Report(100);

                return guide;
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                IgnoreProcessingInstructions = true,
                CheckCharacters = false
            };

            var lastPercent = -1;

            using (var stringReader = new CountingStringReader(text))
            using (var reader = XmlReader.Create(stringReader, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element || reader.Name != "programme")
                    {
                        continue;
                    }

                    var startText = reader.GetAttribute("start");
                    var stopText = reader.GetAttribute("stop");
                    var channel = reader.GetAttribute("channel");

                    string title = null;
                    string description = null;

                    if (!reader.IsEmptyElement)
                    {
                        using (var subtree = reader.ReadSubtree())
                        {
                            subtree.Read();
                            subtree.Read();

                            // Reading an element's content already moves the reader to the next node, so only step when nothing was read
                            while (!subtree.EOF)
                            {
                                if (subtree.NodeType == XmlNodeType.Element && subtree.Name == "title" && title == null)
                                {
                                    title = subtree.ReadElementContentAsString();
                                }
                                else if (subtree.NodeType == XmlNodeType.Element && subtree.Name == "desc" && description == null)
                                {
                                    description = subtree.ReadElementContentAsString();
                                }
                                else
                                {
                                    subtree.Read();
                                }
                            }
                        }
                    }

                    if (!TryParseTime(startText, out var start) || !TryParseTime(stopText, out var stop) || stop <= start)
                    {
                        continue;
                    }

                    guide.Add(new Programme
                    {
                        Channel = channel,
                        Title = title ?? string.Empty,
                        Description = description ?? string.Empty,
                        Start = start,
                        Stop = stop,
                        BlockLength = (int)Math.Floor((stop - start).TotalMinutes / 10d)
                    });

                    if (progress != null && guide.Count % 250 == 0)
                    {
                        var percent = (int)(stringReader.Position * 100L / text.Length);

                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            progress.Report(Math.Min(percent, 99));
                        }
                    }
                }
            }

            progress?.Report(100);

            return guide;
        }

        /// <summary>Parse an XMLTV timestamp</summary>
        /// <param name="value">The attribute text</param>
        /// <param name="result">The parsed value; timestamps without an offset are treated as UTC</param>
        /// <returns>True if parsed</returns>
        public static bool TryParseTime(string value, out DateTimeOffset result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();

            return DateTimeOffset.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
        }

        /// <summary>A <see cref="StringReader"/> that knows how far into the string it has read</summary>
        private class CountingStringReader : TextReader
        {
            private readonly string _text;

            /// <summary>How many characters have been consumed</summary>
            public int Position { get; private set; }

            public CountingStringReader(string text)
            {
                _text = text;
            }

            public override int Peek()
            {
                return Position < _text.Length ? _text[Position] : -1;
            }

            public override int Read()
            {
                return Position < _text.Length ? _text[Position++] : -1;
            }

            public override int Read(char[] buffer, int index, int count)
            {
                var remaining = _text.Length - Position;

                if (remaining <= 0)
                {
                    return 0;
                }

                var toCopy = Math.Min(count, remaining);

                _text.CopyTo(Position, buffer, index, toCopy);

                Position += toCopy;

                return toCopy;
            }
        }
    }
}
