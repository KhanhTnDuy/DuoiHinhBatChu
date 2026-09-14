using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.ViewModels;

/// <summary>
/// Toàn bộ luật chơi của màn chơi: nạp câu đố, xếp ô đáp án, phát chữ cái,
/// kiểm tra đúng/sai, mạng, kim cương, điểm và các trợ giúp.
/// </summary>
public class GameViewModel : ViewModelBase
{
    // Giá các trợ giúp: trợ giúp nào cũng đúng 1 kim cương.
    // Kim cương rất hiếm (tài khoản mới có 2, đúng 5 câu liền mới được thêm 1)
    // nên không cần bảng giá nhiều bậc - dùng hết là phải tự nghĩ. Vì hiếm như
    // vậy nên mỗi lần tiêu đều hỏi lại một câu, xem AskHelp.
    public const int CostReveal = 1;    // mở 1 chữ
    public const int CostBoom = 1;      // xóa bớt chữ thừa

    /// <summary>Đúng liên tiếp đủ chừng này câu thì được thưởng kim cương.</summary>
    public const int StreakForRuby = 5;


    private readonly Account _account;
    private readonly AppSettings _settings;
    private readonly PuzzleRepository _repository = new();
    private readonly List<Puzzle> _puzzles;
    private readonly GameStateService _state;
    private readonly PlayerProfile _profile;
    private readonly Random _rng = new();
    private readonly DispatcherTimer _delay = new();

    /// <summary>
    /// Đồng hồ đếm ngược của câu đang chơi. Nhịp 0,1 giây chứ không phải 1 giây
    /// để thanh thời gian chạy mượt, chứ không giật từng nấc.
    /// </summary>
    private readonly DispatcherTimer _clock =
        new() { Interval = TimeSpan.FromMilliseconds(100) };

    private double _secondsLeft = SoloScoring.MaxSeconds;

    private int _nextTileId;
    private bool _locked;               // đang chờ hiệu ứng -> chặn mọi thao tác
    private Action? _afterDelay;

    /// <param name="account">Tài khoản đang đăng nhập; quyết định file lưu tiến trình.</param>
    /// <param name="settings">Tùy chọn chung, dùng để nhớ chế độ sáng/tối.</param>
    public GameViewModel(Account account, AppSettings settings)
    {
        _account = account;
        _settings = settings;
        _state = new GameStateService(account.Id);
        _profile = _state.LoadProfile();

        // Ván mới thì bốc hạt giống mới; ván đang dở thì dùng lại hạt giống cũ
        // để dựng đúng thứ tự câu hôm trước
        bool isNewRun = _profile.RunSeed == 0;
        if (isNewRun) _profile.RunSeed = NewSeed();

        _puzzles = _repository.LoadAll();
        _baseOrder = _puzzles.ToList();
        Arrange(_puzzles, _profile.RunSeed, _profile.RunOrder);
        _profile.PlayerName = account.DisplayName;
        AudioService.Instance.IsEnabled = _profile.IsSoundEnabled;
        ThemeService.Apply(_settings.IsDarkTheme);

        _delay.Tick += (_, _) =>
        {
            _delay.Stop();
            Action? job = _afterDelay;
            _afterDelay = null;
            job?.Invoke();
        };

        _clock.Tick += (_, _) => Countdown();
        _clock.Start();

        PlaceLetterCommand = new RelayCommand(p => PlaceLetter(p as LetterTile));
        TakeBackCommand = new RelayCommand(p => TakeBack(p as AnswerSlot));
        // Hết kim cương thì nút mờ đi, chứ không để bấm mà không thấy gì
        RevealLetterCommand = new RelayCommand(_ => RevealLetter(), _ => Rubies >= CostReveal);
        BoomCommand = new RelayCommand(_ => BoomExtraLetters(), _ => Rubies >= CostBoom);
        SkipCommand = new RelayCommand(_ => Skip());
        ClearCommand = new RelayCommand(_ => ClearAnswer(), _ => CanClear);
        SubmitCommand = new RelayCommand(_ => SubmitAnswer(), _ => CanSubmit);
        PauseCommand = new RelayCommand(_ => Pause());
        ResumeCommand = new RelayCommand(_ => Resume());
        RestartCommand = new RelayCommand(_ => Restart());
        ConfirmHelpCommand = new RelayCommand(_ => ConfirmHelp());
        CancelHelpCommand = new RelayCommand(_ => CancelHelp());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        ChooseOrderCommand = new RelayCommand(p => ChooseOrder(p));

        // Ván mới thì hỏi lối chơi trước đã, chọn xong mới nạp câu đầu; ván
        // dở thì lối chơi đã có trong hồ sơ, vào thẳng chỗ cũ
        if (isNewRun) BeginChoosingOrder();
        else LoadPuzzle(ResumeIndex());
    }

    // ----- Lối chơi của ván -----

    private bool _isChoosingOrder;

    /// <summary>
    /// Đang hỏi "Mời bạn chọn lối chơi". Lúc này chưa có câu nào được nạp,
    /// đồng hồ đứng yên và lớp phủ che hết màn chơi — ván chỉ thật sự bắt đầu
    /// khi người chơi bấm một trong hai lối.
    /// </summary>
    public bool IsChoosingOrder
    {
        get => _isChoosingOrder;
        private set => SetProperty(ref _isChoosingOrder, value);
    }

