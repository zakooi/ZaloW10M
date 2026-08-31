using System;
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

        public ChatListViewModel(ZaloCredentials creds)
        {
            _creds = creds;
            _chatService = new ZaloChatService(_creds);
            _wsService = new ZaloWebSocketService(_creds);
            _wsService.StatusChanged += (s) => DispatcherHelper.ExecuteOnUIThreadAsync(() => StatusText = s);
            _wsService.MessageReceived += OnLiveMessageReceived;
        }

        public async Task LoadConversationsAsync()
        {
            var list = await _chatService.GetRecentConversationsAsync();
            Conversations.Clear();
            foreach (var item in list)
            {
                Conversations.Add(item);
            }
            await _wsService.ConnectAsync();
        }

        private void OnLiveMessageReceived(ZaloMessage msg)
        {
            _ = DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                // Update or push conversation to top
                foreach (var conv in Conversations)
                {
                    if (conv.ThreadId == msg.UidFrom)
                    {
                        conv.LastMessage = msg.Content;
                        conv.UnreadCount++;
                        return;
                    }
                }
                Conversations.Insert(0, new ZaloConversation
                {
                    ThreadId = msg.UidFrom,
                    Name = msg.SenderName,
                    LastMessage = msg.Content,
                    UnreadCount = 1
                });
            });
        }
    }
}