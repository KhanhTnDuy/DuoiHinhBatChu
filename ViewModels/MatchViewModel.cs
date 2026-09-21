using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.ViewModels;

/// <summary>Việc người chơi định làm khi bước vào phòng chờ.</summary>
public enum LobbyMode { Join, Create }

/// <summary>Một dòng trong bảng điểm.</summary>
public class ScoreRow : ViewModelBase
{
    public required string AccountId { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsHost { get; init; }
    public required bool IsMe { get; init; }

    private bool _isReady;
    /// <summary>Đã bấm sẵn sàng ở sảnh chờ.</summary>
    public bool IsReady
    {
        get => _isReady;
        set => SetProperty(ref _isReady, value);
    }

    private int _score;
    public int Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    private bool _hasAnswered;
    /// <summary>Đã trả lời đúng câu đang chạy — cả phòng cùng nhìn thấy.</summary>
    public bool HasAnswered
    {
        get => _hasAnswered;
        set => SetProperty(ref _hasAnswered, value);
    }
}

/// <summary>
/// Màn đấu nhiều người: phòng chờ rồi vào ván.
///
/// Khác hẳn <see cref="GameViewModel"/> ở chỗ view model này KHÔNG biết đáp án.
/// Nó chỉ nhận số ô và bộ phím chữ, ghép xong thì gửi lên máy chủ chấm. Nhờ vậy
/// không ai đọc trước được đáp án, và thời gian trả lời do máy chủ đo chứ không
/// phải máy người chơi tự khai.
/// </summary>
public class MatchViewModel : ViewModelBase
{
    /// <summary>Đoán sai thì tô đỏ chừng này rồi trả ô về trống cho ghép lại.</summary>
    private static readonly TimeSpan WrongFlash = TimeSpan.FromMilliseconds(700);

    private readonly Account _account;
    private readonly AppSettings _settings;
    private readonly ServerClient _server = new();

    /// <summary>
    /// Đường dây tới máy chủ. Null cho tới khi người chơi tạo / vào phòng lần
    /// đầu — cửa sổ này mở ra mà chưa nối gì cả, xem <see cref="EnsureConnectedAsync"/>.
    /// </summary>
    private MatchClient? _client;

    /// <summary>Mã tài khoản TRÊN MÁY CHỦ, chỉ biết sau khi đăng nhập máy chủ xong.</summary>
    private string _myAccountId = "";

    /// <summary>Nhịp đếm ngược, chỉ để hiện lên màn hình — mốc thật nằm ở máy chủ.</summary>
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _flash = new() { Interval = WrongFlash };

    /// <summary>Đếm ngược quãng chờ sau khi đoán sai.</summary>
    private readonly DispatcherTimer _cooldown =
        new() { Interval = TimeSpan.FromMilliseconds(200) };

    private DateTime _blockedUntil;

    private DateTime _roundStartedLocal;
    private double _secondsAllowed = 1;
    private int _nextTileId;

    /// <summary>Bắn lên khi người chơi bấm quay lại ở phòng chờ.</summary>
    public event Action? GoBack;

    public MatchViewModel(Account account, AppSettings settings, LobbyMode mode)
    {
        _account = account;
        _settings = settings;
        _lobbyMode = mode;
        PlayerName = account.DisplayName;

        _tick.Tick += (_, _) => OnPropertyChanged(nameof(SecondsLeftText));
        _flash.Tick += (_, _) => { _flash.Stop(); ClearSlots(); };
        _cooldown.Tick += (_, _) => ShowCooldownLeft();

        CreateRoomCommand = new RelayCommand(async _ => await CreateRoomAsync(), _ => !IsBusy);
        JoinRoomCommand = new RelayCommand(async _ => await JoinRoomAsync(), _ => !IsBusy);
        StartMatchCommand = new RelayCommand(async _ => await StartMatchAsync(), _ => CanStart);
        PlaceLetterCommand = new RelayCommand(p => PlaceLetter(p as LetterTile));
        TakeBackCommand = new RelayCommand(p => TakeBack(p as AnswerSlot));
        SubmitCommand = new RelayCommand(_ => Submit(), _ => CanSubmit);
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        SwitchLobbyModeCommand = new RelayCommand(_ => IsCreating = !IsCreating);
        BackCommand = new RelayCommand(_ => GoBack?.Invoke(), _ => IsInLobby);
        ToggleReadyCommand = new RelayCommand(async _ => await ToggleReadyAsync(), _ => IsInRoom && !IsPlaying && !IsBusy);
        ChooseModeCommand = new RelayCommand(async p => await ChooseModeAsync((MatchMode)p!), _ => IsHost && !IsBusy);
    }

