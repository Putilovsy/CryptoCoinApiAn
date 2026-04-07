using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CryptoMonitor.Helpers
{
    public class ChangeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal change || value is double changeD)
            {
                decimal val = value is decimal dec ? dec : (decimal)(double)value;
                if (val > 0)
                    return new SolidColorBrush(Color.FromRgb(85, 192, 155)); // Accent Green
                if (val < 0)
                    return new SolidColorBrush(Color.FromRgb(244, 67, 54));  // Accent Red
            }
            return new SolidColorBrush(Color.FromRgb(224, 225, 221)); // TextMain
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class EqualityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                if (targetType == typeof(int) && int.TryParse(parameter.ToString(), out int intVal))
                    return intVal;
                return parameter;
            }
            return Binding.DoNothing;
        }
    }
}
