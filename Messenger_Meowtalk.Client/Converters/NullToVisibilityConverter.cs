using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Messenger_Meowtalk.Client.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter is string strParam && bool.TryParse(strParam, out bool result) && result;

            bool isVisible = value != null;

            if (invert)
                isVisible = !isVisible;

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}