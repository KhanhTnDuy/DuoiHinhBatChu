using System.IO;
using DuoiHinhBatChu.Models;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Tìm file ảnh cho một câu đố.
/// Thứ tự ưu tiên:
///   1. Assets/CauHoi/&lt;tên bất kỳ&gt; có tên trùng đáp án sau khi bỏ dấu và bỏ khoảng trắng
///      (nhờ vậy "CÁ HEO.png", "ca heo.png", "CAHEO.png.png" đều nhận).
///   2. Đường dẫn ghi sẵn trong puzzles.json (Assets/Puzzles/pNNN.png).
/// Trả về null nếu chưa có ảnh — màn chơi sẽ hiện mô tả thay thế.
/// </summary>
public static class PuzzleImageLocator
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

    // Quét thư mục CauHoi một lần rồi nhớ lại: khóa = đáp án chuẩn hóa, không khoảng trắng.
    private static Dictionary<string, string>? _cauHoiIndex;

    public static string? Find(Puzzle puzzle)
    {
        string key = Key(puzzle.Answer);
        if (key.Length > 0 && Index().TryGetValue(key, out string? path))
            return path;

        if (!string.IsNullOrWhiteSpace(puzzle.Image))
        {
            string full = Path.Combine(
                AppContext.BaseDirectory,
                puzzle.Image.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) return full;
        }

        return null;
    }

    /// <summary>Xóa bộ nhớ đệm để lần sau quét lại thư mục ảnh.</summary>
    public static void Refresh() => _cauHoiIndex = null;

    private static Dictionary<string, string> Index()
    {
        if (_cauHoiIndex != null) return _cauHoiIndex;

        var map = new Dictionary<string, string>();
        string dir = Path.Combine(AppContext.BaseDirectory, "Assets", "CauHoi");

        if (Directory.Exists(dir))
        {
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (!Extensions.Contains(ext)) continue;

                // Bỏ hết phần đuôi: "SONGCHO.png.png" -> "SONGCHO"
                string name = Path.GetFileName(file);
                int dot = name.IndexOf('.');
                if (dot > 0) name = name[..dot];

                string key = Key(name);
                if (key.Length > 0) map.TryAdd(key, file);
            }
        }

        return _cauHoiIndex = map;
    }

    /// <summary>"CÁ HEO" -> "caheo" để so tên file không cần gõ dấu.</summary>
    private static string Key(string text) => AnswerChecker.Normalize(text).Replace(" ", "");
}
