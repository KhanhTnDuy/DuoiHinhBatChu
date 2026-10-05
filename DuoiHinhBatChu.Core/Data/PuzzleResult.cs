namespace DuoiHinhBatChu.Data;

/// <summary>
/// Một dòng trong bảng <c>PuzzleResults</c>: tài khoản X đã giải xong câu Y.
///
/// Cặp (AccountId, PuzzleId) là duy nhất — một người giải một câu chỉ ghi một
/// dòng, giải lại cũng không thêm dòng mới.
/// </summary>
public class PuzzleResult
{
    public int Id { get; set; }

    /// <summary>Khóa ngoại trỏ về <c>Accounts.Id</c>.</summary>
    public string AccountId { get; set; } = "";

    /// <summary>Mã câu đố, lấy từ <c>Puzzle.Id</c>.</summary>
    public string PuzzleId { get; set; } = "";

    public DateTime SolvedAt { get; set; } = DateTime.Now;
}
