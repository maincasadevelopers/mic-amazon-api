using System;
using System.IO;
using System.Security.Cryptography;

namespace MIC_AMZ_API.App_Code
{
    public class Cryptography
    {

        public static byte[] EncryptStringToBytesAES(string plainText, string key, string initializationVector)
        {
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (initializationVector == null || initializationVector.Length <= 0)
                throw new ArgumentNullException("initializationVector");

            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(key);
                aes.IV = Convert.FromBase64String(initializationVector);

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (var streamWriter = new StreamWriter(cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }

                        return memoryStream.ToArray();
                    }
                }
            }
        }

        public static string DecryptStringFromBytesAES(byte[] cipherText, string key, string initializationVector)
        {
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (initializationVector == null || initializationVector.Length <= 0)
                throw new ArgumentNullException("IV");

            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(key);
                aes.IV = Convert.FromBase64String(initializationVector);
                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (var memoryStream = new MemoryStream(cipherText))
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (var streamReader = new StreamReader(cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}