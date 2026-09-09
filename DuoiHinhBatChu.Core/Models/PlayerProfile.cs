using System.Collections.Generic;

namespace DuoiHinhBatChu.Models;

/// <summary>
/// Hồ sơ lưu tiến trình người chơi.
/// </summary>
public class PlayerProfile
{
    public string PlayerName { get; set; } = "Người chơi";
    public int Score { get; set; } = 0;

    /// <summary>Vốn ban đầu của tài khoản mới: đủ dùng ba lần trợ giúp.</summary>
    public int Rubies { get; set; } = 3;

    /// <summary>
    /// Đang đúng liên tiếp mấy câu. Đủ 5 câu thì thưởng 1 kim cương rồi đếm lại
    /// từ đầu; trả lời sai hay bỏ qua là mất chuỗi.
    /// </summary>
    public int CorrectStreak { get; set; } = 0;
    public int Lives { get; set; } = 5;
    public int MaxLives { get; set; } = 5;
    public int CurrentPuzzleIndex { get; set; } = 0;
    public List<string> SolvedPuzzleIds { get; set; } = new();
    public Dictionary<string, int> PuzzleStars { get; set; } = new();
    public bool IsSoundEnabled { get; set; } = true;
    public bool IsBgmEnabled { get; set; } = true;
    public bool IsTimerEnabled { get; set; } = true;

}
