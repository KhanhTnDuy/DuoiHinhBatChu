using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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

    /// <summary>Host là "localhost" (có thể có "http://" đứng trước), theo sau là cổng, đường dẫn hoặc hết chuỗi.</summary>
    private static readonly Regex Localhost =
        new(@"^(http://)?localhost(?=[:/]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Địa chỉ không ghi cổng: "host" hoặc "http://host", sau host không có ":số".</summary>
    private static readonly Regex NoPort =
        new(@"^(?:http://)?(\[[^\]]+\]|[^:/\s]+)(?=/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Thêm "http://" nếu người chơi chỉ gõ "192.168.1.10:5180", và bỏ dấu "/" cuối.
    ///
    /// Không ghi cổng thì dùng cổng của máy chủ game (5180). Không làm vậy thì
    /// "192.168.1.10" thành http://192.168.1.10 — cổng 80, nơi máy chủ của game
    /// không bao giờ nghe — và người chơi nhận "Không nối được" dù gõ đúng IP.
    /// </summary>
    public static string Normalize(string address)
    {
        address = address.Trim().TrimEnd('/');
        if (address.Length == 0) return "";

        if (address.StartsWith("https", StringComparison.OrdinalIgnoreCase)) return address;

        // "localhost" -> 127.0.0.1. Máy chủ do app bật nghe ở 0.0.0.0 (chỉ IPv4, để
        // máy khác trong LAN nối được), mà .NET thử "localhost" bằng ::1 trước, và
        // ::1 ở đây không bị từ chối mà TREO: đo 2026-10-05 thấy mỗi request tới
        // localhost mất 2,05 giây (127.0.0.1: 1-5 ms), còn thăm dò cổng thì luôn hết
        // 500ms nên tưởng máy chủ không chạy. ServerClient tạo HttpClient mới cho
        // từng lời gọi, nên chủ phòng chịu 2 giây đó ở MỖI ảnh câu đố — trong khi
        // bạn bè nối bằng IP lại nhanh.
        address = Localhost.Replace(address, m => m.Groups[1].Value + "127.0.0.1", 1);

        if (NoPort.IsMatch(address))
            address = NoPort.Replace(address, m => $"{m.Groups[1].Value}:{LocalServer.DefaultPort}", 1);

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

            // Đọc được JSON chưa đủ: chương trình khác cũng có /api/health. Thiếu
            // bước này, một API lạ giữ cổng 5180 bị nhận nhầm là máy chủ của game
            // ("Máy chủ có 0 câu đố"), game không bật máy chủ của mình, rồi đăng
            // nhập báo "Máy chủ từ chối (404)" mà không ai hiểu vì sao.
            if (health.App != HealthResponse.AppName)
                return new ServerStatus(false,
                    $"Cổng {new Uri(BaseAddress).Port} đang bị một chương trình khác dùng, " +
                    "không phải máy chủ của game. Tắt chương trình đó đi rồi thử lại.");

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
        string address, string userName, string displayName, string password, string phone) =>
        PostAuthAsync(address, "register",
                      new RegisterRequest(userName, displayName, password, password, phone));

    /// <summary>
    /// Vào máy chủ bằng chính tài khoản ở máy này, không hỏi gì thêm.
    ///
    /// Máy chủ vẫn giữ sổ tài khoản riêng (nó là bên ghi điểm ván đấu), nhưng
    /// người chơi không phải đăng ký lần nữa: client tự đăng nhập bằng tên đăng
    /// nhập của tài khoản trên máy, còn "mật khẩu" là mã tài khoản — chuỗi
    /// ngẫu nhiên 32 ký tự sinh lúc tạo tài khoản, chỉ máy này biết. Lần đầu
    /// gặp máy chủ thì đăng nhập không có, chuyển sang đăng ký với cùng bộ đó.
    ///
    /// Đăng ký cũng bị từ chối (tên đã có trên máy chủ) thì còn một cửa: tài
    /// khoản cùng tên đó có thể chính là của người này, tạo từ một bản cài
    /// trước — xóa Data/game.db rồi đăng ký lại cùng tên là mã tài khoản đổi,
    /// mật khẩu trên máy chủ không khớp nữa và người chơi bị chặn khỏi chế độ
    /// đấu vĩnh viễn. Máy chủ có sẵn "quên mật khẩu" bằng số điện thoại, nên
    /// thử luôn: đúng số đã khai thì đặt lại mật khẩu thành mã mới và vào được.
    /// Sai số (tên đó của người khác thật) thì mới báo lỗi ra ngoài.
    /// </summary>
    public async Task<ServerAuth> SignInAsync(string address, Account account)
    {
        string secret = account.Id;

        ServerAuth auth = await LoginAsync(address, account.UserName, secret);
        if (auth.Ok) return auth;

        // Không nối được thì đăng ký cũng vô ích, báo luôn lỗi kết nối
        if (auth.Message.StartsWith("Không nối được") || auth.Message.StartsWith("Máy chủ không trả lời"))
            return auth;

        ServerAuth reg = await RegisterAsync(address, account.UserName, account.DisplayName, secret, account.Phone);
        if (reg.Ok || account.Phone.Length == 0) return reg;

        ServerAuth reset = await ResetPasswordAsync(address, account.UserName, account.Phone, secret);
        if (reset.Ok) return reset;

        // Lấy lại không được thì lỗi đăng ký mới là lỗi thật. Nhưng nguyên văn của nó
        // ("Tên đăng nhập này đã có người dùng") khó hiểu với người vừa vào phòng của
        // bạn: họ chưa hề đăng ký gì trên máy chủ này. Hay gặp nhất khi tự thử bằng
        // hai máy cùng dùng tài khoản "test" — mỗi máy có một "test" riêng.
        if (reg.Message.StartsWith("Tên đăng nhập này đã có"))
            return reg with
            {
                Message = $"Trên máy chủ này đã có người khác dùng tên đăng nhập \"{account.UserName}\" " +
                          "(tài khoản trên mỗi máy là riêng nhau). Đổi sang tên đăng nhập khác ở máy bạn rồi thử lại.",
            };

        if (reg.Message.StartsWith("Số điện thoại này đã dùng"))
            return reg with
            {
                Message = "Số điện thoại của tài khoản bạn đã được một tài khoản khác dùng trên máy chủ này. " +
                          "Dùng tài khoản có số điện thoại khác rồi thử lại.",
            };

        return reg;
    }

    public Task<ServerAuth> ResetPasswordAsync(
        string address, string userName, string phone, string newPassword) =>
        PostAuthAsync(address, "reset-password",
                      new ResetPasswordRequest(userName, phone, newPassword, newPassword));

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
