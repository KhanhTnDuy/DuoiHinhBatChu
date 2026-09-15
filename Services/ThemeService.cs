using System.Windows;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Đổi giữa chế độ sáng và tối lúc đang chạy.
/// Hai file Themes/Light.xaml và Themes/Dark.xaml khai báo cùng một bộ khóa màu,
/// nên chỉ cần thay từ điển ở vị trí 0 là toàn bộ giao diện (dùng DynamicResource) tự đổi màu.
/// </summary>
public static class ThemeService
{
    private const int ThemeSlot = 0;

    public static bool IsDark { get; private set; }

    /// <summary>Bắn sau khi đổi giao diện sáng/tối, cho nơi nào phải nạp lại
    /// tài nguyên theo theme (ảnh nền màn đăng nhập).</summary>
    public static event Action? Changed;

    public static void Apply(bool dark)
    {
        IsDark = dark;

        var dict = new ResourceDictionary
        {
            Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative),
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > ThemeSlot) merged[ThemeSlot] = dict;
        else merged.Add(dict);

        Changed?.Invoke();
    }
}
