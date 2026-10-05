using System.ComponentModel.DataAnnotations.Schema;

namespace DuoiHinhBatChu.Models;

/// <summary>
/// Một tài khoản người chơi, mỗi đối tượng là một dòng trong bảng Accounts.
///
/// Mật khẩu không bao giờ lưu dạng chữ thường: chỉ giữ chuỗi băm PBKDF2 và
/// muối (salt) riêng của từng tài khoản — xem <see cref="Services.AccountService"/>.
/// </summary>
public class Account
{
    /// <summary>Mã tài khoản, dùng để đặt tên file lưu tiến trình.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Tên đăng nhập, không dấu, không trùng nhau.</summary>
    public string UserName { get; set; } = "";

    /// <summary>Tên hiển thị trong game và trên bảng xếp hạng.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Số điện thoại khai lúc đăng ký. Đây là thứ duy nhất để lấy lại tài khoản
    /// khi quên mật khẩu, nên mỗi số chỉ gắn được một tài khoản.
    /// </summary>
    public string Phone { get; set; } = "";

    /// <summary>Chuỗi băm PBKDF2 của mật khẩu, mã Base64.</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>Muối ngẫu nhiên của riêng tài khoản này, mã Base64.</summary>
    public string PasswordSalt { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Tài khoản khách: chơi ngay không cần đăng ký, tiến trình lưu chung một chỗ
    /// và không dùng được cho chế độ đấu online.
    /// </summary>
    public bool IsGuest { get; set; }

    /// <summary>
    /// Mã của tài khoản khách. Là hằng số vì nhiều chỗ phải nhận ra khách:
    /// không lưu tiến trình, không lên bảng xếp hạng, không đấu online.
    /// </summary>
    public const string GuestId = "khach";

    /// <summary>
    /// Chữ cái đầu của tên, hiện trong ô vuông thay cho ảnh đại diện: "Nguyễn
    /// An" ra "NA", tên một chữ thì lấy một chữ cái.
    ///
    /// [NotMapped]: đây là chữ tính ra từ <see cref="DisplayName"/>, không phải
    /// một cột trong bảng.
    /// </summary>
    [NotMapped]
    public string Initials
    {
        get
        {
            string[] words = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return words.Length switch
            {
                0 => "?",
                1 => words[0][..1].ToUpperInvariant(),
                _ => (words[0][..1] + words[^1][..1]).ToUpperInvariant(),
            };
        }
    }

    /// <summary>
    /// Lời chào ở đầu màn chế độ và màn menu — một câu cho cả hai màn, để chúng
    /// nối nhau đọc liền mạch.
    ///
    /// Người quay lại được chào khác người mới: "Chào mừng trở lại" chỉ đúng khi
    /// tài khoản đã có tiến trình lưu. Khách thì lần nào cũng là lần đầu, vì hồ
    /// sơ khách bị dọn sạch mỗi lần khởi động nên không có "lần trước" để nhớ.
    /// </summary>
    public string Greeting(bool hasPlayedBefore) => hasPlayedBefore
        ? $"Chào mừng trở lại, {DisplayName}!"
        : $"Chào mừng, {DisplayName}!";

    public static Account Guest() => new()
    {
        Id = GuestId,
        UserName = GuestId,
        DisplayName = "Khách",
        IsGuest = true,
    };
}
