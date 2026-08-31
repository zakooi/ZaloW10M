using System;

namespace ZaloW10M.Core.Models
{
    public class ZaloMessage
    {
        public string MsgId { get; set; }
        public string UidFrom { get; set; }
        public string SenderName { get; set; }
        public string Content { get; set; }
        public long Timestamp { get; set; }
        public bool IsOutgoing { get; set; }
        public string FormattedTime => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).LocalDateTime.ToString("HH:mm");
    }
}