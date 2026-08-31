using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace ZaloW10M.Core
{
    public static class ZaloCrypto
    {
        public static string GenerateSignKey(Dictionary<string, string> parameters)
        {
            var sortedValues = parameters.OrderBy(p => p.Key)
                                         .Select(p => p.Value)
                                         .ToArray();

            string raw = "zsecuregetserverinfo" + string.Join("", sortedValues);

            var alg = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
            IBuffer buff = CryptographicBuffer.ConvertStringToBinary(raw, BinaryStringEncoding.Utf8);
            var hashed = alg.HashData(buff);

            return CryptographicBuffer.EncodeToHexString(hashed);
        }

        public static string EncryptAesCbc(string plainText, string base64Key)
        {
            try
            {
                var keyBytes = Convert.FromBase64String(base64Key);
                var provider = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);
                var key = provider.CreateSymmetricKey(keyBytes.AsBuffer());

                var iv = new byte[16].AsBuffer();
                var data = CryptographicBuffer.ConvertStringToBinary(plainText, BinaryStringEncoding.Utf8);

                var encrypted = CryptographicEngine.Encrypt(key, data, iv);
                return CryptographicBuffer.EncodeToBase64String(encrypted);
            }
            catch
            {
                return null;
            }
        }

        public static string DecryptAesCbc(string base64Data, string base64Key)
        {
            try
            {
                var keyBytes = Convert.FromBase64String(base64Key);
                var provider = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);
                var key = provider.CreateSymmetricKey(keyBytes.AsBuffer());

                var iv = new byte[16].AsBuffer();
                var cipherBuffer = CryptographicBuffer.DecodeFromBase64String(base64Data);

                var decrypted = CryptographicEngine.Decrypt(key, cipherBuffer, iv);
                return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, decrypted);
            }
            catch
            {
                return null;
            }
        }

        public static string DecryptAesGcm(byte[] payload, string cipherKeyBase64)
        {
            try
            {
                if (payload == null || payload.Length < 32) return null;

                var iv = payload.Take(16).ToArray().AsBuffer();
                var tag = payload.Skip(16).Take(16).ToArray().AsBuffer();
                var cipherText = payload.Skip(32).ToArray().AsBuffer();

                var keyBytes = Convert.FromBase64String(cipherKeyBase64);
                var provider = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesGcm);
                var key = provider.CreateSymmetricKey(keyBytes.AsBuffer());

                var authParams = new AuthenticatedEncryptionParameters(iv, tag, null);
                var decrypted = CryptographicEngine.Decrypt(key, cipherText, authParams);
                return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, decrypted);
            }
            catch
            {
                return null;
            }
        }

        public static string GenerateImei()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 16);
        }
    }
}