using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZaloW10M.ViewModels;

namespace ZaloW10M.Views
{
    public sealed partial class LoginPage : Page
    {
        private readonly LoginViewModel _viewModel = new LoginViewModel();

        public LoginPage()
        {
            this.InitializeComponent();
            this.DataContext = _viewModel;
            _viewModel.LoginSuccess += (creds) =>
            {
                Frame.Navigate(typeof(ChatListPage), creds);
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _viewModel.InitializeAsync();
        }

        private async void RefreshQR_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadQrCodeAsync();
        }
    }
}