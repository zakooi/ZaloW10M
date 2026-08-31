using System;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ZaloW10M.Core
{
    public static class ZaloProtocol
    {
        public const ushort CMD_HANDSHAKE = 1;
        public const ushort CMD_PING = 2;
        public const ushort CMD_USER_MESSAGE = 501;
        public const ushort CMD_GROUP_MESSAGE = 521;
        public const ushort CMD_CONTROL_EVENT = 601;
        public const ushort CMD_REACTION = 612;

        public static (byte version, ushort cmd, byte subCmd, string dataJson) ParseWebSocketFrame(byte[] buffer, string cipherKey)
        {
            if (buffer == null || buffer.Length < 4)
                return (0, 0, 0, null);

            byte version = buffer[0];
            ushort cmd = BitConverter.ToUInt16(buffer, 1);
            byte subCmd = buffer[3];

            if (buffer.Length == 4)
                return (version, cmd, subCmd, string.Empty);

            byte[] payloadBytes = new byte[buffer.Length - 4];
            Array.Copy(buffer, 4, payloadBytes, 0, payloadBytes.Length);

            string rawText = Encoding.UTF8.GetString(payloadBytes);
            
            // Check if it is a JSON with encryption flag
            try
            {
                var jObj = JObject.Parse(rawText);
                if (jObj["encrypt"] != null)
                {
                    int encryptType = jObj["encrypt"].Value<int>();
                    string dataStr = jObj["data"]?.ToString();

                    if (encryptType == 0) // Plain JSON
                    {
                        return (version, cmd, subCmd, dataStr);
                    }
                    else if (encryptType == 1) // Base64 JSON
                    {
                        var bytes = Convert.FromBase64String(dataStr);
                        return (version, cmd, subCmd, Encoding.UTF8.GetString(bytes));
                    }
                    else if (encryptType == 2 || encryptType == 3) // AES-GCM
                    {
                        var encBytes = Convert.FromBase64String(dataStr);
                        string decrypted = ZaloCrypto.DecryptAesGcm(encBytes, cipherKey);
                        return (version, cmd, subCmd, decrypted);
                    }
                }
                return (version, cmd, subCmd, rawText);
            }
            catch
            {
                return (version, cmd, subCmd, rawText);
            }
        }

        public static byte[] BuildPingFrame(long eventId)
        {
            string pingJson = $"{{\"eventId\":{eventId}}}";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(pingJson);
            byte[] frame = new byte[4 + jsonBytes.Length];
            frame[0] = 1; // version
            frame[1] = 2; // cmd ping (UInt16 LE: 0x0002 -> 0x02, 0x00)
            frame[2] = 0;
            frame[3] = 1; // subCmd
            Array.Copy(jsonBytes, 0, frame, 4, jsonBytes.Length);
            return frame;
        }
    }
}