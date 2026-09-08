namespace DuoiHinhBatChu.Services;

/// <summary>
/// Cách tính điểm cho ván đấu nhiều người: cùng một câu, ai trả lời nhanh hơn
/// được nhiều điểm hơn.
///
/// Phép tính này luôn chạy trên máy chủ, vì máy chủ mới là bên giữ mốc thời
/// gian phát câu. Client chỉ hiển thị kết quả nhận về — nếu để client tự khai
/// thời gian của mình thì ai cũng "bấm nhanh nhất".
/// </summary>
public static class MatchScoring
{
    /// <summary>Điểm gốc trước khi nhân độ khó và hệ số tốc độ.</summary>
    public const int BasePoints = 100;

    /// <summary>Quá mốc này thì hết giờ, không ai ghi điểm câu đó nữa.</summary>
    public const double MaxSeconds = 20.0;

    /// <summary>Trả lời sát giờ vẫn giữ được phần này, thua nhưng không mất trắng.</summary>
    public const double MinFactor = 0.2;

    /// <summary>
    /// Điểm cho một câu trả lời đúng.
    /// </summary>
    /// <param name="difficulty">Độ khó của câu, 1..5.</param>
    /// <param name="seconds">Số giây từ lúc máy chủ phát câu đến lúc nhận đáp án.</param>
    public static int Points(int difficulty, double seconds)
    {
        if (seconds > MaxSeconds) return 0;

        int level = Math.Clamp(difficulty, 1, 5);
        double factor = Math.Max(MinFactor, 1.0 - seconds / MaxSeconds);

        return (int)Math.Round(BasePoints * level * factor);
    }
}
