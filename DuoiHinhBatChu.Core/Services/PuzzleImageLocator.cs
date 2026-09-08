using System.IO;
using System.Text.RegularExpressions;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Thư mục <c>Assets/CauHoi</c> là nguồn duy nhất của các câu đố:
/// mỗi file ảnh là một câu, và tên file chính là đáp án.
///
/// Quy ước đặt tên:
///   "CÁ HEO.png"        -> đáp án "CÁ HEO"
///   "01 - CÁ HEO.png"   -> đáp án "CÁ HEO", xếp thứ tự số 1
///   "CAHEO.png.png"     -> đáp án "CAHEO" (mọi phần đuôi đều bị cắt bỏ)
///
/// Viết dấu tiếng Việt và khoảng trắng ngay trong tên file, vì đáp án hiển thị
/// trên các ô chữ đúng như tên file.
/// </summary>
public static class PuzzleImageLocator
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

    /// <summary>Số thứ tự tùy chọn ở đầu tên file, vd "01 - ", "2_", "003.".</summary>
    private static readonly Regex OrderPrefix =
        new(@"^\s*(\d+)\s*[-_.]\s*", RegexOptions.Compiled);

    /// <summary>Một file ảnh đã đọc được đáp án từ tên.</summary>
    public readonly record struct ImageEntry(int Order, string Answer, string Path);

    /// <summary>Thư mục chứa ảnh câu đố, nằm cạnh file .exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Assets", "CauHoi");

    /// <summary>
    /// Quét thư mục ảnh, trả về danh sách câu đố đã sắp xếp:
    /// file có số thứ tự đứng trước, phần còn lại xếp theo bảng chữ cái.
    /// Hai file cho ra cùng một đáp án thì chỉ lấy file đầu tiên.
    /// </summary>
    public static List<ImageEntry> Scan()
    {
        var result = new List<ImageEntry>();
        if (!Directory.Exists(Folder)) return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in Directory.EnumerateFiles(Folder))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (!Extensions.Contains(ext)) continue;

            // Cắt mọi phần đuôi: "CÁ HEO.png.png" -> "CÁ HEO"
            string name = Path.GetFileName(file);
            int dot = name.IndexOf('.');
            if (dot > 0) name = name[..dot];

            // Tách số thứ tự nếu có
            int order = int.MaxValue;
            Match m = OrderPrefix.Match(name);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n))
            {
                order = n;
                name = name[m.Length..];
            }

            string answer = Normalize(name);
            if (answer.Length == 0) continue;
            if (!seen.Add(answer)) continue;

            result.Add(new ImageEntry(order, answer, file));
        }

        return result
            .OrderBy(e => e.Order)
            .ThenBy(e => e.Answer, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>Gộp khoảng trắng thừa và viết hoa toàn bộ đáp án.</summary>
    private static string Normalize(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
              .ToUpperInvariant();
}
