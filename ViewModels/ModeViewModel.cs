using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.ViewModels;

/// <summary>
/// Màn chọn chế độ, mở ra sau khi đã đăng nhập.
///
///   - Cổ điển: chạy hẳn trên máy, ai cũng vào được kể cả khách.
///   - Đấu nhiều người: cần máy chủ và cần tài khoản thật, vì máy chủ mới là
///     bên chấm ai nhanh hơn.
///
/// Tài khoản trên máy chủ là một sổ riêng, không phải tài khoản lưu ở máy này.
/// Nên vào chế độ đấu phải đăng nhập thêm một lần vào máy chủ; tên đăng nhập
/// điền sẵn theo tài khoản đang dùng, lần đầu thì bấm tạo tài khoản trên đó.
/// </summary>
public class ModeViewModel : ViewModelBase
{
    private const string DefaultAddress = "localhost:5180";

    private readonly Account _account;
    private readonly AppSettings _settings;
    private readonly ServerClient _server = new();

    /// <summary>Tài khoản này đã có tiến trình lưu, tức là không phải lần đầu.</summary>
    private readonly bool _hasPlayedBefore;

    /// <summary>Bắn lên khi người chơi chọn chế độ Cổ điển.</summary>
    public event Action? StartSolo;

    /// <summary>
    /// Bắn lên khi đã nối được máy chủ và đăng nhập xong, kèm sẵn đường dây đã mở.
    /// </summary>
    public event Action<MatchClient, ServerClient, AuthResponse>? StartMatch;

    public ModeViewModel(Account account, AppSettings settings)
    {
        _account = account;
        _settings = settings;
        _serverAddress = settings.ServerAddress.Length > 0 ? settings.ServerAddress : DefaultAddress;
        _serverUserName = account.UserName;

        // Hỏi ngay lúc dựng màn, y như MenuViewModel: đây là màn đầu tiên sau
        // khi đăng nhập nên lúc này chắc chắn chưa ai kịp chơi thêm ván nào
        _hasPlayedBefore = new GameStateService(account.Id).HasPlayedBefore();

        PlaySoloCommand = new RelayCommand(_ => StartSolo?.Invoke());
        ShowOnlineCommand = new RelayCommand(_ => IsOnlinePanelOpen = true, _ => CanPlayOnline);
        HideOnlineCommand = new RelayCommand(_ => IsOnlinePanelOpen = false);
        CheckServerCommand = new RelayCommand(async _ => await CheckServerAsync());
        ServerLoginCommand = new RelayCommand(async _ => await EnterMatchAsync(register: false));
        ServerRegisterCommand = new RelayCommand(async _ => await EnterMatchAsync(register: true));
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
    }

    public RelayCommand PlaySoloCommand { get; }
    public RelayCommand ShowOnlineCommand { get; }
    public RelayCommand HideOnlineCommand { get; }
    public RelayCommand CheckServerCommand { get; }
    public RelayCommand ServerLoginCommand { get; }
    public RelayCommand ServerRegisterCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    /// <summary>
    /// Lời chào ở đầu màn chọn chế độ — cùng một câu với màn menu Cổ điển
    /// (<see cref="MenuViewModel.Greeting"/>), để hai màn nối nhau đọc liền mạch.
    /// </summary>
    public string Greeting => _hasPlayedBefore
        ? $"Chào mừng trở lại, {_account.DisplayName}!"
        : $"Chào mừng, {_account.DisplayName}!";

    /// <summary>
    /// Chữ cái đầu của tên, hiện trong ô vuông thay cho ảnh đại diện: "Nguyễn
    /// An" ra "NA", tên một chữ thì lấy một chữ cái.
    /// </summary>
    public string Initials
    {
        get
        {
            string[] words = _account.DisplayName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return words.Length switch
            {
                0 => "?",
                1 => words[0][..1].ToUpperInvariant(),
                _ => (words[0][..1] + words[^1][..1]).ToUpperInvariant(),
            };
        }
    }

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

    private string _serverUserName;
    /// <summary>Tên đăng nhập trên máy chủ; mặc định lấy theo tài khoản ở máy này.</summary>
    public string ServerUserName
    {
        get => _serverUserName;
        set => SetProperty(ref _serverUserName, value);
    }

    /// <summary>
    /// PasswordBox không ràng buộc hai chiều được, nên code-behind của cửa sổ
    /// đẩy giá trị vào đây mỗi lần người chơi gõ.
    /// </summary>
    private string _serverPassword = "";
    public string ServerPassword
    {
        get => _serverPassword;
        set => SetProperty(ref _serverPassword, value);
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

        RememberAddress();
    }

    /// <summary>
    /// Đăng nhập (hoặc đăng ký) trên máy chủ rồi mở luôn đường dây thời gian thực.
    /// Nối được ở đây thì màn đấu khỏi phải lo chuyện kết nối nữa.
    /// </summary>
    private async Task EnterMatchAsync(bool register)
    {
        if (IsChecking) return;

        if (ServerPassword.Length == 0)
        {
            IsServerOk = false;
            ServerMessage = "Nhập mật khẩu tài khoản trên máy chủ.";
            return;
        }

        IsChecking = true;
        ServerMessage = register ? "Đang tạo tài khoản..." : "Đang đăng nhập máy chủ...";

        ServerAuth auth = register
            // Số điện thoại lấy luôn của tài khoản trên máy này, để lỡ quên mật
            // khẩu máy chủ thì vẫn lấy lại được bằng đúng số đó
            ? await _server.RegisterAsync(ServerAddress, ServerUserName,
                                          _account.DisplayName, ServerPassword, _account.Phone)
            : await _server.LoginAsync(ServerAddress, ServerUserName, ServerPassword);

        if (!auth.Ok || auth.Auth == null)
        {
            IsServerOk = false;
            ServerMessage = auth.Message;
            IsChecking = false;
            return;
        }

        var client = new MatchClient(_server.BaseAddress, auth.Auth.Token, App.OnUiThread);

        try
        {
            await client.ConnectAsync();
        }
        catch (Exception ex)
        {
            await client.DisposeAsync();
            IsServerOk = false;
            ServerMessage = $"Đăng nhập được nhưng không mở được kênh đấu: {ex.Message}";
            IsChecking = false;
            return;
        }

        IsServerOk = true;
        ServerMessage = "Đã vào máy chủ.";
        IsChecking = false;

        // Mật khẩu không cần giữ lại sau khi đã có vé
        ServerPassword = "";
        RememberAddress();

        StartMatch?.Invoke(client, _server, auth.Auth);
    }

    private void RememberAddress()
    {
        _settings.ServerAddress = ServerAddress.Trim();
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
