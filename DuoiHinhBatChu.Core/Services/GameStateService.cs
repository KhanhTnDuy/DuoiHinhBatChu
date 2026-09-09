using DuoiHinhBatChu.Data;
using DuoiHinhBatChu.Models;
using Microsoft.EntityFrameworkCore;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Đọc và ghi tiến trình chơi đơn của một tài khoản trong cơ sở dữ liệu.
///
/// Trong bảng, tiến trình nằm ở hai chỗ: <c>PlayerStates</c> giữ các con số
/// (điểm, mạng, kim cương…) và <c>PuzzleResults</c> giữ danh sách câu đã giải.
/// Lớp này gom hai chỗ đó lại thành một <see cref="PlayerProfile"/> cho màn
/// chơi dùng, và lúc lưu thì tách ngược ra.
/// </summary>
public class GameStateService
{
    private readonly string _accountId;

    /// <param name="accountId">Mã tài khoản đang đăng nhập.</param>
    /// <param name="dbPath">Đường dẫn cơ sở dữ liệu thay thế, chỉ dùng khi test.</param>
    public GameStateService(string accountId, string? dbPath = null)
    {
        GameDatabase.EnsureReady(dbPath);
        _accountId = accountId;
    }

    /// <summary>
    /// Lấy tiến trình đã lưu. Tài khoản mới chưa có dòng nào thì trả về hồ sơ
    /// mặc định (5 mạng, 3 kim cương) chứ không ghi gì xuống bảng — chỉ khi
    /// người chơi thực sự chơi và game gọi <see cref="SaveProfile"/> mới ghi.
    /// </summary>
    public PlayerProfile LoadProfile()
    {
        using GameDbContext db = GameDatabase.Open();

        PlayerState? state = db.PlayerStates
            .AsNoTracking()
            .FirstOrDefault(s => s.AccountId == _accountId);

        if (state == null) return new PlayerProfile();

        var profile = new PlayerProfile
        {
            Score = state.Score,
            Rubies = state.Rubies,
            CorrectStreak = state.CorrectStreak,
            Lives = state.Lives,
            MaxLives = state.MaxLives,
            CurrentPuzzleIndex = state.CurrentPuzzleIndex,
            IsSoundEnabled = state.IsSoundEnabled,
            IsBgmEnabled = state.IsBgmEnabled,
            IsTimerEnabled = state.IsTimerEnabled,
        };

        foreach (PuzzleResult r in db.PuzzleResults
                                    .AsNoTracking()
                                    .Where(r => r.AccountId == _accountId))
        {
            profile.SolvedPuzzleIds.Add(r.PuzzleId);
            if (r.Stars > 0) profile.PuzzleStars[r.PuzzleId] = r.Stars;
        }

        return profile;
    }

    /// <summary>Ghi tiến trình xuống bảng, đè lên lần lưu trước.</summary>
    public void SaveProfile(PlayerProfile profile)
    {
        using GameDbContext db = GameDatabase.Open();

        // Bảng tiến trình có khóa ngoại trỏ về bảng tài khoản, tài khoản không
        // còn thì có ghi cũng bị chặn — bỏ qua cho êm
        if (!db.Accounts.Any(a => a.Id == _accountId)) return;

        PlayerState? state = db.PlayerStates.FirstOrDefault(s => s.AccountId == _accountId);
        if (state == null)
        {
            state = new PlayerState { AccountId = _accountId };
            db.PlayerStates.Add(state);
        }

        state.Score = profile.Score;
        state.Rubies = profile.Rubies;
        state.CorrectStreak = profile.CorrectStreak;
        state.Lives = profile.Lives;
        state.MaxLives = profile.MaxLives;
        state.CurrentPuzzleIndex = profile.CurrentPuzzleIndex;
        state.IsSoundEnabled = profile.IsSoundEnabled;
        state.IsBgmEnabled = profile.IsBgmEnabled;
        state.IsTimerEnabled = profile.IsTimerEnabled;
        state.UpdatedAt = DateTime.Now;

        SaveSolvedPuzzles(db, profile);

        db.SaveChanges();
    }

    /// <summary>
    /// Đồng bộ danh sách câu đã giải: thêm câu mới, sửa số sao, và xóa câu nào
    /// không còn trong hồ sơ (lúc người chơi bấm chơi lại từ đầu).
    /// </summary>
    private void SaveSolvedPuzzles(GameDbContext db, PlayerProfile profile)
    {
        List<PuzzleResult> stored = db.PuzzleResults
            .Where(r => r.AccountId == _accountId)
            .ToList();

        var solved = new HashSet<string>(profile.SolvedPuzzleIds);

        foreach (PuzzleResult r in stored)
        {
            if (!solved.Contains(r.PuzzleId))
            {
                db.PuzzleResults.Remove(r);
                continue;
            }

            r.Stars = profile.PuzzleStars.TryGetValue(r.PuzzleId, out int s) ? s : r.Stars;
            solved.Remove(r.PuzzleId);   // đã có sẵn, khỏi thêm lần nữa
        }

        foreach (string puzzleId in solved)
        {
            db.PuzzleResults.Add(new PuzzleResult
            {
                AccountId = _accountId,
                PuzzleId = puzzleId,
                Stars = profile.PuzzleStars.TryGetValue(puzzleId, out int s) ? s : 0,
            });
        }
    }

    /// <summary>Xóa sạch tiến trình của tài khoản này.</summary>
    public void ResetProfile()
    {
        using GameDbContext db = GameDatabase.Open();

        // ExecuteDelete xóa thẳng bằng một câu lệnh SQL, không phải nạp từng
        // dòng lên bộ nhớ rồi mới xóa
        db.PuzzleResults.Where(r => r.AccountId == _accountId).ExecuteDelete();
        db.PlayerStates.Where(s => s.AccountId == _accountId).ExecuteDelete();
    }

    /// <summary>
    /// Bảng xếp hạng: những người chơi điểm cao nhất, kèm số câu đã giải.
    /// Lấy được ngay bằng một câu truy vấn — thứ mà hồi lưu bằng file JSON
    /// phải mở từng file mới đếm ra.
    /// </summary>
    public static List<LeaderboardRow> TopPlayers(int count = 10)
    {
        using GameDbContext db = GameDatabase.Open();

        // Sắp xếp và cắt bớt TRƯỚC, gói vào LeaderboardRow SAU: EF chỉ dịch
        // được sang SQL những phép nó hiểu, mà nó không nhìn được vào bên
        // trong một record vừa dựng để biết .Score là cột nào
        return db.PlayerStates
            .Where(s => s.AccountId != "khach")
            .Join(db.Accounts, s => s.AccountId, a => a.Id, (s, a) => new
            {
                a.DisplayName,
                s.Score,
                Solved = db.PuzzleResults.Count(r => r.AccountId == s.AccountId),
            })
            .OrderByDescending(x => x.Score)
            .Take(count)
            .AsEnumerable()
            .Select(x => new LeaderboardRow(x.DisplayName, x.Score, x.Solved))
            .ToList();
    }
}

/// <summary>Một dòng trên bảng xếp hạng.</summary>
public readonly record struct LeaderboardRow(string DisplayName, int Score, int Solved);
