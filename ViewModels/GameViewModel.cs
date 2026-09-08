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
    // Giá các trợ giúp (kim cương)
    public const int CostReveal = 30;   // mở 1 chữ
    public const int CostBoom = 50;     // xóa bớt chữ thừa
    public const int CostHint = 20;     // xem gợi ý bằng lời

    private const int MinTiles = 12;    // số phím chữ tối thiểu cho đỡ trống trải
    private const int MaxTiles = 21;

    /// <summary>Bảng chữ cái tiếng Việt không dấu (không có F, J, W, Z).</summary>
    private const string Alphabet = "ABCDEGHIKLMNOPQRSTUVXY";

    private readonly Account _account;
    private readonly AppSettings _settings;
    private readonly List<Puzzle> _puzzles;
    private readonly GameStateService _state;
    private readonly PlayerProfile _profile;
    private readonly Random _rng = new();
    private readonly DispatcherTimer _delay = new();

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
        _puzzles = new PuzzleRepository().LoadAll();
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
            OnPropertyChanged(nameof(ThemeToggleText));
        }
    }

    /// <summary>Tên hiện ở cột trái; tài khoản khách thì ghi rõ là khách.</summary>
    public string PlayerName => _account.DisplayName;

    public string AccountKindText => _account.IsGuest ? "Chơi khách - offline" : "Đã đăng nhập";

    public string ThemeToggleText => IsDarkTheme ? "Chế độ sáng" : "Chế độ tối";

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

    // ----- Nạp câu đố -----

    private void LoadPuzzle(int index)
    {
        _index = index;
        _locked = false;
        _profile.CurrentPuzzleIndex = index;

        Slots.Clear();
        Tiles.Clear();
        HintText = "";
        FeedbackText = "";
        IsFeedbackGood = false;

        Puzzle p = Current;
        BuildSlots(ToSlotText(p.Answer));
        BuildTiles();
        LoadImage(p);

        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(DifficultyText));
        OnPropertyChanged(nameof(LetterCountText));
        _state.SaveProfile(_profile);
    }

    /// <summary>
    /// Chữ hiện trên ô đáp án và trên phím: bỏ dấu, đổi "đ" thành "d", viết hoa.
    /// Người chơi chỉ phải chọn chữ không dấu; dạng có dấu đầy đủ chỉ hiện lại
    /// ở dòng "Chính xác: ..." sau khi trả lời đúng.
    /// </summary>
    private static string ToSlotText(string answer) =>
        string.Join(' ', AnswerChecker.Normalize(answer)
                                      .Split(' ', StringSplitOptions.RemoveEmptyEntries))
              .ToUpperInvariant();

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

    /// <summary>Ngân hàng chữ = chữ của đáp án + chữ nhiễu, rồi xáo trộn.</summary>
    private void BuildTiles()
    {
        var letters = Slots.Where(s => !s.IsSpace).Select(s => s.TargetChar).ToList();

        int total = Math.Clamp(letters.Count + 6, MinTiles, MaxTiles);
        char[] pool = FillerPool();
        while (letters.Count < total)
            letters.Add(pool[_rng.Next(pool.Length)]);

        // Xáo trộn Fisher-Yates để vị trí phím không đoán được
        for (int i = letters.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (letters[i], letters[j]) = (letters[j], letters[i]);
        }

        // Đúng số lượng chữ cần cho đáp án được đánh dấu "chữ thật";
        // phần dư là chữ nhiễu, trợ giúp Boom chỉ xóa được nhóm này.
        var remaining = Slots.Where(s => !s.IsSpace)
                             .GroupBy(s => s.TargetChar)
                             .ToDictionary(g => g.Key, g => g.Count());

        foreach (char c in letters)
        {
            bool isReal = remaining.TryGetValue(c, out int n) && n > 0;
            if (isReal) remaining[c] = n - 1;

            Tiles.Add(new LetterTile
            {
                Id = _nextTileId++,
                Character = c,
                IsCorrectLetter = isReal,
            });
        }
    }

    /// <summary>
    /// Chữ nhiễu lấy từ chính các đáp án khác cho sát chất tiếng Việt.
    /// Bộ câu đố còn ít thì bù thêm từ bảng chữ cái để bàn phím khỏi lặp đi lặp lại.
    /// </summary>
    private char[] FillerPool()
    {
        var pool = _puzzles
            .SelectMany(p => ToSlotText(p.Answer))
            .Where(char.IsLetter)
            .ToHashSet();

        if (pool.Count < MinTiles) pool.UnionWith(Alphabet);

        return pool.ToArray();
    }

    private void LoadImage(Puzzle p)
    {
        if (string.IsNullOrEmpty(p.Image) || !File.Exists(p.Image))
        {
            ImageSource = null;
            return;
        }

        string path = p.Image;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;   // đọc xong nhả file, không khóa
        bmp.UriSource = new Uri(path);
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

        Score += 10 * Math.Max(1, Current.Difficulty);
        Rubies += 5;
        if (!_profile.SolvedPuzzleIds.Contains(Current.Id))
            _profile.SolvedPuzzleIds.Add(Current.Id);

        IsFeedbackGood = true;
        FeedbackText = $"Chính xác: {Current.Answer}";
        OnPropertyChanged(nameof(SolvedText));
        _state.SaveProfile(_profile);

        RunAfter(1.4, GoNext);
    }

    private void OnWrong()
    {
        _locked = true;
        AudioService.Instance.PlayWrong();

        Lives--;
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

    private void Skip()
    {
        if (_locked) return;
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
    public void Save() => _state.SaveProfile(_profile);
}
