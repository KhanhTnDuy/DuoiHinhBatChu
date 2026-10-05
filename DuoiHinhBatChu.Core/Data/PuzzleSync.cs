using System.IO;
using System.Text.Json;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;
using Microsoft.EntityFrameworkCore;

namespace DuoiHinhBatChu.Data;

/// <summary>
/// Đối chiếu thư mục <c>Assets/CauHoi</c> với bảng <c>Puzzles</c> trong cơ sở
/// dữ liệu, chạy mỗi lần khởi động.
///
/// Chia việc rõ ràng: **thư mục ảnh là nơi bạn soạn**, còn **cơ sở dữ liệu là
/// nơi game đọc**. Bỏ thêm một ảnh vào thư mục rồi chạy lại là có câu mới; xóa
/// ảnh đi là câu đó biến mất khỏi bảng.
///
/// Ảnh nào không đổi thì không đọc lại byte: so kích thước và giờ sửa file,
/// giống nhau là bỏ qua. Nhờ vậy khởi động lần thứ hai gần như không tốn gì,
/// dù bộ ảnh có nặng.
/// </summary>
public static class PuzzleSync
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Số câu thêm mới, cập nhật và xóa của một lần đối chiếu.</summary>
    public readonly record struct Report(int Added, int Updated, int Removed, int Total)
    {
        public override string ToString() =>
            $"{Total} câu đố (thêm {Added}, cập nhật {Updated}, xóa {Removed})";
    }

    /// <summary>Nơi khai gợi ý và độ khó bổ sung cho từng đáp án (tùy chọn).</summary>
    private static string DefaultMetaPath =>
        Path.Combine(AppContext.BaseDirectory, "Data", "puzzles.json");

    /// <param name="metaPath">Đường dẫn puzzles.json thay thế, chỉ dùng khi test.</param>
    public static Report Sync(string? metaPath = null)
    {
        GameDatabase.EnsureReady();

        List<PuzzleImageLocator.ImageEntry> images = PuzzleImageLocator.Scan();
        Dictionary<string, Puzzle> meta = LoadMeta(metaPath ?? DefaultMetaPath);

        using GameDbContext db = GameDatabase.Open();

        var stored = db.Puzzles.ToDictionary(p => p.Id);
        var seen = new HashSet<string>();
        int added = 0, updated = 0, order = 1;

        foreach (PuzzleImageLocator.ImageEntry img in images)
        {
            string id = MakeId(img.Answer);
            if (!seen.Add(id)) continue;   // hai file cùng ra một đáp án

            var file = new FileInfo(img.Path);
            meta.TryGetValue(id, out Puzzle? extra);

            stored.TryGetValue(id, out StoredPuzzle? row);
            bool isNew = row == null;

            if (row == null)
            {
                row = new StoredPuzzle { Id = id };
                db.Puzzles.Add(row);
            }

            // File có đổi không: so kích thước và giờ sửa, khỏi phải đọc byte
            bool imageChanged = isNew
                || row.SourceSize != file.Length
                || row.SourceModifiedUtc != file.LastWriteTimeUtc
                || row.ImageName != file.Name;

            // Đáp án hiển thị: ưu tiên bản khai trong puzzles.json, chỉ lấy tên
            // file khi không có. Tên file hay bị gõ không dấu cho nhanh ("BAO
            // CAO"), mà đây chính là chuỗi báo ra lúc trả lời xong — lấy thẳng
            // tên file thì người chơi giải đúng lại thấy "Chính xác: BAO CAO".
            // Hai bên vẫn cùng một mã câu nên ghép được (xem MakeId).
            string answer = string.IsNullOrWhiteSpace(extra?.Answer)
                ? img.Answer
                : extra!.Answer.Trim().ToUpperInvariant();

            string category = string.IsNullOrWhiteSpace(extra?.Category)
                ? DefaultCategory(answer)
                : extra!.Category.Trim();

            int difficulty = extra is { Difficulty: >= 1 and <= 5 }
                ? extra.Difficulty
                : DefaultDifficulty(answer);

            bool metaChanged = row.Answer != answer
                || row.Category != category
                || row.Difficulty != difficulty
                || row.Order != order;

            if (imageChanged)
            {
                row.ImageName = file.Name;
                row.ContentType = ContentType(file.Extension);
                row.ImageBytes = File.ReadAllBytes(img.Path);
                row.SourceSize = file.Length;
                row.SourceModifiedUtc = file.LastWriteTimeUtc;
                row.ImportedAt = DateTime.Now;
            }

            row.Answer = answer;
            row.Category = category;
            row.Difficulty = difficulty;
            row.Order = order++;

            if (isNew) added++;
            else if (imageChanged || metaChanged) updated++;

            SyncAnswers(db, id, extra?.AcceptedAnswers);
        }

        // Ảnh đã bị xóa khỏi thư mục thì bỏ luôn câu đó khỏi bảng
        List<StoredPuzzle> gone = stored.Values.Where(p => !seen.Contains(p.Id)).ToList();
        db.Puzzles.RemoveRange(gone);

        db.SaveChanges();

        return new Report(added, updated, gone.Count, seen.Count);
    }

    /// <summary>
    /// Cập nhật các cách viết được chấp nhận của một câu: thêm cái thiếu, xóa
    /// cái không còn khai trong puzzles.json.
    /// </summary>
    private static void SyncAnswers(GameDbContext db, string puzzleId, List<string>? accepted)
    {
        var want = new HashSet<string>(
            (accepted ?? []).Select(a => a.Trim()).Where(a => a.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        List<StoredAnswer> have = db.PuzzleAnswers.Where(a => a.PuzzleId == puzzleId).ToList();

        foreach (StoredAnswer a in have)
        {
            if (!want.Remove(a.Text)) db.PuzzleAnswers.Remove(a);
        }

        foreach (string text in want)
            db.PuzzleAnswers.Add(new StoredAnswer { PuzzleId = puzzleId, Text = text });
    }

    /// <summary>
    /// Mã câu lấy từ đáp án đã chuẩn hóa: "CÁ HEO" và "ca heo" cùng ra "CAHEO".
    /// Đổi tên file mà đáp án vẫn thế thì mã không đổi, nên tiến trình người
    /// chơi không mất.
    /// </summary>
    public static string MakeId(string answer) =>
        AnswerChecker.Normalize(answer).Replace(" ", "").ToUpperInvariant();

    private static string ContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".gif" => "image/gif",
        _ => "image/png",
    };

    // ----- Phần bổ sung tùy chọn trong Data/puzzles.json -----

    /// <summary>Đọc gợi ý và độ khó khai thêm, lập chỉ mục theo mã câu.</summary>
    private static Dictionary<string, Puzzle> LoadMeta(string path)
    {
        var map = new Dictionary<string, Puzzle>();
        if (!File.Exists(path)) return map;

        string json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) return map;

        List<Puzzle> list;
        try
        {
            list = JsonSerializer.Deserialize<List<Puzzle>>(json, JsonOptions) ?? new();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"File {path} sai định dạng JSON: {ex.Message}", ex);
        }

        foreach (Puzzle p in list)
        {
            if (string.IsNullOrWhiteSpace(p.Answer)) continue;
            map.TryAdd(MakeId(p.Answer), p);
        }

        return map;
    }

    /// <summary>
    /// Chủ đề mặc định khi puzzles.json chưa khai cho câu này. Máy không đoán
    /// nổi "CÁ HEO" là con vật, nên chỉ tách được hai loại theo độ dài: đáp án
    /// dài cỡ một câu thì gần như chắc chắn là ca dao / tục ngữ, còn lại xếp
    /// tạm vào "Cụm từ" cho tới khi bạn khai chủ đề thật trong puzzles.json.
    /// </summary>
    private static string DefaultCategory(string answer)
    {
        int words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return words >= 6 ? "Ca dao - tục ngữ" : "Cụm từ";
    }

    /// <summary>Đáp án càng dài thì càng khó, dùng khi puzzles.json không ghi độ khó.</summary>
    private static int DefaultDifficulty(string answer)
    {
        int letters = answer.Count(char.IsLetter);
        return letters switch
        {
            <= 5 => 1,
            <= 7 => 2,
            <= 9 => 3,
            <= 12 => 4,
            _ => 5,
        };
    }
}
