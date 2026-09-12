using System.Collections.Concurrent;
using System.Security.Cryptography;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.Server;

/// <summary>Một người đang ngồi trong phòng.</summary>
public class Player
{
    public required string ConnectionId { get; set; }
    public required string AccountId { get; init; }
    public required string DisplayName { get; init; }
    public int Score { get; set; }

    /// <summary>Đã trả lời đúng câu hiện tại, không cho ghi điểm hai lần.</summary>
    public bool AnsweredThisRound { get; set; }

    /// <summary>Số lần đoán sai trong câu hiện tại.</summary>
    public int WrongThisRound { get; set; }

    /// <summary>
    /// Đoán sai xong thì phải chờ tới mốc này mới được gửi tiếp.
    ///
    /// Không có mốc này thì đoán sai chẳng mất gì: một client tự viết có thể
    /// bắn vài nghìn đáp án mỗi giây cho tới lúc trúng, mà vẫn còn gần như
    /// nguyên điểm tốc độ. Chờ càng lâu khi càng sai nhiều, nên đoán bừa vài
    /// lần thì không sao, còn dò máy móc thì hết cửa.
    /// </summary>
    public DateTime BlockedUntilUtc { get; set; }

    /// <summary>Dọn trạng thái cho một câu mới.</summary>
    public void NewRound()
    {
        AnsweredThisRound = false;
        WrongThisRound = 0;
        BlockedUntilUtc = DateTime.MinValue;
    }

    public PlayerInfo ToInfo(string hostAccountId) =>
        new(AccountId, DisplayName, AccountId == hostAccountId, Score);
}

/// <summary>
/// Một phòng đấu, nhận diện bằng TÊN do chủ phòng tự đặt.
///
/// Trước đây máy chủ phát mã 6 ký tự ngẫu nhiên; đổi sang tên tự đặt vì bạn bè
/// rủ nhau bằng miệng thì "vào phòng LopA1" dễ nhớ hơn "vào phòng K7XQ2M".
/// Tên tự đặt thì ai cũng đoán được, nên kèm thêm mật khẩu để người lạ trên
/// cùng mạng không nhảy vào giữa ván.
/// </summary>
public class Room
{
    public const int NameMax = 20;

    public required string Name { get; init; }

    /// <summary>Rỗng nghĩa là phòng mở, ai biết tên cũng vào được.</summary>
    public required string Password { get; init; }

    public required string HostAccountId { get; set; }

    /// <summary>Kiểu chơi, chủ phòng đổi được khi chưa vào ván.</summary>
    public MatchMode Mode { get; set; } = MatchMode.Compete;

    /// <summary>
    /// Người trong phòng. ĐỂ RIÊNG TƯ có lý do: ván đấu chạy nền (GameHub.RunMatch)
    /// duyệt danh sách này mỗi 200ms, trong khi lời gọi JoinRoom / rớt mạng lại
    /// sửa nó từ luồng khác. Một cú vào phòng đúng lúc vòng lặp đang duyệt là
    /// "Collection was modified" — ván chết giữa chừng, mọi người treo ở màn chờ.
    /// Nên mọi lối đọc và ghi đều phải đi qua các hàm bên dưới, tất cả nằm
    /// trong <see cref="Gate"/>.
    /// </summary>
    private readonly List<Player> _players = new();

    public bool IsPlaying { get; set; }
    public int RoundNumber { get; set; }
    public int TotalRounds { get; set; }

    /// <summary>Thứ tự câu đã bốc sẵn cho cả ván, để mọi người nhận cùng một bộ.</summary>
    public List<Puzzle> Order { get; } = new();

    public Puzzle? CurrentPuzzle { get; set; }
    public PuzzleRound? CurrentRound { get; set; }

    /// <summary>Mốc máy chủ phát câu hiện tại; mọi thời gian trả lời đo từ đây.</summary>
    public DateTime RoundStartedUtc { get; set; }

    /// <summary>Khóa cho mỗi phòng, vì nhiều người có thể gửi đáp án cùng lúc.</summary>
    public object Gate { get; } = new();


