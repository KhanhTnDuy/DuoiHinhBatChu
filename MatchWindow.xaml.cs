using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using DuoiHinhBatChu.Services;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu;

/// <summary>
/// Màn đấu nhiều người. Cửa sổ này chỉ mở khi đã nối được máy chủ và đã có vé
/// đăng nhập, nên trong đây không còn phải lo chuyện kết nối nữa.
/// </summary>
public partial class MatchWindow : Window
{
    private readonly MatchViewModel _vm;

    /// <summary>Đang rời phòng dở thì đừng chạy lại lần nữa khi cửa sổ đóng thật.</summary>
    private bool _leaving;

    public MatchWindow(MatchClient client, ServerClient server, AppSettings settings,
                       string accountId, string displayName)
    {
        InitializeComponent();

        _vm = new MatchViewModel(client, server, settings, accountId, displayName);
        DataContext = _vm;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Rời phòng là việc phải chờ máy chủ, mà Closing thì không chờ được. Nên
    /// hoãn đóng lại, báo cho máy chủ xong mới đóng thật.
    /// </summary>
    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_leaving) return;

        e.Cancel = true;
        _leaving = true;

        await _vm.LeaveAsync();
        Close();
    }
}
