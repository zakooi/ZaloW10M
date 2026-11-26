using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using ZaloW10M.Core.Models;
using ZaloW10M.Helpers;
using ZaloW10M.Services;

namespace ZaloW10M.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private readonly ZaloAuthService _authService = new ZaloAuthService();

        private BitmapImage _qrImage;
        public BitmapImage QrImage
        {
            get => _qrImage;
            set => SetProperty(ref _qrImage, value);
        }

        private string _statusText = "Đang lấy mã QR...";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public event Action<ZaloCredentials> LoginSuccess;

        // Only the newest QR poll may log in.
        private string _currentQrToken;

        public Task InitializeAsync() => LoadQrCodeAsync();

        public async Task LoadQrCodeAsync()
        {
            IsLoading = true;
            StatusText = "Đang tạo mã QR đăng nhập...";
            var (qrUrl, token) = await _authService.GetQrCodeAsync();
            IsLoading = false;

            if (!string.IsNullOrEmpty(qrUrl))
            {
                QrImage = new BitmapImage(new Uri(qrUrl));
                StatusText = "Vui lòng mở Zalo trên điện thoại khác và quét mã QR";
                _currentQrToken = token;
                _ = PollQrStatusAsync(token);
            }
            else
            {
                _currentQrToken = null;
                StatusText = "Không thể lấy mã QR. Nhấn để thử lại.";
            }
        }

        private async Task PollQrStatusAsync(string token)
        {
            for (int attempts = 0; attempts < 60; attempts++)
            {
                await Task.Delay(2000);
                if (token != _currentQrToken) return;

                var (success, cookie, uid) = await _authService.CheckQrStatusAsync(token);
                if (!success) continue;

                var creds = new ZaloCredentials { Cookie = cookie, UserId = uid };
                if (!await _authService.GetServerInfoAsync(creds))
                {
                    await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
                    {
                        StatusText = "Không thể lấy thông tin máy chủ. Quét lại mã QR.";
                    });
                    return;
                }

                await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
                {
                    StatusText = "Đăng nhập thành công!";
                    LoginSuccess?.Invoke(creds);
                });
                return;
            }

            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                StatusText = "Mã QR đã hết hạn. Nhấn vào mã để làm mới.";
            });
        }
    }
}
