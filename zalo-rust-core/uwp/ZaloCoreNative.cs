// Sample P/Invoke wrapper for the C# UWP shell.
// Copy this file into the UWP project (e.g. Core/ZaloCoreNative.cs) and package
// the native zalo_core library built for the target architecture.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace ZaloW10M.Core
{
    internal static class ZaloCoreNative
    {
        // The native library file must be included in the APPX package.
        private const string DllName = "zalo_core";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_generate_sign_key(byte[] paramsJson, out IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_encrypt_aes_cbc(byte[] plain, byte[] keyB64, out IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_decrypt_aes_cbc(byte[] dataB64, byte[] keyB64, out IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_decrypt_aes_gcm(byte[] payload, int payloadLen, byte[] keyB64, out IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_parse_frame(byte[] buffer, int bufferLen, byte[] cipherKey, out IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_build_ping_frame(long eventId, out IntPtr bytes, out int length);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_i420_to_nv21(
            byte[] i420, int length, int width, int height,
            out IntPtr bytes, out int outLength);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_rotate_i420(
            byte[] i420, int length, int width, int height, int degrees,
            out IntPtr bytes, out int outLength, out int outWidth, out int outHeight);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_i420_scale(
            byte[] i420, int length, int width, int height, int newWidth, int newHeight,
            out IntPtr bytes, out int outLength, out int outWidth, out int outHeight);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_i420_to_argb(
            byte[] i420, int length, int width, int height,
            out IntPtr bytes, out int outLength);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int zalo_i420_to_abgr(
            byte[] i420, int length, int width, int height,
            out IntPtr bytes, out int outLength);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void zalo_free_string(IntPtr ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void zalo_free_bytes(IntPtr ptr, int length);

        private static byte[] Utf8(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var bytes = Encoding.UTF8.GetBytes(value);
            var terminated = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, terminated, 0, bytes.Length);
            return terminated;
        }

        private static string ReadAndFree(IntPtr ptr)
        {
            try
            {
                if (ptr == IntPtr.Zero) throw new InvalidOperationException("Native call returned no string.");
                var bytes = new List<byte>();
                for (int i = 0; ; i++)
                {
                    byte b = Marshal.ReadByte(ptr, i);
                    if (b == 0) break;
                    bytes.Add(b);
                }
                return Encoding.UTF8.GetString(bytes.ToArray());
            }
            finally
            {
                if (ptr != IntPtr.Zero) zalo_free_string(ptr);
            }
        }

        internal static string GenerateSignKey(IReadOnlyDictionary<string, string> parameters)
        {
            string json = JsonConvert.SerializeObject(parameters);
            if (zalo_generate_sign_key(Utf8(json), out IntPtr result) != 0)
                throw new InvalidOperationException("zalo_generate_sign_key failed.");
            return ReadAndFree(result);
        }

        internal static string EncryptAesCbc(string plain, string base64Key)
        {
            if (zalo_encrypt_aes_cbc(Utf8(plain), Utf8(base64Key), out IntPtr result) != 0)
                throw new InvalidOperationException("zalo_encrypt_aes_cbc failed.");
            return ReadAndFree(result);
        }

        internal static string DecryptAesCbc(string dataB64, string base64Key)
        {
            if (zalo_decrypt_aes_cbc(Utf8(dataB64), Utf8(base64Key), out IntPtr result) != 0)
                throw new InvalidOperationException("zalo_decrypt_aes_cbc failed.");
            return ReadAndFree(result);
        }

        internal static string DecryptAesGcm(byte[] payload, string cipherKeyBase64)
        {
            if (zalo_decrypt_aes_gcm(payload, payload.Length, Utf8(cipherKeyBase64), out IntPtr result) != 0)
                throw new InvalidOperationException("zalo_decrypt_aes_gcm failed.");
            return ReadAndFree(result);
        }

        internal static (byte version, ushort cmd, byte subCmd, string payload) ParseFrame(
            byte[] buffer, string cipherKey)
        {
            byte[] key = cipherKey == null ? null : Utf8(cipherKey);
            if (zalo_parse_frame(buffer, buffer.Length, key, out IntPtr result) != 0)
                throw new InvalidOperationException("zalo_parse_frame failed.");
            var json = ReadAndFree(result);
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            return (
                (byte)obj["version"],
                (ushort)obj["cmd"],
                (byte)obj["sub_cmd"],
                (string)obj["payload"]);
        }

        private static byte[] ReadAndFreeBytes(IntPtr ptr, int length)
        {
            try
            {
                if (ptr == IntPtr.Zero || length < 0)
                    throw new InvalidOperationException("Native call returned no byte buffer.");
                var result = new byte[length];
                Marshal.Copy(ptr, result, 0, length);
                return result;
            }
            finally
            {
                if (ptr != IntPtr.Zero) zalo_free_bytes(ptr, length);
            }
        }

        private static void Check(int status, string name)
        {
            if (status != 0) throw new InvalidOperationException(name + " failed.");
        }

        internal static byte[] BuildPingFrame(long eventId)
        {
            Check(zalo_build_ping_frame(eventId, out IntPtr bytes, out int length),
                  "zalo_build_ping_frame");
            return ReadAndFreeBytes(bytes, length);
        }

        internal static byte[] I420ToNv21(byte[] i420, int width, int height)
        {
            Check(zalo_i420_to_nv21(i420, i420.Length, width, height, out IntPtr bytes, out int length),
                  "zalo_i420_to_nv21");
            return ReadAndFreeBytes(bytes, length);
        }

        internal static (byte[] data, int width, int height) RotateI420(
            byte[] i420, int width, int height, int degrees)
        {
            Check(zalo_rotate_i420(i420, i420.Length, width, height, degrees,
                      out IntPtr bytes, out int length, out int newWidth, out int newHeight),
                  "zalo_rotate_i420");
            return (ReadAndFreeBytes(bytes, length), newWidth, newHeight);
        }

        internal static (byte[] data, int width, int height) ScaleI420(
            byte[] i420, int width, int height, int newWidth, int newHeight)
        {
            Check(zalo_i420_scale(i420, i420.Length, width, height, newWidth, newHeight,
                      out IntPtr bytes, out int length, out int outWidth, out int outHeight),
                  "zalo_i420_scale");
            return (ReadAndFreeBytes(bytes, length), outWidth, outHeight);
        }

        internal static byte[] I420ToArgb(byte[] i420, int width, int height)
        {
            Check(zalo_i420_to_argb(i420, i420.Length, width, height, out IntPtr bytes, out int length),
                  "zalo_i420_to_argb");
            return ReadAndFreeBytes(bytes, length);
        }

        internal static byte[] I420ToAbgr(byte[] i420, int width, int height)
        {
            Check(zalo_i420_to_abgr(i420, i420.Length, width, height, out IntPtr bytes, out int length),
                  "zalo_i420_to_abgr");
            return ReadAndFreeBytes(bytes, length);
        }
    }
}
