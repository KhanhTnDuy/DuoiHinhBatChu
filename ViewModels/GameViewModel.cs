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
    // Kim cương giờ hiếm (tài khoản mới có 3, đúng 5 câu liền mới được thêm 1)
    // nên không cần bảng giá nhiều bậc nữa - dùng hết là phải tự nghĩ.
    public const int CostReveal = 1;    // mở 1 chữ
    public const int CostBoom = 1;      // xóa bớt chữ thừa
    public const int CostHint = 1;      // xem gợi ý bằng lời

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
        _puzzles = _repository.LoadAll();
        _profile = _state.LoadProfile();
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
        RevealLetterCommand = new RelayCommand(_ => RevealLetter());
        BoomCommand = new RelayCommand(_ => BoomExtraLetters());
        ShowHintCommand = new RelayCommand(_ => ShowHint());
        SkipCommand = new RelayCommand(_ => Skip());
        ClearCommand = new RelayCommand(_ => ClearAnswer(), _ => CanClear);
        SubmitCommand = new RelayCommand(_ => SubmitAnswer(), _ => CanSubmit);
        PauseCommand = new RelayCommand(_ => Pause());
        ResumeCommand = new RelayCommand(_ => Resume());
        RestartCommand = new RelayCommand(_ => Restart());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());

        LoadPuzzle(ResumeIndex());
    }

    /// <summary>
    /// Vào chơi ở câu nào. Hồ sơ giữ MÃ câu chứ không phải số thứ tự, nên phải
    /// tra ngược ra vị trí trong bộ câu hiện tại.
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
    public RelayCommand ShowHintCommand { get; }
    public RelayCommand SkipCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SubmitCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand RestartCommand { get; }
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

    private string _hintText = "";
    public string HintText
    {
        get => _hintText;
        private set => SetProperty(ref _hintText, value);
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
        if (_locked || IsGameOver || IsFinished) return;

        IsPaused = true;
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
        if (_locked || IsPaused) return;

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
        _profile.CurrentPuzzleId = _puzzles[index].Id;
        ResetClock();

        Slots.Clear();
        Tiles.Clear();
        HintText = "";
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
        if (_locked || IsPaused || tile == null || tile.IsUsed || tile.IsEliminated) return;

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

    private void TakeBack(AnswerSlot? slot)
    {
        if (_locked || IsPaused || slot == null || slot.IsSpace || !slot.HasValue
            || slot.IsRevealedByHint) return;

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
        // nhanh nhất là gấp đôi
        int bonus = SoloScoring.SpeedBonus(Current.Difficulty, SecondsUsed);
        Score += SoloScoring.Base(Current.Difficulty) + bonus;

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
        string speed = bonus > 0 ? $" (+{bonus} điểm nhanh tay)" : "";
        FeedbackText = earnedRuby
            ? $"Chính xác: {Current.Answer}{speed} - đúng {StreakForRuby} câu liền, thưởng 1 kim cương!"
            : $"Chính xác: {Current.Answer}{speed}";
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

    private void RevealLetter()
    {
        if (_locked || Rubies < CostReveal) return;

        var empty = Slots.Where(s => !s.IsSpace && !s.HasValue).ToList();
        if (empty.Count == 0) return;

        AnswerSlot slot = empty[_rng.Next(empty.Count)];
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
        if (_locked || Rubies < CostBoom) return;

        var extras = Tiles.Where(t => !t.IsCorrectLetter && !t.IsEliminated && !t.IsUsed).ToList();
        if (extras.Count == 0) return;

        Rubies -= CostBoom;
        int remove = Math.Max(1, extras.Count / 2);
        foreach (LetterTile t in extras.OrderBy(_ => _rng.Next()).Take(remove))
            t.IsEliminated = true;

        AudioService.Instance.PlayHint();
        _state.SaveProfile(_profile);
    }

    private void ShowHint()
    {
        if (_locked || HintText.Length > 0 || Rubies < CostHint) return;

        Rubies -= CostHint;
        HintText = "Gợi ý: " + Current.Hint;
        AudioService.Instance.PlayHint();
        _state.SaveProfile(_profile);
    }

    /// <summary>Bỏ qua cũng làm đứt chuỗi: chuỗi là "đúng liên tiếp", không phải "không sai".</summary>
    private void Skip()
    {
        if (_locked) return;

        CorrectStreak = 0;
        GoNext();
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
    /// Riêng TIẾN ĐỘ bộ câu thì giữ: thua ở câu 22 thì ván mới vẫn vào câu 22,
    /// vì bộ câu là chặng đường dài chung cho mọi ván, không thuộc về ván nào.
    /// Chỉ khi đã đi hết bộ mới quay về câu đầu và xóa danh sách đã giải.
    /// </summary>
    private void Restart()
    {
        bool restartFromStart = IsFinished;

        Score = 0;
        CorrectStreak = 0;
        Lives = _profile.MaxLives;
        IsGameOver = false;
        IsFinished = false;
        IsNewRecord = false;
        _runEnded = false;

        if (restartFromStart)
        {
            _profile.SolvedPuzzleIds.Clear();
            OnPropertyChanged(nameof(SolvedText));
        }

        LoadPuzzle(restartFromStart ? 0 : _index);
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

        // Ván đã kết thúc thì EndRun ghi xong rồi, và cái nó ghi là trạng thái
        // ván MỚI. Ghi đè bằng hồ sơ đang cầm là kéo ván chết sống lại.
        if (_runEnded) _state.SaveEndOfRun(_profile);
        else _state.SaveProfile(_profile);
    }
}
