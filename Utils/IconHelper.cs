using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClearView.Utils
{
    public static class IconHelper
    {
        public static ImageSource? GetIcon(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;

                using (Icon? icon = Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null) return null;
                    return Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch
            {
                return null;
            }
        }
    }
}