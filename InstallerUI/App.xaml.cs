using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InstallerUI
{
    /// <summary>
    /// InstallerUI entry point. This WPF app is the only UI the user sees;
    /// the MSI runs silently in the background and handles files, registry, shortcuts.
    /// </summary>
    public partial class App : Application
    {
    }

    public sealed class ProgressToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return 0.0;
            var progress = values[0] is double d ? d : 0.0;
            var width = values[1] is double w ? w : 0.0;
            return Math.Max(0, Math.Min(width, width * progress / 100.0));
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }

    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Collapsed;
    }

    /// <summary>Converts CheckBox IsChecked (bool?) to ViewModel bool for TwoWay binding.</summary>
    public sealed class NullableBoolToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return (bool?)b;
            return (bool?)false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return b;
            var nullable = value as bool?;
            return nullable == true;
        }
    }
}
