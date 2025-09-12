using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ClearView.Data;

namespace ClearView.Logic
{
    public class ActionButtonsVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // // CHANGED: Updated converter to handle the item's Type.
            if (values.Length < 3 || !(values[0] is bool isSelected) || !(values[1] is bool isSpecialCommand) || !(values[2] is ResultType type))
            {
                return Visibility.Collapsed;
            }

            // // CHANGED: Hide buttons for any web-related search type.
            if (type == ResultType.WebSearch || type == ResultType.RecentWebSearch || type == ResultType.Url)
            {
                return Visibility.Collapsed;
            }

            if (isSelected && !isSpecialCommand)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}