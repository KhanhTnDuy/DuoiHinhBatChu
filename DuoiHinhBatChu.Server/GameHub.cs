using System.Security.Cryptography;
using System.Text;
using DuoiHinhBatChu.Models;
using Microsoft.AspNetCore.SignalR;

namespace DuoiHinhBatChu.Server;

/// <summary>
/// Kênh thời gian thực của một ván đấu.
///
/// Luồng chạy:
///   CreateRoom / JoinRoom  -> mọi người trong phòng nhận RoomChanged
///   StartMatch (chủ phòng) -> máy chủ phát RoundStarted cho từng câu
///   SubmitAnswer           -> máy chủ chấm, phát AnswerJudged
///   hết câu / hết giờ      -> RoundEnded, rồi câu sau; hết ván thì MatchEnded
///
/// Đáp án không bao giờ rời máy chủ trước khi câu kết thúc.
/// </summary>
public class GameHub : Hub
{
    /// <summary>Nghỉ giữa hai câu, đủ để mọi người đọc đáp án vừa rồi.</summary>
    private static readonly TimeSpan BreakBetweenRounds = TimeSpan.FromSeconds(3);

    private readonly RoomManager _rooms;
    private readonly TokenService _tokens;
    private readonly ILogger<GameHub> _log;

    /// <summary>
    /// Đường phát tin không phụ thuộc một lời gọi nào.
    ///
    /// Ván đấu chạy nền lâu hơn lời gọi StartMatch, mà bản thân Hub bị hủy ngay
    /// khi lời gọi đó trả về — dùng Clients của Hub ở đó là ván chết giữa chừng.
    /// </summary>
    private readonly IHubContext<GameHub> _hub;

    public GameHub(RoomManager rooms, TokenService tokens, IHubContext<GameHub> hub,
                   ILogger<GameHub> log)
    {
        _rooms = rooms;
        _tokens = tokens;
        _hub = hub;
        _log = log;
    }

    /// <summary>Mở phòng mới với tên và mật khẩu chủ phòng tự đặt.</summary>
    public async Task<RoomState> CreateRoom(string token, string name, string password)
    {
        Account account = Authenticate(token);
        name = CleanRoomName(name);

        Room room = _rooms.CreateRoom(new Player
        {
            ConnectionId = Context.ConnectionId,
            AccountId = account.Id,
            DisplayName = account.DisplayName,
        }, name, password ?? "")
            ?? throw new HubException($"Đã có phòng tên \"{name}\" rồi, chọn tên khác.");

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Name);
        _log.LogInformation("Mở phòng {Room} bởi {Name}", room.Name, account.DisplayName);

