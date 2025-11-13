using System.Globalization;
using System.Windows;
using System.Windows.Data;

public class IsDeletedToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter is string strParam && bool.TryParse(strParam, out bool result) && result;

        if (value is bool isDeleted)
        {
            if (invert)
                return isDeleted ? Visibility.Collapsed : Visibility.Visible;
            else
                return isDeleted ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}