using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Messenger_Meowtalk.Client.Converters
{
    public class EditSendToolTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isEditing && isEditing) ? "Сохранить изменения" : "Отправить сообщение";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

