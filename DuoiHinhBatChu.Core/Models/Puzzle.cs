namespace DuoiHinhBatChu.Models;

/// <summary>
/// Một câu đố đuổi hình bắt chữ.
/// Đáp án và ảnh dựng từ file trong Assets/CauHoi; gợi ý và độ khó có thể
/// khai báo thêm trong Data/puzzles.json (xem PuzzleRepository).
/// </summary>
public class Puzzle
{
    /// <summary>Mã câu tự sinh theo thứ tự quét được (p001, p002...).</summary>
    public string Id { get; set; } = "";

    /// <summary>Đáp án hiển thị khi trả lời đúng hoặc khi bỏ qua.</summary>
    public string Answer { get; set; } = "";

    /// <summary>Các cách gõ được chấp nhận (có dấu / không dấu).</summary>
    public List<string> AcceptedAnswers { get; set; } = new();

    /// <summary>Đường dẫn đầy đủ tới file ảnh trong Assets/CauHoi.</summary>
    public string Image { get; set; } = "";

    /// <summary>Gợi ý bằng chữ.</summary>
    public string Hint { get; set; } = "";

    /// <summary>Độ khó 1..5.</summary>
    public int Difficulty { get; set; }
}
