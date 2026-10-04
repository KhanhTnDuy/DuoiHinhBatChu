using System.Windows;
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
        _vm.GoBack += GoBack;
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

    /// <summary>Đăng xuất: về màn đăng nhập. Mở cửa sổ mới trước rồi mới đóng.</summary>
    private void GoBack()
    {
        var login = new LoginWindow();
        Application.Current.MainWindow = login;
        login.Show();
        Close();
    }

    /// <summary>
    /// Sang màn đấu ngay, chưa nối máy chủ. Màn đấu tự nối và đăng nhập máy
    /// chủ lúc người chơi bấm tạo / vào phòng.
    /// </summary>
    private void StartMatch(LobbyMode mode)
    {
        var match = new MatchWindow(_account, _settings, mode);
        Application.Current.MainWindow = match;
        match.Show();
        Close();
    }
}
