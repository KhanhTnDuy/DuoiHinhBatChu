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
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object sender, CancelEventArgs e) => _vm?.Save();
}
