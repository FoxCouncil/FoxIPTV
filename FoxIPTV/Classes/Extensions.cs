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
    public static class Extensions
    {
        public static List<Difference> Difference<T>(this T valueA, T valueB)
        {
            if (valueA == null || valueB == null)
            {
                throw new ArgumentException();
            }

            var differences = new List<Difference>();

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var v = new Difference
                {
                    PropertyName = property.Name,
                    ValueA = property.GetValue(valueA),
                    ValueB = property.GetValue(valueB)
                };

                if (v.ValueA == null && v.ValueB == null)
                {
                    continue;
                }

                if (v.ValueA == null && v.ValueB != null || v.ValueA != null && v.ValueB == null)
                {
                    differences.Add(v);

                    continue;
                }

                if (v.ValueA != null && !v.ValueA.Equals(v.ValueB))
                {
                    differences.Add(v);
                }
            }

            return differences;
        }

        public static DateTime RoundUp(this DateTime dt, TimeSpan d)
        {
            return new DateTime((dt.Ticks + d.Ticks - 1) / d.Ticks * d.Ticks, dt.Kind);
        }

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
