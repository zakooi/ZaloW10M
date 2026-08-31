using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace ZaloW10M.Helpers
{
    public static class DispatcherHelper
    {
        public static async Task ExecuteOnUIThreadAsync(Action action)
        {
            var dispatcher = CoreApplication.MainView?.CoreWindow?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
            }
            else
            {
                action();
            }
        }
    }
}