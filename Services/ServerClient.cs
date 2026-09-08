using System.Net.Http;
using System.Net.Http.Json;

namespace DuoiHinhBatChu.Services;

/// <summary>Máy chủ trả lời được hay không, và nếu được thì nó đang có gì.</summary>
/// <param name="Ok">Nối được và máy chủ trả lời hợp lệ.</param>
/// <param name="Message">Câu hiện thẳng lên giao diện.</param>
public readonly record struct ServerStatus(bool Ok, string Message);

/// <summary>
/// Phần client nói chuyện với DuoiHinhBatChu.Server.
///
/// Hiện mới có phép thử kết nối. Phần vào phòng và đấu thật chạy trên SignalR,
/// sẽ thêm ở bước sau — xem Docs/MULTIPLAYER.md.
/// </summary>
public class ServerClient
{
    /// <summary>Chờ lâu hơn thế này thì coi như máy chủ không có ở đó.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private record HealthResponse(string App, string Version, int PuzzleCount, int RoomCount);

    public async Task<ServerStatus> CheckAsync(string address)
    {
        address = address.Trim().TrimEnd('/');
        if (address.Length == 0)
            return new ServerStatus(false, "Chưa nhập địa chỉ máy chủ.");

        if (!address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            address = "http://" + address;

        using var http = new HttpClient { Timeout = Timeout };

        try
        {
            HealthResponse? health =
                await http.GetFromJsonAsync<HealthResponse>($"{address}/api/health");

            if (health == null)
                return new ServerStatus(false, "Máy chủ trả lời nhưng không đọc được nội dung.");

            return new ServerStatus(true,
                $"Nối được. Máy chủ có {health.PuzzleCount} câu đố, " +
                $"{health.RoomCount} phòng đang mở.");
        }
        catch (TaskCanceledException)
        {
            return new ServerStatus(false, "Máy chủ không trả lời trong 5 giây.");
        }
        catch (HttpRequestException ex)
        {
            return new ServerStatus(false, $"Không nối được: {ex.Message}");
        }
    }
}
