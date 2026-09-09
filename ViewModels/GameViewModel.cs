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
        RestartCommand = new RelayCommand(_ => Restart());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());

        LoadPuzzle(Math.Clamp(_profile.CurrentPuzzleIndex, 0, _puzzles.Count - 1));
    }

    // ----- Lệnh cho giao diện -----
    public RelayCommand PlaceLetterCommand { get; }
    public RelayCommand TakeBackCommand { get; }
    public RelayCommand RevealLetterCommand { get; }
    public RelayCommand BoomCommand { get; }
    public RelayCommand ShowHintCommand { get; }
    public RelayCommand SkipCommand { get; }
    public RelayCommand RestartCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    // ----- Dữ liệu hiển thị -----
    public ObservableCollection<AnswerSlot> Slots { get; } = new();
    public ObservableCollection<LetterTile> Tiles { get; } = new();

    private int _index;
    private Puzzle Current => _puzzles[_index];

    public string ProgressText => $"Câu {_index + 1}/{_puzzles.Count}";

    public string DifficultyText => $"{Current.Difficulty}/5";

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

    /// <summary>Tên hiện ở cột trái; tài khoản khách thì ghi rõ là khách.</summary>
    public string PlayerName => _account.DisplayName;

    public string AccountKindText => _account.IsGuest ? "Chơi khách - offline" : "Đã đăng nhập";

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
        if (_locked) return;

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
        _profile.CurrentPuzzleIndex = index;
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
        if (_locked || tile == null || tile.IsUsed || tile.IsEliminated) return;

        AnswerSlot? slot = Slots.FirstOrDefault(s => !s.IsSpace && !s.HasValue);
        if (slot == null) return;

        slot.CurrentChar = tile.Character;
        slot.SourceTileId = tile.Id;
        tile.IsUsed = true;
        AudioService.Instance.PlayClick();

        if (IsAnswerFull()) CheckAnswer();
    }

    private void TakeBack(AnswerSlot? slot)
    {
        if (_locked || slot == null || slot.IsSpace || !slot.HasValue || slot.IsRevealedByHint) return;

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
            _state.SaveProfile(_profile);
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

        if (IsAnswerFull()) CheckAnswer();
        else _state.SaveProfile(_profile);
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
        }
    }

    /// <summary>Chơi lại: hồi đầy mạng; nếu đã hết bộ câu đố thì quay về câu đầu.</summary>
    private void Restart()
    {
        bool restartFromStart = IsFinished;

        Lives = _profile.MaxLives;
        IsGameOver = false;
        IsFinished = false;

        if (restartFromStart)
        {
            Score = 0;
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

        _state.SaveProfile(_profile);
    }
}