    public RelayCommand ChooseOrderCommand { get; }

    /// <summary>Hệ số nhân của lối Ngẫu nhiên, để lớp phủ chọn lối ghi rõ giá.</summary>
    public string RandomBonusText => $"×{SoloScoring.RandomOrderMultiplier:0.#} điểm";

    /// <summary>Tên lối chơi đang dùng, hiện ở cột trái suốt ván.</summary>
    public string RunOrderText => _profile.RunOrder == RunOrder.EasyFirst
        ? "Từ dễ đến khó"
        : $"Ngẫu nhiên ({RandomBonusText})";

    private void BeginChoosingOrder()
    {
        IsChoosingOrder = true;
        IsPickingReveal = false;
        CancelHelp();
        ResetClock();   // không để lộ số giây còn thừa của ván trước
    }

    /// <summary>
    /// Người chơi vừa chọn lối. Xếp lại bộ câu theo lối đó (cùng hạt giống
    /// của ván) rồi mới nạp câu đầu — vì thế câu đầu của "từ dễ đến khó" và
    /// "ngẫu nhiên" là hai câu khác nhau dù chung một hạt giống.
    /// </summary>
    private void ChooseOrder(object? parameter)
    {
        if (!IsChoosingOrder) return;

        _profile.RunOrder = parameter is RunOrder o ? o
            : Enum.TryParse(parameter?.ToString(), out RunOrder parsed) ? parsed
            : RunOrder.Random;

        Arrange(_puzzles, _profile.RunSeed, _profile.RunOrder);
        OnPropertyChanged(nameof(RunOrderText));

        IsChoosingOrder = false;
        LoadPuzzle(0);
    }

    /// <summary>
    /// Bộ câu theo đúng thứ tự trong bảng, chưa xáo. <see cref="Arrange"/> luôn
    /// xuất phát từ đây: xáo một danh sách ĐÃ xáo bằng cùng hạt giống cho ra
    /// thứ tự khác hẳn, và "Chơi tiếp" (chỉ xáo một lần) sẽ không tìm lại được
    /// đúng chỗ — bot chơi thử 2026-09-15 đã bắt được đúng lỗi này.
    /// </summary>
    private readonly List<Puzzle> _baseOrder;

    /// <summary>Từ chừng này chữ cái trở lên là "câu dài", xếp cuối bậc khi chơi từ dễ đến khó.</summary>
    private const int LongAnswer = 12;

    private static int LetterCount(Puzzle p) =>
        PuzzleRound.ToSlotText(p.Answer).Count(char.IsLetter);

    /// <summary>
    /// Xếp bộ câu theo lối chơi: về thứ tự gốc, xáo theo hạt giống, rồi nếu là
    /// "từ dễ đến khó" thì sắp lại theo độ khó. OrderBy của .NET là sắp xếp
    /// ổn định nên hai câu cùng độ khó giữ nguyên thứ tự vừa xáo — trong mỗi
    /// bậc vẫn ngẫu nhiên, và vẫn dựng lại được y nguyên từ hạt giống.
    /// </summary>
    private void Arrange(List<Puzzle> puzzles, int seed, RunOrder order)
    {
        puzzles.Clear();
        puzzles.AddRange(_baseOrder);
        Shuffle(puzzles, seed);

        if (order != RunOrder.EasyFirst) return;

        // Trong cùng một bậc, câu dài (ca dao, khẩu hiệu…) xếp sau: "dễ" mà
        // mở màn bằng 27 chữ / 31 phím thì người mới nhìn đã nản, còn về mặt
        // đoán thì nó không khó hơn — chỉ mất công gõ hơn
        List<Puzzle> sorted = puzzles
            .OrderBy(p => p.Difficulty)
            .ThenBy(p => LetterCount(p) > LongAnswer ? 1 : 0)
            .ToList();
        puzzles.Clear();
        puzzles.AddRange(sorted);
    }

    /// <summary>
    /// Hạt giống cho một ván mới. Tránh số 0 vì 0 là dấu hiệu "chưa có ván nào".
    /// </summary>
    private static int NewSeed() => Random.Shared.Next(1, int.MaxValue);

    /// <summary>
    /// Xáo bộ câu theo hạt giống của ván.
    ///
    /// Cùng một hạt giống luôn cho ra cùng một thứ tự, nên chỉ cần lưu đúng con
    /// số đó là dựng lại được ván đang dở — khỏi phải lưu cả danh sách. Hạt
    /// giống mới mỗi ván nên chơi lại không bao giờ gặp lại đúng thứ tự cũ.
    ///
    /// Fisher-Yates: mỗi hoán vị có xác suất như nhau.
    /// </summary>
    private static List<Puzzle> Shuffle(List<Puzzle> puzzles, int seed)
    {
        var rng = new Random(seed);

        for (int i = puzzles.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (puzzles[i], puzzles[j]) = (puzzles[j], puzzles[i]);
        }

        return puzzles;
    }

