namespace DuoiHinhBatChu.Data;

/// <summary>
/// Một dòng trong bảng <c>PlayerStates</c>: tiến trình chơi đơn của một tài khoản.
///
/// Mỗi tài khoản đúng một dòng, khóa chính chính là mã tài khoản (quan hệ 1-1).
/// Danh sách câu đã giải KHÔNG nằm ở đây mà tách sang bảng
/// <see cref="PuzzleResult"/> — đó là cách làm đúng của cơ sở dữ liệu quan hệ:
/// một ô chỉ giữ một giá trị, cái gì "nhiều" thì cho ra bảng riêng.
/// </summary>
public class PlayerState
{
    /// <summary>Khóa chính, đồng thời là khóa ngoại trỏ về <c>Accounts.Id</c>.</summary>
    public string AccountId { get; set; } = "";

    public int Score { get; set; }
    public int Rubies { get; set; } = 3;
    public int CorrectStreak { get; set; }
    public int Lives { get; set; } = 5;
    public int MaxLives { get; set; } = 5;
    public int CurrentPuzzleIndex { get; set; }

    public bool IsSoundEnabled { get; set; } = true;
    public bool IsBgmEnabled { get; set; } = true;
    public bool IsTimerEnabled { get; set; } = true;

    /// <summary>Lần lưu gần nhất, để sau này làm mục "chơi tiếp".</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
