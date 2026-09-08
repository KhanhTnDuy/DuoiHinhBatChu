using System.Collections.Generic;

namespace DuoiHinhBatChu.Models;

/// <summary>
/// Hồ sơ lưu tiến trình người chơi.
/// </summary>
public class PlayerProfile
{
    public string PlayerName { get; set; } = "Người chơi";
    public int Score { get; set; } = 0;
    public int Rubies { get; set; } = 150;
    public int Lives { get; set; } = 5;
    public int MaxLives { get; set; } = 5;
    public int CurrentPuzzleIndex { get; set; } = 0;
    public List<string> SolvedPuzzleIds { get; set; } = new();
    public Dictionary<string, int> PuzzleStars { get; set; } = new();
    public bool IsSoundEnabled { get; set; } = true;
    public bool IsBgmEnabled { get; set; } = true;
    public bool IsTimerEnabled { get; set; } = true;

    /// <summary>Người chơi đang dùng chế độ tối hay sáng.</summary>
    public bool IsDarkTheme { get; set; } = false;
}
