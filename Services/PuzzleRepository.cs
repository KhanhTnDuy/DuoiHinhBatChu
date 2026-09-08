using System.IO;
using System.Text.Json;
using DuoiHinhBatChu.Models;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Dựng danh sách câu đố từ các ảnh trong <c>Assets/CauHoi</c>.
///
/// Ảnh là nguồn chính: có ảnh nào thì có câu đó, đáp án lấy từ tên file.
/// File <c>Data/puzzles.json</c> chỉ là phần bổ sung tùy chọn (gợi ý, độ khó),
/// ghép vào theo đáp án; thiếu file đó thì game vẫn chạy với giá trị mặc định.
/// </summary>
public class PuzzleRepository
{
    // Bỏ qua khác biệt hoa/thường giữa khóa JSON (camelCase) và thuộc tính C# (PascalCase).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _metaPath;

    /// <summary>
    /// Mặc định đọc phần bổ sung ở Data/puzzles.json nằm cạnh file .exe.
    /// Có thể truyền đường dẫn khác (dùng khi test).
    /// </summary>
    public PuzzleRepository(string? metaPath = null)
    {
        _metaPath = metaPath
            ?? Path.Combine(AppContext.BaseDirectory, "Data", "puzzles.json");
    }

    /// <summary>
    /// Nạp toàn bộ câu đố theo đúng thứ tự đã quét được ở thư mục ảnh.
    /// </summary>
    /// <exception cref="InvalidDataException">Thư mục ảnh chưa có câu đố nào.</exception>
    public List<Puzzle> LoadAll()
    {
        var images = PuzzleImageLocator.Scan();
        if (images.Count == 0)
            throw new InvalidDataException(
                "Chưa có câu đố nào.\n\n" +
                $"Hãy bỏ ảnh vào thư mục:\n{PuzzleImageLocator.Folder}\n\n" +
                "Tên file chính là đáp án, ví dụ \"CÁ HEO.png\".");

        Dictionary<string, Puzzle> meta = LoadMeta();

        var puzzles = new List<Puzzle>();
        int no = 1;

        foreach (PuzzleImageLocator.ImageEntry img in images)
        {
            meta.TryGetValue(MetaKey(img.Answer), out Puzzle? extra);

            puzzles.Add(new Puzzle
            {
                Id = $"p{no:D3}",
                Answer = img.Answer,
                Image = img.Path,
                AcceptedAnswers = extra?.AcceptedAnswers ?? new(),
                Hint = string.IsNullOrWhiteSpace(extra?.Hint)
                    ? DefaultHint(img.Answer)
                    : extra!.Hint,
                Difficulty = extra is { Difficulty: >= 1 and <= 5 }
                    ? extra.Difficulty
                    : DefaultDifficulty(img.Answer),
            });

            no++;
        }

        return puzzles;
    }

    /// <summary>Đọc phần bổ sung, lập chỉ mục theo đáp án đã chuẩn hóa.</summary>
    private Dictionary<string, Puzzle> LoadMeta()
    {
        var map = new Dictionary<string, Puzzle>();
        if (!File.Exists(_metaPath)) return map;

        string json = File.ReadAllText(_metaPath);
        if (string.IsNullOrWhiteSpace(json)) return map;

        List<Puzzle> list;
        try
        {
            list = JsonSerializer.Deserialize<List<Puzzle>>(json, JsonOptions) ?? new();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"File {_metaPath} sai định dạng JSON: {ex.Message}", ex);
        }

        foreach (Puzzle p in list)
        {
            if (string.IsNullOrWhiteSpace(p.Answer)) continue;
            map.TryAdd(MetaKey(p.Answer), p);
        }

        return map;
    }

    /// <summary>"CÁ HEO" và "ca heo" cùng cho ra "caheo" để ghép được với nhau.</summary>
    private static string MetaKey(string answer) =>
        AnswerChecker.Normalize(answer).Replace(" ", "");

    /// <summary>Gợi ý mặc định khi puzzles.json chưa khai báo gì cho câu này.</summary>
    private static string DefaultHint(string answer)
    {
        int letters = answer.Count(char.IsLetter);
        int words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        return words > 1
            ? $"Đáp án gồm {words} tiếng, tất cả {letters} chữ cái."
            : $"Đáp án là một tiếng gồm {letters} chữ cái.";
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
