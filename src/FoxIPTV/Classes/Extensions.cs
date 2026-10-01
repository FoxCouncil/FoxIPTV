// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
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
