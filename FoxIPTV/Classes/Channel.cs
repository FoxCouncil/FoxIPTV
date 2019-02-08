// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using System;
using System.Drawing;

namespace FoxIPTV.Classes
{
    public class Channel
    {
        public uint Index { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public Uri Logo { get; set; }

        public Image LogoImage { get; set; }

        public string Group { get; set; }

        public Uri Stream { get; set; }
    }
}
