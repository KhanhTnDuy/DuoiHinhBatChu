using System.IO;
using DuoiHinhBatChu.Data;
using DuoiHinhBatChu.Models;
using Microsoft.EntityFrameworkCore;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Đọc câu đố từ bảng <c>Puzzles</c> trong cơ sở dữ liệu.
///
/// Ảnh nằm trong bảng chứ không phải trên đĩa, nhưng lớp này KHÔNG nạp byte
/// ảnh cùng lúc với danh sách câu: 50 câu là mấy chục megabyte, mà mỗi lúc chỉ
/// hiện một ảnh. Danh sách lấy bằng <see cref="LoadAll"/>, ảnh lấy riêng bằng
/// <see cref="LoadImage(string)"/> khi thật sự cần hiện.
///
/// Thư mục <c>Assets/CauHoi</c> vẫn là nơi thêm câu mới; việc chuyển ảnh vào
/// bảng do <see cref="PuzzleSync"/> làm lúc khởi động.
/// </summary>
public class PuzzleRepository
{
    public PuzzleRepository() => GameDatabase.EnsureReady();

    /// <summary>Nạp toàn bộ câu đố (không kèm ảnh), theo đúng thứ tự trong game.</summary>
    /// <exception cref="InvalidDataException">Chưa có câu đố nào trong bảng.</exception>
    public List<Puzzle> LoadAll()
    {
        using GameDbContext db = GameDatabase.Open();

        // Chọn sẵn từng cột cần dùng, cố tình bỏ cột ImageBytes lại: viết
        // db.Puzzles.ToList() là kéo cả đống ảnh lên bộ nhớ một cách vô ích
        var rows = db.Puzzles
            .AsNoTracking()
            .OrderBy(p => p.Order)
            .Select(p => new { p.Id, p.Answer, p.Hint, p.Difficulty, p.ImageName })
            .ToList();

        if (rows.Count == 0)
            throw new InvalidDataException(
                "Chưa có câu đố nào.\n\n" +
                $"Hãy bỏ ảnh vào thư mục:\n{PuzzleImageLocator.Folder}\n\n" +
                "Tên file chính là đáp án, ví dụ \"CÁ HEO.png\".\n" +
                "Bỏ ảnh xong thì chạy lại game để nạp vào cơ sở dữ liệu.");

        // Các cách viết được chấp nhận: lấy một lượt cho cả bộ rồi gom theo câu,
        // chứ hỏi cơ sở dữ liệu 50 lần trong vòng lặp là chậm oan
        Dictionary<string, List<string>> accepted = db.PuzzleAnswers
            .AsNoTracking()
            .GroupBy(a => a.PuzzleId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Text).ToList());

        return rows.Select(r => new Puzzle
        {
            Id = r.Id,
            Answer = r.Answer,
            Hint = r.Hint,
            Difficulty = r.Difficulty,
            ImageName = r.ImageName,
            AcceptedAnswers = accepted.TryGetValue(r.Id, out List<string>? list) ? list : new(),
        }).ToList();
    }

    /// <summary>Số câu đố đang có, đếm ngay trong cơ sở dữ liệu.</summary>
    public int Count()
    {
        using GameDbContext db = GameDatabase.Open();
        return db.Puzzles.Count();
    }

    /// <summary>Byte ảnh của một câu, lấy theo mã câu. Không có thì trả về null.</summary>
    public byte[]? LoadImage(string puzzleId)
    {
        using GameDbContext db = GameDatabase.Open();

        return db.Puzzles
            .AsNoTracking()
            .Where(p => p.Id == puzzleId)
            .Select(p => p.ImageBytes)
            .FirstOrDefault();
    }

    /// <summary>
    /// Byte ảnh lấy theo tên file, kèm kiểu nội dung — dùng cho đường dẫn
    /// <c>GET /api/puzzles/{imageName}/image</c> của máy chủ.
    /// </summary>
    public (byte[] Bytes, string ContentType)? LoadImageByName(string imageName)
    {
        using GameDbContext db = GameDatabase.Open();

        var row = db.Puzzles
            .AsNoTracking()
            .Where(p => p.ImageName == imageName)
            .Select(p => new { p.ImageBytes, p.ContentType })
            .FirstOrDefault();

        return row == null ? null : (row.ImageBytes, row.ContentType);
    }
}
