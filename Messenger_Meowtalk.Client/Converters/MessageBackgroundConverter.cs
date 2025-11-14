using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Messenger_Meowtalk.Client.Converters
{
public class MessageBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isMyMessage && isMyMessage)
        {
            return new SolidColorBrush(Color.FromRgb(255, 230, 229));
        }
        return new SolidColorBrush(Color.FromRgb(225, 193, 209));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
}