    // ----- Lối vào danh sách người chơi, tất cả đều khóa -----

    public int PlayerCount { get { lock (Gate) return _players.Count; } }

    public void Add(Player player) { lock (Gate) _players.Add(player); }

    /// <summary>Bỏ một người ra; trả về true khi phòng không còn ai.</summary>
    public bool Remove(Player player)
    {
        lock (Gate)
        {
            _players.Remove(player);
            if (_players.Count == 0) return true;

            // Chủ phòng rời đi thì người vào sớm nhất còn lại lên thay
            if (HostAccountId == player.AccountId) HostAccountId = _players[0].AccountId;
            return false;
        }
    }

    public bool HasAccount(string accountId)
    {
        lock (Gate) return _players.Any(p => p.AccountId == accountId);
    }

    public Player? ByAccount(string accountId)
    {
        lock (Gate) return _players.FirstOrDefault(p => p.AccountId == accountId);
    }

    public Player? ByConnection(string connectionId)
    {
        lock (Gate) return _players.FirstOrDefault(p => p.ConnectionId == connectionId);
    }

    /// <summary>Bắt đầu một câu mới: xóa dấu "đã trả lời" của mọi người.</summary>
    public void ResetAnswers()
    {
        lock (Gate) foreach (Player p in _players) p.NewRound();
    }

    /// <summary>Còn ai chưa trả lời đúng không — điều kiện để câu chạy tiếp.</summary>
    public bool AnyUnanswered()
    {
        lock (Gate) return _players.Any(p => !p.AnsweredThisRound);
    }

    public void ResetScores()
    {
        lock (Gate) foreach (Player p in _players) p.Score = 0;
    }

    public RoomState ToState()
    {
        lock (Gate)
            return new RoomState(Name, HostAccountId, Mode, IsPlaying, RoundNumber, TotalRounds,
                                 _players.Select(p => p.ToInfo(HostAccountId)).ToList());
    }

    public IReadOnlyList<PlayerInfo> Scores()
    {
        lock (Gate)
            return _players
                .OrderByDescending(p => p.Score)
                .Select(p => p.ToInfo(HostAccountId))
                .ToList();
    }
}

