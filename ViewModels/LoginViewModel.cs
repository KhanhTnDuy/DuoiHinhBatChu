using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.ViewModels;

/// <summary>
/// Màn đăng nhập: hai thẻ Đăng nhập / Đăng ký, cộng lối vào nhanh cho khách.
///
/// Chọn chế độ chơi cũng nằm ở đây:
///   - Chơi một mình: chạy offline, không cần tài khoản.
///   - Đấu nhiều người: cần tài khoản và cần máy chủ, sẽ mở ở phần sau
///     (xem Docs/MULTIPLAYER.md).
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly AccountService _accounts;
    private readonly AppSettings _settings;

    /// <summary>Bắn lên khi đã xác định được người chơi, để cửa sổ mở màn chơi.</summary>
    public event Action<Account>? LoggedIn;

    public LoginViewModel(AccountService accounts, AppSettings settings)
    {
        _accounts = accounts;
        _settings = settings;

        UserName = settings.LastUserName;
        // Chưa có tài khoản nào thì mở thẳng thẻ Đăng ký cho đỡ phải bấm thêm
        _isRegisterMode = !accounts.HasAnyAccount();

        LoginCommand = new RelayCommand(_ => Login());
        RegisterCommand = new RelayCommand(_ => Register());
        PlayAsGuestCommand = new RelayCommand(_ => PlayAsGuest());
        ShowLoginCommand = new RelayCommand(_ => IsRegisterMode = false);
        ShowRegisterCommand = new RelayCommand(_ => IsRegisterMode = true);
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
    }

    public RelayCommand LoginCommand { get; }
    public RelayCommand RegisterCommand { get; }
    public RelayCommand PlayAsGuestCommand { get; }
    public RelayCommand ShowLoginCommand { get; }
    public RelayCommand ShowRegisterCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    // ----- Ô nhập -----

    private string _userName = "";
    public string UserName
    {
        get => _userName;
        set { if (SetProperty(ref _userName, value)) Error = ""; }
    }

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// PasswordBox không cho ràng buộc hai chiều vào Password, nên phần code-behind
    /// của cửa sổ đẩy giá trị vào đây mỗi khi người dùng gõ.
    /// </summary>
    private string _password = "";
    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) Error = ""; }
    }

    private string _confirmPassword = "";
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    // ----- Trạng thái màn hình -----

    private bool _isRegisterMode;
    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set
        {
            if (!SetProperty(ref _isRegisterMode, value)) return;
            Error = "";
            OnPropertyChanged(nameof(IsLoginMode));
        }
    }

    public bool IsLoginMode => !IsRegisterMode;

    private string _error = "";
    public string Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public bool IsDarkTheme => _settings.IsDarkTheme;

    // ----- Hành động -----

    private void Login()
    {
        AuthResult result = _accounts.Login(UserName, Password);
        if (!result.Ok)
        {
            Error = result.Error;
            return;
        }

        Remember(result.Account!);
        LoggedIn?.Invoke(result.Account!);
    }

    private void Register()
    {
        AuthResult result = _accounts.Register(UserName, DisplayName, Password, ConfirmPassword);
        if (!result.Ok)
        {
            Error = result.Error;
            return;
        }

        Remember(result.Account!);
        LoggedIn?.Invoke(result.Account!);
    }

    /// <summary>Chơi ngay không tài khoản; tiến trình lưu chung vào hồ sơ "khách".</summary>
    private void PlayAsGuest() => LoggedIn?.Invoke(Account.Guest());

    private void Remember(Account account)
    {
        _settings.LastUserName = account.UserName;
        _settings.Save();
    }

    private void ToggleTheme()
    {
        _settings.IsDarkTheme = !_settings.IsDarkTheme;
        _settings.Save();
        ThemeService.Apply(_settings.IsDarkTheme);

        OnPropertyChanged(nameof(IsDarkTheme));
    }
}
