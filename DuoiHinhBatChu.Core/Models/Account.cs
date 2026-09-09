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

    public static Account Guest() => new()
    {
        Id = "khach",
        UserName = "khach",
        DisplayName = "Khách",
        IsGuest = true,
    };
}
