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
    /// Hạt giống xáo bài của ván đang chơi; 0 nghĩa là chưa có ván nào.
    /// Xem <see cref="Data.PlayerState.RunSeed"/>.
    /// </summary>
    public int RunSeed { get; set; } = 0;

    /// <summary>Lối chơi của ván đang dở; chỉ có nghĩa khi <see cref="RunSeed"/> khác 0.</summary>
    public RunOrder RunOrder { get; set; } = RunOrder.Random;

    /// <summary>
    /// Mã câu đang chơi dở, rỗng nghĩa là chưa chơi câu nào.
    /// Lưu bằng mã chứ không phải số thứ tự — xem <see cref="Data.PlayerState.CurrentPuzzleId"/>.
    /// </summary>
    public string CurrentPuzzleId { get; set; } = "";

    /// <summary>Số giây còn lại lúc tạm dừng, xem <see cref="Data.PlayerState.SecondsLeft"/>.</summary>
    public double SecondsLeft { get; set; } = 0;

    /// <summary>
    /// Mã các câu đã giải. Dùng tập hợp chứ không phải danh sách vì mỗi câu
    /// đúng đều phải tra "giải chưa" — tra trong tập hợp là tức thì, còn dò
    /// danh sách thì phải đi hết cả bộ.
    /// </summary>
    public HashSet<string> SolvedPuzzleIds { get; set; } = new();

    public bool IsSoundEnabled { get; set; } = true;

    /// <summary>
    /// Đưa phần "ván" về trạng thái chưa bắt đầu, giữ nguyên phần thuộc về tài
    /// khoản (kỷ lục, kim cương, câu đã giải, tùy chọn).
    /// </summary>
    public void ResetRun()
    {
        Score = 0;
        CorrectStreak = 0;
        Lives = MaxLives;
        RunSeed = 0;                 // ván sau xáo lại thứ tự câu
        RunOrder = RunOrder.Random;  // và hỏi lại lối chơi
        // Đi cùng RunSeed: ván sau xáo lại thì câu đang dở của ván cũ không còn
        // nghĩa gì, giữ lại là ván mới nhảy vào giữa danh sách vừa xáo
        CurrentPuzzleId = "";
        SecondsLeft = 0;
    }

    /// <summary>
    /// Bản hồ sơ của ván SAU, dùng lúc một ván vừa chốt sổ.
    ///
    /// Phải là một bản riêng vì hồ sơ trên tay người gọi không được đụng tới:
    /// màn hình còn phải hiện điểm của ván vừa xong.
    /// </summary>
    public PlayerProfile ForNextRun()
    {
        var next = (PlayerProfile)MemberwiseClone();
        next.ResetRun();
        return next;
    }
}
