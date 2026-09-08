using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DuoiHinhBatChu.Converters;

/// <summary>
/// true -> Visible, false -> Collapsed.
/// Truyền ConverterParameter="Invert" để đảo ngược (dùng cho phần hiện khi CHƯA có ảnh).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (parameter as string == "Invert") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Chuỗi rỗng -> Collapsed, có chữ -> Visible.</summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