        return room.ToState();
    }

    /// <summary>Vào một phòng đang mở bằng tên và mật khẩu.</summary>
    public async Task<RoomState> JoinRoom(string token, string name, string password)
    {
        Account account = Authenticate(token);
        name = CleanRoomName(name);

        Room room = _rooms.Find(name)
            ?? throw new HubException($"Không có phòng nào tên \"{name}\".");

        // So bằng thời gian cố định để không dò được mật khẩu qua độ trễ
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(room.Password), Encoding.UTF8.GetBytes(password ?? "")))
            throw new HubException("Sai mật khẩu phòng.");

        if (room.IsPlaying)
            throw new HubException("Phòng đang chơi dở, chờ ván này xong đã.");

        if (room.HasAccount(account.Id))
            throw new HubException("Tài khoản này đã ở trong phòng rồi.");

        room.Add(new Player
        {
            ConnectionId = Context.ConnectionId,
            AccountId = account.Id,
            DisplayName = account.DisplayName,
        });

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Name);
        await Clients.Group(room.Name).SendAsync("RoomChanged", room.ToState());

        return room.ToState();
    }

    public async Task LeaveRoom(string token)
    {
        Room? room = _rooms.FindByConnection(Context.ConnectionId);
        if (room == null) return;

        await RemoveFromRoom(room, Context.ConnectionId);
    }

    /// <summary>
    /// Chủ phòng chọn kiểu chơi. Cả phòng nhận RoomChanged để người chờ thấy
    /// mình sắp chơi kiểu gì — họ không chọn được, chỉ xem.
    /// </summary>
    public async Task SetMode(string token, MatchMode mode)
    {
        Room room = HostedRoom(Authenticate(token));

        if (room.IsPlaying)
            throw new HubException("Đang giữa ván, xong ván này rồi đổi.");

        if (!Enum.IsDefined(mode))
            throw new HubException("Kiểu chơi không hợp lệ.");

        room.Mode = mode;
        await Clients.Group(room.Name).SendAsync("RoomChanged", room.ToState());
    }

    /// <summary>Chủ phòng bắt đầu ván. Cần ít nhất hai người.</summary>
    public async Task StartMatch(string token, int rounds)
    {
        Room room = HostedRoom(Authenticate(token));

        if (room.PlayerCount < 2)
            throw new HubException("Cần ít nhất 2 người mới đấu được.");

        if (room.IsPlaying)
            throw new HubException("Ván này đang chạy rồi.");

        if (room.Mode == MatchMode.Draw)
            throw new HubException("Kiểu \"Tôi vẽ bạn đoán\" đang xây dựng, tạm chọn Thi đấu.");

        _rooms.StartMatch(room, Math.Clamp(rounds, 1, 20));
        await Clients.Group(room.Name).SendAsync("RoomChanged", room.ToState());

        // Chạy nền để lời gọi StartMatch trả về ngay, không giữ kết nối của chủ phòng
        _ = RunMatch(room);
    }

    /// <summary>
    /// Gửi đáp án. Máy chủ tự đo thời gian từ mốc phát câu — client không được
    /// khai thời gian của mình.
    /// </summary>
    public async Task SubmitAnswer(string token, string answer)
    {
        Account account = Authenticate(token);

        Room room = _rooms.FindByConnection(Context.ConnectionId)
            ?? throw new HubException("Bạn chưa ở trong phòng nào.");

        Player? player = room.ByAccount(account.Id);
        if (player == null || !room.IsPlaying) return;

        AnswerResult result = _rooms.Judge(room, player, answer);
        await Clients.Group(room.Name).SendAsync("AnswerJudged", result);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Room? room = _rooms.FindByConnection(Context.ConnectionId);
        if (room != null) await RemoveFromRoom(room, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    // ----- Nội bộ -----

    /// <summary>Phát lần lượt từng câu cho tới hết ván.</summary>
    private async Task RunMatch(Room room)
    {
        try
        {
            while (true)
            {
                RoundInfo? round = _rooms.NextRound(room);
                if (round == null) break;

                await _hub.Clients.Group(room.Name).SendAsync("RoundStarted", round);

                // Kết thúc câu khi mọi người đã trả lời đúng, hoặc khi hết giờ
                DateTime deadline = room.RoundStartedUtc.AddSeconds(round.SecondsAllowed);
                while (DateTime.UtcNow < deadline &&
                       room.PlayerCount > 0 &&
                       room.AnyUnanswered())
                {
                    await Task.Delay(200);
                }

                string answer = room.CurrentPuzzle?.Answer ?? "";
                await _hub.Clients.Group(room.Name)
                                .SendAsync("RoundEnded",
                                           new RoundEnded(round.RoundNumber, answer, room.Scores()));

                if (room.PlayerCount == 0) break;
                await Task.Delay(BreakBetweenRounds);
            }

            room.IsPlaying = false;
            await _hub.Clients.Group(room.Name).SendAsync("MatchEnded", new MatchEnded(room.Scores()));
        }
        catch (Exception ex)
        {
            room.IsPlaying = false;
            _log.LogError(ex, "Ván ở phòng {Room} dừng giữa chừng", room.Name);
        }
    }

    private async Task RemoveFromRoom(Room room, string connectionId)
    {
        Player? player = room.ByConnection(connectionId);
        if (player == null) return;

        _rooms.Remove(room, player);
        await Groups.RemoveFromGroupAsync(connectionId, room.Name);

        if (room.PlayerCount > 0)
            await Clients.Group(room.Name).SendAsync("RoomChanged", room.ToState());
    }

    /// <summary>Phòng mà người này đang LÀM CHỦ; không ở phòng nào hoặc không phải chủ thì từ chối.</summary>
    private Room HostedRoom(Account account)
    {
        Room room = _rooms.FindByConnection(Context.ConnectionId)
            ?? throw new HubException("Bạn chưa ở trong phòng nào.");

        if (room.HostAccountId != account.Id)
            throw new HubException("Chỉ chủ phòng mới làm được việc này.");

        return room;
    }

    /// <summary>Tên phòng: bỏ khoảng trắng thừa, bắt buộc có, không quá dài.</summary>
    private static string CleanRoomName(string? name)
    {
        name = (name ?? "").Trim();

        if (name.Length == 0)
            throw new HubException("Đặt tên phòng đã.");

        if (name.Length > Room.NameMax)
            throw new HubException($"Tên phòng tối đa {Room.NameMax} ký tự.");

        return name;
    }

    private Account Authenticate(string token) =>
        _tokens.Resolve(token) ?? throw new HubException("Vé đăng nhập không hợp lệ, đăng nhập lại.");
}
