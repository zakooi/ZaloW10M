using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZaloW10M.Core.Models;
using ZaloW10M.ViewModels;

namespace ZaloW10M.Views
{
    public sealed partial class ChatListPage : Page
    {
        private ChatListViewModel _viewModel;
        private ZaloCredentials _creds;

        public ChatListPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is ZaloCredentials creds)
            {
                _creds = creds;
                _viewModel = new ChatListViewModel(_creds);
                this.DataContext = _viewModel;
                await _viewModel.LoadConversationsAsync();
            }
        }

        private void Conversation_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ZaloConversation conv)
            {
                Frame.Navigate(typeof(ChatPage), (creds: _creds, conv: conv));
            }
        }
    }
}