/// <summary>
/// Giữ toàn bộ phòng đang mở. Đây là bên duy nhất biết đáp án của câu đang chạy
/// — client chỉ nhận số ô và bộ phím chữ.
/// </summary>
public class RoomManager
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Puzzle> _puzzles;
    private readonly PuzzleRepository _repository;
    private readonly Random _rng = new();

    public RoomManager(PuzzleRepository puzzles)
    {
        _repository = puzzles;

        // Danh sách câu giữ luôn trong bộ nhớ vì mỗi ván đấu bốc câu liên tục;
        // riêng byte ảnh thì KHÔNG giữ, chỉ lấy từ cơ sở dữ liệu khi có người tải
        _puzzles = puzzles.LoadAll();
    }

    public int PuzzleCount => _puzzles.Count;
    public int RoomCount => _rooms.Count;

    /// <summary>
    /// Mở phòng theo tên chủ phòng đặt. Trả về null khi tên đã có người dùng —
    /// dùng TryAdd chứ không kiểm tra rồi mới thêm, vì hai người có thể cùng
    /// mở một tên trong cùng một khoảnh khắc.
    /// </summary>
    public Room? CreateRoom(Player host, string name, string password)
    {
        var room = new Room { Name = name, Password = password, HostAccountId = host.AccountId };
        room.Add(host);

        return _rooms.TryAdd(name, room) ? room : null;
    }

    /// <summary>Tên phòng không phân biệt hoa thường.</summary>
    public Room? Find(string name) =>
        _rooms.TryGetValue(name, out Room? r) ? r : null;

    /// <summary>Tìm phòng theo mã kết nối, dùng khi ai đó rớt mạng.</summary>
    public Room? FindByConnection(string connectionId) =>
        _rooms.Values.FirstOrDefault(r => r.ByConnection(connectionId) != null);

    /// <summary>
    /// Bỏ một người khỏi phòng. Phòng trống thì xóa luôn; chủ phòng rời đi thì
    /// người vào sớm nhất còn lại lên làm chủ (việc đó do <see cref="Room.Remove"/> lo).
    /// </summary>
    public void Remove(Room room, Player player)
    {
        if (room.Remove(player)) _rooms.TryRemove(room.Name, out _);
    }

    /// <summary>Bốc ngẫu nhiên danh sách câu cho cả ván.</summary>
    public void StartMatch(Room room, int rounds)
    {
        room.Order.Clear();
        room.Order.AddRange(_puzzles.OrderBy(_ => _rng.Next()).Take(rounds));

        room.TotalRounds = room.Order.Count;
        room.RoundNumber = 0;
        room.IsPlaying = true;

        room.ResetScores();
    }

    /// <summary>Dựng câu tiếp theo, hoặc null khi đã hết ván.</summary>
    public RoundInfo? NextRound(Room room)
    {
        if (room.RoundNumber >= room.Order.Count)
        {
            room.IsPlaying = false;
            room.CurrentPuzzle = null;
            room.CurrentRound = null;
            return null;
        }

        Puzzle puzzle = room.Order[room.RoundNumber];
        room.RoundNumber++;

        PuzzleRound round = PuzzleRound.Create(
            puzzle.Answer,
            _puzzles.Where(p => p != puzzle).Select(p => p.Answer),
            _rng);

        room.CurrentPuzzle = puzzle;
        room.CurrentRound = round;
        room.RoundStartedUtc = DateTime.UtcNow;

        room.ResetAnswers();

        return new RoundInfo(
            room.RoundNumber,
            room.TotalRounds,
            puzzle.ImageName,
            round.WordLengths,
            new string(round.Tiles.Select(t => t.Character).ToArray()),
            puzzle.Difficulty,
            MatchScoring.MaxSeconds);
    }

    /// <summary>Sai lần thứ n thì phải chờ chừng này giây: 1, 2, 3… tối đa 5.</summary>
    private static double Cooldown(int wrongCount) => Math.Min(wrongCount, 5);

    /// <summary>
    /// Chấm một đáp án. Thời gian lấy từ đồng hồ máy chủ, không nhận số client gửi lên.
    /// </summary>
    public AnswerResult Judge(Room room, Player player, string guess)
    {
        lock (room.Gate)
        {
            DateTime now = DateTime.UtcNow;
            double seconds = (now - room.RoundStartedUtc).TotalSeconds;

            if (room.CurrentPuzzle == null || player.AnsweredThisRound)
                return new AnswerResult(player.AccountId, player.DisplayName, false, 0, seconds, 0);

            // Còn trong thời gian phạt của lần sai trước: không chấm gì cả, chỉ
            // nhắc còn phải chờ bao lâu. Chặn ở đây chứ không ở client, vì
            // client là thứ người ta thay được.
            if (now < player.BlockedUntilUtc)
            {
                double waitLeft = (player.BlockedUntilUtc - now).TotalSeconds;
                return new AnswerResult(player.AccountId, player.DisplayName, false, 0,
                                        seconds, waitLeft);
            }

            bool correct = AnswerChecker.IsCorrect(guess, room.CurrentPuzzle);
            if (!correct)
            {
                player.WrongThisRound++;
                double wait = Cooldown(player.WrongThisRound);
                player.BlockedUntilUtc = now.AddSeconds(wait);

                return new AnswerResult(player.AccountId, player.DisplayName, false, 0,
                                        seconds, wait);
            }

            int points = MatchScoring.Points(room.CurrentPuzzle.Difficulty, seconds);
            player.Score += points;
            player.AnsweredThisRound = true;

            return new AnswerResult(player.AccountId, player.DisplayName, true, points, seconds, 0);
        }
    }

    /// <summary>
    /// Lấy byte ảnh của một câu theo tên file, để phục vụ GET ảnh.
    /// Đọc thẳng từ cơ sở dữ liệu mỗi lần hỏi, không có thì trả về null.
    /// </summary>
    public (byte[] Bytes, string ContentType)? Image(string imageName) =>
        _repository.LoadImageByName(imageName);
}
