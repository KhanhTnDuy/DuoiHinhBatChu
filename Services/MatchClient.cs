using DuoiHinhBatChu.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Đầu dây bên client của <c>GameHub</c>.
///
/// Lớp này chỉ lo việc nối dây và dịch lời gọi; không giữ luật chơi nào cả.
/// Luật nằm hết trên máy chủ — client gửi đáp án lên rồi chờ máy chủ chấm,
/// nên không ai sửa được điểm của mình bằng cách sửa bản chạy trên máy họ.
///
/// Mọi sự kiện bắn ra đã được đưa về luồng giao diện sẵn, view model cứ thế
/// gán thẳng vào thuộc tính.
/// </summary>
public class MatchClient : IAsyncDisposable
{
    private readonly string _token;
    private readonly HubConnection _hub;

    /// <summary>Máy chủ vừa phát một câu mới.</summary>
    public event Action<RoundInfo>? RoundStarted;

    /// <summary>Có người vừa gửi đáp án và máy chủ đã chấm xong.</summary>
    public event Action<AnswerResult>? AnswerJudged;

    /// <summary>Đố nhau: đầu mỗi câu, máy chủ báo ai đang chọn câu (người ra đề nhận kèm danh sách).</summary>
    public event Action<PickInfo>? PickStarted;

    /// <summary>Hết câu: lúc này đáp án mới lộ ra.</summary>
    public event Action<RoundEnded>? RoundEnded;

    public event Action<MatchEnded>? MatchEnded;

    /// <summary>Có người vào hoặc rời phòng.</summary>
    public event Action<RoomState>? RoomChanged;

    /// <summary>Rớt kết nối; tham số là lý do để hiện lên màn hình.</summary>
    public event Action<string>? Disconnected;

    /// <param name="baseAddress">Địa chỉ đã chuẩn hóa, ví dụ "http://192.168.1.10:5180".</param>
    /// <param name="token">Vé lấy được lúc đăng nhập máy chủ.</param>
    /// <param name="toUi">
    /// Cách đẩy một việc về luồng giao diện. Sự kiện của SignalR tới từ luồng nền,
    /// mà WPF chỉ cho sửa dữ liệu ràng buộc từ luồng của nó.
    /// </param>
    public MatchClient(string baseAddress, string token, Action<Action> toUi)
    {
        _token = token;

        // KHÔNG tự nối lại (WithAutomaticReconnect). Máy chủ nhận ra đứt dây là
        // xóa người đó khỏi phòng ngay (GameHub.OnDisconnectedAsync); nối lại
        // xong là một kết nối MỚI, không ở phòng nào, mà sự kiện Closed lại không
        // bắn — client cứ tưởng mình vẫn trong phòng, gửi đáp án thì bị "Bạn
        // chưa ở trong phòng nào". Để đứt là đứt hẳn, Closed bắn, màn đấu về
        // sảnh chờ và người chơi vào lại phòng cho rõ ràng.
        _hub = new HubConnectionBuilder()
            .WithUrl($"{baseAddress}/game")
            .Build();

        On<RoundInfo>("RoundStarted", x => RoundStarted?.Invoke(x), toUi);
        On<AnswerResult>("AnswerJudged", x => AnswerJudged?.Invoke(x), toUi);
        On<PickInfo>("PickStarted", x => PickStarted?.Invoke(x), toUi);
        On<RoundEnded>("RoundEnded", x => RoundEnded?.Invoke(x), toUi);
        On<MatchEnded>("MatchEnded", x => MatchEnded?.Invoke(x), toUi);
        On<RoomState>("RoomChanged", x => RoomChanged?.Invoke(x), toUi);

        _hub.Closed += error =>
        {
            toUi(() => Disconnected?.Invoke(
                error?.Message ?? "Mất kết nối tới máy chủ."));
            return Task.CompletedTask;
        };
    }

    public bool IsConnected => _hub.State == HubConnectionState.Connected;

    public Task ConnectAsync() => _hub.StartAsync();

    public Task<RoomState> CreateRoomAsync(string name, string password) =>
        _hub.InvokeAsync<RoomState>("CreateRoom", _token, name, password);

    public Task<RoomState> JoinRoomAsync(string code, string password) =>
        _hub.InvokeAsync<RoomState>("JoinRoom", _token, code.Trim().ToUpperInvariant(), password);

    public Task SetReadyAsync(bool ready) =>
        _hub.InvokeAsync("SetReady", _token, ready);

    public Task SetModeAsync(MatchMode mode) =>
        _hub.InvokeAsync("SetMode", _token, mode);

    public Task StartMatchAsync(int rounds) =>
        _hub.InvokeAsync("StartMatch", _token, rounds);

    public Task PickPuzzleAsync(string imageKey) =>
        _hub.InvokeAsync("PickPuzzle", _token, imageKey);

    public Task SubmitAnswerAsync(string answer) =>
        _hub.InvokeAsync("SubmitAnswer", _token, answer);

    public Task LeaveRoomAsync() => _hub.InvokeAsync("LeaveRoom", _token);

    public async ValueTask DisposeAsync() => await _hub.DisposeAsync();

    private void On<T>(string name, Action<T> handler, Action<Action> toUi) =>
        _hub.On<T>(name, x => toUi(() => handler(x)));
}
