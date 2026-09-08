using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DuoiHinhBatChu.Models;

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
/// Đăng ký và đăng nhập bằng danh sách tài khoản lưu ở Data/accounts.json.
///
/// Mật khẩu băm bằng PBKDF2-SHA256, mỗi tài khoản một muối ngẫu nhiên,
/// và so sánh theo kiểu chống dò thời gian.
///
/// Lưu ý: đây là đăng nhập cục bộ trên máy người chơi, đủ để tách tiến trình
/// giữa nhiều người dùng chung một máy. Khi làm chế độ đấu online thì việc
/// xác thực phải chuyển lên máy chủ — xem Docs/MULTIPLAYER.md.
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

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public AccountService(string? filePath = null)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dir);
        _filePath = filePath ?? Path.Combine(dir, "accounts.json");
    }

    /// <summary>Đã có tài khoản nào chưa — dùng để chọn mở tab Đăng nhập hay Đăng ký.</summary>
    public bool HasAnyAccount() => Load().Count > 0;

    /// <summary>Tạo tài khoản mới rồi ghi xuống file.</summary>
    public AuthResult Register(string userName, string displayName, string password, string confirm)
    {
        userName = userName.Trim();
        displayName = displayName.Trim();

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

        List<Account> accounts = Load();

        if (accounts.Any(a => a.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase)))
            return AuthResult.Fail("Tên đăng nhập này đã có người dùng.");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);

        var account = new Account
        {
            UserName = userName,
            DisplayName = displayName.Length > 0 ? displayName : userName,
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(Hash(password, salt)),
        };

        accounts.Add(account);
        Save(accounts);

        return AuthResult.Success(account);
    }

    /// <summary>Kiểm tra tên đăng nhập và mật khẩu.</summary>
    public AuthResult Login(string userName, string password)
    {
        userName = userName.Trim();

        if (userName.Length == 0 || password.Length == 0)
            return AuthResult.Fail("Nhập đủ tên đăng nhập và mật khẩu.");

        Account? account = Load()
            .FirstOrDefault(a => a.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

        // Báo lỗi chung cho cả hai trường hợp để không lộ tài khoản nào có thật
        if (account == null || !Verify(password, account))
            return AuthResult.Fail("Sai tên đăng nhập hoặc mật khẩu.");

        return AuthResult.Success(account);
    }

    /// <summary>Đổi tên hiển thị của tài khoản đang đăng nhập.</summary>
    public void UpdateDisplayName(Account account, string displayName)
    {
        if (account.IsGuest) return;

        List<Account> accounts = Load();
        Account? stored = accounts.FirstOrDefault(a => a.Id == account.Id);
        if (stored == null) return;

        stored.DisplayName = displayName.Trim();
        account.DisplayName = stored.DisplayName;
        Save(accounts);
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
            return false;   // file tài khoản bị sửa tay hỏng
        }

        // So sánh theo thời gian cố định, không thoát sớm ở byte đầu tiên khác nhau
        return CryptographicOperations.FixedTimeEquals(Hash(password, salt), expected);
    }

    // ----- Đọc ghi file -----

    private List<Account> Load()
    {
        if (!File.Exists(_filePath)) return new();

        try
        {
            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<Account>>(json, JsonOptions) ?? new();
        }
        catch
        {
            return new();   // file hỏng thì coi như chưa có tài khoản nào
        }
    }

    private void Save(List<Account> accounts)
    {
        try
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(accounts, JsonOptions));
        }
        catch (IOException)
        {
        }
    }
}
