// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    public class Difference
    {
        public string PropertyName { get; set; }

        public object ValueA { get; set; }

        public object ValueB { get; set; }

        public override string ToString()
        {
            return $"{PropertyName} Changed {ValueA ?? "NULL"} -> {ValueB ?? "NULL"}";
        }
    }
}