    // ----- Lệnh cho giao diện -----
    public RelayCommand CreateRoomCommand { get; }
    public RelayCommand JoinRoomCommand { get; }
    public RelayCommand StartMatchCommand { get; }
    public RelayCommand PlaceLetterCommand { get; }
    public RelayCommand TakeBackCommand { get; }
    public RelayCommand SubmitCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand SwitchLobbyModeCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand ToggleReadyCommand { get; }
    public RelayCommand ChooseModeCommand { get; }

    // ----- Dữ liệu hiển thị -----
    public ObservableCollection<AnswerSlot> Slots { get; } = new();
    public ObservableCollection<LetterTile> Tiles { get; } = new();
    public ObservableCollection<ScoreRow> Players { get; } = new();

    public string PlayerName { get; }

    public bool IsDarkTheme => _settings.IsDarkTheme;

    private string _status = "";
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) RaiseCommandStates();
        }
    }
    // ----- Phòng chờ -----

    private LobbyMode _lobbyMode;
    /// <summary>
    /// Đang ở dạng "tạo phòng" hay "vào phòng". Cùng một cặp ô tên + mật khẩu,
    /// chỉ khác nút bấm gọi lệnh nào — tách ra hai dạng để người chơi không
    /// phải nghĩ "tôi nên bấm nút nào", vì họ đã chọn từ màn chế độ rồi.
    /// </summary>
    public bool IsCreating
    {
        get => _lobbyMode == LobbyMode.Create;
        private set
        {
            LobbyMode next = value ? LobbyMode.Create : LobbyMode.Join;
            if (next == _lobbyMode) return;

            _lobbyMode = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LobbyTitle));
            OnPropertyChanged(nameof(SwitchLobbyModeText));
        }
    }

    public string LobbyTitle => IsCreating ? "TẠO PHÒNG MỚI" : "VÀO PHÒNG CÓ SẴN";

    public string SwitchLobbyModeText => IsCreating
        ? "Đã có phòng bạn bè mở? Vào phòng"
        : "Chưa ai mở phòng? Tạo phòng mới";


    private string _roomNameInput = "";
    /// <summary>Tên phòng chủ phòng đặt lúc tạo; để trống cũng được.</summary>
    public string RoomNameInput
    {
        get => _roomNameInput;
        set => SetProperty(ref _roomNameInput, value);
    }

    private string _joinCode = "";
    /// <summary>Mã phòng người chơi gõ để vào; chỉ dùng ở dạng "vào phòng".</summary>
    public string JoinCode
    {
        get => _joinCode;
        set => SetProperty(ref _joinCode, value);
    }

    /// <summary>
    /// Mật khẩu phòng. PasswordBox không ràng buộc hai chiều được, nên
    /// code-behind của cửa sổ đẩy giá trị vào đây mỗi lần người chơi gõ.
    /// </summary>
    private string _roomPassword = "";
    public string RoomPassword
    {
        get => _roomPassword;
        set => SetProperty(ref _roomPassword, value);
    }

    private string _roomCode = "";
    /// <summary>Mã phòng đang ở (máy chủ cấp); rỗng nghĩa là chưa vào phòng nào.</summary>
    public string RoomCode
    {
        get => _roomCode;
        private set
        {
            if (!SetProperty(ref _roomCode, value)) return;
            OnPropertyChanged(nameof(IsInRoom));
            RaiseCommandStates();
        }
    }

    public bool IsInRoom => RoomCode.Length > 0;

    private string _roomName = "";
    /// <summary>Tên phòng đang ở; rỗng nếu chủ phòng không đặt.</summary>
    public string RoomName
    {
        get => _roomName;
        private set => SetProperty(ref _roomName, value);
    }

    private int _maxPlayers = 5;
    /// <summary>Sức chứa máy chủ báo, để hiện "3/5".</summary>
    public int MaxPlayers
    {
        get => _maxPlayers;
        private set
        {
            if (SetProperty(ref _maxPlayers, value)) OnPropertyChanged(nameof(PlayerCountText));
        }
    }

    public string PlayerCountText => $"{Players.Count}/{MaxPlayers} người";

    private bool _isReady;
    /// <summary>Mình đã bấm sẵn sàng chưa; giá trị lấy từ RoomState máy chủ gửi về.</summary>
    public bool IsReady
    {
        get => _isReady;
        private set
        {
            if (SetProperty(ref _isReady, value)) OnPropertyChanged(nameof(ReadyButtonText));
        }
    }

    public string ReadyButtonText => IsReady ? "✓ Đã sẵn sàng — bấm để hủy" : "Sẵn sàng";

    /// <summary>Mọi người trừ chủ phòng đã sẵn sàng — điều kiện thứ hai để bắt đầu.</summary>
    public bool AllGuestsReady => Players.All(p => p.IsReady || p.IsHost);

    /// <summary>Vì sao chưa bắt đầu được, hiện ngay dưới nút cho chủ phòng đỡ đoán.</summary>
    public string StartBlockedText
    {
        get
        {
            if (!IsHost || IsPlaying) return "";
            if (Players.Count < 2) return "Cần ít nhất 2 người.";
            if (!AllGuestsReady) return "Còn người chưa bấm sẵn sàng.";
            return "";
        }
    }

    private bool _isHost;
    public bool IsHost
    {
        get => _isHost;
        private set
        {
            if (SetProperty(ref _isHost, value)) RaiseCommandStates();
        }
    }

    private int _rounds = 5;
    /// <summary>Số câu của ván, chủ phòng chọn trước khi bắt đầu.</summary>
    public int Rounds
    {
        get => _rounds;
        set => SetProperty(ref _rounds, Math.Clamp(value, 1, 20));
    }

    /// <summary>Máy chủ đòi ít nhất hai người và mọi khách đã sẵn sàng, nên nút chỉ sáng khi đủ cả hai.</summary>
    public bool CanStart => IsHost && !IsPlaying && !IsBusy && Players.Count >= 2 && AllGuestsReady;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (!SetProperty(ref _isPlaying, value)) return;
            OnPropertyChanged(nameof(IsInLobby));
            RaiseCommandStates();
        }
    }

    /// <summary>Chưa vào ván thì còn đang ở phòng chờ.</summary>
    public bool IsInLobby => !IsPlaying;

    // ----- Câu đang chạy -----

    private BitmapImage? _imageSource;
    public BitmapImage? ImageSource
    {
        get => _imageSource;
        private set
        {
            if (SetProperty(ref _imageSource, value)) OnPropertyChanged(nameof(HasImage));
        }
    }

    public bool HasImage => ImageSource != null;

    private bool _isImageBroken;
    /// <summary>Tải ảnh hỏng thật, khác với lúc đang tải dở.</summary>
    public bool IsImageBroken
    {
        get => _isImageBroken;
        private set => SetProperty(ref _isImageBroken, value);
    }

    private string _progressText = "";
    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    private string _difficultyText = "";
    public string DifficultyText
    {
        get => _difficultyText;
        private set => SetProperty(ref _difficultyText, value);
    }

    private string _promptText = CategoryPrompt.Fallback;
    /// <summary>Câu dẫn theo chủ đề ("Đây là một con vật"…), cùng bảng với màn Cổ điển.</summary>
    public string PromptText
    {
        get => _promptText;
        private set => SetProperty(ref _promptText, value);
    }

    /// <summary>Đồng hồ đếm ngược phía client, xê xích chút so với máy chủ nhưng đủ để nhìn.</summary>
    public string SecondsLeftText
    {
        get
        {
            double left = _secondsAllowed - (DateTime.UtcNow - _roundStartedLocal).TotalSeconds;
            return $"{Math.Max(0, left):0.0}s";
        }
    }

    private string _feedbackText = "";
    public string FeedbackText
    {
        get => _feedbackText;
        private set => SetProperty(ref _feedbackText, value);
    }

    private bool _isFeedbackGood;
    public bool IsFeedbackGood
    {
        get => _isFeedbackGood;
        private set => SetProperty(ref _isFeedbackGood, value);
    }

    private bool _isAnswered;
    /// <summary>Đã trả lời đúng câu này rồi thì chỉ ngồi xem, không ghi điểm thêm.</summary>
    public bool IsAnswered
    {
        get => _isAnswered;
        private set => SetProperty(ref _isAnswered, value);
    }

    private bool _isCoolingDown;
    /// <summary>Đang trong quãng chờ vì vừa đoán sai; bàn phím chữ tạm khóa.</summary>
    public bool IsCoolingDown
    {
        get => _isCoolingDown;
        private set => SetProperty(ref _isCoolingDown, value);
    }

    private string _revealedAnswer = "";
    /// <summary>Đáp án của câu vừa xong, chỉ có sau khi máy chủ báo hết câu.</summary>
    public string RevealedAnswer
    {
        get => _revealedAnswer;
        private set => SetProperty(ref _revealedAnswer, value);
    }

    private bool _isMatchOver;
    public bool IsMatchOver
    {
        get => _isMatchOver;
        private set => SetProperty(ref _isMatchOver, value);
    }

    private string _winnerText = "";
    public string WinnerText
    {
        get => _winnerText;
        private set => SetProperty(ref _winnerText, value);
    }
    // ----- Kiểu chơi của phòng -----

    private MatchMode _mode = MatchMode.Compete;
    /// <summary>
    /// Kiểu chơi máy chủ đang giữ cho phòng này. Client KHÔNG tự đổi giá trị này
    /// khi bấm — gửi lên máy chủ rồi chờ RoomChanged về, để mọi người trong
    /// phòng (kể cả chủ phòng) nhìn cùng một nguồn sự thật.
    /// </summary>
    public MatchMode Mode
    {
        get => _mode;
        private set
        {
            if (!SetProperty(ref _mode, value)) return;
            OnPropertyChanged(nameof(IsCompete));
            OnPropertyChanged(nameof(IsDraw));
            OnPropertyChanged(nameof(ModeText));
        }
    }

    public bool IsCompete => Mode == MatchMode.Compete;
    public bool IsDraw => Mode == MatchMode.Draw;

    public string ModeText => Mode switch
    {
        MatchMode.Draw => "Tôi vẽ bạn đoán",
        _ => "Thi đấu",
    };

    private async Task ChooseModeAsync(MatchMode mode) => await CallAsync(async () =>
    {
        if (_client == null || !IsHost || mode == Mode) return;
        await _client.SetModeAsync(mode);
    });

    // ===== Hành động của người chơi =====

    private async Task CreateRoomAsync() => await CallAsync(async () =>
    {
        MatchClient client = await EnsureConnectedAsync();
        ApplyRoom(await client.CreateRoomAsync(RoomNameInput, RoomPassword));
        Status = $"Đã mở phòng. Đọc mã {RoomCode} và mật khẩu cho bạn bè; đủ người và ai cũng sẵn sàng thì bấm bắt đầu.";
    });

    private async Task JoinRoomAsync() => await CallAsync(async () =>
    {
        if (JoinCode.Trim().Length == 0)
        {
            Status = "Nhập mã phòng đã.";
            return;
        }

        MatchClient client = await EnsureConnectedAsync();
        ApplyRoom(await client.JoinRoomAsync(JoinCode, RoomPassword));
        Status = $"Đã vào phòng {RoomCode}. Bấm Sẵn sàng rồi chờ chủ phòng bắt đầu.";
    });

    private async Task ToggleReadyAsync() => await CallAsync(async () =>
    {
        if (_client == null || !IsInRoom || IsPlaying) return;
        await _client.SetReadyAsync(!IsReady);   // IsReady đổi khi RoomChanged về
    });

    /// <summary>
    /// Nối máy chủ NGAY LÚC CẦN, tức là lúc người chơi bấm tạo / vào phòng —
    /// không có bước "chọn máy chủ" hay "đăng nhập máy chủ" riêng nữa.
    ///
    /// Địa chỉ lấy từ <see cref="AppSettings.ServerAddress"/>; tài khoản máy chủ
    /// do <see cref="ServerClient.SignInAsync"/> tự lo bằng tài khoản ở máy này.
    /// Nối được một lần thì giữ đường dây đó cho cả phiên; rớt thì lần bấm sau
    /// nối lại.
    /// </summary>
    private async Task<MatchClient> EnsureConnectedAsync()
    {
        if (_client is { IsConnected: true }) return _client;

        // Đường dây cũ đã rớt (hoặc chưa có): dọn rồi mở lại từ đầu
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        Status = "Đang nối máy chủ...";

        string address = _settings.ServerAddress.Trim().Length > 0
            ? _settings.ServerAddress
            : $"localhost:{LocalServer.DefaultPort}";

        // Máy chủ ở chính máy này mà chưa bật thì bật giúp, khỏi bắt người chơi
        // mở cửa sổ dòng lệnh
        string launchError = await LocalServer.EnsureRunningAsync(address, s => Status = s);
        if (launchError.Length > 0)
            throw new InvalidOperationException(launchError);

        ServerAuth auth = await _server.SignInAsync(address, _account);
        if (!auth.Ok || auth.Auth == null)
            throw new InvalidOperationException(auth.Message);

        var client = new MatchClient(_server.BaseAddress, auth.Auth.Token, App.OnUiThread);

        try
        {
            await client.ConnectAsync();
        }
        catch (Exception ex)
        {
            await client.DisposeAsync();
            throw new InvalidOperationException($"Không mở được kênh đấu: {ex.Message}");
        }

        client.RoomChanged += ApplyRoom;
        client.RoundStarted += StartRound;
        client.AnswerJudged += ApplyJudgement;
        client.RoundEnded += EndRound;
        client.MatchEnded += EndMatch;
        client.Disconnected += OnDisconnected;

        _myAccountId = auth.Auth.AccountId;
        _client = client;
        return client;
    }

    private async Task StartMatchAsync() => await CallAsync(async () =>
    {
        if (_client == null) return;

        await _client.StartMatchAsync(Rounds);
        Status = "Bắt đầu!";
    });

    /// <summary>
    /// Gọi máy chủ và biến lỗi thành một dòng chữ trên màn hình. Máy chủ từ chối
    /// bằng HubException (không có phòng, chưa đủ người...) — đó là luật chơi chứ
    /// không phải hỏng hóc, nên chỉ hiện lên chứ không để nổ ra ngoài.
    /// </summary>
    private async Task CallAsync(Func<Task> work)
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await work();
        }
        catch (Exception ex)
        {
            Status = CleanHubError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// SignalR bọc lỗi máy chủ thành "An unexpected error occurred invoking
    /// 'StartMatch' on the server. HubException: Cần ít nhất 2 người…" — người
    /// chơi chỉ cần đọc phần sau dấu hai chấm.
    /// </summary>
    private static string CleanHubError(string message)
    {
        const string marker = "HubException: ";
        int i = message.IndexOf(marker, StringComparison.Ordinal);
        return i >= 0 ? message[(i + marker.Length)..] : message;
    }

    // ===== Máy chủ báo về =====

    /// <summary>
    /// Đứt dây với máy chủ. Máy chủ đã xóa mình khỏi phòng rồi, nên phía này
    /// cũng phải về sảnh chờ: trước đây chỉ đổi dòng trạng thái, còn màn chơi
    /// vẫn treo nguyên — đồng hồ chạy, bàn phím gõ được, nút quay lại thì ẩn vì
    /// IsPlaying vẫn true — người chơi kẹt không lối ra ngoài ESC.
    /// </summary>
    private void OnDisconnected(string _)
    {
        if (_leaving) return;   // tự đóng cửa sổ thì dây đứt là chuyện đương nhiên

        _tick.Stop();
        _flash.Stop();
        _cooldown.Stop();

        IsPlaying = false;
        IsMatchOver = false;
        IsAnswered = false;
        IsCoolingDown = false;
        IsHost = false;
        IsReady = false;
        RoomCode = "";
        RoomName = "";
        Players.Clear();
        Slots.Clear();
        Tiles.Clear();
        ImageSource = null;
        FeedbackText = "";
        RevealedAnswer = "";

        OnPropertyChanged(nameof(PlayerCountText));
        RaiseCommandStates();

        // Lý do kỹ thuật ("The remote party closed the WebSocket…") không giúp
        // gì người chơi, chỉ cần biết là đứt và phải vào lại
        Status = "Mất kết nối tới máy chủ. Tạo hoặc vào lại phòng để chơi tiếp.";
    }

    private void ApplyRoom(RoomState room)
    {
        RoomCode = room.Code;
        RoomName = room.Name;
        IsHost = room.HostAccountId == _myAccountId;
        Mode = room.Mode;
        MaxPlayers = room.MaxPlayers;
        IsReady = room.Players.FirstOrDefault(p => p.AccountId == _myAccountId)?.IsReady ?? false;

        Players.Clear();
        foreach (PlayerInfo p in room.Players)
            Players.Add(new ScoreRow
            {
                AccountId = p.AccountId,
                DisplayName = p.DisplayName,
                IsHost = p.IsHost,
                IsMe = p.AccountId == _myAccountId,
                IsReady = p.IsReady,
                Score = p.Score,
            });

        OnPropertyChanged(nameof(PlayerCountText));
        OnPropertyChanged(nameof(AllGuestsReady));
        RaiseCommandStates();
    }

    private void StartRound(RoundInfo round)
    {
        IsPlaying = true;
        IsMatchOver = false;
        IsAnswered = false;
        RevealedAnswer = "";
        FeedbackText = "";

        // Câu mới thì quãng phạt của câu cũ hết hiệu lực - máy chủ cũng dọn
        // đúng như vậy trong Room.ResetAnswers()
        _cooldown.Stop();
        IsCoolingDown = false;

        ProgressText = $"Câu {round.RoundNumber}/{round.TotalRounds}";
        DifficultyText = $"{round.Difficulty}/5";
        PromptText = CategoryPrompt.For(round.Category);

        BuildSlots(round.WordLengths);
        BuildTiles(round.Tiles);

        ImageSource = null;
        IsImageBroken = false;
        _ = LoadImageAsync(round.RoundNumber, round.ImageName);

        foreach (ScoreRow row in Players) row.HasAnswered = false;

        _secondsAllowed = round.SecondsAllowed;
        _roundStartedLocal = DateTime.UtcNow;
        _tick.Start();
    }

    /// <summary>
    /// Ảnh tải thẳng từ máy chủ nên máy người chơi không cần có sẵn đúng bộ ảnh.
    ///
    /// Tải xong mới dựng ảnh từ mảng byte; nếu lúc đó máy chủ đã sang câu khác
    /// thì bỏ, không đè ảnh cũ lên câu mới.
    /// </summary>
    private async Task LoadImageAsync(int roundNumber, string imageName)
    {
        byte[]? bytes = await _server.DownloadImageAsync(imageName);

        // Máy chủ đã sang câu khác thì ảnh này không còn dùng vào đâu nữa
        if (!ProgressText.StartsWith($"Câu {roundNumber}/")) return;

        if (bytes == null)
        {
            IsImageBroken = true;
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = new MemoryStream(bytes);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            ImageSource = image;
        }
        catch
        {
            ImageSource = null;
            IsImageBroken = true;
        }
    }

    private void ApplyJudgement(AnswerResult result)
    {
        ScoreRow? row = Players.FirstOrDefault(p => p.AccountId == result.AccountId);

        if (result.Correct && row != null)
        {
            row.Score += result.Points;
            row.HasAnswered = true;
            Reorder();
        }

        if (result.AccountId != _myAccountId)
        {
            // Người khác đoán sai thì mình không cần biết, khỏi làm nhiễu màn hình
            if (result.Correct)
                Status = $"{result.DisplayName} trả lời đúng sau {result.Seconds:0.0}s (+{result.Points})";
            return;
        }

        if (result.Correct)
        {
            IsAnswered = true;
            IsFeedbackGood = true;
            FeedbackText = $"Đúng! +{result.Points} điểm ({result.Seconds:0.0}s)";
            _tick.Stop();
        }
        else
        {
            IsFeedbackGood = false;
            foreach (AnswerSlot slot in Slots) slot.IsWrong = !slot.IsSpace;
            _flash.Start();
            StartCooldown(result.CooldownSeconds);
        }
    }

    /// <summary>
    /// Bắt đầu quãng chờ sau khi đoán sai. Máy chủ mới là bên thật sự chặn
    /// (xem <c>RoomManager.Judge</c>); phần này chỉ để người chơi nhìn thấy còn
    /// phải chờ bao lâu, thay vì bấm mãi mà không hiểu sao không ăn thua.
    /// </summary>
    private void StartCooldown(double seconds)
    {
        if (seconds <= 0)
        {
            FeedbackText = "Chưa đúng, thử lại!";
            return;
        }

        _blockedUntil = DateTime.UtcNow.AddSeconds(seconds);
        IsCoolingDown = true;
        ShowCooldownLeft();
        _cooldown.Start();
    }

    private void ShowCooldownLeft()
    {
        double left = (_blockedUntil - DateTime.UtcNow).TotalSeconds;

        if (left <= 0)
        {
            _cooldown.Stop();
            IsCoolingDown = false;
            FeedbackText = "Thử lại đi!";
            return;
        }

        FeedbackText = $"Chưa đúng — chờ {Math.Ceiling(left):0}s";
    }

    private void EndRound(RoundEnded ended)
    {
        _tick.Stop();
        _flash.Stop();
        _cooldown.Stop();

        RevealedAnswer = ended.Answer;
        ApplyScores(ended.Scores);

        if (!IsAnswered)
        {
            IsFeedbackGood = false;
            FeedbackText = "Hết giờ câu này.";
        }
    }

    private void EndMatch(MatchEnded ended)
    {
        _tick.Stop();
        _flash.Stop();
        _cooldown.Stop();

        ApplyScores(ended.Scores);
        IsPlaying = false;
        IsMatchOver = true;

        ScoreRow? best = Players.FirstOrDefault();
        int top = best?.Score ?? 0;
        var leaders = Players.Where(p => p.Score == top).ToList();

        // Hai người bằng điểm (hay cả phòng 0 điểm) mà bảo "bạn thắng" thì kỳ
        WinnerText = best == null
            ? "Ván đã kết thúc."
            : leaders.Count > 1
                ? (leaders.Any(p => p.IsMe) ? $"Hòa {top} điểm!" : $"Hòa {top} điểm.")
                : best.IsMe
                    ? $"Bạn thắng với {best.Score} điểm!"
                    : $"{best.DisplayName} thắng với {best.Score} điểm.";

        // Cờ sẵn sàng đã bị xóa lúc ván bắt đầu, nên khách phải bấm lại; nói
        // rõ kẻo chủ phòng thấy nút "Ván mới" mờ mà không hiểu vì sao
        Status = IsHost
            ? $"Ván xong. Vẫn ở phòng {RoomCode}; mọi người bấm Sẵn sàng lại là bạn bắt đầu được ván mới."
            : $"Ván xong. Vẫn ở phòng {RoomCode}; bấm Sẵn sàng lại để chủ phòng mở ván mới.";
    }

    private void ApplyScores(IReadOnlyList<PlayerInfo> scores)
    {
        foreach (PlayerInfo info in scores)
        {
            ScoreRow? row = Players.FirstOrDefault(p => p.AccountId == info.AccountId);
            if (row != null) row.Score = info.Score;
        }

        Reorder();
    }

    /// <summary>Xếp lại bảng điểm từ cao xuống thấp, vẫn giữ nguyên các dòng cũ.</summary>
    private void Reorder()
    {
        List<ScoreRow> sorted = Players.OrderByDescending(p => p.Score).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            int from = Players.IndexOf(sorted[i]);
            if (from != i) Players.Move(from, i);
        }
    }

    // ===== Ghép chữ =====

    /// <summary>Dựng hàng ô trống theo số chữ của từng tiếng, chèn ô hở giữa hai tiếng.</summary>
    private void BuildSlots(int[] wordLengths)
    {
        Slots.Clear();
        int index = 0;

        for (int w = 0; w < wordLengths.Length; w++)
        {
            if (w > 0) Slots.Add(new AnswerSlot { Index = index++, IsSpace = true });

            for (int i = 0; i < wordLengths[w]; i++)
                Slots.Add(new AnswerSlot { Index = index++ });
        }
    }

    private void BuildTiles(string letters)
    {
        Tiles.Clear();
        foreach (char c in letters)
            Tiles.Add(new LetterTile { Id = _nextTileId++, Character = c });
    }

    private void PlaceLetter(LetterTile? tile)
    {
        if (tile == null || tile.IsUsed || IsAnswered || IsCoolingDown || !IsPlaying) return;

        AnswerSlot? slot = Slots.FirstOrDefault(s => !s.IsSpace && !s.HasValue);
        if (slot == null) return;

        slot.CurrentChar = tile.Character;
        slot.SourceTileId = tile.Id;
        slot.IsWrong = false;
        tile.IsUsed = true;

        // KHÔNG tự gửi khi điền kín nữa. Trước đây có, vì đấu tính từng phần
        // mười giây; nhưng gõ bàn phím nhanh thì chữ cuối gõ nhầm là mất luôn
        // lượt (sai là chịu phạt chờ). Người chơi bấm Enter khi thấy ưng.
        RaiseCommandStates();
    }

    /// <summary>Điền kín hết ô rồi thì mới gửi được.</summary>
    public bool CanSubmit =>
        IsPlaying && !IsAnswered && !IsCoolingDown
        && Slots.Count > 0 && Slots.All(s => s.IsSpace || s.HasValue);

    /// <summary>
    /// Gõ một chữ trên bàn phím vật lý. Tìm phím trên màn hình còn trống có
    /// đúng chữ đó rồi đặt vào ô kế tiếp — y như bấm phím đó bằng chuột. Chữ
    /// không có trên bàn phím (hoặc đã dùng hết) thì bỏ qua.
    ///
    /// Đấu là cuộc đua tốc độ; gõ 7 chữ trên bàn phím thật nhanh hơn hẳn nhắm
    /// rồi bấm 7 ô trên màn hình.
    /// </summary>
    public void TypeLetter(char c)
    {
        c = char.ToUpperInvariant(c);
        LetterTile? tile = Tiles.FirstOrDefault(t => !t.IsUsed && t.Character == c);
        if (tile != null) PlaceLetter(tile);
    }

    /// <summary>Backspace: lấy chữ ở ô có chữ cuối cùng ra.</summary>
    public void EraseLast()
    {
        AnswerSlot? last = Slots.LastOrDefault(s => !s.IsSpace && s.HasValue);
        if (last != null) TakeBack(last);
    }

    /// <summary>Enter (hoặc nút Gửi): nộp đáp án nếu đã điền kín.</summary>
    public void Submit()
    {
        if (CanSubmit) _ = SubmitAsync();
    }

    private void TakeBack(AnswerSlot? slot)
    {
        if (slot == null || slot.IsSpace || !slot.HasValue || IsAnswered) return;

        LetterTile? tile = Tiles.FirstOrDefault(t => t.Id == slot.SourceTileId);
        if (tile != null) tile.IsUsed = false;

        slot.CurrentChar = null;
        slot.SourceTileId = null;
        slot.IsWrong = false;
        RaiseCommandStates();
    }

    private async Task SubmitAsync()
    {
        string guess = string.Concat(Slots.Select(s => s.IsSpace ? " " : s.DisplayText));

        try
        {
            if (_client != null) await _client.SubmitAnswerAsync(guess);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>Trả hết chữ về bàn phím sau khi đoán sai.</summary>
    private void ClearSlots()
    {
        foreach (AnswerSlot slot in Slots)
        {
            if (slot.SourceTileId is int id)
            {
                LetterTile? tile = Tiles.FirstOrDefault(t => t.Id == id);
                if (tile != null) tile.IsUsed = false;
            }

            slot.CurrentChar = null;
            slot.SourceTileId = null;
            slot.IsWrong = false;
        }
    }

    private void ToggleTheme()
    {
        _settings.IsDarkTheme = !_settings.IsDarkTheme;
        _settings.Save();
        ThemeService.Apply(_settings.IsDarkTheme);

        OnPropertyChanged(nameof(IsDarkTheme));
    }

    private void RaiseCommandStates()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(StartBlockedText));
        ToggleReadyCommand.RaiseCanExecuteChanged();
        CreateRoomCommand.RaiseCanExecuteChanged();
        JoinRoomCommand.RaiseCanExecuteChanged();
        StartMatchCommand.RaiseCanExecuteChanged();
        BackCommand.RaiseCanExecuteChanged();
        ChooseModeCommand.RaiseCanExecuteChanged();
        SubmitCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSubmit));
    }

    /// <summary>Rời phòng cho gọn khi đóng cửa sổ; máy chủ cũng tự dọn khi rớt kết nối.</summary>
    /// <summary>Đang tự rời (đóng cửa sổ); Closed bắn lúc này không phải là rớt mạng.</summary>
    private bool _leaving;

    public async Task LeaveAsync()
    {
        _leaving = true;
        _tick.Stop();
        _flash.Stop();
        _cooldown.Stop();

        try
        {
            if (_client is { IsConnected: true }) await _client.LeaveRoomAsync();
        }
        catch
        {
            // Đang đóng cửa sổ rồi, lỗi ở đây không cứu được gì
        }

        if (_client != null) await _client.DisposeAsync();
    }
}
