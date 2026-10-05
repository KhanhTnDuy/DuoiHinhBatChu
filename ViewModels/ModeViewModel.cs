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
public class ModeViewModel : ThemedViewModel
{
    private readonly Account _account;

    /// <summary>Bắn lên khi người chơi chọn chế độ Cổ điển.</summary>
    public event Action? StartSolo;

    /// <summary>
    /// Bắn lên khi người chơi chọn chế độ đấu nhiều người, kèm việc muốn làm
    /// đầu tiên trong phòng chờ: vào phòng có sẵn hay tự mở phòng.
    /// </summary>
    public event Action<LobbyMode>? StartMatch;

    /// <summary>Quay về màn đăng nhập (đăng xuất).</summary>
    public event Action? GoBack;

    public ModeViewModel(Account account, AppSettings settings) : base(settings)
    {
        _account = account;

        // Hỏi ngay lúc dựng màn, y như MenuViewModel: đây là màn đầu tiên sau
        // khi đăng nhập nên lúc này chắc chắn chưa ai kịp chơi thêm ván nào
        Greeting = account.Greeting(new GameStateService(account.Id).HasPlayedBefore());

        PlaySoloCommand = new RelayCommand(_ => StartSolo?.Invoke());
        JoinRoomCommand = new RelayCommand(_ => StartMatch?.Invoke(LobbyMode.Join), _ => CanPlayOnline);
        CreateRoomCommand = new RelayCommand(_ => StartMatch?.Invoke(LobbyMode.Create), _ => CanPlayOnline);
        BackCommand = new RelayCommand(_ => GoBack?.Invoke());
    }

    public RelayCommand PlaySoloCommand { get; }
    public RelayCommand JoinRoomCommand { get; }
    public RelayCommand CreateRoomCommand { get; }
    public RelayCommand BackCommand { get; }

    /// <summary>Lời chào ở đầu màn, chốt một lần lúc dựng màn.</summary>
    public string Greeting { get; }

    public string Initials => _account.Initials;

    /// <summary>Khách không đấu được: máy chủ cần một tài khoản thật để ghi điểm.</summary>
    public bool CanPlayOnline => !_account.IsGuest;

    public string OnlineBlockedText => CanPlayOnline
        ? ""
        : "Đang chơi khách nên chưa đấu được. Thoát ra đăng ký một tài khoản rồi quay lại.";
}