    /// <summary>
    /// Vào lại chỗ cũ của ván đang dở. Hồ sơ giữ MÃ câu chứ không phải số thứ
    /// tự, nên phải tra ngược ra vị trí trong thứ tự vừa dựng lại.
    ///
    /// Mã không còn trong bộ (ảnh bị xóa hoặc đổi đáp án) thì không quay về câu
    /// đầu — làm vậy là bắt người chơi giải lại từ đầu chỉ vì một câu biến mất.
    /// Nhảy tới câu đầu tiên chưa giải là đúng ý người chơi hơn.
    /// </summary>
    private int ResumeIndex()
    {
        int saved = _puzzles.FindIndex(p => p.Id == _profile.CurrentPuzzleId);
        if (saved >= 0) return saved;

        int unsolved = _puzzles.FindIndex(p => !_profile.SolvedPuzzleIds.Contains(p.Id));
        return unsolved >= 0 ? unsolved : 0;
    }

    // ----- Lệnh cho giao diện -----
    public RelayCommand PlaceLetterCommand { get; }
    public RelayCommand TakeBackCommand { get; }
    public RelayCommand RevealLetterCommand { get; }
    public RelayCommand BoomCommand { get; }
    public RelayCommand SkipCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SubmitCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand RestartCommand { get; }
    public RelayCommand ConfirmHelpCommand { get; }
    public RelayCommand CancelHelpCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    // ----- Dữ liệu hiển thị -----
    public ObservableCollection<AnswerSlot> Slots { get; } = new();
    public ObservableCollection<LetterTile> Tiles { get; } = new();

    private int _index;
    private Puzzle Current => _puzzles[_index];

    public string ProgressText => $"Câu {_index + 1}/{_puzzles.Count}";

    public string DifficultyText => $"{Current.Difficulty}/5";

    /// <summary>
    /// Chủ đề của đáp án ("Đồ vật", "Ca dao - tục ngữ"…). Cho không, hiện ngay
    /// dưới câu hỏi: nhìn hình mà không ra thì ít ra cũng biết đang tìm cái gì,
    /// đỡ phải tiêu kim cương chỉ để có hướng nghĩ.
    /// </summary>
    public string CategoryText => Current.Category;

    /// <summary>
    /// Câu dẫn trên đầu màn chơi ("Đây là một con vật"…), dựng từ chủ đề.
    /// Bảng câu nằm ở <see cref="CategoryPrompt"/> vì màn Đấu cũng dùng.
    /// </summary>
    public string PromptText => CategoryPrompt.For(Current.Category);

    /// <summary>Số ô chữ cái của đáp án (không tính khoảng trắng).</summary>
    public string LetterCountText => $"{Slots.Count(x => !x.IsSpace)} chữ cái";

    /// <summary>Số câu đã giải trên tổng số câu.</summary>
    public string SolvedText => $"{_profile.SolvedPuzzleIds.Count}/{_puzzles.Count}";

