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

    public GameHub(RoomManager rooms, TokenService tokens, ILogger<GameHub> log)
    {
        _rooms = rooms;
        _tokens = tokens;
        _log = log;
    }

    /// <summary>Mở phòng mới, trả về mã 6 ký tự để đọc cho người khác vào.</summary>
    public async Task<RoomState> CreateRoom(string token)
    {
        Account account = Authenticate(token);

        Room room = _rooms.CreateRoom(new Player
        {
            ConnectionId = Context.ConnectionId,
            AccountId = account.Id,
            DisplayName = account.DisplayName,
        });

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
        _log.LogInformation("Mở phòng {Code} bởi {Name}", room.Code, account.DisplayName);

        return room.ToState();
    }

    /// <summary>Vào một phòng đang mở bằng mã.</summary>
    public async Task<RoomState> JoinRoom(string token, string code)
    {
        Account account = Authenticate(token);

        Room room = _rooms.Find(code)
            ?? throw new HubException("Không tìm thấy phòng nào có mã này.");

        if (room.IsPlaying)
            throw new HubException("Phòng đang chơi dở, chờ ván này xong đã.");

        if (room.Players.Any(p => p.AccountId == account.Id))
            throw new HubException("Tài khoản này đã ở trong phòng rồi.");

        room.Players.Add(new Player
        {
            ConnectionId = Context.ConnectionId,
            AccountId = account.Id,
            DisplayName = account.DisplayName,
        });

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
        await Clients.Group(room.Code).SendAsync("RoomChanged", room.ToState());

        return room.ToState();
    }

    public async Task LeaveRoom(string token)
    {
        Room? room = _rooms.FindByConnection(Context.ConnectionId);
        if (room == null) return;

        await RemoveFromRoom(room, Context.ConnectionId);
    }

    /// <summary>Chủ phòng bắt đầu ván. Cần ít nhất hai người.</summary>
    public async Task StartMatch(string token, int rounds)
    {
        Account account = Authenticate(token);

        Room room = _rooms.FindByConnection(Context.ConnectionId)
            ?? throw new HubException("Bạn chưa ở trong phòng nào.");

        if (room.HostAccountId != account.Id)
            throw new HubException("Chỉ chủ phòng mới bắt đầu được.");

        if (room.Players.Count < 2)
            throw new HubException("Cần ít nhất 2 người mới đấu được.");

        if (room.IsPlaying)
            throw new HubException("Ván này đang chạy rồi.");

        _rooms.StartMatch(room, Math.Clamp(rounds, 1, 20));
        await Clients.Group(room.Code).SendAsync("RoomChanged", room.ToState());

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

        Player? player = room.Players.FirstOrDefault(p => p.AccountId == account.Id);
        if (player == null || !room.IsPlaying) return;

        AnswerResult result = _rooms.Judge(room, player, answer);
        await Clients.Group(room.Code).SendAsync("AnswerJudged", result);
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

                await Clients.Group(room.Code).SendAsync("RoundStarted", round);

                // Kết thúc câu khi mọi người đã trả lời đúng, hoặc khi hết giờ
                DateTime deadline = room.RoundStartedUtc.AddSeconds(round.SecondsAllowed);
                while (DateTime.UtcNow < deadline &&
                       room.Players.Count > 0 &&
                       room.Players.Any(p => !p.AnsweredThisRound))
                {
                    await Task.Delay(200);
                }

                string answer = room.CurrentPuzzle?.Answer ?? "";
                await Clients.Group(room.Code)
                             .SendAsync("RoundEnded",
                                        new RoundEnded(round.RoundNumber, answer, room.Scores()));

                if (room.Players.Count == 0) break;
                await Task.Delay(BreakBetweenRounds);
            }

            room.IsPlaying = false;
            await Clients.Group(room.Code).SendAsync("MatchEnded", new MatchEnded(room.Scores()));
        }
        catch (Exception ex)
        {
            room.IsPlaying = false;
            _log.LogError(ex, "Ván ở phòng {Code} dừng giữa chừng", room.Code);
        }
    }

    private async Task RemoveFromRoom(Room room, string connectionId)
    {
        Player? player = room.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (player == null) return;

        _rooms.Remove(room, player);
        await Groups.RemoveFromGroupAsync(connectionId, room.Code);

        if (room.Players.Count > 0)
            await Clients.Group(room.Code).SendAsync("RoomChanged", room.ToState());
    }

    private Account Authenticate(string token) =>
        _tokens.Resolve(token) ?? throw new HubException("Vé đăng nhập không hợp lệ, đăng nhập lại.");
}
