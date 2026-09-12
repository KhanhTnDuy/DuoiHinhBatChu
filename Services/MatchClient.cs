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

        _hub = new HubConnectionBuilder()
            .WithUrl($"{baseAddress}/game")
            .WithAutomaticReconnect()
            .Build();

        On<RoundInfo>("RoundStarted", x => RoundStarted?.Invoke(x), toUi);
        On<AnswerResult>("AnswerJudged", x => AnswerJudged?.Invoke(x), toUi);
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

    public Task<RoomState> CreateRoomAsync(string password) =>
        _hub.InvokeAsync<RoomState>("CreateRoom", _token, password);

    public Task<RoomState> JoinRoomAsync(string code, string password) =>
        _hub.InvokeAsync<RoomState>("JoinRoom", _token, code.Trim().ToUpperInvariant(), password);

    public Task SetReadyAsync(bool ready) =>
        _hub.InvokeAsync("SetReady", _token, ready);

    public Task SetModeAsync(MatchMode mode) =>
        _hub.InvokeAsync("SetMode", _token, mode);

    public Task StartMatchAsync(int rounds) =>
        _hub.InvokeAsync("StartMatch", _token, rounds);

    public Task SubmitAnswerAsync(string answer) =>
        _hub.InvokeAsync("SubmitAnswer", _token, answer);

    public Task LeaveRoomAsync() => _hub.InvokeAsync("LeaveRoom", _token);

    public async ValueTask DisposeAsync() => await _hub.DisposeAsync();

    private void On<T>(string name, Action<T> handler, Action<Action> toUi) =>
        _hub.On<T>(name, x => toUi(() => handler(x)));
}
