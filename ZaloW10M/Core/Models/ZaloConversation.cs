namespace ZaloW10M.Core.Models
{
    public class ZaloConversation
    {
        public string ThreadId { get; set; }
        public string Name { get; set; }
        public string LastMessage { get; set; }
        public int UnreadCount { get; set; }
        public string AvatarUrl { get; set; }
        public bool IsGroup { get; set; }
        public long UpdatedTime { get; set; }
    }
}