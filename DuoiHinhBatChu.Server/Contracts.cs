namespace DuoiHinhBatChu.Server;

// ===== Xác thực (REST) =====

public record RegisterRequest(string UserName, string DisplayName, string Password, string Confirm);

public record LoginRequest(string UserName, string Password);

/// <param name="Token">Đưa kèm mỗi lần gọi về sau, thay cho việc gửi lại mật khẩu.</param>
public record AuthResponse(string Token, string AccountId, string UserName, string DisplayName);

public record ErrorResponse(string Error);

public record HealthResponse(string App, string Version, int PuzzleCount, int RoomCount);

// ===== Phòng chơi (SignalR) =====

/// <param name="IsHost">Người tạo phòng, chỉ người này bấm bắt đầu được.</param>
public record PlayerInfo(string AccountId, string DisplayName, bool IsHost, int Score);

public record RoomState(string Code, string HostAccountId, bool IsPlaying, int RoundNumber,
                        int TotalRounds, IReadOnlyList<PlayerInfo> Players);

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
public record AnswerResult(string AccountId, string DisplayName, bool Correct,
                           int Points, double Seconds);

/// <param name="Answer">Đáp án đầy đủ có dấu, chỉ lộ ra khi câu đã kết thúc.</param>
public record RoundEnded(int RoundNumber, string Answer, IReadOnlyList<PlayerInfo> Scores);

public record MatchEnded(IReadOnlyList<PlayerInfo> Scores);
