using System.Collections.Concurrent;
using System.Security.Cryptography;
using DuoiHinhBatChu.Models;

namespace DuoiHinhBatChu.Server;

/// <summary>
/// Cấp và tra vé đăng nhập.
///
/// Vé chỉ nằm trong bộ nhớ nên khởi động lại máy chủ là mọi người phải đăng
/// nhập lại. Với một ván đấu trong nhà thì vậy là đủ; muốn giữ phiên qua lần
/// khởi động sau thì đổi sang JWT có hạn dùng.
/// </summary>
public class TokenService
{
    private readonly ConcurrentDictionary<string, Account> _tokens = new();

    public string Issue(Account account)
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        _tokens[token] = account;
        return token;
    }

    public Account? Resolve(string? token) =>
        token != null && _tokens.TryGetValue(token, out Account? a) ? a : null;
}
