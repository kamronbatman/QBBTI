using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QBBTI.App.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isAutoMapped && isAutoMapped)
        {
            return new SolidColorBrush(Color.FromRgb(220, 245, 220)); // light green
        }

        return new SolidColorBrush(Color.FromRgb(255, 250, 215)); // light yellow
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
