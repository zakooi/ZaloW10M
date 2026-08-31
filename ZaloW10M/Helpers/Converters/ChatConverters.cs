using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace ZaloW10M.Helpers.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isOutgoing = value is bool b && b;
            return isOutgoing 
                ? new SolidColorBrush(Color.FromArgb(255, 225, 240, 255)) // Light Blue for outgoing
                : new SolidColorBrush(Color.FromArgb(255, 240, 240, 240)); // Gray for incoming
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class BoolToAlignConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isOutgoing = value is bool b && b;
            return isOutgoing ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}