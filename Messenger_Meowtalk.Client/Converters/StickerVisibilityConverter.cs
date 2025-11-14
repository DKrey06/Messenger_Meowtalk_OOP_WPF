using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Messenger_Meowtalk.Client.Converters
{
    public class StickerVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string content && content.StartsWith("[STICKER_IMG]")
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}