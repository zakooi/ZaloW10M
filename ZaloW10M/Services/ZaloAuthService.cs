using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ZaloW10M.Core;
using ZaloW10M.Core.Models;

namespace ZaloW10M.Services
{
    public class ZaloAuthService
    {
        private readonly HttpClient _httpClient;

        public ZaloAuthService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public async Task<(string qrCodeUrl, string token)> GetQrCodeAsync()
        {
            try
            {
                string url = $"https://id.zalo.me/account/login/qr?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var response = await _httpClient.GetStringAsync(url);
                var jObj = JObject.Parse(response);
                
                string code = jObj["data"]?["code"]?.ToString();
                string token = jObj["data"]?["token"]?.ToString();
                
                string qrImageUrl = $"https://id.zalo.me/account/qr/image?code={code}";
                return (qrImageUrl, token);
            }
            catch
            {
                return (null, null);
            }
        }

        public async Task<(bool isSuccess, string cookie, string userId)> CheckQrStatusAsync(string token)
        {
            try
            {
                string url = $"https://id.zalo.me/account/login/qr/check?token={token}&t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var res = await _httpClient.GetAsync(url);
                string body = await res.Content.ReadAsStringAsync();
                var jObj = JObject.Parse(body);
                
                int error = jObj["error"]?.Value<int>() ?? -1;
                if (error == 0) // Login approved
                {
                    // Extract cookies
                    string cookieHeader = string.Empty;
                    if (res.Headers.TryGetValues("Set-Cookie", out var cookies))
                    {
                        cookieHeader = string.Join("; ", cookies);
                    }
                    string uid = jObj["data"]?["uid"]?.ToString();
                    return (true, cookieHeader, uid);
                }
                return (false, null, null);
            }
            catch
            {
                return (false, null, null);
            }
        }

        public async Task<bool> GetServerInfoAsync(ZaloCredentials creds)
        {
            try
            {
                if (string.IsNullOrEmpty(creds.Imei))
                    creds.Imei = ZaloCrypto.GenerateImei();

                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var queryParams = new Dictionary<string, string>
                {
                    { "computer_name", "Web" },
                    { "imei", creds.Imei },
                    { "language", "vi" },
                    { "ts", ts.ToString() }
                };

                string signKey = ZaloCrypto.GenerateSignKey(queryParams);
                string url = $"https://wpa.chat.zalo.me/api/login/getServerInfo?computer_name=Web&imei={creds.Imei}&language=vi&ts={ts}&signkey={signKey}";

                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Cookie", creds.Cookie);

                var response = await _httpClient.SendAsync(req);
                string content = await response.Content.ReadAsStringAsync();
                var jObj = JObject.Parse(content);

                if (jObj["error_code"]?.Value<int>() == 0)
                {
                    creds.SecretKey = jObj["data"]?["zpw_sek"]?.ToString();
                    creds.WsUrl = jObj["data"]?["ws_url"]?.ToString() ?? "wss://chat.zalo.me/ws/";
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}