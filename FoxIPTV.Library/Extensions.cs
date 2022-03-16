using System;
using System.Collections.Generic;

namespace FoxIPTV.Library
{
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    public static class Extensions
    {
        public static string Encrypt(this string text)
        {
            var keyString = Core.SettingGet<string>("e_s_guid");

            var key = Encoding.UTF8.GetBytes(keyString).Take(32).ToArray();

            using (var aesAlg = Aes.Create())
            {
                aesAlg.Padding = PaddingMode.PKCS7;

                using (var encryptor = aesAlg.CreateEncryptor(key, aesAlg.IV))
                {
                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(text);
                        }

                        var iv = aesAlg.IV;

                        var decryptedContent = msEncrypt.ToArray();

                        var result = new byte[iv.Length + decryptedContent.Length];

                        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                        Buffer.BlockCopy(decryptedContent, 0, result, iv.Length, decryptedContent.Length);

                        return Convert.ToBase64String(result);
                    }
                }
            }
        }

        public static string Decrypt(this string text)
        {
            var keyString = Core.SettingGet<string>("e_s_guid");

            var fullCipher = Convert.FromBase64String(text);

            var iv = new byte[16];
            var cipher = new byte[16];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, iv.Length);

            var key = Encoding.UTF8.GetBytes(keyString).Take(32).ToArray();

            using (var aesAlg = Aes.Create())
            {
                aesAlg.Padding = PaddingMode.PKCS7;

                using (var decryptor = aesAlg.CreateDecryptor(key, iv))
                {
                    var result = string.Empty;

                    using (var msDecrypt = new MemoryStream(fullCipher.Skip(iv.Length).ToArray()))
                    {
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                result = srDecrypt.ReadToEnd();
                            }
                        }
                    }

                    return result;
                }
            }
        }
    }
}
