using System.Windows;
using System.Windows.Controls;

namespace DuoiHinhBatChu.Converters;

/// <summary>
/// Chữ mờ trong ô nhập ("Mã phòng", "Mật khẩu phòng"…), thay cho nhãn và câu
/// hướng dẫn đặt bên ngoài ô. Bấm vào ô là chữ mờ biến mất, rời ô mà chưa gõ
/// gì thì hiện lại — kiểu placeholder quen mắt trên web.
///
/// Dùng: <c>conv:Placeholder.Text="Mã phòng"</c> trên TextBox / PasswordBox
/// có style <c>Field</c> / <c>PasswordField</c>. TextBox tự biết mình trống
/// (trigger trên Text); PasswordBox không lộ nội dung qua thuộc tính ràng buộc
/// được, nên lớp này nghe <c>PasswordChanged</c> và cập nhật
/// <see cref="HasContentProperty"/> cho template dùng.
/// </summary>
public static class Placeholder
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(Placeholder),
                                            new PropertyMetadata("", OnTextChanged));

    public static string GetText(DependencyObject d) => (string)d.GetValue(TextProperty);
    public static void SetText(DependencyObject d, string value) => d.SetValue(TextProperty, value);

    /// <summary>Ô đang có nội dung — chỉ PasswordBox cần, TextBox có sẵn Text.</summary>
    public static readonly DependencyProperty HasContentProperty =
        DependencyProperty.RegisterAttached("HasContent", typeof(bool), typeof(Placeholder),
                                            new PropertyMetadata(false));

    public static bool GetHasContent(DependencyObject d) => (bool)d.GetValue(HasContentProperty);
    public static void SetHasContent(DependencyObject d, bool value) => d.SetValue(HasContentProperty, value);

    /// <summary>Gắn chữ mờ cho PasswordBox là bắt đầu theo dõi nội dung của nó.</summary>
    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box || GetTracked(box)) return;

        SetTracked(box, true);
        box.PasswordChanged += (_, _) => SetHasContent(box, box.Password.Length > 0);
        SetHasContent(box, box.Password.Length > 0);
    }

    private static readonly DependencyProperty TrackedProperty =
        DependencyProperty.RegisterAttached("Tracked", typeof(bool), typeof(Placeholder),
                                            new PropertyMetadata(false));

    private static bool GetTracked(DependencyObject d) => (bool)d.GetValue(TrackedProperty);
    private static void SetTracked(DependencyObject d, bool value) => d.SetValue(TrackedProperty, value);
}
