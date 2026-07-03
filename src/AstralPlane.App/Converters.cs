using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace AstralPlane_App;

/// <summary>Maps a bool to Visibility (true = Visible). Pass ConverterParameter="invert" to reverse.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool flag = value is bool b && b;
        if (parameter as string == "invert")
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Inverts a bool (for IsEnabled while running).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is bool b && !b;
}

/// <summary>Maps encoded thumbnail bytes to a BitmapImage for an Image source. Null → null.</summary>
public sealed class BytesToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
            return null;

        var image = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            writer.StoreAsync().GetAwaiter().GetResult();
            writer.FlushAsync().GetAwaiter().GetResult();
            writer.DetachStream();
        }
        stream.Seek(0);
        image.SetSource(stream);
        return image;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Maps a bool to opacity: true → dimmed (0.5), false → full (1.0). Used to dim unsupported tiles.</summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b && b ? 0.5 : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
