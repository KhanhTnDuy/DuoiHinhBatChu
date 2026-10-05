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
        if (await IsPortOpenAsync(address))
        {
            ServerStatus status = await probe.CheckAsync(address);
            if (status.Ok) return "";

            // Cổng CÓ người nghe mà không phải máy chủ của game (hoặc nó không
            // trả lời): bật máy chủ mới ở cổng đó chỉ thất bại, nên báo thẳng lý do.
            return status.Message;
        }

        if (!IsLocal(address))
            return $"Không nối được máy chủ ở {ServerClient.Normalize(address)}. " +
                   "Kiểm tra: máy tạo phòng đã bấm Tạo phòng chưa, hai máy cùng một mạng chưa, " +
                   "và tường lửa Windows của máy đó có cho phép (nếu Wi-Fi ở chế độ Public thì " +
                   "phải tích cả mạng công cộng).";

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
                // Nghe ở mọi card mạng (0.0.0.0) chứ không chỉ localhost, để máy
                // khác trong LAN nối vào được. Chỉ nghe localhost thì máy kia
                // gõ đúng IP cũng bị từ chối.
                Arguments = $"--urls http://0.0.0.0:{ListenPort(address)}",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex)
        {
            return $"Không bật được máy chủ: {ex.Message}";
        }

        // Process.Start trả null khi hệ thống dùng lại một tiến trình có sẵn —
        // với file .exe thường thì không xảy ra, nhưng null là không có gì để theo dõi
        if (_process == null)
            return "Không bật được máy chủ: hệ thống không tạo tiến trình mới.";

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

    /// <summary>Cổng trong địa chỉ, không ghi cổng thì dùng cổng mặc định.</summary>
    private static int ListenPort(string address) =>
        Uri.TryCreate(ServerClient.Normalize(address), UriKind.Absolute, out Uri? uri) && uri.Port > 0
            ? uri.Port
            : DefaultPort;

    /// <summary>Có ai đang nghe ở cổng của <paramref name="address"/> không, trả lời trong tối đa nửa giây.</summary>
    private static async Task<bool> IsPortOpenAsync(string address)
    {
        if (!Uri.TryCreate(ServerClient.Normalize(address), UriKind.Absolute, out Uri? uri))
            return false;

        try
        {
            // Máy này: cổng không ai nghe thì bị từ chối ngay, nửa giây là dư. Máy
            // khác qua Wi-Fi: tường lửa chặn thì gói tin bị NUỐT chứ không bị từ
            // chối, và lần chạm đầu qua Wi-Fi có thể mất hơn nửa giây để thức dậy
            // — cho 2 giây để khỏi báo "không nối được" oan.
            var wait = TimeSpan.FromMilliseconds(IsLocal(address) ? 500 : 2000);
            using var tcp = new System.Net.Sockets.TcpClient();
            using var cts = new CancellationTokenSource(wait);
            await tcp.ConnectAsync(uri.Host, uri.Port, cts.Token);
            return tcp.Connected;
        }
        catch
        {
            return false;   // từ chối, hết giờ, tên máy sai… đều là "không có ai"
        }
    }

    /// <summary>
    /// Địa chỉ LAN của máy này để đọc cho bạn bè gõ vào ("192.168.1.85:5180").
    ///
    /// Máy thường có nhiều card: Wi-Fi thật cùng card ảo của VirtualBox / Hyper-V /
    /// VPN, và đưa nhầm IP ảo thì bạn bè gõ mãi không vào. Card thật là card có
    /// cổng ra (gateway); không card nào có (mạng nội bộ không qua router) thì mới
    /// liệt kê hết các card đang chạy.
    /// </summary>
    public static List<string> LanAddresses(int port = DefaultPort)
    {
        var up = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                     && n.NetworkInterfaceType is not (System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                                                     or System.Net.NetworkInformation.NetworkInterfaceType.Tunnel))
            .ToList();

        static bool HasGateway(System.Net.NetworkInformation.NetworkInterface n) =>
            n.GetIPProperties().GatewayAddresses.Any(g =>
                g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && !g.Address.Equals(System.Net.IPAddress.Any));

        List<System.Net.NetworkInformation.NetworkInterface> real = up.Where(HasGateway).ToList();

        return (real.Count > 0 ? real : up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                     && !System.Net.IPAddress.IsLoopback(a)
                     && !a.ToString().StartsWith("169.254."))     // địa chỉ tự gán, không có mạng thật
            .Select(a => $"{a}:{port}")
            .Distinct()
            .ToList();
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
