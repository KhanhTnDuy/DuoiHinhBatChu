using System.IO;
using System.Text.Json;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Vài tùy chọn chung của cả ứng dụng, không thuộc riêng tài khoản nào:
/// nhớ chế độ sáng/tối và tên đăng nhập lần trước để điền sẵn ô đăng nhập.
/// Lưu ở Data/app-settings.json.
/// </summary>
public class AppSettings
{
    public bool IsDarkTheme { get; set; }
    public string LastUserName { get; set; } = "";

    /// <summary>Địa chỉ máy chủ đấu nhiều người dùng lần gần nhất.</summary>
    public string ServerAddress { get; set; } = "";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath
    {
        get
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "app-settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))
                       ?? new AppSettings();
        }
        catch
        {
            // File hỏng thì quay về mặc định
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (IOException)
        {
        }
    }
}
