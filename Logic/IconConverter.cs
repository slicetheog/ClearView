using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using SpotlightClean.Data;
using SpotlightClean.Utils;

namespace SpotlightClean.Logic
{
    public class IconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SearchResult searchResult)
            {
                if (searchResult.IsSpecialCommand)
                {
                    if (searchResult.FullPath == "SETTINGS_COMMAND")
                    {
                        return new BitmapImage(new Uri("pack://application:,,,/Assets/settings.png"));
                    }
                    if (searchResult.FullPath == "EXIT_COMMAND")
                    {
                        return new BitmapImage(new Uri("pack://application:,,,/Assets/close.png"));
                    }
                }
                
                switch (searchResult.Type)
                {
                    case ResultType.Application:
                    case ResultType.File:
                        try
                        {
                            var icon = IconHelper.GetIcon(searchResult.FullPath);
                            if (icon != null)
                            {
                                return icon;
                            }
                        }
                        catch
                        {
                            // Fallback icon
                        }
                        return new BitmapImage(new Uri("pack://application:,,,/Assets/file.png"));
                    case ResultType.Folder:
                        return new BitmapImage(new Uri("pack://application:,,,/Assets/folder.png"));
                    case ResultType.WebSearch:
                    case ResultType.RecentWebSearch:
                    case ResultType.Url: // CHANGED: Use the same icon for URLs.
                        return new BitmapImage(new Uri("pack://application:,,,/Assets/search_icon.png"));

                }
            }
            return new BitmapImage(new Uri("pack://application:,,,/Assets/file.png"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}