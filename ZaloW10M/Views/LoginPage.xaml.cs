using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZaloW10M.Services;
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
                App.WebSocketService = new ZaloWebSocketService(creds);
                Frame.Navigate(typeof(ChatListPage), creds);
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch
            {
                _viewModel.StatusText = "Không thể bắt đầu đăng nhập. Nhấn để thử lại.";
            }
        }

        private async void RefreshQR_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.LoadQrCodeAsync();
            }
            catch
            {
                _viewModel.StatusText = "Không thể lấy mã QR. Nhấn để thử lại.";
            }
        }
    }
}
