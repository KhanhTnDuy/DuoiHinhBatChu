using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu;

/// <summary>
/// Màn đấu nhiều người. Mở ra là vào thẳng phòng chờ; việc nối máy chủ do
/// view model tự làm lúc người chơi bấm tạo / vào phòng.
/// </summary>
public partial class MatchWindow : Window
{
    private readonly MatchViewModel _vm;

    /// <summary>Đang rời phòng dở thì đừng chạy lại lần nữa khi cửa sổ đóng thật.</summary>
    private bool _leaving;

    private readonly Account _account;
    private readonly AppSettings _settings;

    /// <summary>Đang quay về màn chế độ, nên lúc đóng KHÔNG được để app tắt.</summary>
    private bool _goingBack;

    public MatchWindow(Account account, AppSettings settings, LobbyMode mode)
    {
        InitializeComponent();

        _account = account;
        _settings = settings;

        _vm = new MatchViewModel(account, settings, mode);
        _vm.GoBack += GoBack;
        DataContext = _vm;
    }

    /// <summary>
    /// Về màn chọn chế độ: mở cửa sổ kia trước rồi mới đóng cửa sổ này, để app
    /// không tự thoát. Việc rời phòng vẫn do Window_Closing lo.
    /// </summary>
    private void GoBack()
    {
        if (_goingBack) return;
        _goingBack = true;

        var mode = new ModeWindow(_account, _settings);
        Application.Current.MainWindow = mode;
        mode.Show();
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// PasswordBox không cho ràng buộc dữ liệu vào Password, nên đẩy tay sang
    /// view model giống màn đăng nhập.
    /// </summary>
    private void RoomPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.RoomPassword = ((PasswordBox)sender).Password;

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
