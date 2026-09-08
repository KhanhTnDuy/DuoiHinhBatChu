using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.ViewModels;

/// <summary>Một dòng trong bảng điểm.</summary>
public class ScoreRow : ViewModelBase
{
    public required string AccountId { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsHost { get; init; }
    public required bool IsMe { get; init; }

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

    private readonly MatchClient _client;
    private readonly ServerClient _server;
    private readonly AppSettings _settings;
    private readonly string _myAccountId;

    /// <summary>Nhịp đếm ngược, chỉ để hiện lên màn hình — mốc thật nằm ở máy chủ.</summary>
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _flash = new() { Interval = WrongFlash };

    private DateTime _roundStartedLocal;
    private double _secondsAllowed = 1;
    private int _nextTileId;

    public MatchViewModel(MatchClient client, ServerClient server, AppSettings settings,
                          string myAccountId, string myDisplayName)
    {
        _client = client;
        _server = server;
        _settings = settings;
        _myAccountId = myAccountId;
        PlayerName = myDisplayName;

        _client.RoomChanged += ApplyRoom;
        _client.RoundStarted += StartRound;
        _client.AnswerJudged += ApplyJudgement;
        _client.RoundEnded += EndRound;
        _client.MatchEnded += EndMatch;
        _client.Disconnected += reason => Status = reason;

        _tick.Tick += (_, _) => OnPropertyChanged(nameof(SecondsLeftText));
        _flash.Tick += (_, _) => { _flash.Stop(); ClearSlots(); };

        CreateRoomCommand = new RelayCommand(async _ => await CreateRoomAsync(), _ => !IsBusy);
        JoinRoomCommand = new RelayCommand(async _ => await JoinRoomAsync(), _ => !IsBusy);
        StartMatchCommand = new RelayCommand(async _ => await StartMatchAsync(), _ => CanStart);
        PlaceLetterCommand = new RelayCommand(p => PlaceLetter(p as LetterTile));
        TakeBackCommand = new RelayCommand(p => TakeBack(p as AnswerSlot));
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
    }

    // ----- Lệnh cho giao diện -----
    public RelayCommand CreateRoomCommand { get; }
    public RelayCommand JoinRoomCommand { get; }
    public RelayCommand StartMatchCommand { get; }
    public RelayCommand PlaceLetterCommand { get; }
    public RelayCommand TakeBackCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    // ----- Dữ liệu hiển thị -----
    public ObservableCollection<AnswerSlot> Slots { get; } = new();
    public ObservableCollection<LetterTile> Tiles { get; } = new();
    public ObservableCollection<ScoreRow> Players { get; } = new();

    public string PlayerName { get; }

    public bool IsDarkTheme => _settings.IsDarkTheme;

    private string _status = "Tạo phòng mới, hoặc nhập mã phòng bạn bè đọc cho.";
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

    private string _joinCode = "";
    public string JoinCode
    {
        get => _joinCode;
        set => SetProperty(ref _joinCode, value);
    }

    private string _roomCode = "";
    /// <summary>Mã phòng đang ở; rỗng nghĩa là chưa vào phòng nào.</summary>
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

    /// <summary>Máy chủ đòi ít nhất hai người, nên nút bắt đầu chỉ sáng khi đủ.</summary>
    public bool CanStart => IsHost && !IsPlaying && !IsBusy && Players.Count >= 2;

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

    // ===== Hành động của người chơi =====

    private async Task CreateRoomAsync() => await CallAsync(async () =>
    {
        ApplyRoom(await _client.CreateRoomAsync());
        Status = $"Đã mở phòng {RoomCode}. Đọc mã này cho bạn bè vào.";
    });

    private async Task JoinRoomAsync() => await CallAsync(async () =>
    {
        if (JoinCode.Trim().Length == 0)
        {
            Status = "Nhập mã phòng đã.";
            return;
        }

        ApplyRoom(await _client.JoinRoomAsync(JoinCode));
        Status = $"Đã vào phòng {RoomCode}. Chờ chủ phòng bấm bắt đầu.";
    });

    private async Task StartMatchAsync() => await CallAsync(async () =>
    {
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
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ===== Máy chủ báo về =====

    private void ApplyRoom(RoomState room)
    {
        RoomCode = room.Code;
        IsHost = room.HostAccountId == _myAccountId;

        Players.Clear();
        foreach (PlayerInfo p in room.Players)
            Players.Add(new ScoreRow
            {
                AccountId = p.AccountId,
                DisplayName = p.DisplayName,
                IsHost = p.IsHost,
                IsMe = p.AccountId == _myAccountId,
                Score = p.Score,
            });

        RaiseCommandStates();
    }

    private void StartRound(RoundInfo round)
    {
        IsPlaying = true;
        IsMatchOver = false;
        IsAnswered = false;
        RevealedAnswer = "";
        FeedbackText = "";

        ProgressText = $"Câu {round.RoundNumber}/{round.TotalRounds}";
        DifficultyText = $"{round.Difficulty}/5";

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
            FeedbackText = "Chưa đúng, thử lại!";
            foreach (AnswerSlot slot in Slots) slot.IsWrong = !slot.IsSpace;
            _flash.Start();
        }
    }

    private void EndRound(RoundEnded ended)
    {
        _tick.Stop();
        _flash.Stop();

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

        ApplyScores(ended.Scores);
        IsPlaying = false;
        IsMatchOver = true;

        ScoreRow? best = Players.FirstOrDefault();
        WinnerText = best == null
            ? "Ván đã kết thúc."
            : best.IsMe
                ? $"Bạn thắng với {best.Score} điểm!"
                : $"{best.DisplayName} thắng với {best.Score} điểm.";

        Status = $"Ván xong. Vẫn ở phòng {RoomCode}, chủ phòng bấm bắt đầu là chơi ván mới.";
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
        if (tile == null || tile.IsUsed || IsAnswered || !IsPlaying) return;

        AnswerSlot? slot = Slots.FirstOrDefault(s => !s.IsSpace && !s.HasValue);
        if (slot == null) return;

        slot.CurrentChar = tile.Character;
        slot.SourceTileId = tile.Id;
        slot.IsWrong = false;
        tile.IsUsed = true;

        // Điền kín là gửi luôn: ván đấu tính từng phần mười giây, bắt bấm thêm
        // một nút "gửi" nữa thì chỉ tổ chậm
        if (Slots.All(s => s.IsSpace || s.HasValue)) _ = SubmitAsync();
    }

    private void TakeBack(AnswerSlot? slot)
    {
        if (slot == null || slot.IsSpace || !slot.HasValue || IsAnswered) return;

        LetterTile? tile = Tiles.FirstOrDefault(t => t.Id == slot.SourceTileId);
        if (tile != null) tile.IsUsed = false;

        slot.CurrentChar = null;
        slot.SourceTileId = null;
        slot.IsWrong = false;
    }

    private async Task SubmitAsync()
    {
        string guess = string.Concat(Slots.Select(s => s.IsSpace ? " " : s.DisplayText));

        try
        {
            await _client.SubmitAnswerAsync(guess);
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
        CreateRoomCommand.RaiseCanExecuteChanged();
        JoinRoomCommand.RaiseCanExecuteChanged();
        StartMatchCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Rời phòng cho gọn khi đóng cửa sổ; máy chủ cũng tự dọn khi rớt kết nối.</summary>
    public async Task LeaveAsync()
    {
        _tick.Stop();
        _flash.Stop();

        try
        {
            if (_client.IsConnected) await _client.LeaveRoomAsync();
        }
        catch
        {
            // Đang đóng cửa sổ rồi, lỗi ở đây không cứu được gì
        }

        await _client.DisposeAsync();
    }
}
