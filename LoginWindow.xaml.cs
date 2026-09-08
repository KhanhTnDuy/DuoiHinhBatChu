using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;
    private readonly AppSettings _settings;

    public LoginWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        ThemeService.Apply(_settings.IsDarkTheme);

        _vm = new LoginViewModel(new AccountService(), _settings);
        _vm.LoggedIn += StartGame;
        DataContext = _vm;

        Loaded += (_, _) => UserNameBox.Focus();
    }

    /// <summary>
    /// PasswordBox không cho ràng buộc dữ liệu vào Password (cố ý, để mật khẩu
    /// không nằm lại trong bộ nhớ dưới dạng chuỗi), nên đẩy tay sang view model.
    /// </summary>
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.Password = ((PasswordBox)sender).Password;

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.ConfirmPassword = ((PasswordBox)sender).Password;

    /// <summary>
    /// Enter chạy đúng nút của thẻ đang mở. Không dùng IsDefault vì hai nút
    /// Đăng nhập và Tạo tài khoản cùng nằm trong cây, chỉ khác nhau ở Visibility.
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (_vm.IsRegisterMode) _vm.RegisterCommand.Execute(null);
        else _vm.LoginCommand.Execute(null);
    }

    /// <summary>Mở màn chơi rồi mới đóng cửa sổ này, để ứng dụng không tự thoát.</summary>
    private void StartGame(Account account)
    {
        var game = new MainWindow(account, _settings);
        Application.Current.MainWindow = game;
        game.Show();
        Close();
    }
}
