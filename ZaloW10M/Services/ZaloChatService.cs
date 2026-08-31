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
        private readonly HttpClient _httpClient;
        private readonly ZaloCredentials _creds;

        public ZaloChatService(ZaloCredentials creds)
        {
            _creds = creds;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _creds.UserAgent);
            if (!string.IsNullOrEmpty(_creds.Cookie))
            {
                _httpClient.DefaultRequestHeaders.Add("Cookie", _creds.Cookie);
            }
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
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("params", encryptedParams)
                });

                var res = await _httpClient.PostAsync(endpoint, content);
                string resBody = await res.Content.ReadAsStringAsync();
                var resObj = JObject.Parse(resBody);

                return resObj["error_code"]?.Value<int>() == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<ZaloConversation>> GetRecentConversationsAsync()
        {
            var list = new List<ZaloConversation>();
            // Sample placeholder data if API requires dynamic sync token
            list.Add(new ZaloConversation
            {
                ThreadId = "sample_user_1",
                Name = "Zalo Official Support",
                LastMessage = "Chào mừng bạn đến với Zalo trên Windows 10 Mobile!",
                UnreadCount = 0,
                IsGroup = false,
                UpdatedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            return await Task.FromResult(list);
        }
    }
}