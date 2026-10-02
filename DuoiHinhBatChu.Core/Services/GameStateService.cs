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

    /// <summary>
    /// Chơi khách thì KHÔNG lưu gì cả: chơi xong là mọi thứ về mặc định.
    /// Muốn giữ điểm và kim cương thì phải đăng ký một tài khoản — cũng vì
    /// hồ sơ khách là chung cho mọi người ngồi vào máy này, lưu vào đó thì
    /// người sau tiếp tục ván của người trước, chẳng của ai cả.
    /// </summary>
    private readonly bool _isGuest;

    /// <param name="accountId">Mã tài khoản đang đăng nhập.</param>
    /// <param name="dbPath">Đường dẫn cơ sở dữ liệu thay thế, chỉ dùng khi test.</param>
    public GameStateService(string accountId, string? dbPath = null)
    {
        GameDatabase.EnsureReady(dbPath);
        _accountId = accountId;
        _isGuest = accountId == Account.GuestId;
    }

    /// <summary>
    /// Tài khoản này đã chơi lần nào chưa.
    ///
    /// Dấu hiệu là có dòng tiến trình trong bảng hay không: <see cref="SaveProfile"/>
    /// chỉ ghi dòng đó khi người chơi đã thực sự vào chơi, nên "có dòng" đúng
    /// bằng "đã chơi rồi". Khách luôn trả về false vì khách không được lưu gì.
    /// </summary>
    public bool HasPlayedBefore()
    {
        if (_isGuest) return false;

        using GameDbContext db = GameDatabase.Open();
        return db.PlayerStates.Any(s => s.AccountId == _accountId);
    }

    /// <summary>
    /// Lấy tiến trình đã lưu. Tài khoản mới chưa có dòng nào thì trả về hồ sơ
    /// mặc định (5 mạng, 3 kim cương) chứ không ghi gì xuống bảng — chỉ khi
    /// người chơi thực sự chơi và game gọi <see cref="SaveProfile"/> mới ghi.
    /// </summary>
    public PlayerProfile LoadProfile()
    {
        // Khách luôn bắt đầu lại từ đầu, không đọc gì trong bảng
        if (_isGuest) return new PlayerProfile();

        using GameDbContext db = GameDatabase.Open();

        PlayerState? state = db.PlayerStates
            .AsNoTracking()
            .FirstOrDefault(s => s.AccountId == _accountId);

        if (state == null) return new PlayerProfile();

        var profile = new PlayerProfile
        {
            Score = state.Score,
            BestScore = state.BestScore,
            Rubies = state.Rubies,
            CorrectStreak = state.CorrectStreak,
            Lives = state.Lives,
            MaxLives = state.MaxLives,
            RunSeed = state.RunSeed,
            RunOrder = (RunOrder)state.RunOrder,
            CurrentPuzzleId = state.CurrentPuzzleId,
            SecondsLeft = state.SecondsLeft,
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
        // Khách: tiến trình chỉ sống trong bộ nhớ của ván đang chơi
        if (_isGuest) return;

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
        state.BestScore = profile.BestScore;
        state.Rubies = profile.Rubies;
        state.CorrectStreak = profile.CorrectStreak;
        state.Lives = profile.Lives;
        state.MaxLives = profile.MaxLives;
        state.RunSeed = profile.RunSeed;
        state.RunOrder = (int)profile.RunOrder;
        state.CurrentPuzzleId = profile.CurrentPuzzleId;
        state.SecondsLeft = profile.SecondsLeft;
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

    /// <summary>
    /// Ghi xuống lúc một ván vừa kết thúc (hết mạng hoặc hết bộ câu).
    ///
    /// Lưu kỷ lục mới, rồi ghi phần "ván" ở trạng thái CHƯA BẮT ĐẦU: 0 điểm,
    /// đầy mạng, chuỗi về 0. Nếu chỉ lưu nguyên hồ sơ như lúc chơi thì bảng còn
    /// giữ 800 điểm và 0 mạng của ván vừa chết — người chơi bấm "Về màn hình
    /// chính" rồi vào lại là ván đã xong sống dậy, điểm cũ chạy tiếp sang ván
    /// mới, đúng cái kiểu cộng dồn vừa bỏ đi.
    ///
    /// Hồ sơ trên tay người gọi KHÔNG bị đụng tới: màn hình còn phải hiện điểm
    /// của ván vừa xong.
    /// </summary>
    public void SaveEndOfRun(PlayerProfile profile)
    {
        if (_isGuest) return;

        var fresh = new PlayerProfile();

        SaveProfile(new PlayerProfile
        {
            PlayerName = profile.PlayerName,
            BestScore = profile.BestScore,      // thành tích: giữ
            Rubies = profile.Rubies,            // kim cương: của tài khoản, không phải của ván
            SolvedPuzzleIds = profile.SolvedPuzzleIds,
            PuzzleStars = profile.PuzzleStars,
            IsSoundEnabled = profile.IsSoundEnabled,
            IsBgmEnabled = profile.IsBgmEnabled,
            IsTimerEnabled = profile.IsTimerEnabled,

            RunSeed = 0,                        // ván sau xáo lại thứ tự câu
            RunOrder = RunOrder.Random,         // và hỏi lại lối chơi
            // Đi cùng RunSeed: ván sau xáo lại thì câu đang dở của ván cũ không
            // còn nghĩa gì, giữ lại là ván mới nhảy vào giữa danh sách vừa xáo
            CurrentPuzzleId = "",
            Score = 0,                          // ván sau bắt đầu từ 0
            CorrectStreak = 0,
            Lives = fresh.Lives,
            MaxLives = profile.MaxLives,
        });
    }

    /// <summary>
    /// Xóa tiến trình của tài khoản này để chơi lại từ đầu — nhưng GIỮ kỷ lục.
    ///
    /// Kỷ lục là thành tích cả đời, không thuộc về ván nào, nên "chơi lại từ
    /// đầu" không được đụng vào: xóa nó đi là người chơi bay khỏi bảng xếp hạng
    /// chỉ vì muốn làm lại bộ câu. Trước đây hàm này xóa nguyên dòng tiến trình,
    /// hồi điểm còn cộng dồn thì không sao vì chẳng có gì đáng giữ.
    /// </summary>
    public void ResetProfile()
    {
        if (_isGuest) return;   // khách có lưu gì đâu mà xóa

        using GameDbContext db = GameDatabase.Open();

        // ExecuteDelete xóa thẳng bằng một câu lệnh SQL, không phải nạp từng
        // dòng lên bộ nhớ rồi mới xóa
        db.PuzzleResults.Where(r => r.AccountId == _accountId).ExecuteDelete();

        PlayerState? state = db.PlayerStates.FirstOrDefault(s => s.AccountId == _accountId);
        if (state == null) return;

        var fresh = new PlayerProfile();      // các giá trị mặc định của ván mới

        state.Score = 0;
        state.CorrectStreak = 0;
        state.Rubies = fresh.Rubies;
        state.Lives = fresh.Lives;
        state.MaxLives = fresh.MaxLives;
        state.RunSeed = 0;
        state.RunOrder = 0;
        state.CurrentPuzzleId = "";
        state.UpdatedAt = DateTime.Now;
        // state.BestScore: cố ý không đụng tới

        db.SaveChanges();
    }

    /// <summary>
    /// Bảng xếp hạng: những người chơi có điểm VÁN cao nhất, kèm số câu đã giải.
    ///
    /// Xếp theo <see cref="PlayerState.BestScore"/> chứ không phải điểm đang
    /// chơi dở: bảng phải đo thành tích tốt nhất của mỗi người, không phải họ
    /// vừa đi được bao xa trong ván đang mở.
    /// </summary>
    public static List<LeaderboardRow> TopPlayers(int count = 10)
    {
        using GameDbContext db = GameDatabase.Open();

        // Sắp xếp và cắt bớt TRƯỚC, gói vào LeaderboardRow SAU: EF chỉ dịch
        // được sang SQL những phép nó hiểu, mà nó không nhìn được vào bên
        // trong một record vừa dựng để biết .BestScore là cột nào
        return db.PlayerStates
            .Where(s => s.AccountId != Account.GuestId && s.BestScore > 0)
            .Join(db.Accounts, s => s.AccountId, a => a.Id, (s, a) => new
            {
                a.DisplayName,
                s.BestScore,
                Solved = db.PuzzleResults.Count(r => r.AccountId == s.AccountId),
            })
            .OrderByDescending(x => x.BestScore)
            .Take(count)
            .AsEnumerable()
            .Select(x => new LeaderboardRow(x.DisplayName, x.BestScore, x.Solved))
            .ToList();
    }
}

/// <summary>Một dòng trên bảng xếp hạng.</summary>
/// <param name="BestScore">Điểm ván cao nhất của người này.</param>
public readonly record struct LeaderboardRow(string DisplayName, int BestScore, int Solved);
