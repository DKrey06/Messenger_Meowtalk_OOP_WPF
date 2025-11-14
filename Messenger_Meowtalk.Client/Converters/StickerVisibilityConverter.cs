using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Messenger_Meowtalk.Shared.Models;

namespace Messenger_Meowtalk.Client.Converters
{
    public class StickerVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Message.MessageType messageType)
            {
                return messageType == Message.MessageType.Sticker ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}