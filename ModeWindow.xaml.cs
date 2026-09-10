using System.Windows;
using System.Windows.Controls;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu;

/// <summary>
/// Màn chọn chế độ chơi, mở ngay sau khi đăng nhập xong.
/// </summary>
public partial class ModeWindow : Window
{
    private readonly Account _account;
    private readonly AppSettings _settings;
    private readonly ModeViewModel _vm;

    public ModeWindow(Account account, AppSettings settings)
    {
        InitializeComponent();

        _account = account;
        _settings = settings;

        _vm = new ModeViewModel(account, settings);
        _vm.StartSolo += StartSolo;
        _vm.StartMatch += StartMatch;
        DataContext = _vm;
    }

    /// <summary>
    /// Chọn Cổ điển thì sang MENU, không vào thẳng màn chơi: ở đó người
    /// chơi còn chọn chơi tiếp hay chơi mới, và xem bảng xếp hạng.
    ///
    /// Mở cửa sổ mới rồi mới đóng cửa sổ này, để ứng dụng không tự thoát.
    /// </summary>
    private void StartSolo()
    {
        var menu = new MenuWindow(_account, _settings);
        Application.Current.MainWindow = menu;
        menu.Show();
        Close();
    }

    /// <summary>
    /// Sang màn đấu, mang theo đường dây đã mở sẵn. Tên hiện trong phòng lấy
    /// theo tài khoản trên máy chủ, vì đó mới là tên người khác nhìn thấy.
    /// </summary>
    private void StartMatch(MatchClient client, ServerClient server, AuthResponse auth)
    {
        var match = new MatchWindow(client, server, _settings, auth.AccountId, auth.DisplayName);
        Application.Current.MainWindow = match;
        match.Show();
        Close();
    }

    /// <summary>
    /// PasswordBox không cho ràng buộc dữ liệu vào Password, nên đẩy tay sang
    /// view model giống màn đăng nhập.
    /// </summary>
    private void ServerPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.ServerPassword = ((PasswordBox)sender).Password;
}
