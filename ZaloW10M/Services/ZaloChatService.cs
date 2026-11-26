using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ZaloW10M.Core;
using ZaloW10M.Core.Models;

namespace ZaloW10M.Services
{
    public class ZaloChatService
    {
        // One client for all chats; per-request headers carry the current user.
        private static readonly HttpClient SharedClient = new HttpClient();
        private readonly ZaloCredentials _creds;

        public ZaloChatService(ZaloCredentials creds)
        {
            _creds = creds;
        }

        public async Task<bool> SendTextMessageAsync(string threadId, string text, bool isGroup)
        {
            try
            {
                string endpoint = isGroup
                    ? "https://wpa.chat.zalo.me/api/group/sendmsg"
                    : "https://wpa.chat.zalo.me/api/message/sendmsg";

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var payload = new JObject
                {
                    [isGroup ? "grid" : "toid"] = threadId,
                    ["message"] = text,
                    ["clientId"] = now,
                    ["mentionInfo"] = new JArray(),
                    ["ttl"] = 0
                };

                string encryptedParams = ZaloCrypto.EncryptAesCbc(payload.ToString(), _creds.SecretKey);
                if (string.IsNullOrEmpty(encryptedParams)) return false;

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("params", encryptedParams)
                });
                var req = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
                req.Headers.TryAddWithoutValidation("User-Agent", _creds.UserAgent);
                if (!string.IsNullOrEmpty(_creds.Cookie))
                    req.Headers.TryAddWithoutValidation("Cookie", _creds.Cookie);

                var res = await SharedClient.SendAsync(req);
                string resBody = await res.Content.ReadAsStringAsync();
                var resObj = JObject.Parse(resBody);

                return resObj["error_code"]?.Value<int>() == 0;
            }
            catch
            {
                return false;
            }
        }

        public Task<List<ZaloConversation>> GetRecentConversationsAsync()
        {
            // shortcut: no recent-conversation API yet, replace when the sync endpoint is known
            return Task.FromResult(new List<ZaloConversation>());
        }
    }
}
