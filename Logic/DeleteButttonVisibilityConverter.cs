using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SpotlightClean.Logic
{
    public class DeleteButtonVisibilityConverter : IMultiValueConverter
    {
        private const string PlaceholderText = "Search files, apps, and more..."; // CHANGED: Make converter aware of placeholder

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 &&
                values[0] is string searchText &&
                values[1] is bool isSpecialCommand)
            {
                // CHANGED: Logic now checks for empty OR placeholder text.
                bool isSearchBoxEmpty = string.IsNullOrEmpty(searchText) || searchText == PlaceholderText;

                if (isSearchBoxEmpty && !isSpecialCommand)
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}