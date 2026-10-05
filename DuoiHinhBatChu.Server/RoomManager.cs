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

    /// <summary>Số mạng còn lại; mất 1 mạng mỗi câu không trả lời đúng kịp giờ.</summary>
    public int Lives { get; set; } = Room.StartingLives;

    /// <summary>Đã bấm sẵn sàng ở sảnh chờ. Về false khi ván bắt đầu, để ván sau phải bấm lại.</summary>
    public bool IsReady { get; set; }

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
        new(AccountId, DisplayName, AccountId == hostAccountId, IsReady, Score, Lives);
}

/// <summary>
/// Một phòng đấu: mã do máy chủ sinh + mật khẩu do chủ phòng đặt.
///
/// Mã sinh ngẫu nhiên nên không bao giờ trùng và không đoán được; mật khẩu là
/// lớp thứ hai để người lạ trên cùng mạng có nghe được mã cũng không vào được.
/// </summary>
public class Room
{
    /// <summary>Sức chứa, tính cả chủ phòng: thi đấu chỉ có hai người.</summary>
    public const int MaxPlayers = 2;

    /// <summary>Số mạng mỗi người lúc bắt đầu ván, bằng chế độ Cổ điển.</summary>
    public const int StartingLives = 5;

    /// <summary>Bỏ các ký tự dễ đọc nhầm: 0/O, 1/I.</summary>
    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public const int NameMax = 20;

    public required string Code { get; init; }

    /// <summary>Tên do chủ phòng đặt, chỉ để hiển thị (vào phòng vẫn bằng mã). Có thể rỗng.</summary>
    public required string Name { get; init; }

    /// <summary>Rỗng nghĩa là phòng mở, ai biết mã cũng vào được.</summary>
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

    /// <summary>Thứ tự câu đã bốc sẵn cho cả ván, để mọi người nhận cùng một bộ.</summary>
    public List<Puzzle> Order { get; } = new();

    /// <summary>Đố nhau: người đang ra đề, các câu được chọn, và câu đã chọn (null = chưa chọn).</summary>
    public string AskerAccountId { get; set; } = "";
    public List<Puzzle> PickOptions { get; } = new();
    public volatile string? PickedKey;
    public volatile bool IsPicking;

    /// <summary>Mã các câu đã ra trong ván này, để không lặp lại.</summary>
    public HashSet<string> UsedPuzzleIds { get; } = new();

    public Puzzle? CurrentPuzzle { get; set; }
    public PuzzleRound? CurrentRound { get; set; }

    /// <summary>Mốc máy chủ phát câu hiện tại; mọi thời gian trả lời đo từ đây.</summary>
    public DateTime RoundStartedUtc { get; set; }

    /// <summary>Khóa cho mỗi phòng, vì nhiều người có thể gửi đáp án cùng lúc.</summary>
    public object Gate { get; } = new();

    public static string NewCode(Random rng) =>
        new(Enumerable.Range(0, 6).Select(_ => CodeChars[rng.Next(CodeChars.Length)]).ToArray());

    // ----- Lối vào danh sách người chơi, tất cả đều khóa -----

    public int PlayerCount { get { lock (Gate) return _players.Count; } }

    /// <summary>Bản chụp danh sách người chơi theo thứ tự vào phòng, để duyệt không lo bị sửa giữa chừng.</summary>
    public List<Player> Snapshot() { lock (Gate) return _players.ToList(); }

    /// <summary>Mọi người TRỪ chủ phòng đã sẵn sàng chưa — chủ phòng bấm bắt đầu tức là đã sẵn sàng.</summary>
    public bool AllGuestsReady
    {
        get { lock (Gate) return _players.All(p => p.IsReady || p.AccountId == HostAccountId); }
    }

    /// <summary>Xóa cờ sẵn sàng của mọi người, gọi lúc ván bắt đầu.</summary>
    public void ClearReady()
    {
        lock (Gate) foreach (Player p in _players) p.IsReady = false;
    }

