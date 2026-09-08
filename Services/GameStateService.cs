using System;
using System.IO;
using System.Text.Json;
using DuoiHinhBatChu.Models;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Quản lý lưu trữ trạng thái và tiến trình người chơi.
/// </summary>
public class GameStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _savePath;

    public GameStateService(string? path = null)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dir);
        _savePath = path ?? Path.Combine(dir, "player_save.json");
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
