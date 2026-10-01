using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Uno.ViewModels;

public class BitmapAssetValueConverter : IValueConverter
{
    public static readonly BitmapAssetValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string rawUri && !string.IsNullOrEmpty(rawUri))
        {
            try
            {
                var uri = new Uri(rawUri);
                if (AssetLoader.Exists(uri))
                {
                    return new Bitmap(AssetLoader.Open(uri));
                }
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}