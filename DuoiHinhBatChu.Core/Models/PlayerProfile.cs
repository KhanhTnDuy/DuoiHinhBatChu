using System.Collections.Generic;

namespace DuoiHinhBatChu.Models;

/// <summary>
/// Hồ sơ lưu tiến trình người chơi.
/// </summary>
public class PlayerProfile
{
    public string PlayerName { get; set; } = "Người chơi";
    /// <summary>Điểm của ván đang chơi; ván mới là về 0.</summary>
    public int Score { get; set; } = 0;

    /// <summary>Điểm ván cao nhất từ trước tới nay — cái lên bảng xếp hạng.</summary>
    public int BestScore { get; set; } = 0;

    /// <summary>
    /// Vốn ban đầu của tài khoản mới: đủ đúng hai lần trợ giúp.
    ///
    /// Từng là 3. Hạ xuống 2 vì trợ giúp "Mở 1 chữ" nay để người chơi tự chọn ô
    /// nên mạnh hơn hẳn hồi còn bốc ngẫu nhiên — giữ giá 1 kim cương thì phải
    /// siết ở đầu vào, không thì mở thoải mái ba ô là xong câu.
    /// </summary>
    public int Rubies { get; set; } = 2;

    /// <summary>
    /// Đang đúng liên tiếp mấy câu. Đủ 5 câu thì thưởng 1 kim cương rồi đếm lại
    /// từ đầu; trả lời sai hay bỏ qua là mất chuỗi.
    /// </summary>
    public int CorrectStreak { get; set; } = 0;
    public int Lives { get; set; } = 5;
    public int MaxLives { get; set; } = 5;

    /// <summary>
    /// Mã câu đang chơi dở, rỗng nghĩa là chưa chơi câu nào.
    /// Lưu bằng mã chứ không phải số thứ tự — xem <see cref="Data.PlayerState.CurrentPuzzleId"/>.
    /// </summary>
    public string CurrentPuzzleId { get; set; } = "";
    public List<string> SolvedPuzzleIds { get; set; } = new();
    public Dictionary<string, int> PuzzleStars { get; set; } = new();
    public bool IsSoundEnabled { get; set; } = true;
    public bool IsBgmEnabled { get; set; } = true;
    public bool IsTimerEnabled { get; set; } = true;

}
