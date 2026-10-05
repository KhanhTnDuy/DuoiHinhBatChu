using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu;

public partial class MainWindow : Window
{
    private readonly GameViewModel? _vm;

    /// <param name="account">Tài khoản vừa đăng nhập ở LoginWindow.</param>
    /// <param name="settings">Tùy chọn chung, dùng chung với màn đăng nhập.</param>
    public MainWindow(Account account, AppSettings settings)
    {
        InitializeComponent();

        try
        {
            _vm = new GameViewModel(account, settings);
        }
        catch (Exception ex)
        {
            // Hay gặp nhất: thư mục Assets/CauHoi chưa có ảnh nào.
            // Báo bằng hộp thoại rồi thoát, thay vì để cửa sổ hỏng.
            MessageBox.Show(ex.Message, "Không mở được màn chơi",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            Loaded += (_, _) => Close();
            return;
        }

        DataContext = _vm;

        // Lớp phủ (tạm dừng, chọn lối chơi, hỏi kim cương) đóng lại thì kéo
        // focus về cửa sổ. Nút vừa bấm trong lớp phủ bị ẩn đi nhưng vẫn giữ
        // focus bàn phím, và chữ gõ sau đó rơi vào cái nút vô hình ấy chứ
        // không tới Window_KeyDown — bot thử 2026-09-15 gõ mà ô không nhận.
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GameViewModel.IsPaused)
                                or nameof(GameViewModel.IsChoosingOrder)
                                or nameof(GameViewModel.IsConfirmingHelp)
                                or nameof(GameViewModel.IsGameOver)
                                or nameof(GameViewModel.IsFinished))
                Dispatcher.BeginInvoke(() => Keyboard.Focus(this));
        };
        Loaded += (_, _) => Keyboard.Focus(this);
    }

    /// <summary>
    /// ESC bật/tắt tạm dừng chứ không đóng thẳng màn chơi nữa: đang chơi dở mà
    /// lỡ tay là mất luôn ván thì tiếc.
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_vm == null)
        {
            if (e.Key == Key.Escape) Close();   // màn chơi mở không được, ESC là lối thoát duy nhất
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (_vm.IsPaused) _vm.ResumeCommand.Execute(null);
            else _vm.PauseCommand.Execute(null);
            return;
        }

        // Bàn phím vật lý: chữ điền vào ô, Backspace lấy ra, Enter trả lời —
        // giống màn Đấu. View model tự từ chối khi đang tạm dừng, đang hỏi
        // kim cương, đang chọn ô để mở…
        LetterKeys.Handle(e, _vm);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object sender, CancelEventArgs e) => _vm?.Save();
}
