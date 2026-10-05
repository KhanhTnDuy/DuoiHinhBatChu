namespace DuoiHinhBatChu.Models;

// Các bản tin đi lại giữa máy chủ và client của chế độ đấu nhiều người.
//
// Đặt ở Core vì cả hai bên phải hiểu y hệt nhau: máy chủ gửi đi, client đọc vào.
// Để ở riêng mỗi bên một bản là kiểu gì cũng có ngày sửa một bên quên bên kia.

// ===== Xác thực (REST) =====

public record RegisterRequest(string UserName, string DisplayName, string Password,
                              string Confirm, string Phone);

/// <param name="Phone">Số đã khai lúc đăng ký; khớp thì được đặt mật khẩu mới.</param>
public record ResetPasswordRequest(string UserName, string Phone,
                                   string NewPassword, string Confirm);

public record LoginRequest(string UserName, string Password);

/// <param name="Token">Đưa kèm mỗi lần gọi về sau, thay cho việc gửi lại mật khẩu.</param>
public record AuthResponse(string Token, string AccountId, string UserName, string DisplayName);

public record ErrorResponse(string Error);

public record HealthResponse(string App, string Version, int PuzzleCount, int RoomCount);

// ===== Phòng chơi (SignalR) =====

/// <summary>
/// Kiểu chơi của một phòng, chỉ chủ phòng chọn được, chọn trước khi bắt đầu.
///   - Compete: cả phòng cùng nhận một ảnh câu đố, ai ghép chữ nhanh hơn thắng.
///   - Duel: "Đố nhau" — hai người lần lượt làm người ra đề: chọn một câu từ
///     kho để đố người kia; người kia ghép chữ trong thời gian cho phép.
/// </summary>
public enum MatchMode { Compete = 0, Duel = 1 }

/// <param name="IsHost">Người tạo phòng, chỉ người này bấm bắt đầu được.</param>
/// <param name="IsReady">Đã bấm "sẵn sàng" ở sảnh chờ; chủ phòng chỉ bắt đầu được khi mọi người đều sẵn sàng.</param>
/// <param name="Lives">Số mạng còn lại trong ván; hết mạng là ván kết thúc, ai nhiều điểm hơn thắng.</param>
public record PlayerInfo(string AccountId, string DisplayName, bool IsHost, bool IsReady, int Score,
                         int Lives = 0);

/// <param name="Code">Mã phòng máy chủ sinh ra, chủ phòng đọc cho bạn bè gõ vào.</param>
/// <param name="Name">Tên phòng chủ phòng đặt, chỉ để hiển thị; rỗng nếu không đặt.</param>
/// <param name="Mode">Kiểu chơi chủ phòng đã chọn; người vào sau nhìn thấy nhưng không đổi được.</param>
/// <param name="MaxPlayers">Sức chứa của phòng, tính cả chủ phòng.</param>
public record RoomState(string Code, string Name, string HostAccountId, MatchMode Mode, bool IsPlaying,
                        int RoundNumber, int MaxPlayers, IReadOnlyList<PlayerInfo> Players);

/// <summary>
/// Một câu phát cho người chơi. Cố ý KHÔNG có đáp án: client vẽ ô trống theo
/// <paramref name="WordLengths"/> và bàn phím theo <paramref name="Tiles"/>,
/// còn đúng/sai do máy chủ chấm.
/// </summary>
/// <param name="ImageName">
/// MÃ tải ảnh (chuỗi hex ngẫu nhiên máy chủ cấp mỗi lần khởi động), dùng cho
/// GET /api/puzzles/{imageName}/image. Cố ý KHÔNG phải tên file: tên file
/// chính là đáp án, gửi xuống là lộ.
/// </param>
/// <param name="SecondsAllowed">Hết mốc này thì không ai ghi điểm câu đó nữa.</param>
/// <param name="Category">
/// Chủ đề của đáp án ("Động vật", "Địa danh"…) — gợi ý cho không, giống màn
/// Cổ điển. Chỉ gửi tên chủ đề; câu dẫn "Đây là một con vật" do client dựng
/// bằng <c>CategoryPrompt</c>.
/// </param>
/// <param name="AskerAccountId">
/// Chế độ Đố nhau: người ra đề của câu này (người kia là người đoán). Rỗng ở
/// chế độ Thi đấu, nơi cả hai cùng đoán.
/// </param>
public record RoundInfo(int RoundNumber, string ImageName,
                        int[] WordLengths, string Tiles, int Difficulty,
                        double SecondsAllowed, string Category = "",
                        string AskerAccountId = "", string AskerName = "");

/// <summary>Một câu trong số các câu người ra đề được chọn. Người ra đề thấy cả đáp án.</summary>
/// <param name="ImageKey">Mã tải ảnh, giống <see cref="RoundInfo.ImageName"/>.</param>
public record PickOption(string ImageKey, string Answer, string Category);

/// <summary>
/// Chế độ Đố nhau, đầu mỗi câu: máy chủ báo ai đang chọn câu. Chỉ người ra đề
/// nhận được <paramref name="Options"/>; người đoán nhận mảng rỗng, nên không
/// đọc trước được đáp án nào.
/// </summary>
public record PickInfo(int RoundNumber, string AskerAccountId, string AskerName,
                       double SecondsToPick, PickOption[] Options);

/// <param name="Seconds">Thời gian trả lời, do máy chủ đo.</param>
/// <param name="CooldownSeconds">
/// Đoán sai thì phải chờ chừng này giây mới được gửi tiếp; 0 là gửi được ngay.
/// Máy chủ mới là bên giữ mốc chờ — số này chỉ để client hiện ra cho biết.
/// </param>
/// <param name="Judged">
/// Máy chủ có thật sự chấm không. false khi đáp án tới lúc còn trong quãng
/// phạt (hoặc đã trả lời rồi, câu đã hết): lúc đó Correct = false KHÔNG có
/// nghĩa là sai — client không được nháy đỏ và xóa chữ đang ghép.
/// </param>
public record AnswerResult(string AccountId, string DisplayName, bool Correct,
                           int Points, double Seconds, double CooldownSeconds,
                           bool Judged = true);

/// <param name="Answer">Đáp án đầy đủ có dấu, chỉ lộ ra khi câu đã kết thúc.</param>
/// <param name="AskerBonus">Chế độ Đố nhau: điểm người ra đề nhận được vì người kia không đoán ra.</param>
public record RoundEnded(int RoundNumber, string Answer, IReadOnlyList<PlayerInfo> Scores,
                         int AskerBonus = 0);

public record MatchEnded(IReadOnlyList<PlayerInfo> Scores);
