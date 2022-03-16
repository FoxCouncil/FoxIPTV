// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    class Provider
    {
        public Guid ServiceId { get; set; }

        public List<Channel> Channels { get; set; }

        public List<Programme> Guide { get; set; } 
    }
}