    /// <summary>true = đang ở chế độ tối.</summary>
    public bool IsDarkTheme
    {
        get => _settings.IsDarkTheme;
        private set
        {
            _settings.IsDarkTheme = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Tên hiện ở cột trái, để giữa trận vẫn biết đang chơi bằng tài khoản nào.</summary>
    public string PlayerName => _account.DisplayName;

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ThemeService.Apply(IsDarkTheme);
        _settings.Save();
    }

    public int Score
    {
        get => _profile.Score;
        private set { _profile.Score = value; OnPropertyChanged(); }
    }

    /// <summary>Điểm ván cao nhất từ trước tới nay.</summary>
    public int BestScore
    {
        get => _profile.BestScore;
        private set { _profile.BestScore = value; OnPropertyChanged(); }
    }

    private bool _isNewRecord;
    /// <summary>Ván vừa xong có phá kỷ lục không — để lớp phủ kết thúc khoe.</summary>
    public bool IsNewRecord
    {
        get => _isNewRecord;
        private set => SetProperty(ref _isNewRecord, value);
    }

    /// <summary>
    /// Chốt sổ một ván: điểm dừng lại ở đây, đem so với kỷ lục cũ.
    ///
    /// Gọi đúng hai chỗ — hết mạng và hết bộ câu — vì đó là hai cách duy nhất
    /// một ván kết thúc. Thoát giữa chừng KHÔNG tính: ván còn dở thì điểm còn
    /// chạy, đóng cửa sổ rồi vào lại là chơi tiếp chính ván đó.
    /// </summary>
    private void EndRun()
    {
        _runEnded = true;

        IsNewRecord = Score > BestScore;
        if (IsNewRecord) BestScore = Score;

        // Lưu KIỂU KẾT THÚC VÁN chứ không phải lưu thường: bảng phải nhận
        // trạng thái ván sau (0 điểm, đầy mạng), còn màn hình vẫn giữ điểm ván
        // vừa xong để hiện lên lớp phủ.
        _state.SaveEndOfRun(_profile);
    }

    /// <summary>
    /// Ván đã chốt sổ chưa. Cần nhớ vì <see cref="Save"/> chạy lúc đóng cửa sổ,
    /// mà nếu lúc đó nó ghi đè hồ sơ đang cầm trên tay thì điểm và số mạng của
    /// ván vừa chết quay lại bảng, xóa mất trạng thái ván mới.
    /// </summary>
    private bool _runEnded;

    public int Rubies
    {
        get => _profile.Rubies;
        private set { _profile.Rubies = value; OnPropertyChanged(); }
    }

    /// <summary>Số câu đang đúng liên tiếp.</summary>
    public int CorrectStreak
    {
        get => _profile.CorrectStreak;
        private set
        {
            _profile.CorrectStreak = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StreakText));
        }
    }

    /// <summary>Hiện dạng "3/5" để người chơi biết còn mấy câu nữa được thưởng.</summary>
    public string StreakText => $"{CorrectStreak}/{StreakForRuby}";

    public int Lives
    {
        get => _profile.Lives;
        private set
        {
            _profile.Lives = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LivesText));
        }
    }

    public string LivesText =>
        $"{Math.Max(0, Lives)}/{_profile.MaxLives}";

    private BitmapImage? _imageSource;
    public BitmapImage? ImageSource
    {
        get => _imageSource;
        private set
        {
            SetProperty(ref _imageSource, value);
            OnPropertyChanged(nameof(HasImage));
        }
    }

    public bool HasImage => ImageSource != null;

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

    private bool _isGameOver;
    public bool IsGameOver
    {
        get => _isGameOver;
        private set => SetProperty(ref _isGameOver, value);
    }

    private bool _isFinished;
    public bool IsFinished
    {
        get => _isFinished;
        private set => SetProperty(ref _isFinished, value);
    }

    // ----- Tạm dừng -----

    private bool _isPaused;

    /// <summary>
    /// Đang tạm dừng. Lúc này đồng hồ đứng yên, VÀ giao diện giấu hết ảnh câu
    /// đố lẫn hàng ô đáp án — nếu không thì tạm dừng thành cái mẹo: bấm dừng
    /// rồi ngồi ngắm ảnh nghĩ thoải mái, đồng hồ chẳng mất giây nào.
    /// </summary>
    public bool IsPaused
    {
        get => _isPaused;
        private set => SetProperty(ref _isPaused, value);
    }

    /// <summary>
    /// Chỉ cho dừng khi đang thật sự chơi. Đang chờ hiệu ứng đúng/sai, hết mạng
    /// hay hết bộ câu thì bấm cũng không có gì để dừng.
    /// </summary>
    private void Pause()
    {
        if (_locked || IsGameOver || IsFinished || IsChoosingOrder) return;

        IsPaused = true;
        IsPickingReveal = false;
        CancelHelp();
        _state.SaveProfile(_profile);
    }

    private void Resume() => IsPaused = false;

    // ----- Đồng hồ đếm ngược -----

    /// <summary>Số giây còn lại, dạng "0:47".</summary>
    public string TimeText
    {
        get
        {
            int whole = (int)Math.Ceiling(Math.Max(0, _secondsLeft));
            return $"{whole / 60}:{whole % 60:D2}";
        }
    }

    /// <summary>Phần thời gian còn lại, 1 là vừa bắt đầu và 0 là hết giờ.</summary>
    public double TimeFraction => Math.Clamp(_secondsLeft / SoloScoring.MaxSeconds, 0, 1);

    /// <summary>Còn dưới 10 giây thì đổi màu cảnh báo.</summary>
    public bool IsTimeLow => _secondsLeft <= 10;

    /// <summary>Số giây đã dùng cho câu đang chơi, để tính điểm thưởng tốc độ.</summary>
    private double SecondsUsed => SoloScoring.MaxSeconds - _secondsLeft;

    /// <summary>
    /// Một nhịp đồng hồ. Đang khóa (chờ hiệu ứng đúng/sai, hết mạng, hết bộ câu)
    /// thì không trừ giờ — không ai đáng bị mất thời gian vì đang xem chữ
    /// "Chính xác" chạy.
    /// </summary>
    private void Countdown()
    {
        if (_locked || IsPaused || IsChoosingOrder) return;

        _secondsLeft -= _clock.Interval.TotalSeconds;

        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(TimeFraction));
        OnPropertyChanged(nameof(IsTimeLow));

        if (_secondsLeft <= 0) OnTimeout();
    }

    /// <summary>Hết giờ: mất một mạng như trả lời sai, nhưng không cho làm lại câu đó.</summary>
    private void OnTimeout()
    {
        _secondsLeft = 0;
        _locked = true;
        // Hộp hỏi lại kim cương có thể đang mở (đồng hồ vẫn chạy lúc hỏi). Câu
        // đã kết thúc thì phải đóng nó, không thì bấm "Dùng" là mất kim cương
        // cho một câu không còn tồn tại
        IsPickingReveal = false;
        CancelHelp();
        AudioService.Instance.PlayWrong();

        Lives--;
        CorrectStreak = 0;
        IsFeedbackGood = false;
        FeedbackText = $"Hết giờ! Đáp án: {Current.Answer}";
        _state.SaveProfile(_profile);

        RunAfter(1.6, () =>
        {
            if (Lives <= 0)
            {
                IsGameOver = true;
                _locked = true;
                EndRun();
                return;
            }

            GoNext();
        });
    }

    private void ResetClock()
    {
        _secondsLeft = SoloScoring.MaxSeconds;

        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(TimeFraction));
        OnPropertyChanged(nameof(IsTimeLow));
    }

    // ----- Nạp câu đố -----

    private void LoadPuzzle(int index)
    {
        _index = index;
        _locked = false;
        IsPickingReveal = false;
        CancelHelp();
        _profile.CurrentPuzzleId = _puzzles[index].Id;
        ResetClock();

        Slots.Clear();
        Tiles.Clear();
        FeedbackText = "";
        IsFeedbackGood = false;

        Puzzle p = Current;

        // Cùng luật dựng lượt với máy chủ ván đấu nhiều người
        PuzzleRound round = PuzzleRound.Create(
            p.Answer, _puzzles.Where(x => x != p).Select(x => x.Answer), _rng);

        BuildSlots(round.SlotText);
        BuildTiles(round);
        LoadImage(p);

        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(DifficultyText));
        OnPropertyChanged(nameof(CategoryText));
        OnPropertyChanged(nameof(PromptText));
        OnPropertyChanged(nameof(LetterCountText));
        _state.SaveProfile(_profile);
    }

    /// <summary>Mỗi ký tự của đáp án thành một ô; khoảng trắng thành ô ngăn cách hai từ.</summary>
    private void BuildSlots(string answer)
    {
        int i = 0;
        foreach (char c in answer)
        {
            Slots.Add(new AnswerSlot
            {
                Index = i++,
                TargetChar = c,
                IsSpace = char.IsWhiteSpace(c),
            });
        }
    }

    /// <summary>Đổ ngân hàng phím chữ mà máy chủ (hoặc PuzzleRound) đã dựng sẵn.</summary>
    private void BuildTiles(PuzzleRound round)
    {
        foreach (RoundTile t in round.Tiles)
        {
            Tiles.Add(new LetterTile
            {
                Id = _nextTileId++,
                Character = t.Character,
                IsCorrectLetter = t.IsAnswerLetter,
            });
        }
    }

    /// <summary>
    /// Lấy ảnh của câu đang chơi. Ảnh nằm trong cơ sở dữ liệu nên phải đọc ra
    /// mảng byte rồi dựng ảnh từ luồng nhớ, chứ không mở file như trước.
    /// </summary>
    private void LoadImage(Puzzle p)
    {
        byte[]? bytes = _repository.LoadImage(p.Id);

        if (bytes == null || bytes.Length == 0)
        {
            ImageSource = null;
            return;
        }

        var bmp = new BitmapImage();
        bmp.BeginInit();
        // OnLoad: giải mã hết ngay tại đây, để đóng luồng nhớ xong ảnh vẫn dùng được
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = new MemoryStream(bytes);
        bmp.EndInit();
        bmp.Freeze();
        ImageSource = bmp;
    }

    // ----- Thao tác của người chơi -----

    private void PlaceLetter(LetterTile? tile)
    {
        // Đang chờ chỉ ô để mở thì bàn phím chữ tạm khóa: lúc đó cú bấm của
        // người chơi đang dành cho hàng ô đáp án, không phải cho phím chữ
        if (_locked || IsPaused || IsPickingReveal
            || tile == null || tile.IsUsed || tile.IsEliminated) return;

        AnswerSlot? slot = Slots.FirstOrDefault(s => !s.IsSpace && !s.HasValue);
        if (slot == null) return;

        slot.CurrentChar = tile.Character;
        slot.SourceTileId = tile.Id;
        tile.IsUsed = true;
        AudioService.Instance.PlayClick();

        // Điền kín ô KHÔNG còn tự chấm nữa: người chơi tự bấm "Trả lời" khi
        // thấy ưng. Tự chấm nghĩa là chữ cuối vừa đặt xuống là mất mạng ngay,
        // không kịp nhìn lại hay đổi ý.
    }

    // ----- Bàn phím vật lý (cùng cách với màn Đấu) -----

    /// <summary>Đang ở trạng thái nhận chữ gõ từ bàn phím không.</summary>
    private bool CanType =>
        !_locked && !IsPaused && !IsChoosingOrder && !IsConfirmingHelp && !IsPickingReveal;

    /// <summary>Gõ một chữ: tìm phím còn trống có chữ đó rồi đặt vào ô kế tiếp; không có thì bỏ qua.</summary>
    public void TypeLetter(char c)
    {
        if (!CanType) return;

        c = char.ToUpperInvariant(c);
        LetterTile? tile = Tiles.FirstOrDefault(
            t => !t.IsUsed && !t.IsEliminated && t.Character == c);
        if (tile != null) PlaceLetter(tile);
    }

    /// <summary>Backspace: lấy chữ ở ô cuối cùng người chơi tự điền ra (chữ mở bằng trợ giúp thì giữ).</summary>
    public void EraseLast()
    {
        if (!CanType) return;

        AnswerSlot? last = Slots.LastOrDefault(s => !s.IsSpace && s.HasValue && !s.IsRevealedByHint);
        if (last != null) TakeBack(last);
    }

    /// <summary>Enter: trả lời nếu đã điền kín.</summary>
    public void SubmitFromKeyboard()
    {
        if (CanType && CanSubmit) SubmitAnswer();
    }

    /// <summary>Nhả hết chữ người chơi đã đặt về ngân hàng phím.</summary>
    private void ClearAnswer()
    {
        if (!CanClear) return;

        foreach (AnswerSlot slot in Slots)
        {
            // Chữ do trợ giúp mở thì giữ nguyên - đã trả kim cương cho nó rồi
            if (slot.IsSpace || slot.IsRevealedByHint) continue;
            ReturnTile(slot);
        }

        AudioService.Instance.PlayClick();
    }

    private void SubmitAnswer()
    {
        if (!CanSubmit) return;
        CheckAnswer();
    }

    /// <summary>Còn chữ nào người chơi tự điền để mà xóa không.</summary>
    public bool CanClear => !_locked && !IsPaused
        && Slots.Any(s => !s.IsSpace && s.HasValue && !s.IsRevealedByHint);

    /// <summary>Phải điền kín hết ô mới trả lời được.</summary>
    public bool CanSubmit => !_locked && !IsPaused && Slots.Count > 0 && IsAnswerFull();

    /// <summary>
    /// Bấm vào một ô đáp án. Bình thường là lấy chữ ra; đang chọn ô để mở thì
    /// cú bấm đó có nghĩa khác hẳn — chính là ô người chơi muốn mở.
    /// </summary>
    private void TakeBack(AnswerSlot? slot)
    {
        if (_locked || IsPaused || slot == null || slot.IsSpace) return;

        if (IsPickingReveal)
        {
            RevealSlot(slot);
            return;
        }

        if (!slot.HasValue || slot.IsRevealedByHint) return;

        ReturnTile(slot);
        AudioService.Instance.PlayClick();
    }

    private bool IsAnswerFull() => Slots.Where(s => !s.IsSpace).All(s => s.HasValue);

    /// <summary>Nhả chữ trong ô về lại ngân hàng phím.</summary>
    private void ReturnTile(AnswerSlot slot)
    {
        if (slot.SourceTileId is int id)
        {
            LetterTile? tile = Tiles.FirstOrDefault(t => t.Id == id);
            if (tile != null) tile.IsUsed = false;
        }
        slot.CurrentChar = null;
        slot.SourceTileId = null;
    }

    private void CheckAnswer()
    {
        if (Slots.Where(s => !s.IsSpace).All(s => s.CurrentChar == s.TargetChar)) OnCorrect();
        else OnWrong();
    }

    private void OnCorrect()
    {
        _locked = true;
        AudioService.Instance.PlayVictory();

        // Trả lời đúng trong giờ luôn được điểm nền; nhanh thì được thưởng thêm,
        // nhanh nhất là gấp đôi. Chơi Ngẫu nhiên thì cả cục được nhân hệ số.
        int bonus = SoloScoring.SpeedBonus(Current.Difficulty, SecondsUsed);
        int gained = SoloScoring.Points(Current.Difficulty, SecondsUsed, _profile.RunOrder);
        Score += gained;

        if (!_profile.SolvedPuzzleIds.Contains(Current.Id))
            _profile.SolvedPuzzleIds.Add(Current.Id);

        // Kim cương chỉ đến từ chuỗi đúng liên tiếp, không rơi ra sau mỗi câu
        CorrectStreak++;
        bool earnedRuby = CorrectStreak >= StreakForRuby;
        if (earnedRuby)
        {
            Rubies++;
            CorrectStreak = 0;
        }

        IsFeedbackGood = true;
        string speed = bonus > 0 ? $", nhanh tay +{bonus}" : "";
        FeedbackText = earnedRuby
            ? $"Chính xác: {Current.Answer} (+{gained}{speed}) - đúng {StreakForRuby} câu liền, thưởng 1 kim cương!"
            : $"Chính xác: {Current.Answer} (+{gained}{speed})";
        OnPropertyChanged(nameof(SolvedText));
        _state.SaveProfile(_profile);

        RunAfter(1.4, GoNext);
    }

    private void OnWrong()
    {
        _locked = true;
        AudioService.Instance.PlayWrong();

        Lives--;
        CorrectStreak = 0;          // sai một câu là mất cả chuỗi đang có
        IsFeedbackGood = false;
        FeedbackText = "Chưa đúng, thử lại!";
        foreach (AnswerSlot s in Slots) s.IsWrong = !s.IsSpace;

        RunAfter(0.8, () =>
        {
            foreach (AnswerSlot s in Slots)
            {
                s.IsWrong = false;
                if (s.IsSpace || s.IsRevealedByHint) continue;
                ReturnTile(s);
            }

            FeedbackText = "";
            _locked = Lives <= 0;
            IsGameOver = Lives <= 0;

            if (IsGameOver) EndRun();
            else _state.SaveProfile(_profile);
        });
    }

    // ----- Trợ giúp -----

    private bool _isPickingReveal;

    /// <summary>
    /// Đang chờ người chơi chỉ vào ô muốn mở.
    ///
    /// Trước đây trợ giúp này bốc ngẫu nhiên một ô trống. Bốc ngẫu nhiên hay
    /// trúng chữ giữa tiếng, gần như không giúp được gì cho việc đoán — trả 1
    /// kim cương mà không biết mình mua được cái gì. Để người chơi tự chọn thì
    /// họ mở đúng chỗ đang bí, và trợ giúp thành một nước đi có tính toán chứ
    /// không phải một lần quay số.
    /// </summary>
    public bool IsPickingReveal
    {
        get => _isPickingReveal;
        private set => SetProperty(ref _isPickingReveal, value);
    }

    // ----- Hỏi lại trước khi tiêu kim cương -----

    private Action? _pendingHelp;

    private bool _isConfirmingHelp;
    /// <summary>
    /// Đang hỏi lại "có chắc dùng trợ giúp không".
    ///
    /// Kim cương giờ chỉ có 2 và kiếm rất chậm (đúng 5 câu liền mới được 1), nên
    /// một cú bấm nhầm là mất nửa số vốn. Hỏi lại một câu buộc người chơi dừng
    /// một nhịp — đó chính là chỗ họ nhìn lại hình thêm lần nữa và nhiều khi ra
    /// đáp án mà chẳng cần tiêu gì.
    /// </summary>
    public bool IsConfirmingHelp
    {
        get => _isConfirmingHelp;
        private set => SetProperty(ref _isConfirmingHelp, value);
    }

    private string _confirmHelpTitle = "";
    public string ConfirmHelpTitle
    {
        get => _confirmHelpTitle;
        private set => SetProperty(ref _confirmHelpTitle, value);
    }

    private string _confirmHelpDetail = "";
    public string ConfirmHelpDetail
    {
        get => _confirmHelpDetail;
        private set => SetProperty(ref _confirmHelpDetail, value);
    }

    /// <summary>
    /// Hỏi lại rồi mới làm. Đồng hồ VẪN CHẠY trong lúc hỏi — dừng nó thì hộp
    /// thoại này thành mẹo câu giờ, bấm trợ giúp rồi ngồi ngắm hình thoải mái.
    /// </summary>
    private void AskHelp(string title, string detail, Action job)
    {
        _pendingHelp = job;
        ConfirmHelpTitle = title;
        ConfirmHelpDetail = detail;
        IsConfirmingHelp = true;
    }

    private void ConfirmHelp()
    {
        // Chốt chặn cuối: câu vừa kết thúc (hết giờ, hết mạng) trong lúc hộp
        // đang mở thì có bấm "Dùng" cũng không mất gì
        if (_locked) { CancelHelp(); return; }

        Action? job = _pendingHelp;
        CancelHelp();
        job?.Invoke();
    }

    private void CancelHelp()
    {
        _pendingHelp = null;
        IsConfirmingHelp = false;
    }

    /// <summary>Bật chế độ chọn ô; bấm lần nữa là hủy. Kim cương chưa trừ ở đây.</summary>
    private void RevealLetter()
    {
        if (_locked || IsPaused) return;

        if (IsPickingReveal)
        {
            IsPickingReveal = false;    // bấm lại nút = đổi ý
            return;
        }

        if (Rubies < CostReveal) return;

        // Không còn ô nào để mở thì đừng bật chế độ chọn cho người ta bấm hụt
        if (!Slots.Any(s => !s.IsSpace && !s.IsRevealedByHint)) return;

        IsPickingReveal = true;
    }

    /// <summary>
    /// Mở ô người chơi vừa chỉ. Chỉ tới đây kim cương mới bị trừ — chọn hụt
    /// hay đổi ý thì không mất gì.
    /// </summary>
    /// <summary>
    /// Người chơi vừa chỉ vào một ô. Hỏi lại rồi mới mở — hỏi ở ĐÂY chứ không
    /// phải lúc bấm nút trợ giúp, vì đây mới là lúc kim cương thật sự ra đi, và
    /// lúc này họ đã thấy rõ mình sắp mở ô nào.
    /// </summary>
    private void RevealSlot(AnswerSlot slot)
    {
        IsPickingReveal = false;

        if (slot.IsSpace || slot.IsRevealedByHint || Rubies < CostReveal) return;

        AskHelp("Mở ô này?",
                $"Tốn {CostReveal} kim cương, còn lại {Rubies - CostReveal}. " +
                "Thử nhìn lại hình một lần nữa xem sao.",
                () => DoRevealSlot(slot));
    }

    private void DoRevealSlot(AnswerSlot slot)
    {
        if (slot.IsSpace || slot.IsRevealedByHint || Rubies < CostReveal) return;

        // Ô đang có chữ người chơi tự đặt: nhả phím đó về ngân hàng trước, không
        // thì phím vừa bị đánh dấu đã dùng mà chữ trong ô lại bị thay
        if (slot.HasValue) ReturnTile(slot);

        Rubies -= CostReveal;

        // Dùng luôn một phím chữ tương ứng để số phím còn lại vẫn khớp số ô trống
        LetterTile? tile = Tiles.FirstOrDefault(
            t => !t.IsUsed && !t.IsEliminated && t.Character == slot.TargetChar);
        if (tile != null) tile.IsUsed = true;

        slot.CurrentChar = slot.TargetChar;
        slot.SourceTileId = tile?.Id;
        slot.IsRevealedByHint = true;
        AudioService.Instance.PlayHint();

        // KHÔNG tự chấm dù chữ vừa mở làm kín hết ô. Trước đây có, và đó là cái
        // bẫy: mấy ô còn lại đang điền sai thì mua trợ giúp xong là mất luôn
        // một mạng, chưa kịp bấm "Trả lời". Cùng lý do với PlaceLetter.
        _state.SaveProfile(_profile);
    }

    private void BoomExtraLetters()
    {
        if (_locked || IsPaused || IsPickingReveal || Rubies < CostBoom) return;

        var extras = Tiles.Where(t => !t.IsCorrectLetter && !t.IsEliminated && !t.IsUsed).ToList();
        if (extras.Count == 0) return;

        AskHelp("Xóa bớt chữ thừa?",
                $"Tốn {CostBoom} kim cương, còn lại {Rubies - CostBoom}. " +
                $"Sẽ bỏ đi {Math.Max(1, extras.Count / 2)} phím gây nhiễu.",
                DoBoomExtraLetters);
    }

    private void DoBoomExtraLetters()
    {
        var extras = Tiles.Where(t => !t.IsCorrectLetter && !t.IsEliminated && !t.IsUsed).ToList();
        if (extras.Count == 0 || Rubies < CostBoom) return;

        Rubies -= CostBoom;
        int remove = Math.Max(1, extras.Count / 2);
        foreach (LetterTile t in extras.OrderBy(_ => _rng.Next()).Take(remove))
            t.IsEliminated = true;

        AudioService.Instance.PlayHint();
        _state.SaveProfile(_profile);
    }

    /// <summary>
    /// Bỏ qua câu này: mất 1 mạng và mất chuỗi, đổi lại được biết đáp án.
    ///
    /// Trước đây bỏ qua không mất gì cả, nên nó là cái nút đi hết bộ câu miễn
    /// phí — bí câu nào bấm câu đó, chẳng phải nghĩ. Tính đúng bằng giá của hết
    /// giờ thì bỏ qua mới là một lựa chọn thật: chịu mất một mạng để khỏi ngồi
    /// hết 60 giây cho một câu mình biết chắc là không ra.
    /// </summary>
    private void Skip()
    {
        if (_locked || IsPaused) return;

        _locked = true;
        IsPickingReveal = false;
        CancelHelp();
        AudioService.Instance.PlayWrong();

        Lives--;
        CorrectStreak = 0;
        IsFeedbackGood = false;
        FeedbackText = $"Bỏ qua - mất 1 mạng. Đáp án: {Current.Answer}";
        _state.SaveProfile(_profile);

        RunAfter(1.6, () =>
        {
            if (Lives <= 0)
            {
                IsGameOver = true;
                _locked = true;
                EndRun();
                return;
            }

            GoNext();
        });
    }

    private void GoNext()
    {
        if (_index + 1 < _puzzles.Count)
        {
            LoadPuzzle(_index + 1);
        }
        else
        {
            IsFinished = true;
            _locked = true;
            EndRun();
        }
    }

    /// <summary>
    /// Bắt đầu một ván mới sau khi ván cũ đã chốt sổ.
    ///
    /// Điểm LUÔN về 0, kể cả khi thua giữa chừng: điểm giờ là điểm của một ván,
    /// mà ván cũ vừa kết thúc và đã đem so kỷ lục ở <see cref="EndRun"/> rồi.
    /// Giữ lại điểm cũ là cộng dồn hai ván làm một, đúng cái kiểu tính điểm vừa
    /// bỏ đi.
    ///
    /// Bộ câu cũng được XÁO LẠI: mỗi ván một thứ tự mới, nên chơi lại không gặp
    /// lại đúng dãy câu vừa rồi. Vì thế ván mới luôn bắt đầu từ đầu danh sách
    /// vừa xáo — "vào lại đúng câu đang dở" chỉ còn nghĩa trong cùng một ván.
    /// </summary>
    private void Restart()
    {
        bool clearSolved = IsFinished;

        Score = 0;
        CorrectStreak = 0;
        Lives = _profile.MaxLives;
        IsGameOver = false;
        IsFinished = false;
        IsNewRecord = false;
        _runEnded = false;

        _profile.RunSeed = NewSeed();

        // Đi hết cả bộ thì danh sách "đã giải" mới về 0; thua giữa chừng thì
        // những câu đã giải vẫn là đã giải
        if (clearSolved)
        {
            _profile.SolvedPuzzleIds.Clear();
            OnPropertyChanged(nameof(SolvedText));
        }

        // Ván mới là hỏi lại lối chơi; xếp bộ và nạp câu đầu diễn ra sau khi chọn
        Slots.Clear();
        Tiles.Clear();
        FeedbackText = "";
        ImageSource = null;
        BeginChoosingOrder();
    }

    // ----- Tiện ích -----

    /// <summary>Chạy <paramref name="action"/> sau <paramref name="seconds"/> giây (trên luồng giao diện).</summary>
    private void RunAfter(double seconds, Action action)
    {
        _afterDelay = action;
        _delay.Interval = TimeSpan.FromSeconds(seconds);
        _delay.Stop();
        _delay.Start();
    }

    /// <summary>Gọi khi đóng cửa sổ để không mất tiến trình.</summary>
    public void Save()
    {
        // Dừng hẳn hai đồng hồ, không thì chúng còn tích sau khi cửa sổ đã đóng
        _clock.Stop();
        _delay.Stop();

        // Đóng cửa sổ khi còn đang hỏi lối chơi: ván chưa bắt đầu, đừng lưu
        // hạt giống — lần sau vào phải được hỏi lại chứ không bị gán "ngẫu
        // nhiên" mà chưa hề chọn
        if (IsChoosingOrder)
        {
            _profile.RunSeed = 0;
            _profile.RunOrder = RunOrder.Random;
            _profile.CurrentPuzzleId = "";
        }

        // Ván đã kết thúc thì EndRun ghi xong rồi, và cái nó ghi là trạng thái
        // ván MỚI. Ghi đè bằng hồ sơ đang cầm là kéo ván chết sống lại.
        if (_runEnded) _state.SaveEndOfRun(_profile);
        else _state.SaveProfile(_profile);
    }
}
