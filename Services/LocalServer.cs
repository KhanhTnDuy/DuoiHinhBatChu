using System.Diagnostics;
using System.IO;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Tự bật máy chủ đấu ngay trên máy này khi cần.
///
/// Trước đây chế độ đấu bắt người chơi mở thêm một cửa sổ dòng lệnh chạy
/// <c>dotnet run --project DuoiHinhBatChu.Server</c> rồi mới bấm tạo phòng —
/// không ai làm thế, nên bấm "Tạo phòng" là gặp ngay "Không nối được". Lớp
/// này lo việc đó: địa chỉ trong <see cref="AppSettings.ServerAddress"/> mà
/// trỏ về chính máy này và chưa có ai nghe ở cổng đó, thì tìm file chạy của
/// máy chủ và bật lên, chờ nó trả lời rồi mới nối.
///
/// Địa chỉ trỏ sang máy khác (đấu qua LAN) thì không làm gì — máy chủ phải ở
/// bên đó.
///
/// Máy chủ do app bật thì app tắt là tắt theo (<see cref="Stop"/>), để không
/// bỏ lại một tiến trình mồ côi cứ nghe cổng 5180 mãi. Hệ quả: hai bản app
/// trên cùng một máy đấu với nhau thì bản nào bật máy chủ, bản đó thoát là
/// ván đứt — chấp nhận, vì đó chỉ là cách thử trên một máy.
/// </summary>
public static class LocalServer
{
    private static Process? _process;

    /// <summary>Cổng máy chủ mặc định, khớp với <c>launchSettings.json</c> của dự án Server.</summary>
    public const int DefaultPort = 5180;

    /// <summary>Địa chỉ này có trỏ về chính máy đang chạy app không.</summary>
    public static bool IsLocal(string address)
    {
        if (!Uri.TryCreate(ServerClient.Normalize(address), UriKind.Absolute, out Uri? uri))
            return false;

        return uri.IsLoopback
            || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Bảo đảm có máy chủ nghe ở <paramref name="address"/>. Đã có thì trả về
    /// ngay; chưa có mà là máy này thì bật lên và chờ. Trả về rỗng nếu ổn,
    /// ngược lại là câu báo lỗi để hiện lên màn hình.
    /// </summary>
    /// <param name="report">Nơi nhận tiến trình ("Đang bật máy chủ..."), để người chơi biết đang chờ gì.</param>
    public static async Task<string> EnsureRunningAsync(string address, Action<string> report)
    {
        var probe = new ServerClient();

        // Gõ cửa bằng TCP trước: cổng không ai nghe thì biết ngay trong vài
        // chục mili giây. Hỏi thẳng bằng HttpClient thì lần đầu mất tới ~4 giây
        // (khởi tạo HttpClient, thử cả ::1 lẫn 127.0.0.1) chỉ để nhận "không
        // nối được" — người chơi ngồi nhìn "Đang nối máy chủ..." vô ích.
        if (await IsPortOpenAsync(address) && (await probe.CheckAsync(address)).Ok) return "";

        if (!IsLocal(address))
            return $"Không nối được máy chủ ở {ServerClient.Normalize(address)}. " +
                   "Máy đó phải đang chạy DuoiHinhBatChu.Server.";

        if (_process is { HasExited: false })
            return "Máy chủ đã được bật nhưng chưa trả lời. Thử lại sau vài giây.";

        string? exe = FindExecutable();
        if (exe == null)
            return "Không tìm thấy DuoiHinhBatChu.Server.exe. Hãy build dự án Server " +
                   "(dotnet build DuoiHinhBatChu.Server) rồi thử lại.";

        report("Đang bật máy chủ trên máy này...");

        try
        {
            _process = Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                // Chạy thẳng file .exe thì Kestrel không đọc launchSettings.json,
                // nên phải chỉ rõ cổng, không thì nó nghe ở 5000
                Arguments = $"--urls {ServerClient.Normalize(address)}",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex)
        {
            return $"Không bật được máy chủ: {ex.Message}";
        }

        // Máy chủ cần vài giây: mở cơ sở dữ liệu, đối chiếu kho ảnh, rồi mới
        // nghe cổng. Hỏi thăm mỗi nửa giây, tối đa 20 giây.
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(500);

            if (_process.HasExited)
                return $"Máy chủ vừa bật đã tắt (mã {_process.ExitCode}). " +
                       "Có thể cổng đang bị chương trình khác giữ.";

            if ((await probe.CheckAsync(address)).Ok) return "";
        }

        return "Máy chủ đã bật nhưng chưa trả lời sau 20 giây.";
    }

    /// <summary>Có ai đang nghe ở cổng của <paramref name="address"/> không, trả lời trong tối đa nửa giây.</summary>
    private static async Task<bool> IsPortOpenAsync(string address)
    {
        if (!Uri.TryCreate(ServerClient.Normalize(address), UriKind.Absolute, out Uri? uri))
            return false;

        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await tcp.ConnectAsync(uri.Host, uri.Port, cts.Token);
            return tcp.Connected;
        }
        catch
        {
            return false;   // từ chối, hết giờ, tên máy sai… đều là "không có ai"
        }
    }

    /// <summary>Tắt máy chủ nếu là app này bật. Gọi lúc app thoát.</summary>
    public static void Stop()
    {
        if (_process is not { HasExited: false }) return;

        try { _process.Kill(entireProcessTree: true); }
        catch { /* đang tắt app, không còn gì để báo */ }
        finally { _process.Dispose(); _process = null; }
    }

    /// <summary>
    /// Tìm file chạy của máy chủ. Hai chỗ, theo thứ tự:
    ///   1. <c>Server\</c> nằm cạnh app — dành cho bản đóng gói sau này.
    ///   2. Thư mục build của dự án Server trong cùng kho mã, đi ngược từ
    ///      <c>bin\Debug\net10.0-windows</c> lên gốc — dành cho lúc đang phát
    ///      triển. Dự án app tham chiếu dự án Server (chỉ để build cùng), nên
    ///      build app xong là file này luôn có.
    /// </summary>
    private static string? FindExecutable()
    {
        const string name = "DuoiHinhBatChu.Server.exe";
        string baseDir = AppContext.BaseDirectory;

        string packaged = Path.Combine(baseDir, "Server", name);
        if (File.Exists(packaged)) return packaged;

        // bin\<Configuration>\net10.0-windows\ -> gốc kho mã là 3 cấp trên
        string? root = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        string configuration = new DirectoryInfo(baseDir).Parent?.Name ?? "Debug";
        string dev = Path.Combine(root, "DuoiHinhBatChu.Server", "bin", configuration, "net10.0", name);
        return File.Exists(dev) ? dev : null;
    }
}
