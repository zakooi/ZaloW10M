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
        private readonly SessionManager _sessionManager = new SessionManager();

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

        public async Task InitializeAsync()
        {
            // Check existing session
            var saved = _sessionManager.LoadSession();
            if (saved != null && !string.IsNullOrEmpty(saved.Cookie))
            {
                StatusText = "Đang khôi phục phiên đăng nhập...";
                IsLoading = true;
                bool ok = await _authService.GetServerInfoAsync(saved);
                IsLoading = false;
                if (ok)
                {
                    LoginSuccess?.Invoke(saved);
                    return;
                }
            }

            await LoadQrCodeAsync();
        }

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
                _ = PollQrStatusAsync(token);
            }
            else
            {
                StatusText = "Không thể lấy mã QR. Nhấn để thử lại.";
            }
        }

        private async Task PollQrStatusAsync(string token)
        {
            int attempts = 0;
            while (attempts < 60)
            {
                await Task.Delay(2000);
                var (success, cookie, uid) = await _authService.CheckQrStatusAsync(token);
                if (success)
                {
                    await DispatcherHelper.ExecuteOnUIThreadAsync(async () =>
                    {
                        StatusText = "Đăng nhập thành công! Đang cấu hình...";
                        var creds = new ZaloCredentials
                        {
                            Cookie = cookie,
                            UserId = uid
                        };

                        await _authService.GetServerInfoAsync(creds);
                        _sessionManager.SaveSession(creds);
                        LoginSuccess?.Invoke(creds);
                    });
                    return;
                }
                attempts++;
            }
            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                StatusText = "Mã QR đã hết hạn. Nhấn vào mã để làm mới.";
            });
        }
    }
}