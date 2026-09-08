using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu;

public partial class MainWindow : Window
{
    private readonly GameViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _vm = new GameViewModel();
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
