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

    public ModeWindow(Account account, AppSettings settings)
    {
        InitializeComponent();

        _account = account;
        _settings = settings;

        var vm = new ModeViewModel(account, settings);
        vm.StartSolo += StartSolo;
        DataContext = vm;
    }

    /// <summary>Mở màn chơi rồi mới đóng cửa sổ này, để ứng dụng không tự thoát.</summary>
    private void StartSolo()
    {
        var game = new MainWindow(_account, _settings);
        Application.Current.MainWindow = game;
        game.Show();
        Close();
    }
}
