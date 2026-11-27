using System.Text;
using ZaloW10M.Core;

const string KEY = "MDEyMzQ1Njc4OWFiY2RlZg==";

Console.WriteLine("sign=" + ZaloCrypto.GenerateSignKey(new Dictionary<string, string>
{
    ["computer_name"] = "Web",
    ["imei"] = "abc123",
    ["language"] = "vi",
    ["ts"] = "1700000000000",
}));
Console.WriteLine("cbc=" + ZaloCrypto.EncryptAesCbc("hello", KEY));

byte[] ping = ZaloProtocol.BuildPingFrame(123);
Console.WriteLine("ping=" + Convert.ToHexString(ping).ToLowerInvariant());
Console.WriteLine("payload=" + ZaloProtocol.ParseWebSocketFrame(ping, null).dataJson);

var b64Frame = new byte[] { 1, 245, 1, 1 }
    .Concat(Encoding.UTF8.GetBytes("{\"encrypt\":1,\"data\":\"aGVsbG8=\"}"))
    .ToArray();
Console.WriteLine("b64=" + ZaloProtocol.ParseWebSocketFrame(b64Frame, null).dataJson);
// AES-GCM is left out: the shipped payload has a 16-byte IV, while .NET's
// AesGcm only accepts 12-byte nonces. Needs a device check against UWP.
