using System.IO;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Thư mục <c>Assets/TaiNguyen</c> chứa ảnh tài nguyên của ứng dụng: mã QR ủng
/// hộ, logo, hình trang trí… — mọi thứ KHÔNG phải ảnh câu đố.
///
/// Cách dùng giống hệt ảnh câu đố: **tên file chính là tên tài nguyên**, còn
/// đuôi file gì cũng được.
///
///   Assets/TaiNguyen/qr-ung-ho.png   -> AppImageLocator.Find("qr-ung-ho")
///   Assets/TaiNguyen/qr-ung-ho.jpg   -> vẫn ra đúng file đó
///
/// Khác với ảnh câu đố ở một chỗ: ảnh câu đố nạp vào cơ sở dữ liệu (xem
/// <see cref="Data.PuzzleSync"/>) vì máy chủ phải gửi chúng cho người chơi
/// khác, còn ảnh tài nguyên chỉ nằm cạnh file .exe và đọc thẳng từ đĩa.
/// </summary>
public static class AppImageLocator
{
    private static readonly string[] Extensions =
        [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];

    /// <summary>Thư mục ảnh tài nguyên, nằm cạnh file .exe.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Assets", "TaiNguyen");

    /// <summary>
    /// Tìm đường dẫn của một ảnh tài nguyên theo tên, không phân biệt hoa
    /// thường và không cần biết đuôi file. Không có thì trả về null — nơi gọi
    /// tự quyết định hiện gì thay thế, chứ thiếu ảnh không được làm hỏng app.
    /// </summary>
    public static string? Find(string name)
    {
        if (!Directory.Exists(Folder)) return null;

        return Directory.EnumerateFiles(Folder).FirstOrDefault(file =>
            Path.GetFileNameWithoutExtension(file)
                .Equals(name, StringComparison.OrdinalIgnoreCase)
            && Extensions.Contains(Path.GetExtension(file).ToLowerInvariant()));
    }
}
