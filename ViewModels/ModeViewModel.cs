using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.ViewModels;

/// <summary>
/// Màn chọn chế độ, mở ra sau khi đã đăng nhập.
///
///   - Chơi một mình: chạy hẳn trên máy, ai cũng vào được kể cả khách.
///   - Đấu nhiều người: cần máy chủ và cần tài khoản thật, vì máy chủ mới là
///     bên chấm ai nhanh hơn.
/// </summary>
public class ModeViewModel : ViewModelBase
{
    private const string DefaultAddress = "localhost:5180";

    private readonly Account _account;
    private readonly AppSettings _settings;
    private readonly ServerClient _server = new();

    /// <summary>Bắn lên khi người chơi chọn chế độ một mình.</summary>
    public event Action? StartSolo;

    public ModeViewModel(Account account, AppSettings settings)
    {
        _account = account;
        _settings = settings;
        _serverAddress = settings.ServerAddress.Length > 0 ? settings.ServerAddress : DefaultAddress;

        PlaySoloCommand = new RelayCommand(_ => StartSolo?.Invoke());
        ShowOnlineCommand = new RelayCommand(_ => IsOnlinePanelOpen = true, _ => CanPlayOnline);
        HideOnlineCommand = new RelayCommand(_ => IsOnlinePanelOpen = false);
        CheckServerCommand = new RelayCommand(async _ => await CheckServerAsync());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
    }

    public RelayCommand PlaySoloCommand { get; }
    public RelayCommand ShowOnlineCommand { get; }
    public RelayCommand HideOnlineCommand { get; }
    public RelayCommand CheckServerCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    public string PlayerName => _account.DisplayName;

    public string AccountKindText => _account.IsGuest ? "Chơi khách - offline" : "Đã đăng nhập";

    /// <summary>Khách không đấu được: máy chủ cần một tài khoản thật để ghi điểm.</summary>
    public bool CanPlayOnline => !_account.IsGuest;

    public string OnlineBlockedText => CanPlayOnline
        ? ""
        : "Đang chơi khách nên chưa đấu được. Thoát ra đăng ký một tài khoản rồi quay lại.";

    private bool _isOnlinePanelOpen;
    public bool IsOnlinePanelOpen
    {
        get => _isOnlinePanelOpen;
        private set => SetProperty(ref _isOnlinePanelOpen, value);
    }

    private string _serverAddress;
    public string ServerAddress
    {
        get => _serverAddress;
        set => SetProperty(ref _serverAddress, value);
    }

    private string _serverMessage = "";
    public string ServerMessage
    {
        get => _serverMessage;
        private set => SetProperty(ref _serverMessage, value);
    }

    private bool _isServerOk;
    public bool IsServerOk
    {
        get => _isServerOk;
        private set => SetProperty(ref _isServerOk, value);
    }

    private bool _isChecking;
    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }

    public bool IsDarkTheme => _settings.IsDarkTheme;

    public string ThemeToggleText => IsDarkTheme ? "Chế độ sáng" : "Chế độ tối";

    private async Task CheckServerAsync()
    {
        if (IsChecking) return;

        IsChecking = true;
        ServerMessage = "Đang thử nối...";

        ServerStatus status = await _server.CheckAsync(ServerAddress);

        IsServerOk = status.Ok;
        ServerMessage = status.Message;
        IsChecking = false;

        if (!status.Ok) return;

        _settings.ServerAddress = ServerAddress.Trim();
        _settings.Save();
    }

    private void ToggleTheme()
    {
        _settings.IsDarkTheme = !_settings.IsDarkTheme;
        _settings.Save();
        ThemeService.Apply(_settings.IsDarkTheme);

        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeToggleText));
    }
}
