// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;

    /// <summary>A class to contain the data that represents a logical TV programme</summary>
    public class Programme
    {
        /// <summary>The channel ID this programme is associated with</summary>
        public string Channel { get; set; }

        /// <summary>The start time of this programme</summary>
        public DateTimeOffset Start { get; set; }

        /// <summary>The stop time of this programme</summary>
        public DateTimeOffset Stop { get; set; }

        /// <summary>The title of the programme</summary>
        public string Title { get; set; }

        /// <summary>The description of the programme</summary>
        public string Description { get; set; }
    }
}
