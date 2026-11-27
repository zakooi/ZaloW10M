using System;
using System.Security.Cryptography;
using System.Text;

namespace Windows.Storage.Streams
{
    public sealed class IBuffer
    {
        public byte[] Data { get; }
        public uint Length => (uint)Data.Length;
        public IBuffer(byte[] data) { Data = data; }
    }
}

namespace System.Runtime.InteropServices.WindowsRuntime
{
    using IBuffer = global::Windows.Storage.Streams.IBuffer;
    public static class BufferExtensions
    {
        public static IBuffer AsBuffer(this byte[] bytes) => new IBuffer(bytes);
    }
}

namespace Windows.Security.Cryptography
{
    using IBuffer = global::Windows.Storage.Streams.IBuffer;
    public enum BinaryStringEncoding { Utf8 }

    public static class CryptographicBuffer
    {
        public static IBuffer ConvertStringToBinary(string value, BinaryStringEncoding encoding) =>
            new IBuffer(Encoding.UTF8.GetBytes(value));
        public static string ConvertBinaryToString(BinaryStringEncoding encoding, IBuffer buffer) =>
            Encoding.UTF8.GetString(buffer.Data);
        public static string EncodeToHexString(IBuffer buffer) =>
            Convert.ToHexString(buffer.Data).ToLowerInvariant();
        public static string EncodeToBase64String(IBuffer buffer) =>
            Convert.ToBase64String(buffer.Data);
        public static IBuffer DecodeFromBase64String(string value) =>
            new IBuffer(Convert.FromBase64String(value));
    }
}

namespace Windows.Security.Cryptography.Core
{
    using IBuffer = global::Windows.Storage.Streams.IBuffer;
    public static class HashAlgorithmNames { public static string Md5 => "MD5"; }
    public static class SymmetricAlgorithmNames
    {
        public static string AesCbcPkcs7 => "AesCbcPkcs7";
        public static string AesGcm => "AesGcm";
    }

    public sealed class HashAlgorithmProvider
    {
        public static HashAlgorithmProvider OpenAlgorithm(string name) => new HashAlgorithmProvider();
        public IBuffer HashData(IBuffer data) => new IBuffer(MD5.HashData(data.Data));
    }

    public sealed class CryptographicKey { public byte[] Key { get; set; } }

    public sealed class SymmetricKeyAlgorithmProvider
    {
        public static SymmetricKeyAlgorithmProvider OpenAlgorithm(string name) =>
            new SymmetricKeyAlgorithmProvider();
        public CryptographicKey CreateSymmetricKey(IBuffer key) =>
            new CryptographicKey { Key = key.Data };
    }

    public static class CryptographicEngine
    {
        public static IBuffer Encrypt(CryptographicKey key, IBuffer data, IBuffer iv)
        {
            using var aes = Aes.Create();
            aes.Key = key.Key;
            return new IBuffer(aes.EncryptCbc(data.Data, iv.Data, PaddingMode.PKCS7));
        }

        public static IBuffer Decrypt(CryptographicKey key, IBuffer data, IBuffer iv)
        {
            using var aes = Aes.Create();
            aes.Key = key.Key;
            return new IBuffer(aes.DecryptCbc(data.Data, iv.Data, PaddingMode.PKCS7));
        }

        public static IBuffer DecryptAndAuthenticate(
            CryptographicKey key, IBuffer data, IBuffer iv, IBuffer tag, IBuffer associated)
        {
            using var gcm = new AesGcm(key.Key, tag.Data.Length);
            var plain = new byte[data.Data.Length];
            gcm.Decrypt(iv.Data, data.Data, tag.Data, plain, associated?.Data);
            return new IBuffer(plain);
        }
    }
}

namespace Windows.System.Profile
{
    using IBuffer = global::Windows.Storage.Streams.IBuffer;
    public sealed class SystemIdentificationInfo { public IBuffer Id => new IBuffer(new byte[16]); }
    public static class SystemIdentification
    {
        public static SystemIdentificationInfo GetSystemIdForPublisher() => new SystemIdentificationInfo();
    }
}
