using System;
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
            if (e.Parameter is ValueTuple<ZaloCredentials, ZaloConversation, ZaloWebSocketService> nav)
            {
                _viewModel = new ChatViewModel(nav.Item1, nav.Item2, nav.Item3);
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
