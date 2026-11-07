using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
namespace Uno.ViewModels;

public class BitmapAssetValueConverter : IValueConverter
{
    public static readonly BitmapAssetValueConverter Instance = new();
    private static readonly Dictionary<string, Bitmap> _cache = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string rawUri && !string.IsNullOrEmpty(rawUri))
        {
            try
            {
                if (_cache.TryGetValue(rawUri, out var cached)) return cached;
                var uri = new Uri(rawUri);
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    var bitmap = new Bitmap(stream);
                    _cache[rawUri] = bitmap;
                    return bitmap;
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