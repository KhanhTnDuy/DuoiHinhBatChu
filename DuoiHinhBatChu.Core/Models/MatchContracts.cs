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
///   - Draw: "Tôi vẽ bạn đoán" — một người vẽ, những người còn lại đoán chữ.
/// </summary>
public enum MatchMode { Compete = 0, Draw = 1 }

/// <param name="IsHost">Người tạo phòng, chỉ người này bấm bắt đầu được.</param>
/// <param name="IsReady">Đã bấm "sẵn sàng" ở sảnh chờ; chủ phòng chỉ bắt đầu được khi mọi người đều sẵn sàng.</param>
public record PlayerInfo(string AccountId, string DisplayName, bool IsHost, bool IsReady, int Score);

/// <param name="Code">Mã phòng máy chủ sinh ra, chủ phòng đọc cho bạn bè gõ vào.</param>
/// <param name="Mode">Kiểu chơi chủ phòng đã chọn; người vào sau nhìn thấy nhưng không đổi được.</param>
/// <param name="MaxPlayers">Sức chứa của phòng, tính cả chủ phòng.</param>
public record RoomState(string Code, string HostAccountId, MatchMode Mode, bool IsPlaying,
                        int RoundNumber, int TotalRounds, int MaxPlayers,
                        IReadOnlyList<PlayerInfo> Players);

/// <summary>
/// Một câu phát cho người chơi. Cố ý KHÔNG có đáp án: client vẽ ô trống theo
/// <paramref name="WordLengths"/> và bàn phím theo <paramref name="Tiles"/>,
/// còn đúng/sai do máy chủ chấm.
/// </summary>
/// <param name="ImageName">Tên file ảnh, tải qua GET /api/puzzles/{imageName}/image.</param>
/// <param name="SecondsAllowed">Hết mốc này thì không ai ghi điểm câu đó nữa.</param>
public record RoundInfo(int RoundNumber, int TotalRounds, string ImageName,
                        int[] WordLengths, string Tiles, int Difficulty,
                        double SecondsAllowed);

/// <param name="Seconds">Thời gian trả lời, do máy chủ đo.</param>
/// <param name="CooldownSeconds">
/// Đoán sai thì phải chờ chừng này giây mới được gửi tiếp; 0 là gửi được ngay.
/// Máy chủ mới là bên giữ mốc chờ — số này chỉ để client hiện ra cho biết.
/// </param>
public record AnswerResult(string AccountId, string DisplayName, bool Correct,
                           int Points, double Seconds, double CooldownSeconds);

/// <param name="Answer">Đáp án đầy đủ có dấu, chỉ lộ ra khi câu đã kết thúc.</param>
public record RoundEnded(int RoundNumber, string Answer, IReadOnlyList<PlayerInfo> Scores);

public record MatchEnded(IReadOnlyList<PlayerInfo> Scores);
