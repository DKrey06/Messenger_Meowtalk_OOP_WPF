using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Messenger_Meowtalk.Shared.Models;

namespace Messenger_Meowtalk.Client.Converters
{
    public class TextMessageVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Message.MessageType type && type == Message.MessageType.Text
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}