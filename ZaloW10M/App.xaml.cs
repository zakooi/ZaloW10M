using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZaloW10M.Services;
using ZaloW10M.Views;

namespace ZaloW10M
{
    sealed partial class App : Application
    {
        // One socket for the whole app lifetime. Screens come and go; this does not.
        public static ZaloWebSocketService WebSocketService { get; set; }

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                try
                {
                    rootFrame.Navigate(typeof(LoginPage), e.Arguments);
                }
                catch (Exception ex)
                {
                    _ = ShowCrashAsync(ex);
                    return;
                }
            }

            Window.Current.Activate();
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            e.Handled = true;
            _ = ShowCrashAsync(e.Exception);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            _ = ShowCrashAsync(e.Exception);
        }

        // Keep the failure visible on screen and on disk instead of exiting silently.
        private static async Task ShowCrashAsync(Exception ex)
        {
            string text = $"[{DateTimeOffset.Now:o}]\r\n{ex}";

            try
            {
                var file = await ApplicationData.Current.LocalFolder
                    .CreateFileAsync("crash.log", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, text);
            }
            catch
            {
                // Logging must never mask the original failure.
            }

            var frame = new Frame();
            frame.Content = new Page
            {
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = "App gặp lỗi khi khởi động:\r\n\r\n" + text,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(16),
                        FontSize = 14
                    }
                }
            };
            Window.Current.Content = frame;
            Window.Current.Activate();
        }
    }
}
