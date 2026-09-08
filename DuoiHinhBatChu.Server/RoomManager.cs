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

    public PlayerInfo ToInfo(string hostAccountId) =>
        new(AccountId, DisplayName, AccountId == hostAccountId, Score);
}

/// <summary>Một phòng đấu, nhận diện bằng mã 6 ký tự.</summary>
public class Room
{
    /// <summary>Bỏ các ký tự dễ đọc nhầm: 0/O, 1/I.</summary>
    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public required string Code { get; init; }
    public required string HostAccountId { get; set; }

    public List<Player> Players { get; } = new();

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

    public static string NewCode(Random rng) =>
        new(Enumerable.Range(0, 6).Select(_ => CodeChars[rng.Next(CodeChars.Length)]).ToArray());

    public RoomState ToState() => new(
        Code, HostAccountId, IsPlaying, RoundNumber, TotalRounds,
        Players.Select(p => p.ToInfo(HostAccountId)).ToList());

    public IReadOnlyList<PlayerInfo> Scores() => Players
        .OrderByDescending(p => p.Score)
        .Select(p => p.ToInfo(HostAccountId))
        .ToList();
}

/// <summary>
/// Giữ toàn bộ phòng đang mở. Đây là bên duy nhất biết đáp án của câu đang chạy
/// — client chỉ nhận số ô và bộ phím chữ.
/// </summary>
public class RoomManager
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Puzzle> _puzzles;
    private readonly Random _rng = new();

    public RoomManager(PuzzleRepository puzzles)
    {
        _puzzles = puzzles.LoadAll();
    }

    public int PuzzleCount => _puzzles.Count;
    public int RoomCount => _rooms.Count;

    public Room CreateRoom(Player host)
    {
        string code;
        do { code = Room.NewCode(_rng); } while (_rooms.ContainsKey(code));

        var room = new Room { Code = code, HostAccountId = host.AccountId };
        room.Players.Add(host);
        _rooms[code] = room;

        return room;
    }

    public Room? Find(string code) =>
        _rooms.TryGetValue(code, out Room? r) ? r : null;

    /// <summary>Tìm phòng theo mã kết nối, dùng khi ai đó rớt mạng.</summary>
    public Room? FindByConnection(string connectionId) =>
        _rooms.Values.FirstOrDefault(r => r.Players.Any(p => p.ConnectionId == connectionId));

    /// <summary>
    /// Bỏ một người khỏi phòng. Phòng trống thì xóa luôn; chủ phòng rời đi thì
    /// người vào sớm nhất còn lại lên làm chủ.
    /// </summary>
    public void Remove(Room room, Player player)
    {
        room.Players.Remove(player);

        if (room.Players.Count == 0)
        {
            _rooms.TryRemove(room.Code, out _);
            return;
        }

        if (room.HostAccountId == player.AccountId)
            room.HostAccountId = room.Players[0].AccountId;
    }

    /// <summary>Bốc ngẫu nhiên danh sách câu cho cả ván.</summary>
    public void StartMatch(Room room, int rounds)
    {
        room.Order.Clear();
        room.Order.AddRange(_puzzles.OrderBy(_ => _rng.Next()).Take(rounds));

        room.TotalRounds = room.Order.Count;
        room.RoundNumber = 0;
        room.IsPlaying = true;

        foreach (Player p in room.Players) p.Score = 0;
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

        foreach (Player p in room.Players) p.AnsweredThisRound = false;

        return new RoundInfo(
            room.RoundNumber,
            room.TotalRounds,
            Path.GetFileName(puzzle.Image),
            round.WordLengths,
            new string(round.Tiles.Select(t => t.Character).ToArray()),
            puzzle.Difficulty,
            MatchScoring.MaxSeconds);
    }

    /// <summary>
    /// Chấm một đáp án. Thời gian lấy từ đồng hồ máy chủ, không nhận số client gửi lên.
    /// </summary>
    public AnswerResult Judge(Room room, Player player, string guess)
    {
        lock (room.Gate)
        {
            double seconds = (DateTime.UtcNow - room.RoundStartedUtc).TotalSeconds;

            if (room.CurrentPuzzle == null || player.AnsweredThisRound)
                return new AnswerResult(player.AccountId, player.DisplayName, false, 0, seconds);

            bool correct = AnswerChecker.IsCorrect(guess, room.CurrentPuzzle);
            if (!correct)
                return new AnswerResult(player.AccountId, player.DisplayName, false, 0, seconds);

            int points = MatchScoring.Points(room.CurrentPuzzle.Difficulty, seconds);
            player.Score += points;
            player.AnsweredThisRound = true;

            return new AnswerResult(player.AccountId, player.DisplayName, true, points, seconds);
        }
    }

    /// <summary>Tìm file ảnh của một câu theo tên file, để phục vụ GET ảnh.</summary>
    public string? ImagePath(string imageName) => _puzzles
        .FirstOrDefault(p => string.Equals(
            Path.GetFileName(p.Image), imageName, StringComparison.OrdinalIgnoreCase))
        ?.Image;
}
