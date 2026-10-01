// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    // ReSharper disable once UnusedMember.Global
    /// <summary>A static class containing FoxIPTV's extension methods</summary>
    public static class Extensions
    {
        /// <summary>Return an MD5 hash of a string</summary>
        /// <param name="inputString">The input string to hash</param>
        /// <returns>A MD5 hash string value</returns>
        public static string ToMD5(this string inputString)
        {
            // Use input string to calculate MD5 hash
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(inputString)));
        }
    }
}
