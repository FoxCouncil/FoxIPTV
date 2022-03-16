// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class Channel
    {
        public uint Index { get; set; }

        public string Name { get; set; }

        public string CategoryKey { get; set; }

        public string GuideKey { get; set; }

        public Uri Stream { get; set; }

        public Uri Icon { get; set; }
    }
}
