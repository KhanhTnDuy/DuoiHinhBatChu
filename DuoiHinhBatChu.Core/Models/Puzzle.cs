namespace DuoiHinhBatChu.Models;

/// <summary>
/// Một câu đố đuổi hình bắt chữ.
/// Câu đố đọc từ bảng Puzzles trong cơ sở dữ liệu. Nguồn ban đầu vẫn là ảnh
/// trong Assets/CauHoi (tên file là đáp án), chuyển vào bảng bởi PuzzleSync.
/// </summary>
public class Puzzle
{
    /// <summary>Mã câu, sinh từ đáp án đã chuẩn hóa (vd "CAHEO").</summary>
    public string Id { get; set; } = "";

    /// <summary>Đáp án hiển thị khi trả lời đúng hoặc khi bỏ qua.</summary>
    public string Answer { get; set; } = "";

    /// <summary>Các cách gõ được chấp nhận (có dấu / không dấu).</summary>
    public List<string> AcceptedAnswers { get; set; } = new();

    /// <summary>
    /// Tên file ảnh, vd "CÁ HEO.png". Đây chỉ là **tên gọi** của ảnh, không
    /// phải đường dẫn: byte ảnh nằm trong cơ sở dữ liệu, lấy ra bằng
    /// <c>PuzzleRepository.LoadImage</c>. Máy chủ cũng dùng tên này làm địa chỉ
    /// gửi ảnh cho máy người chơi.
    /// </summary>
    public string ImageName { get; set; } = "";

    /// <summary>Gợi ý bằng chữ.</summary>
    public string Hint { get; set; } = "";

    /// <summary>Độ khó 1..5.</summary>
    public int Difficulty { get; set; }
}
