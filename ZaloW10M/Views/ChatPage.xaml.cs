using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZaloW10M.Core.Models;
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
            if (e.Parameter is (ZaloCredentials creds, ZaloConversation conv))
            {
                _viewModel = new ChatViewModel(creds, conv);
                this.DataContext = _viewModel;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}