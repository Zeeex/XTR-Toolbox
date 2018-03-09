using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace XTR_Toolbox.Classes
{
    internal class StringVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty((string) value) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture) => Binding.DoNothing;
    }

    internal class DoubleVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;
            return Math.Abs((double) value) < 0.01 ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture) => Binding.DoNothing;
    }
}