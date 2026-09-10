using System.Windows;
using System.Windows.Input;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu;

/// <summary>
/// Menu của chế độ Cổ điển. Chọn chế độ xong là tới đây, chứ không vào
/// thẳng màn chơi nữa.
///
/// Cửa sổ này không đóng khi vào chơi mà chỉ ẩn đi, rồi hiện lại lúc màn chơi
/// đóng — nhờ vậy thoát khỏi ván là về đúng menu, và ứng dụng không bị coi là
/// hết cửa sổ mà tự tắt.
/// </summary>
public partial class MenuWindow : Window
{
    private readonly Account _account;
    private readonly AppSettings _settings;
    private readonly MenuViewModel _vm;

    public MenuWindow(Account account, AppSettings settings)
    {
        InitializeComponent();

        _account = account;
        _settings = settings;

        _vm = new MenuViewModel(account, settings);
        _vm.StartGame += StartGame;
        _vm.GoBack += GoBack;
        DataContext = _vm;
    }

    /// <summary>ESC đóng lớp phủ đang mở; không có lớp nào thì quay lại chọn chế độ.</summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (_vm.IsLeaderboardOpen) _vm.HideLeaderboardCommand.Execute(null);
        else if (_vm.IsConfirmingNewGame) _vm.CancelNewGameCommand.Execute(null);
        else GoBack();
    }

    private void StartGame()
    {
        var game = new MainWindow(_account, _settings);

        // Màn chơi đóng thì hiện lại menu và đọc lại tiến trình vừa lưu
        game.Closed += (_, _) =>
        {
            _vm.Refresh();
            Application.Current.MainWindow = this;
            Show();
            Activate();
        };

        Application.Current.MainWindow = game;
        Hide();
        game.Show();
    }

    /// <summary>Quay lại màn chọn chế độ: mở cửa sổ kia trước rồi mới đóng cửa sổ này.</summary>
    private void GoBack()
    {
        var mode = new ModeWindow(_account, _settings);
        Application.Current.MainWindow = mode;
        mode.Show();
        Close();
    }
}