    /// <summary>Thêm người; trả về false khi phòng đã đầy (kiểm tra và thêm trong cùng một khóa).</summary>
    public bool Add(Player player)
    {
        lock (Gate)
        {
            if (_players.Count >= MaxPlayers) return false;
            _players.Add(player);
            return true;
        }
    }

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
        lock (Gate)
            foreach (Player p in _players)
            {
                p.Score = 0;
                p.Lives = StartingLives;
            }
    }

    /// <summary>Hết câu: ai chưa trả lời đúng thì mất 1 mạng.</summary>
    public void LoseLivesOfUnanswered()
    {
        lock (Gate)
            foreach (Player p in _players)
                if (!p.AnsweredThisRound && p.Lives > 0) p.Lives--;
    }

    /// <summary>Có ai hết mạng chưa — điều kiện kết thúc ván.</summary>
    public bool AnyOutOfLives()
    {
        lock (Gate) return _players.Any(p => p.Lives <= 0);
    }

    public RoomState ToState()
    {
        lock (Gate)
            return new RoomState(Code, Name, HostAccountId, Mode, IsPlaying, RoundNumber, MaxPlayers,
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

        // Mỗi ảnh một mã ngẫu nhiên, sinh lại mỗi lần máy chủ khởi động: client
        // tải ảnh bằng mã này chứ không bằng tên file. Tên file chính là đáp án
        // ("SÓNG CHÓ.png"), gửi thẳng xuống là đưa đáp án cho ai chịu khó đọc
        // gói tin — trái với nguyên tắc "đáp án không rời máy chủ" của ván đấu.
        foreach (Puzzle p in _puzzles)
        {
            string key = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
            _imageKeys[key] = p.ImageName;
            _keysByImage[p.ImageName] = key;
        }
    }

    /// <summary>Mã tải ảnh (ngẫu nhiên) → tên file ảnh thật trong bảng.</summary>
    private readonly Dictionary<string, string> _imageKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Chiều ngược lại của <see cref="_imageKeys"/>. Giữ sẵn cả hai chiều vì
    /// mỗi câu phát ra đều phải tra mã, mà dò ngược trong từ điển là quét cả bộ
    /// — riêng lúc dựng 6 câu cho người ra đề chọn đã là 6 lần quét.
    /// </summary>
    private readonly Dictionary<string, string> _keysByImage = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Mã tải ảnh của một câu, gửi cho client thay cho tên file.</summary>
    private string ImageKey(Puzzle puzzle) => _keysByImage[puzzle.ImageName];

    public int PuzzleCount => _puzzles.Count;
    public int RoomCount => _rooms.Count;

    /// <summary>Mở phòng với mã mới sinh; mật khẩu do chủ phòng đặt.</summary>
    public Room CreateRoom(Player host, string name, string password)
    {
        Room room;
        do
        {
            room = new Room
            {
                Code = Room.NewCode(_rng),
                Name = name,
                Password = password,
                HostAccountId = host.AccountId,
            };
        } while (!_rooms.TryAdd(room.Code, room));   // TryAdd: hai phòng cùng sinh một mã thì thử lại

        room.Add(host);
        return room;
    }

    public Room? Find(string code) =>
        _rooms.TryGetValue(code, out Room? r) ? r : null;

    /// <summary>Tìm phòng theo mã kết nối, dùng khi ai đó rớt mạng.</summary>
    public Room? FindByConnection(string connectionId) =>
        _rooms.Values.FirstOrDefault(r => r.ByConnection(connectionId) != null);

    /// <summary>
    /// Bỏ một người khỏi phòng. Phòng trống thì xóa luôn; chủ phòng rời đi thì
    /// người vào sớm nhất còn lại lên làm chủ (việc đó do <see cref="Room.Remove"/> lo).
    /// </summary>
    public void Remove(Room room, Player player)
    {
        if (room.Remove(player)) _rooms.TryRemove(room.Code, out _);
    }

    /// <summary>
    /// Xáo ngẫu nhiên cả bộ câu cho ván. Ván không đếm số câu: chạy tới khi một
    /// người hết mạng (hoặc hết bộ câu).
    /// </summary>
    public void StartMatch(Room room)
    {
        room.Order.Clear();
        room.Order.AddRange(_puzzles.OrderBy(_ => _rng.Next()));

        room.RoundNumber = 0;
        room.IsPlaying = true;
        room.UsedPuzzleIds.Clear();
        room.AskerAccountId = "";
        room.IsPicking = false;

        room.ResetScores();
        room.ClearReady();   // ván sau phải bấm sẵn sàng lại
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
        return BeginRound(room, puzzle, null);
    }

    /// <summary>
    /// Dựng bản tin cho một câu đã biết. Đố nhau: <paramref name="asker"/> là người ra
    /// đề, được tính là "đã trả lời" sẵn để câu chỉ chờ người kia đoán.
    /// </summary>
    private RoundInfo BeginRound(Room room, Puzzle puzzle, Player? asker)
    {
        PuzzleRound round = PuzzleRound.Create(
            puzzle.Answer,
            _puzzles.Where(p => p != puzzle).Select(p => p.Answer),
            _rng);

        room.CurrentPuzzle = puzzle;
        room.CurrentRound = round;
        room.RoundStartedUtc = DateTime.UtcNow;

        room.ResetAnswers();
        if (asker != null) asker.AnsweredThisRound = true;

        return new RoundInfo(
            room.RoundNumber,
            ImageKey(puzzle),
            round.WordLengths,
            new string(round.Tiles.Select(t => t.Character).ToArray()),
            puzzle.Difficulty,
            MatchScoring.MaxSeconds,
            puzzle.Category,
            asker?.AccountId ?? "",
            asker?.DisplayName ?? "");
    }

    // ----- Đố nhau -----

    /// <summary>Số câu để người ra đề chọn.</summary>
    public const int PickCount = 6;

    /// <summary>Bốc ngẫu nhiên các câu chưa ra trong ván cho người ra đề chọn. Rỗng khi hết kho.</summary>
    public PickOption[] BuildPickOptions(Room room)
    {
        room.PickOptions.Clear();
        room.PickOptions.AddRange(_puzzles
            .Where(p => !room.UsedPuzzleIds.Contains(p.Id))
            .OrderBy(_ => _rng.Next())
            .Take(PickCount));

        return room.PickOptions
            .Select(p => new PickOption(ImageKey(p), p.Answer, p.Category))
            .ToArray();
    }

    /// <summary>Câu trong danh sách chọn ứng với mã ảnh, null nếu mã lạ.</summary>
    public Puzzle? FindPickOption(Room room, string imageKey) =>
        room.PickOptions.FirstOrDefault(p => ImageKey(p) == imageKey);

    /// <summary>Người ra đề đã chọn (hoặc hết giờ chọn thì bốc bừa một câu): dựng câu và phát.</summary>
    public RoundInfo BeginDuelRound(Room room, Puzzle puzzle, Player asker)
    {
        room.UsedPuzzleIds.Add(puzzle.Id);
        room.RoundNumber++;
        return BeginRound(room, puzzle, asker);
    }

    /// <summary>
    /// Hết câu Đố nhau: người đoán không ra thì người ra đề được điểm. Trả về số điểm đã cộng.
    /// </summary>
    public int AwardAskerIfUnsolved(Room room, Player asker)
    {
        lock (room.Gate)
        {
            bool solved = room.Snapshot().Any(p => p != asker && p.AnsweredThisRound);
            if (solved || room.CurrentPuzzle == null) return 0;

            int bonus = MatchScoring.AskerPoints(room.CurrentPuzzle.Difficulty);
            asker.Score += bonus;
            return bonus;
        }
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
                return new AnswerResult(player.AccountId, player.DisplayName, false, 0, seconds, 0, Judged: false);

            // Còn trong thời gian phạt của lần sai trước: không chấm gì cả, chỉ
            // nhắc còn phải chờ bao lâu. Chặn ở đây chứ không ở client, vì
            // client là thứ người ta thay được.
            if (now < player.BlockedUntilUtc)
            {
                double waitLeft = (player.BlockedUntilUtc - now).TotalSeconds;
                return new AnswerResult(player.AccountId, player.DisplayName, false, 0,
                                        seconds, waitLeft, Judged: false);
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
    /// Lấy byte ảnh của một câu theo MÃ tải ảnh (xem <see cref="ImageKey"/>), để
    /// phục vụ GET ảnh. Mã lạ thì trả về null. Byte ảnh đọc thẳng từ cơ sở dữ
    /// liệu mỗi lần hỏi, không giữ trong bộ nhớ.
    /// </summary>
    public (byte[] Bytes, string ContentType)? Image(string imageKey) =>
        _imageKeys.TryGetValue(imageKey, out string? imageName)
            ? _repository.LoadImageByName(imageName)
            : null;
}
