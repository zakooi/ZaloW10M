namespace ZaloW10M.Core.Models
{
    public class ZaloCredentials
    {
        public string Cookie { get; set; }
        public string Imei { get; set; }
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        public string SecretKey { get; set; }
        public string CipherKey { get; set; }
        public string WsUrl { get; set; }
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public string AvatarUrl { get; set; }
    }
}