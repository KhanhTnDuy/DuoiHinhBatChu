using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
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

        LoadQrImage();
        LoadBackdrop();
        ThemeService.Changed += LoadBackdrop;
        Closed += (_, _) => ThemeService.Changed -= LoadBackdrop;

        Loaded += (_, _) => UserNameBox.Focus();
    }

    /// <summary>Tên tài nguyên của ảnh mã QR trong Assets/TaiNguyen.</summary>
    private const string QrResourceName = "qr-ung-ho";

    /// <summary>Ảnh nền cột trái màn đăng nhập, bản sáng và bản tối.
    /// Thiếu bản tối thì dùng bản sáng cho cả hai.</summary>
    private const string BackdropResourceName = "dang-nhap-nen";
    private const string BackdropDarkResourceName = "dang-nhap-nen-toi";

    /// <summary>
    /// Nạp ảnh QR ủng hộ từ thư mục ảnh tài nguyên.
    ///
    /// Chưa có ảnh thì cứ để nguyên ô gạch đứt chỉ chỗ đặt — thiếu ảnh không
    /// phải là lý do để cả màn đăng nhập hỏng.
    /// </summary>
    private void LoadQrImage()
    {
        var image = LoadBitmap(QrResourceName);
        if (image == null) return;

        QrImage.Source = image;
        QrImage.Visibility = Visibility.Visible;
        QrPlaceholder.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Nạp ảnh nền cột trái theo giao diện đang dùng. Không có ảnh thì cột
    /// trái giữ nguyên nền mờ + bầu trời như trước, dải mờ sau logo cũng tắt.
    /// </summary>
    private void LoadBackdrop()
    {
        BitmapImage? image = null;
        if (ThemeService.IsDark) image = LoadBitmap(BackdropDarkResourceName);
        image ??= LoadBitmap(BackdropResourceName);

        LoginBackdrop.Source = image;
        LoginBackdropScrim.Visibility = image == null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Đọc một ảnh tài nguyên thành BitmapImage đã đóng băng; null nếu không có
    /// file hoặc file không phải ảnh.
    /// </summary>
    private static BitmapImage? LoadBitmap(string resourceName)
    {
        string? file = AppImageLocator.Find(resourceName);
        if (file == null) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            // OnLoad: đọc hết ảnh vào bộ nhớ rồi nhả file ra ngay, không thì
            // file bị giữ và người chơi không thay ảnh khác vào được
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(file);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            // File hỏng hay không phải ảnh thật: coi như không có
            return null;
        }
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
        // Bảng hướng dẫn đang che màn hình thì ESC đóng nó, Enter không chạy nút nào
        if (_vm.IsHelpOpen)
        {
            if (e.Key is Key.Escape or Key.Enter) _vm.IsHelpOpen = false;
            return;
        }

        if (e.Key != Key.Enter) return;

        if (_vm.IsRegisterMode) _vm.RegisterCommand.Execute(null);
        else if (_vm.IsForgotMode) _vm.ResetPasswordCommand.Execute(null);
        else _vm.LoginCommand.Execute(null);
    }

    /// <summary>
    /// Sang màn chọn chế độ. Mở cửa sổ mới trước rồi mới đóng cửa sổ này,
    /// để ứng dụng không coi là đã hết cửa sổ và tự thoát.
    /// </summary>
    private void StartGame(Account account)
    {
        var mode = new ModeWindow(account, _settings);
        Application.Current.MainWindow = mode;
        mode.Show();
        Close();
    }
}
