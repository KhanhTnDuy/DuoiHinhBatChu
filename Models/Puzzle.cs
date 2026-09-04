namespace DuoiHinhBatChu.Models;

/// <summary>
/// Một câu đố đuổi hình bắt chữ. Các thuộc tính khớp với từng khóa trong Data/puzzles.json.
/// </summary>
public class Puzzle
{
    /// <summary>Mã câu, trùng tên file ảnh (vd "p001" -> p001.png).</summary>
    public string Id { get; set; } = "";

    /// <summary>Đáp án hiển thị khi trả lời đúng hoặc khi bỏ qua.</summary>
    public string Answer { get; set; } = "";

    /// <summary>Các cách gõ được chấp nhận (có dấu / không dấu).</summary>
    public List<string> AcceptedAnswers { get; set; } = new();

    /// <summary>Đường dẫn ảnh tương đối, vd "Assets/Puzzles/p001.png".</summary>
    public string Image { get; set; } = "";

    /// <summary>Gợi ý bằng chữ.</summary>
    public string Hint { get; set; } = "";

    /// <summary>Độ khó 1..5.</summary>
    public int Difficulty { get; set; }

    /// <summary>Mô tả rebus cần vẽ (chỉ dùng khi làm ảnh, game không hiển thị).</summary>
    public string Draw { get; set; } = "";
}
