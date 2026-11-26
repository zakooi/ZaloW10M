using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ZaloW10M.Core.Models;
using ZaloW10M.Helpers;
using ZaloW10M.Services;

namespace ZaloW10M.ViewModels
{
    public class ChatListViewModel : ObservableObject
    {
        private readonly ZaloCredentials _creds;
        private readonly ZaloChatService _chatService;
        private readonly ZaloWebSocketService _wsService;

        public ObservableCollection<ZaloConversation> Conversations { get; } = new ObservableCollection<ZaloConversation>();

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        internal ZaloWebSocketService WebSocketService => _wsService;

        public ChatListViewModel(ZaloCredentials creds)
        {
            _creds = creds;
            _chatService = new ZaloChatService(_creds);
            _wsService = App.WebSocketService ?? (App.WebSocketService = new ZaloWebSocketService(creds));
            _wsService.StatusChanged += OnStatusChanged;
            _wsService.MessageReceived += OnLiveMessageReceived;
        }

        private void OnStatusChanged(string status) =>
            _ = DispatcherHelper.ExecuteOnUIThreadAsync(() => { StatusText = status; });

        public async Task LoadConversationsAsync()
        {
            var list = await _chatService.GetRecentConversationsAsync();
            Conversations.Clear();
            foreach (var item in list)
                Conversations.Add(item);
            if (list.Count == 0) StatusText = "Chưa có hội thoại nào.";
            await _wsService.ConnectAsync();
        }

        private void OnLiveMessageReceived(ZaloMessage msg)
        {
            if (string.IsNullOrEmpty(msg.UidFrom)) return;

            _ = DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                foreach (var conv in Conversations)
                {
                    if (conv.ThreadId == msg.UidFrom)
                    {
                        conv.LastMessage = msg.Content;
                        if (!msg.IsOutgoing) conv.UnreadCount++;
                        return;
                    }
                }
                if (msg.IsOutgoing) return;
                Conversations.Insert(0, new ZaloConversation
                {
                    ThreadId = msg.UidFrom,
                    Name = msg.SenderName,
                    LastMessage = msg.Content,
                    UnreadCount = 1
                });
            });
        }

        public void Detach()
        {
            _wsService.StatusChanged -= OnStatusChanged;
            _wsService.MessageReceived -= OnLiveMessageReceived;
        }
    }
}
