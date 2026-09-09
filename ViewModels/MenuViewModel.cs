using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.ViewModels;

/// <summary>
/// Menu của chế độ 1 người chơi, mở ra sau khi chọn chế độ.
///
/// Trước đây bấm "1 người chơi" là vào thẳng màn chơi, nên người chơi không có
/// chỗ nào để xem mình đang ở đâu, chơi lại từ đầu hay xem bảng xếp hạng.
/// Menu này là chỗ đó; màn chơi chỉ mở khi bấm Chơi tiếp hoặc Chơi mới.
/// </summary>
public class MenuViewModel : ViewModelBase
{
    private readonly Account _account;
    private readonly AppSettings _settings;
    private readonly GameStateService _state;
    private readonly int _totalPuzzles;

    private PlayerProfile _profile;

    /// <summary>Bắn lên khi người chơi muốn vào màn chơi.</summary>
    public event Action? StartGame;

    /// <summary>Bắn lên khi người chơi muốn quay lại màn chọn chế độ.</summary>
    public event Action? GoBack;

    public MenuViewModel(Account account, AppSettings settings)
    {
        _account = account;
        _settings = settings;
        _state = new GameStateService(account.Id);
        _profile = _state.LoadProfile();

        // Chỉ cần con số tổng, đếm thẳng trong cơ sở dữ liệu chứ không nạp cả bộ câu
        _totalPuzzles = new PuzzleRepository().Count();

        ContinueCommand = new RelayCommand(_ => StartGame?.Invoke());
        NewGameCommand = new RelayCommand(_ => AskNewGame());
        ConfirmNewGameCommand = new RelayCommand(_ => ConfirmNewGame());
        CancelNewGameCommand = new RelayCommand(_ => IsConfirmingNewGame = false);
        ShowLeaderboardCommand = new RelayCommand(_ => ShowLeaderboard());
        HideLeaderboardCommand = new RelayCommand(_ => IsLeaderboardOpen = false);
        BackCommand = new RelayCommand(_ => GoBack?.Invoke());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
    }

    public RelayCommand ContinueCommand { get; }
    public RelayCommand NewGameCommand { get; }
    public RelayCommand ConfirmNewGameCommand { get; }
    public RelayCommand CancelNewGameCommand { get; }
    public RelayCommand ShowLeaderboardCommand { get; }
    public RelayCommand HideLeaderboardCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    // ----- Người chơi -----

    public string PlayerName => _account.DisplayName;

    public string AccountKindText => _account.IsGuest ? "Chơi khách - offline" : "Đã đăng nhập";

    /// <summary>Chữ cái đầu của tên, hiện trong ô vuông thay cho ảnh đại diện.</summary>
    public string Initials
    {
        get
        {
            string[] words = _account.DisplayName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return words.Length switch
            {
                0 => "?",
                1 => words[0][..1].ToUpperInvariant(),
                _ => (words[0][..1] + words[^1][..1]).ToUpperInvariant(),
            };
        }
    }

    // ----- Tiến trình -----

    /// <summary>
    /// Đã chơi dở hay chưa. Tài khoản mới tinh thì không có gì để "tiếp", nên
    /// nút đổi tên thành Bắt đầu chơi và thẻ Chơi mới ẩn đi.
    /// </summary>
    public bool HasProgress =>
        _profile.CurrentPuzzleIndex > 0 || _profile.Score > 0 || _profile.SolvedPuzzleIds.Count > 0;

    public string ContinueTitle => HasProgress ? "Chơi tiếp" : "Bắt đầu chơi";

    public string ContinueDetail => HasProgress
        ? "Vào lại đúng câu bạn đang dở, giữ nguyên điểm và kim cương."
        : $"Bộ câu đố hiện có {_totalPuzzles} câu. Chơi tới đâu game tự lưu tới đó.";

    /// <summary>Viên nhãn trên thẻ Chơi tiếp: "CÂU 3/6" hoặc "CHƯA CHƠI CÂU NÀO".</summary>
    public string ContinuePill => HasProgress
        ? $"CÂU {Math.Min(_profile.CurrentPuzzleIndex + 1, _totalPuzzles)}/{_totalPuzzles}"
        : "VÁN MỚI";

    public string ScoreText => _profile.Score.ToString();
    public string RubiesText => _profile.Rubies.ToString();
    public string LivesText => $"{Math.Max(0, _profile.Lives)}/{_profile.MaxLives}";
    public string SolvedText => $"{_profile.SolvedPuzzleIds.Count}/{_totalPuzzles}";

    /// <summary>
    /// Đọc lại tiến trình từ cơ sở dữ liệu. Gọi mỗi khi từ màn chơi quay về,
    /// để mấy con số trên menu không còn là số cũ.
    /// </summary>
    public void Refresh()
    {
        _profile = _state.LoadProfile();

        OnPropertyChanged(nameof(HasProgress));
        OnPropertyChanged(nameof(ContinueTitle));
        OnPropertyChanged(nameof(ContinueDetail));
        OnPropertyChanged(nameof(ContinuePill));
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(RubiesText));
        OnPropertyChanged(nameof(LivesText));
        OnPropertyChanged(nameof(SolvedText));
    }

    // ----- Chơi mới -----

    private bool _isConfirmingNewGame;
    public bool IsConfirmingNewGame
    {
        get => _isConfirmingNewGame;
        private set => SetProperty(ref _isConfirmingNewGame, value);
    }

    /// <summary>Chưa chơi gì thì chơi mới cũng như bắt đầu, khỏi hỏi lại.</summary>
    private void AskNewGame()
    {
        if (!HasProgress) StartGame?.Invoke();
        else IsConfirmingNewGame = true;
    }

    private void ConfirmNewGame()
    {
        IsConfirmingNewGame = false;

        // Xóa sạch cả điểm, kim cương lẫn danh sách câu đã giải
        _state.ResetProfile();
        Refresh();

        StartGame?.Invoke();
    }

    // ----- Bảng xếp hạng -----

    private bool _isLeaderboardOpen;
    public bool IsLeaderboardOpen
    {
        get => _isLeaderboardOpen;
        private set => SetProperty(ref _isLeaderboardOpen, value);
    }

    private List<LeaderboardRow> _leaderboard = new();
    public List<LeaderboardRow> Leaderboard
    {
        get => _leaderboard;
        private set => SetProperty(ref _leaderboard, value);
    }

    public bool IsLeaderboardEmpty => Leaderboard.Count == 0;

    private void ShowLeaderboard()
    {
        // Lấy lại mỗi lần mở, vì người khác dùng chung máy có thể vừa chơi xong
        Leaderboard = GameStateService.TopPlayers();
        OnPropertyChanged(nameof(IsLeaderboardEmpty));
        IsLeaderboardOpen = true;
    }

    // ----- Sáng / tối -----

    public bool IsDarkTheme => _settings.IsDarkTheme;

    private void ToggleTheme()
    {
        _settings.IsDarkTheme = !_settings.IsDarkTheme;
        _settings.Save();
        ThemeService.Apply(_settings.IsDarkTheme);

        OnPropertyChanged(nameof(IsDarkTheme));
    }
}
