using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Messenger_Meowtalk.Client.Converters
{
    public class StickerImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string content && content.StartsWith("[STICKER_IMG]"))
            {
                var imagePath = content.Substring("[STICKER_IMG]".Length);
                return new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute));
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}