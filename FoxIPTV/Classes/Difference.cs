// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    /// <summary>A class to represent a change in a object's property against another object of the same type</summary>
    public class Difference
    {
        /// <summary>The property name that has changed</summary>
        public string PropertyName { get; set; }

        /// <summary>The first, original, value</summary>
        public object ValueA { get; set; }

        /// <summary>The second, changed, value</summary>
        public object ValueB { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{PropertyName} Changed {ValueA ?? "NULL"} -> {ValueB ?? "NULL"}";
        }
    }
}
