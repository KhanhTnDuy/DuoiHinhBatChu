namespace DuoiHinhBatChu.Services;

/// <summary>
/// Cách tính điểm và giới hạn giờ cho chế độ 1 người chơi.
///
/// Khác chỗ nào với <see cref="MatchScoring"/> của ván đấu nhiều người:
/// đấu nhiều người là cuộc đua, ai chậm thì gần như mất trắng, nên hết giờ là
/// 0 điểm. Chơi một mình thì không đua với ai, mục đích là luyện tay — nên trả
/// lời đúng trong giờ luôn được **điểm nền**, còn nhanh thì được **thưởng thêm**
/// tối đa bằng đúng điểm nền (tức là nhanh nhất thì gấp đôi).
/// </summary>
public static class SoloScoring
{
    /// <summary>Mỗi câu có một phút. Hết giờ coi như trả lời sai.</summary>
    public const double MaxSeconds = 60.0;

    /// <summary>Điểm nền cho mỗi bậc độ khó.</summary>
    public const int BasePoints = 10;

    /// <summary>Điểm chắc chắn được nếu trả lời đúng, không phụ thuộc nhanh chậm.</summary>
    public static int Base(int difficulty) => BasePoints * Math.Clamp(difficulty, 1, 5);

    /// <summary>
    /// Thưởng thêm cho tốc độ: trả lời ngay được thêm đúng bằng điểm nền, càng
    /// sát giờ thưởng càng ít, và hết giờ thì không còn gì.
    /// </summary>
    /// <param name="seconds">Số giây đã dùng cho câu này.</param>
    public static int SpeedBonus(int difficulty, double seconds)
    {
        double left = Math.Clamp(1.0 - seconds / MaxSeconds, 0.0, 1.0);
        return (int)Math.Round(Base(difficulty) * left);
    }

    /// <summary>Tổng điểm cho một câu trả lời đúng.</summary>
    public static int Points(int difficulty, double seconds) =>
        Base(difficulty) + SpeedBonus(difficulty, seconds);
}
