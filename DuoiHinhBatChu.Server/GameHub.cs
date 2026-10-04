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

    /// <summary>Đố nhau: thời gian để người ra đề chọn câu.</summary>
    private const double PickSeconds = 15;

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

    /// <summary>
    /// Mở phòng mới. Máy chủ sinh mã phòng; chủ phòng đặt mật khẩu và tên (tên
    /// không bắt buộc, chỉ để hiển thị). Mã trả về trong RoomState để đọc cho bạn bè.
    /// </summary>
    public async Task<RoomState> CreateRoom(string token, string name, string password)
    {
        Account account = Authenticate(token);

        name = (name ?? "").Trim();
        if (name.Length > Room.NameMax)
            throw new HubException($"Tên phòng tối đa {Room.NameMax} ký tự.");

        Room room = _rooms.CreateRoom(new Player
        {
            ConnectionId = Context.ConnectionId,
            AccountId = account.Id,
            DisplayName = account.DisplayName,
        }, name, password ?? "");

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
        _log.LogInformation("Mở phòng {Room} bởi {Name}", room.Code, account.DisplayName);

        return room.ToState();
    }

    /// <summary>Vào một phòng đang mở bằng mã và mật khẩu.</summary>
    public async Task<RoomState> JoinRoom(string token, string code, string password)
    {
        Account account = Authenticate(token);
        code = (code ?? "").Trim().ToUpperInvariant();

        Room room = _rooms.Find(code)
            ?? throw new HubException($"Không có phòng nào mã \"{code}\".");

        // So bằng thời gian cố định để không dò được mật khẩu qua độ trễ
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(room.Password), Encoding.UTF8.GetBytes(password ?? "")))
            throw new HubException("Sai mật khẩu phòng.");

        if (room.IsPlaying)
            throw new HubException("Phòng đang chơi dở, chờ ván này xong đã.");

        if (room.HasAccount(account.Id))
            throw new HubException("Tài khoản này đã ở trong phòng rồi.");

        // Kiểm tra ngay trước khi thêm: hai người cùng vào chỗ cuối thì người
        // sau vẫn có thể lọt — Room.Add sẽ chặn lần nữa bên trong khóa
        if (room.IsFull)
            throw new HubException($"Phòng đã đủ {Room.MaxPlayers} người.");

        if (!room.Add(new Player
        {
            ConnectionId = Context.ConnectionId,
            AccountId = account.Id,
            DisplayName = account.DisplayName,
        }))
            throw new HubException($"Phòng đã đủ {Room.MaxPlayers} người.");

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
        await Clients.Group(room.Code).SendAsync("RoomChanged", room.ToState());

        return room.ToState();
    }

    /// <summary>
    /// Bật / tắt sẵn sàng ở sảnh chờ. Ai cũng bấm được (kể cả chủ phòng, dù
    /// không bắt buộc); cả phòng nhận RoomChanged để thấy dấu tích cạnh tên.
    /// </summary>
    public async Task SetReady(string token, bool ready)
    {
        Account account = Authenticate(token);

        Room room = _rooms.FindByConnection(Context.ConnectionId)
            ?? throw new HubException("Bạn chưa ở trong phòng nào.");

        if (room.IsPlaying) return;

        Player? player = room.ByAccount(account.Id);
        if (player == null) return;

        player.IsReady = ready;
        await Clients.Group(room.Code).SendAsync("RoomChanged", room.ToState());
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
        await Clients.Group(room.Code).SendAsync("RoomChanged", room.ToState());
    }

    /// <summary>Chủ phòng bắt đầu ván. Cần ít nhất hai người.</summary>
    public async Task StartMatch(string token, int rounds)
    {
        Room room = HostedRoom(Authenticate(token));

        if (room.PlayerCount < 2)
            throw new HubException("Cần ít nhất 2 người mới đấu được.");

        if (!room.AllGuestsReady)
            throw new HubException("Còn người chưa bấm sẵn sàng.");

        if (room.IsPlaying)
            throw new HubException("Ván này đang chạy rồi.");

        // rounds giữ lại cho khớp client cũ, nhưng ván nay chạy tới khi hết mạng
        _rooms.StartMatch(room);
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

        Player? player = room.ByAccount(account.Id);
        if (player == null || !room.IsPlaying) return;

        AnswerResult result = _rooms.Judge(room, player, answer);
        await Clients.Group(room.Code).SendAsync("AnswerJudged", result);
    }

    /// <summary>
    /// Đố nhau: người ra đề chọn một câu trong số máy chủ đưa. Chỉ nhận đúng người
    /// ra đề, đúng lúc đang chọn, và đúng mã trong danh sách đã phát.
    /// </summary>
    public Task PickPuzzle(string token, string imageKey)
    {
        Account account = Authenticate(token);

        Room room = _rooms.FindByConnection(Context.ConnectionId)
            ?? throw new HubException("Bạn chưa ở trong phòng nào.");

        lock (room.Gate)
        {
            if (!room.IsPlaying || room.Mode != MatchMode.Duel || !room.IsPicking)
                throw new HubException("Chưa đến lúc chọn câu.");

            if (room.AskerAccountId != account.Id)
                throw new HubException("Không phải lượt bạn ra đề.");

            if (_rooms.FindPickOption(room, imageKey) == null)
                throw new HubException("Câu này không có trong danh sách chọn.");

            room.PickedKey = imageKey;
        }

        return Task.CompletedTask;
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
            int turn = 0;
            while (true)
            {
                RoundInfo? round = room.Mode == MatchMode.Duel
                    ? await PickAndBeginDuelRound(room, turn)
                    : _rooms.NextRound(room);
                if (round == null) break;

                Player? asker = room.Mode == MatchMode.Duel
                    ? room.ByAccount(round.AskerAccountId)
                    : null;

                await _hub.Clients.Group(room.Code).SendAsync("RoundStarted", round);

                // Kết thúc câu khi mọi người đã trả lời đúng, hoặc khi hết giờ
                DateTime deadline = room.RoundStartedUtc.AddSeconds(round.SecondsAllowed);
                while (DateTime.UtcNow < deadline &&
                       room.PlayerCount > 0 &&
                       room.AnyUnanswered())
                {
                    await Task.Delay(200);
                }

                // Đố nhau: người đoán không ra thì người ra đề được điểm
                int askerBonus = asker != null ? _rooms.AwardAskerIfUnsolved(room, asker) : 0;

                // Ai chưa trả lời đúng kịp giờ thì mất 1 mạng (người ra đề đã tính là trả lời)
                room.LoseLivesOfUnanswered();

                string answer = room.CurrentPuzzle?.Answer ?? "";
                await _hub.Clients.Group(room.Code)
                                .SendAsync("RoundEnded",
                                           new RoundEnded(round.RoundNumber, answer, room.Scores(), askerBonus));

                // Một người hết mạng, hoặc đối thủ đã rời phòng: dừng ván
                if (room.PlayerCount < 2 || room.AnyOutOfLives()) break;
                await Task.Delay(BreakBetweenRounds);
                turn++;
            }

            room.IsPlaying = false;
            await _hub.Clients.Group(room.Code).SendAsync("MatchEnded", new MatchEnded(room.Scores()));
        }
        catch (Exception ex)
        {
            room.IsPlaying = false;
            _log.LogError(ex, "Ván ở phòng {Room} dừng giữa chừng", room.Code);
        }
    }

    /// <summary>
    /// Đố nhau, đầu mỗi câu: người ra đề (luân phiên) nhận 6 câu để chọn, người kia
    /// chờ. Hết giờ chọn mà chưa chọn thì máy chủ chọn bừa. Trả về null khi hết kho
    /// câu hoặc không còn đủ hai người.
    /// </summary>
    private async Task<RoundInfo?> PickAndBeginDuelRound(Room room, int turn)
    {
        List<Player> players = room.Snapshot();
        if (players.Count < 2) return null;

        Player asker = players[turn % 2];

        PickOption[] options = _rooms.BuildPickOptions(room);
        if (options.Length == 0) return null;

        room.AskerAccountId = asker.AccountId;
        room.PickedKey = null;
        room.IsPicking = true;

        int number = room.RoundNumber + 1;
        foreach (Player p in players)
        {
            // Chỉ người ra đề nhận danh sách (có đáp án); người đoán nhận danh sách rỗng
            var info = new PickInfo(number, asker.AccountId, asker.DisplayName, PickSeconds,
                                    p == asker ? options : Array.Empty<PickOption>());
            await _hub.Clients.Client(p.ConnectionId).SendAsync("PickStarted", info);
        }

        DateTime until = DateTime.UtcNow.AddSeconds(PickSeconds);
        while (DateTime.UtcNow < until && room.PickedKey == null && room.PlayerCount >= 2)
            await Task.Delay(200);

        room.IsPicking = false;
        if (room.PlayerCount < 2) return null;

        Puzzle chosen = (room.PickedKey != null ? _rooms.FindPickOption(room, room.PickedKey) : null)
            ?? room.PickOptions[Random.Shared.Next(room.PickOptions.Count)];

        // Người ra đề có thể vừa rời phòng trong lúc chọn
        Player? current = room.ByAccount(asker.AccountId);
        return current == null ? null : _rooms.BeginDuelRound(room, chosen, current);
    }

    private async Task RemoveFromRoom(Room room, string connectionId)
    {
        Player? player = room.ByConnection(connectionId);
        if (player == null) return;

        _rooms.Remove(room, player);
        await Groups.RemoveFromGroupAsync(connectionId, room.Code);

        if (room.PlayerCount > 0)
            await Clients.Group(room.Code).SendAsync("RoomChanged", room.ToState());
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

    private Account Authenticate(string token) =>
        _tokens.Resolve(token) ?? throw new HubException("Vé đăng nhập không hợp lệ, đăng nhập lại.");
}
