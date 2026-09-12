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
/// Màn này KHÔNG hỏi địa chỉ máy chủ hay tài khoản máy chủ nữa. Địa chỉ nằm
/// trong <see cref="AppSettings.ServerAddress"/>, còn việc nối và đăng nhập
/// máy chủ do màn đấu tự làm đúng lúc người chơi tạo / vào phòng — người chơi
/// chỉ thấy một việc: đặt tên phòng và mật khẩu phòng.
/// </summary>
public class ModeViewModel : ViewModelBase
{
    private readonly Account _account;
    private readonly AppSettings _settings;

    /// <summary>Tài khoản này đã có tiến trình lưu, tức là không phải lần đầu.</summary>
    private readonly bool _hasPlayedBefore;

    /// <summary>Bắn lên khi người chơi chọn chế độ Cổ điển.</summary>
    public event Action? StartSolo;

    /// <summary>
    /// Bắn lên khi người chơi chọn chế độ đấu nhiều người, kèm việc muốn làm
    /// đầu tiên trong phòng chờ: vào phòng có sẵn hay tự mở phòng.
    /// </summary>
    public event Action<LobbyMode>? StartMatch;

    /// <summary>Quay về màn đăng nhập (đăng xuất).</summary>
    public event Action? GoBack;

    public ModeViewModel(Account account, AppSettings settings)
    {
        _account = account;
        _settings = settings;

        // Hỏi ngay lúc dựng màn, y như MenuViewModel: đây là màn đầu tiên sau
        // khi đăng nhập nên lúc này chắc chắn chưa ai kịp chơi thêm ván nào
        _hasPlayedBefore = new GameStateService(account.Id).HasPlayedBefore();

        PlaySoloCommand = new RelayCommand(_ => StartSolo?.Invoke());
        JoinRoomCommand = new RelayCommand(_ => StartMatch?.Invoke(LobbyMode.Join), _ => CanPlayOnline);
        CreateRoomCommand = new RelayCommand(_ => StartMatch?.Invoke(LobbyMode.Create), _ => CanPlayOnline);
        BackCommand = new RelayCommand(_ => GoBack?.Invoke());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
    }

    public RelayCommand PlaySoloCommand { get; }
    public RelayCommand JoinRoomCommand { get; }
    public RelayCommand CreateRoomCommand { get; }
    public RelayCommand BackCommand { get; }
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

    public bool IsDarkTheme => _settings.IsDarkTheme;

    private void ToggleTheme()
    {
        _settings.IsDarkTheme = !_settings.IsDarkTheme;
        _settings.Save();
        ThemeService.Apply(_settings.IsDarkTheme);

        OnPropertyChanged(nameof(IsDarkTheme));
    }
}
