using System.Windows;
using System.Windows.Threading;

namespace DuoiHinhBatChu;

/// <summary>
/// Ứng dụng khởi động ở màn đăng nhập; LoginWindow tự mở MainWindow sau khi
/// xác định được người chơi (tài khoản thật hoặc khách).
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new LoginWindow().Show();
    }

    /// <summary>
    /// Chạy một việc trên luồng giao diện.
    ///
    /// Tin từ máy chủ tới ở luồng nền, mà WPF chỉ cho sửa dữ liệu đang ràng buộc
    /// từ luồng của nó, nên chỗ nào nhận tin cũng phải đi qua đây.
    /// </summary>
    public static void OnUiThread(Action job)
    {
        Dispatcher dispatcher = Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        if (dispatcher.CheckAccess()) job();
        else dispatcher.Invoke(job);
    }
}
