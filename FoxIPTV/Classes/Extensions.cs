// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Windows.Forms;

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

        /// <summary>Round up a <see cref="DateTime"/> object to the nearest <see cref="TimeSpan"/></summary>
        /// <param name="dt">The datetime object to round up</param>
        /// <param name="d">The length of time to round up to</param>
        /// <returns>The new rounded up <see cref="DateTime"/> instance</returns>
        public static DateTime RoundUp(this DateTime dt, TimeSpan d)
        {
            return new DateTime((dt.Ticks + d.Ticks - 1) / d.Ticks * d.Ticks, dt.Kind);
        }

        /// <summary>Invoke the provided method if the caller isn't on the control's thread</summary>
        /// <param name="control">The control to invoke against</param>
        /// <param name="action">The method you want to invoke on the provided control's thread</param>
        public static void InvokeIfRequired(this Control control, MethodInvoker action)
        {
            if (control.Disposing)
            {
                return;
            }

            if (control.InvokeRequired)
            {
                try
                {
                    control.Invoke(action);
                }
                catch (ObjectDisposedException ex)
                {
                    TvCore.LogError($"[.NET] ObjectDisposedException({ex.Message})");
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>Takes a <see cref="uint"/> that may be FourCC encoded and converts it</summary>
        /// <param name="fourCCNum">The <see cref="uint"/> to decode</param>
        /// <returns>A FourCC decoded string</returns>
        public static string ToFourCC(this uint fourCCNum)
        {
            var sb = new StringBuilder();
            var num = fourCCNum;

            while (num > 0)
            {
                sb.Append((char)(num & 0xff));
                num = (uint)Math.Floor((decimal)num / 256);
            }

            return sb.ToString();
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

        /// <summary>"Protect" some sensitive user data to a <see cref="DataProtectionScope"/></summary>
        /// <param name="clearText">The <see cref="string"/> you want to encrypt</param>
        /// <param name="optionalEntropy">The optional salt entropy <see cref="string"/></param>
        /// <param name="scope">The <see cref="DataProtectionScope"/> of the encrypted data</param>
        /// <returns>A base64 encoded encrypted <see cref="string"/></returns>
        public static string Protect(this string clearText, string optionalEntropy = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
        {
            if (clearText == null)
            {
                throw new ArgumentNullException(nameof(clearText));
            }

            var clearBytes = Encoding.UTF8.GetBytes(clearText);
            var entropyBytes = string.IsNullOrEmpty(optionalEntropy) ? null : Encoding.UTF8.GetBytes(optionalEntropy);
            var encryptedBytes = ProtectedData.Protect(clearBytes, entropyBytes, scope);

            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>"Unprotect" some sensitive user data back to a <see cref="string"/></summary>
        /// <param name="encryptedText">The "protected" data to be unprotected</param>
        /// <param name="optionalEntropy">The optional salt entropy <see cref="string"/> used to "protect" this data</param>
        /// <param name="scope">The <see cref="DataProtectionScope"/> of the encrypted data</param>
        /// <returns>A plain text <see cref="string"/> of the encrypted data contents</returns>
        public static string Unprotect(this string encryptedText, string optionalEntropy = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
        {
            if (encryptedText == null)
            {
                throw new ArgumentNullException(nameof(encryptedText));
            }

            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var entropyBytes = string.IsNullOrEmpty(optionalEntropy) ? null : Encoding.UTF8.GetBytes(optionalEntropy);
            var clearBytes = ProtectedData.Unprotect(encryptedBytes, entropyBytes, scope);

            return Encoding.UTF8.GetString(clearBytes);
        }
    }
}
