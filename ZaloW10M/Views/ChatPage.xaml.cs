using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZaloW10M.Core.Models;
using ZaloW10M.Services;
using ZaloW10M.ViewModels;

namespace ZaloW10M.Views
{
    public sealed partial class ChatPage : Page
    {
        private ChatViewModel _viewModel;

        public ChatPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is (ZaloCredentials creds, ZaloConversation conv, ZaloWebSocketService ws))
            {
                _viewModel = new ChatViewModel(creds, conv, ws);
                this.DataContext = _viewModel;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _viewModel?.Detach();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}
