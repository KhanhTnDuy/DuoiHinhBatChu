using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DuoiHinhBatChu.Data;
using DuoiHinhBatChu.Models;
using Microsoft.EntityFrameworkCore;

namespace DuoiHinhBatChu.Services;

/// <summary>Kết quả một lần đăng ký hoặc đăng nhập.</summary>
/// <param name="Account">Tài khoản lấy được, null khi thất bại.</param>
/// <param name="Error">Lời báo lỗi để hiện thẳng lên giao diện, rỗng khi thành công.</param>
public readonly record struct AuthResult(Account? Account, string Error)
{
    public bool Ok => Account != null;

    public static AuthResult Success(Account a) => new(a, "");
    public static AuthResult Fail(string error) => new(null, error);
}

/// <summary>
/// Đăng ký và đăng nhập bằng bảng <c>Accounts</c> trong cơ sở dữ liệu
/// (SQLite, file Data/game.db — xem <see cref="GameDatabase"/>).
///
/// Mật khẩu băm bằng PBKDF2-SHA256, mỗi tài khoản một muối ngẫu nhiên,
/// và so sánh theo kiểu chống dò thời gian.
///
/// Lớp này không giữ sẵn DbContext nào: mỗi việc mở một phiên rồi đóng ngay,
/// nhờ vậy máy chủ dùng chung một AccountService cho nhiều người cùng lúc vẫn
/// an toàn.
///
/// Lưu ý: ở máy người chơi đây là đăng nhập cục bộ, đủ để tách tiến trình giữa
/// nhiều người dùng chung một máy. Chế độ đấu online thì tài khoản nằm trong
/// cơ sở dữ liệu của máy chủ — xem Docs/MULTIPLAYER.md.
/// </summary>
public class AccountService
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 100_000;

    private const int UserNameMin = 3;
    private const int UserNameMax = 20;
    private const int PasswordMin = 4;
    private const int DisplayNameMax = 20;

    /// <summary>Tên đăng nhập chỉ nhận chữ cái không dấu, chữ số và gạch dưới.</summary>
    private static readonly Regex UserNamePattern =
        new("^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

    /// <summary>Số điện thoại Việt Nam: 10 chữ số, bắt đầu bằng 0.</summary>
    private static readonly Regex PhonePattern =
        new("^0[0-9]{9}$", RegexOptions.Compiled);

    /// <param name="dbPath">Đường dẫn cơ sở dữ liệu thay thế, chỉ dùng khi test.</param>
    public AccountService(string? dbPath = null) => GameDatabase.EnsureReady(dbPath);

    /// <summary>Đã có tài khoản nào chưa — dùng để chọn mở tab Đăng nhập hay Đăng ký.</summary>
    public bool HasAnyAccount()
    {
        using GameDbContext db = GameDatabase.Open();
        return db.Accounts.Any(a => !a.IsGuest);
    }

    /// <summary>Tạo tài khoản mới rồi ghi xuống cơ sở dữ liệu.</summary>
    /// <param name="phone">
    /// Số điện thoại, dùng để lấy lại tài khoản khi quên mật khẩu.
    /// </param>
    public AuthResult Register(string userName, string displayName, string password,
                               string confirm, string phone)
    {
        userName = userName.Trim();
        displayName = displayName.Trim();
        phone = NormalizePhone(phone);

        if (userName.Length is < UserNameMin or > UserNameMax)
            return AuthResult.Fail($"Tên đăng nhập cần {UserNameMin}-{UserNameMax} ký tự.");

        if (!UserNamePattern.IsMatch(userName))
            return AuthResult.Fail("Tên đăng nhập chỉ gồm chữ không dấu, số và dấu gạch dưới.");

        if (password.Length < PasswordMin)
            return AuthResult.Fail($"Mật khẩu cần ít nhất {PasswordMin} ký tự.");

        if (password != confirm)
            return AuthResult.Fail("Hai lần nhập mật khẩu chưa giống nhau.");

        if (displayName.Length > DisplayNameMax)
            return AuthResult.Fail($"Tên hiển thị tối đa {DisplayNameMax} ký tự.");

        if (!PhonePattern.IsMatch(phone))
            return AuthResult.Fail("Số điện thoại phải là 10 chữ số và bắt đầu bằng 0.");

        using GameDbContext db = GameDatabase.Open();

        // SQLite so sánh chuỗi có phân biệt hoa thường, nên phải hạ cả hai bên
        // về chữ thường thì "Nam" và "nam" mới coi là một tên
        string lower = userName.ToLowerInvariant();
        if (db.Accounts.Any(a => a.UserName.ToLower() == lower))
            return AuthResult.Fail("Tên đăng nhập này đã có người dùng.");

        // Một số chỉ gắn một tài khoản, không thì lúc quên mật khẩu không biết mở tài khoản nào
        if (db.Accounts.Any(a => a.Phone == phone))
            return AuthResult.Fail("Số điện thoại này đã dùng cho một tài khoản khác.");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);

        var account = new Account
        {
            UserName = userName,
            DisplayName = displayName.Length > 0 ? displayName : userName,
            Phone = phone,
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(Hash(password, salt)),
        };

        db.Accounts.Add(account);

        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            // Hai người bấm đăng ký cùng lúc cùng một tên: kiểm tra ở trên lọt
            // lưới nhưng ràng buộc duy nhất trong bảng vẫn chặn được
            return AuthResult.Fail("Tên đăng nhập hoặc số điện thoại này vừa có người dùng.");
        }

        return AuthResult.Success(account);
    }

    /// <summary>Kiểm tra tên đăng nhập và mật khẩu.</summary>
    public AuthResult Login(string userName, string password)
    {
        userName = userName.Trim();

        if (userName.Length == 0 || password.Length == 0)
            return AuthResult.Fail("Nhập đủ tên đăng nhập và mật khẩu.");

        using GameDbContext db = GameDatabase.Open();

        string lower = userName.ToLowerInvariant();
        Account? account = db.Accounts.FirstOrDefault(a => a.UserName.ToLower() == lower);

        // Báo lỗi chung cho cả hai trường hợp để không lộ tài khoản nào có thật
        if (account == null || account.IsGuest || !Verify(password, account))
            return AuthResult.Fail("Sai tên đăng nhập hoặc mật khẩu.");

        return AuthResult.Success(account);
    }

    /// <summary>
    /// Quên mật khẩu: khai đúng tên đăng nhập và số điện thoại đã đăng ký thì
    /// được đặt mật khẩu mới ngay.
    ///
    /// Đây là cách của một game chơi trong nhà, không phải cách của ngân hàng:
    /// ai biết số điện thoại của bạn là vào được. Muốn chắc thì phải gửi mã xác
    /// nhận qua SMS, mà việc đó cần dịch vụ nhắn tin trả tiền.
    /// </summary>
    public AuthResult ResetPassword(string userName, string phone,
                                    string newPassword, string confirm)
    {
        userName = userName.Trim();
        phone = NormalizePhone(phone);

        if (userName.Length == 0 || phone.Length == 0)
            return AuthResult.Fail("Nhập đủ tên đăng nhập và số điện thoại.");

        if (newPassword.Length < PasswordMin)
            return AuthResult.Fail($"Mật khẩu mới cần ít nhất {PasswordMin} ký tự.");

        if (newPassword != confirm)
            return AuthResult.Fail("Hai lần nhập mật khẩu chưa giống nhau.");

        using GameDbContext db = GameDatabase.Open();

        string lower = userName.ToLowerInvariant();
        Account? account = db.Accounts.FirstOrDefault(a => a.UserName.ToLower() == lower);

        // Nói chung một câu, không tách "không có tài khoản" với "sai số điện thoại",
        // để người lạ không dò được ai đang dùng số nào
        if (account == null || account.IsGuest || account.Phone != phone)
            return AuthResult.Fail("Tên đăng nhập và số điện thoại không khớp.");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        account.PasswordSalt = Convert.ToBase64String(salt);
        account.PasswordHash = Convert.ToBase64String(Hash(newPassword, salt));

        // account là đối tượng do chính phiên này lấy lên nên EF đang theo dõi
        // nó: chỉ cần sửa thuộc tính rồi lưu, không phải gọi Update
        db.SaveChanges();
        return AuthResult.Success(account);
    }

    /// <summary>Bỏ khoảng trắng, dấu chấm và gạch nối để "0912 345 678" cũng khớp.</summary>
    private static string NormalizePhone(string phone) =>
        new(phone.Where(char.IsDigit).ToArray());

    /// <summary>Đổi tên hiển thị của tài khoản đang đăng nhập.</summary>
    public void UpdateDisplayName(Account account, string displayName)
    {
        if (account.IsGuest) return;

        using GameDbContext db = GameDatabase.Open();

        Account? stored = db.Accounts.FirstOrDefault(a => a.Id == account.Id);
        if (stored == null) return;

        stored.DisplayName = displayName.Trim();
        db.SaveChanges();

        account.DisplayName = stored.DisplayName;
    }

    // ----- Băm mật khẩu -----

    private static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);

    private static bool Verify(string password, Account account)
    {
        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(account.PasswordSalt);
            expected = Convert.FromBase64String(account.PasswordHash);
        }
        catch (FormatException)
        {
            return false;   // dữ liệu tài khoản bị sửa tay hỏng
        }

        // So sánh theo thời gian cố định, không thoát sớm ở byte đầu tiên khác nhau
        return CryptographicOperations.FixedTimeEquals(Hash(password, salt), expected);
    }
}
