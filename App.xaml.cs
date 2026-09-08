using System.Windows;

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
}
