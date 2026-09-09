using System.IO;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Thư mục <c>Assets/TaiNguyen</c> chứa ảnh tài nguyên của ứng dụng: mã QR ủng
/// hộ, logo, hình trang trí… — mọi thứ KHÔNG phải ảnh câu đố.
///
/// Cách dùng giống hệt ảnh câu đố: **tên file chính là tên tài nguyên**, còn
/// đuôi file gì cũng được. Khoảng trắng, gạch nối, gạch dưới và chuyện hoa hay
/// thường đều bỏ qua, nên đặt tên kiểu nào cũng khớp:
///
///   Assets/TaiNguyen/qr-ung-ho.png   -> AppImageLocator.Find("qr-ung-ho")
///   Assets/TaiNguyen/QR UNG HO.jpg   -> vẫn ra đúng file đó
///   Assets/TaiNguyen/Qr_Ung_Ho.webp  -> vẫn ra đúng file đó
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
    /// Tìm đường dẫn của một ảnh tài nguyên theo tên, không cần biết đuôi file.
    /// Không có thì trả về null — nơi gọi tự quyết định hiện gì thay thế, chứ
    /// thiếu ảnh không được làm hỏng app.
    /// </summary>
    public static string? Find(string name)
    {
        if (!Directory.Exists(Folder)) return null;

        string want = Key(name);

        return Directory.EnumerateFiles(Folder).FirstOrDefault(file =>
            Extensions.Contains(Path.GetExtension(file).ToLowerInvariant())
            && Key(Path.GetFileNameWithoutExtension(file)) == want);
    }

    /// <summary>
    /// Rút tên về dạng để so khớp: bỏ hết thứ không phải chữ và số, rồi hạ về
    /// chữ thường. Nhờ vậy "QR UNG HO", "qr-ung-ho" và "Qr_Ung_Ho" cùng ra
    /// "qrungho" — người đặt tên file không phải nhớ đúng cách viết trong code.
    /// </summary>
    private static string Key(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
