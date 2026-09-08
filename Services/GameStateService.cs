using System;
using System.IO;
using System.Text.Json;
using DuoiHinhBatChu.Models;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Quản lý lưu trữ tiến trình người chơi.
/// Mỗi tài khoản một file riêng trong Data/saves/ nên nhiều người dùng chung
/// một máy vẫn giữ được điểm, mạng và câu đang chơi của riêng mình.
/// </summary>
public class GameStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _savePath;

    /// <param name="accountId">Mã tài khoản, dùng làm tên file lưu.</param>
    /// <param name="path">Đường dẫn thay thế, chỉ dùng khi test.</param>
    public GameStateService(string accountId, string? path = null)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Data", "saves");
        Directory.CreateDirectory(dir);
        _savePath = path ?? Path.Combine(dir, $"{Sanitize(accountId)}.json");
    }

    /// <summary>Bỏ ký tự không đặt tên file được, phòng khi mã tài khoản bị sửa tay.</summary>
    private static string Sanitize(string id)
    {
        var safe = id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray();
        return safe.Length > 0 ? new string(safe) : "khach";
    }

    public PlayerProfile LoadProfile()
    {
        if (File.Exists(_savePath))
        {
            try
            {
                string json = File.ReadAllText(_savePath);
                var profile = JsonSerializer.Deserialize<PlayerProfile>(json, JsonOptions);
                if (profile != null) return profile;
            }
            catch
            {
                // Fallback nếu file lỗi
            }
        }
        return new PlayerProfile();
    }

    public void SaveProfile(PlayerProfile profile)
    {
        try
        {
            string json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(_savePath, json);
        }
        catch { }
    }

    public void ResetProfile()
    {
        try
        {
            if (File.Exists(_savePath))
            {
                File.Delete(_savePath);
            }
        }
        catch { }
    }
}
