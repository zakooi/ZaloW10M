using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ZaloW10M.Core.Models;
using ZaloW10M.Helpers;
using ZaloW10M.Services;

namespace ZaloW10M.ViewModels
{
    public class ChatViewModel : ObservableObject
    {
        private readonly ZaloCredentials _creds;
        private readonly ZaloConversation _conversation;
        private readonly ZaloChatService _chatService;
        private readonly ZaloWebSocketService _wsService;

        public ObservableCollection<ZaloMessage> Messages { get; } = new ObservableCollection<ZaloMessage>();

        public string Title => _conversation.Name;

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _inputMessage;
        public string InputMessage
        {
            get => _inputMessage;
            set => SetProperty(ref _inputMessage, value);
        }

        public ICommand SendCommand { get; }

        public ChatViewModel(ZaloCredentials creds, ZaloConversation conversation, ZaloWebSocketService wsService)
        {
            _creds = creds;
            _conversation = conversation;
            _wsService = wsService;
            _chatService = new ZaloChatService(_creds);
            _wsService.MessageReceived += OnLiveMessageReceived;

            SendCommand = new RelayCommand(async () => await SendMessageAsync());
        }

        private void OnLiveMessageReceived(ZaloMessage msg)
        {
            if (string.IsNullOrEmpty(msg.UidFrom) || msg.UidFrom != _conversation.ThreadId) return;
            _ = DispatcherHelper.ExecuteOnUIThreadAsync(() => { Messages.Add(msg); });
        }

        public async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputMessage)) return;

            string textToSend = InputMessage;
            InputMessage = string.Empty;

            var localMsg = new ZaloMessage
            {
                Content = textToSend,
                IsOutgoing = true,
                Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            Messages.Add(localMsg);

            bool sent = await _chatService.SendTextMessageAsync(_conversation.ThreadId, textToSend, _conversation.IsGroup);
            if (!sent)
            {
                Messages.Remove(localMsg);
                StatusText = "Gửi thất bại.";
            }
        }

        public void Detach()
        {
            _wsService.MessageReceived -= OnLiveMessageReceived;
        }
    }
}
