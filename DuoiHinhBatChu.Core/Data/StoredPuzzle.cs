namespace DuoiHinhBatChu.Data;

/// <summary>
/// Một dòng trong bảng <c>Puzzles</c>: một câu đố, kèm luôn **byte của ảnh**.
///
/// Ảnh nằm hẳn trong cơ sở dữ liệu chứ không chỉ lưu đường dẫn, vì hai lẽ:
/// máy chủ phải gửi ảnh cho người chơi ở máy khác (họ không có sẵn file), và
/// một file game.db là đủ để mang cả bộ câu đố sang máy khác.
///
/// Thư mục <c>Assets/CauHoi</c> vẫn là nơi bạn thêm câu mới; mỗi lần khởi động
/// <see cref="PuzzleSync"/> đối chiếu thư mục đó với bảng này.
/// </summary>
public class StoredPuzzle
{
    /// <summary>
    /// Mã câu, sinh từ đáp án đã chuẩn hóa ("CÁ HEO" -> "CAHEO").
    ///
    /// Cố tình KHÔNG đánh số p001, p002 theo thứ tự quét: thêm một ảnh mới vào
    /// giữa là mọi số phía sau xê dịch, và tiến trình đã lưu của người chơi
    /// (bảng PuzzleResults) sẽ trỏ sang câu khác.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>Thứ tự hiện trong game.</summary>
    public int Order { get; set; }

    /// <summary>Đáp án đúng như tên file, có dấu và viết hoa.</summary>
    public string Answer { get; set; } = "";

    /// <summary>Chủ đề của đáp án, vd "Đồ vật" — gợi ý nhỏ hiện sẵn cho người chơi.</summary>
    public string Category { get; set; } = "";

    /// <summary>Gợi ý bằng lời.</summary>
    public string Hint { get; set; } = "";

    /// <summary>Độ khó 1..5.</summary>
    public int Difficulty { get; set; }

    /// <summary>
    /// Tên file ảnh gốc. Máy chủ dùng nó làm địa chỉ ảnh:
    /// <c>GET /api/puzzles/{ImageName}/image</c>.
    /// </summary>
    public string ImageName { get; set; } = "";

    /// <summary>Kiểu nội dung của ảnh, vd "image/png", để máy chủ trả đúng header.</summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>Toàn bộ nội dung file ảnh.</summary>
    public byte[] ImageBytes { get; set; } = [];

    // ----- Dấu vết file gốc, để biết ảnh có đổi hay không mà nạp lại -----

    public long SourceSize { get; set; }
    public DateTime SourceModifiedUtc { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Một dòng trong bảng <c>PuzzleAnswers</c>: một cách viết khác cũng được chấm
/// là đúng (vd "CA HEO" cho đáp án "CÁ HEO").
///
/// Tách bảng riêng vì một câu có nhiều cách viết — nhét cả danh sách vào một ô
/// của bảng Puzzles là phạm đúng cái lỗi mà cơ sở dữ liệu quan hệ sinh ra để
/// tránh.
/// </summary>
public class StoredAnswer
{
    public int Id { get; set; }

    /// <summary>Khóa ngoại trỏ về <c>Puzzles.Id</c>.</summary>
    public string PuzzleId { get; set; } = "";

    public string Text { get; set; } = "";
}
