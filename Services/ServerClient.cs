using System.Net.Http;
using System.Net.Http.Json;
using DuoiHinhBatChu.Models;

namespace DuoiHinhBatChu.Services;

/// <summary>Máy chủ trả lời được hay không, và nếu được thì nó đang có gì.</summary>
/// <param name="Ok">Nối được và máy chủ trả lời hợp lệ.</param>
/// <param name="Message">Câu hiện thẳng lên giao diện.</param>
public readonly record struct ServerStatus(bool Ok, string Message);

/// <summary>Kết quả đăng nhập / đăng ký trên máy chủ.</summary>
/// <param name="Auth">Vé đăng nhập, chỉ có khi <paramref name="Ok"/> đúng.</param>
public readonly record struct ServerAuth(bool Ok, string Message, AuthResponse? Auth);

/// <summary>
/// Phần REST của client: thử kết nối, đăng nhập, đăng ký, và địa chỉ ảnh câu đố.
///
/// Ván đấu chạy theo thời gian thực nên đi đường khác — xem <see cref="MatchClient"/>.
/// </summary>
public class ServerClient
{
    /// <summary>Chờ lâu hơn thế này thì coi như máy chủ không có ở đó.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Địa chỉ đã chuẩn hóa của lần gọi gần nhất, ví dụ "http://192.168.1.10:5180".</summary>
    public string BaseAddress { get; private set; } = "";

    /// <summary>
    /// Thêm "http://" nếu người chơi chỉ gõ "192.168.1.10:5180", và bỏ dấu "/" cuối.
    /// </summary>
    public static string Normalize(string address)
    {
        address = address.Trim().TrimEnd('/');
        if (address.Length == 0) return "";

        return address.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? address
            : "http://" + address;
    }

    /// <summary>Đường tải ảnh của một câu, dùng cho màn đấu.</summary>
    public string ImageUrl(string imageName) =>
        $"{BaseAddress}/api/puzzles/{Uri.EscapeDataString(imageName)}/image";

    /// <summary>
    /// Tải ảnh một câu về thành mảng byte.
    ///
    /// Phải tự tải rồi mới dựng ảnh: đưa thẳng địa chỉ http cho BitmapImage thì
    /// nó tải ngầm ở luồng khác, mình chưa có gì trong tay đã gọi Freeze nên hỏng.
    /// </summary>
    public async Task<byte[]?> DownloadImageAsync(string imageName)
    {
        using var http = NewClient();

        try
        {
            return await http.GetByteArrayAsync(ImageUrl(imageName));
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            return null;
        }
    }

    public async Task<ServerStatus> CheckAsync(string address)
    {
        if (!Prepare(address, out string error))
            return new ServerStatus(false, error);

        using var http = NewClient();

        try
        {
            HealthResponse? health =
                await http.GetFromJsonAsync<HealthResponse>($"{BaseAddress}/api/health");

            if (health == null)
                return new ServerStatus(false, "Máy chủ trả lời nhưng không đọc được nội dung.");

            return new ServerStatus(true,
                $"Nối được. Máy chủ có {health.PuzzleCount} câu đố, " +
                $"{health.RoomCount} phòng đang mở.");
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            return new ServerStatus(false, Explain(ex));
        }
    }

    /// <summary>
    /// Đăng nhập vào máy chủ. Tài khoản trên máy chủ là một sổ riêng, không phải
    /// tài khoản lưu ở máy này — vì máy chủ mới là bên ghi điểm ván đấu.
    /// </summary>
    public Task<ServerAuth> LoginAsync(string address, string userName, string password) =>
        PostAuthAsync(address, "login", new LoginRequest(userName, password));

    public Task<ServerAuth> RegisterAsync(
        string address, string userName, string displayName, string password) =>
        PostAuthAsync(address, "register",
                      new RegisterRequest(userName, displayName, password, password));

    // ----- Nội bộ -----

    private async Task<ServerAuth> PostAuthAsync(string address, string path, object body)
    {
        if (!Prepare(address, out string error))
            return new ServerAuth(false, error, null);

        using var http = NewClient();

        try
        {
            HttpResponseMessage response =
                await http.PostAsJsonAsync($"{BaseAddress}/api/auth/{path}", body);

            if (response.IsSuccessStatusCode)
            {
                AuthResponse? auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
                return auth == null
                    ? new ServerAuth(false, "Máy chủ trả lời nhưng không đọc được nội dung.", null)
                    : new ServerAuth(true, "", auth);
            }

            // Máy chủ từ chối có lý do (sai mật khẩu, trùng tên...) thì hiện đúng lý do đó
            ErrorResponse? problem = await ReadErrorAsync(response);
            return new ServerAuth(false, problem?.Error ?? $"Máy chủ từ chối ({(int)response.StatusCode}).", null);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            return new ServerAuth(false, Explain(ex), null);
        }
    }

    private static async Task<ErrorResponse?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ErrorResponse>();
        }
        catch
        {
            return null;
        }
    }

    private bool Prepare(string address, out string error)
    {
        BaseAddress = Normalize(address);
        error = BaseAddress.Length == 0 ? "Chưa nhập địa chỉ máy chủ." : "";
        return BaseAddress.Length > 0;
    }

    private static HttpClient NewClient() => new() { Timeout = Timeout };

    private static string Explain(Exception ex) => ex is TaskCanceledException
        ? "Máy chủ không trả lời trong 5 giây."
        : $"Không nối được: {ex.Message}";
}
