// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using System;

namespace FoxIPTV.Classes
{
    public class Programme
    {
        public string Channel { get; set; }

        public DateTime Start { get; set; }

        public DateTime Stop { get; set; }

        public int BlockLength { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }
    }
}
