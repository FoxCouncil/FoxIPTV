// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;

    // ReSharper disable once UnusedMember.Global
    /// <summary>A static class containing FoxIPTV's extension methods</summary>
    public static class Extensions
    {
        /// <summary>Compare two different instances of the same generic typed objects</summary>
        /// <typeparam name="T">The generic type to compare</typeparam>
        /// <param name="valueA">The first, original, object to compare</param>
        /// <param name="valueB">The second, different, object to compare</param>
        /// <returns>A generic <see cref="IList{T}"/> of <see cref="Difference"/> objects</returns>
        public static IList<Difference> Difference<T>(this T valueA, T valueB)
        {
            if (valueA == null || valueB == null)
            {
                throw new ArgumentException();
            }

            var differences = new List<Difference>();

            // We stick to public properties
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var v = new Difference
                {
                    PropertyName = property.Name,
                    ValueA = property.GetValue(valueA),
                    ValueB = property.GetValue(valueB)
                };

                // If both values are null, nothing's changed
                if (v.ValueA == null && v.ValueB == null)
                {
                    continue;
                }

                // If either of the values are null and the other is not, it's a difference!
                if (v.ValueA == null && v.ValueB != null || v.ValueA != null && v.ValueB == null)
                {
                    differences.Add(v);

                    continue;
                }

                // If we are not null, and we don't equal the same value, it's a difference!
                if (v.ValueA != null && !v.ValueA.Equals(v.ValueB))
                {
                    differences.Add(v);
                }
            }

            return differences;
        }

        /// <summary>Return an MD5 hash of a string</summary>
        /// <param name="inputString">The input string to hash</param>
        /// <returns>A MD5 hash string value</returns>
        public static string ToMD5(this string inputString)
        {
            // Use input string to calculate MD5 hash
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.ASCII.GetBytes(inputString);
                var hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                var sb = new StringBuilder();

                foreach (var aByte in hashBytes)
                {
                    sb.Append(aByte.ToString("X2"));
                }

                return sb.ToString();
            }
        }
    }
}
