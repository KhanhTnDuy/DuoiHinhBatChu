using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.ViewModels;

/// <summary>
/// Menu của chế độ Cổ điển, mở ra sau khi chọn chế độ.
///
/// Trước đây bấm "Cổ điển" là vào thẳng màn chơi, nên người chơi không có
/// chỗ nào để xem mình đang ở đâu, chơi lại từ đầu hay xem bảng xếp hạng.
/// Menu này là chỗ đó; màn chơi chỉ mở khi bấm Chơi tiếp hoặc Chơi mới.
/// </summary>
public class MenuViewModel : ViewModelBase
{
    private readonly Account _account;
    private readonly AppSettings _settings;
    private readonly GameStateService _state;
    /// <summary>
    /// Có ván nào đang chơi dở không.
    ///
    /// Dấu hiệu là hạt giống xáo bài: mỗi ván có một hạt giống, và ván kết thúc
    /// thì nó về 0 (xem <see cref="GameStateService.SaveEndOfRun"/>). KHÔNG dựa
    /// vào "đã giải câu nào chưa" nữa — chơi xong một ván là danh sách đã giải
    /// có tên, mà lúc đó đâu còn ván nào để "tiếp".
    /// </summary>
    private bool _hasRun;

    /// <summary>
    /// Hỏi một lần lúc dựng màn, TRƯỚC khi người chơi kịp bấm gì.
    ///
    /// Phải chốt sớm như vậy vì chơi xong một ván là bảng có dòng tiến trình:
    /// hỏi lại lúc đó thì người mới chơi lần đầu quay về menu đã thành "người
    /// quen", lời chào đổi ngay trước mắt họ.
    /// </summary>
    private readonly bool _hasPlayedBefore;

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
        _hasPlayedBefore = _state.HasPlayedBefore();
        _profile = _state.LoadProfile();

        _hasRun = _profile.RunSeed != 0;

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

    /// <summary>
    /// Lời chào ở đầu màn menu, thay cho nhãn "NGƯỜI CHƠI" khô khan trước đây.
    ///
    /// Người quay lại được chào khác người mới: "Chào mừng trở lại" chỉ đúng khi
    /// tài khoản đã có tiến trình lưu. Khách thì lần nào cũng là lần đầu — hồ sơ
    /// khách bị dọn sạch mỗi lần khởi động nên không có "lần trước" để nhớ.
    /// </summary>
    public string Greeting => _hasPlayedBefore
        ? $"Chào mừng trở lại, {_account.DisplayName}!"
        : $"Chào mừng, {_account.DisplayName}!";

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
    public bool HasProgress => _hasRun;

    public string ContinueTitle => HasProgress ? "Chơi tiếp" : "Bắt đầu chơi";

    public string ContinueDetail => HasProgress
        ? "Vào lại đúng câu bạn đang dở, giữ nguyên điểm ván và kim cương."
        : "Bắt đầu từ câu đầu tiên, với 5 mạng và 2 kim cương.";

    /// <summary>
    /// Đang chơi khách. Nói thẳng ngay trên menu thay vì để người ta chơi cả
    /// buổi rồi mới ngã ngửa vì mất sạch điểm.
    /// </summary>
    public bool IsGuest => _account.IsGuest;

    /// <summary>
    /// Viên nhãn trên thẻ Chơi tiếp.
    ///
    /// Từng hiện "CÂU 22". Bỏ con số đi vì thứ tự câu nay xáo lại mỗi ván, nên
    /// "câu thứ 22" không nói lên điều gì: nó không phải câu thứ 22 của bộ, mà
    /// là câu thứ 22 của một thứ tự chỉ ván này mới có.
    /// </summary>
    public string ContinuePill => HasProgress ? "ĐANG DỞ" : "VÁN MỚI";

    /// <summary>
    /// Kỷ lục, KHÔNG phải điểm ván đang dở.
    ///
    /// Menu là chỗ nhìn lại thành tích, mà điểm ván đang dở thì nay còn mai mất
    /// — thua một ván là nó về 0. Con số đáng khoe ở đây là điểm ván cao nhất.
    /// Điểm ván đang chạy vẫn hiện đầy đủ trên thanh trạng thái màn chơi.
    /// </summary>
    public string BestScoreText => _profile.BestScore.ToString();
    public string RubiesText => _profile.Rubies.ToString();
    public string LivesText => $"{Math.Max(0, _profile.Lives)}/{_profile.MaxLives}";
    /// <summary>
    /// Chỉ đếm số câu đã giải, KHÔNG kèm tổng số câu trong cơ sở dữ liệu.
    ///
    /// Tổng số câu là chuyện nội bộ của kho câu đố, mà lại đang thay đổi liên
    /// tục (mới có 6/50 ảnh): hiện "2/6" hôm nay rồi "2/50" tuần sau thì người
    /// chơi tưởng mình tụt lùi, dù họ vẫn giải đúng chừng ấy câu.
    /// </summary>
    public string SolvedText => _profile.SolvedPuzzleIds.Count.ToString();

    /// <summary>
    /// Đọc lại tiến trình từ cơ sở dữ liệu. Gọi mỗi khi từ màn chơi quay về,
    /// để mấy con số trên menu không còn là số cũ.
    /// </summary>
    public void Refresh()
    {
        _profile = _state.LoadProfile();
        _hasRun = _profile.RunSeed != 0;

        OnPropertyChanged(nameof(HasProgress));
        OnPropertyChanged(nameof(ContinueTitle));
        OnPropertyChanged(nameof(ContinueDetail));
        OnPropertyChanged(nameof(ContinuePill));
        OnPropertyChanged(nameof(BestScoreText));
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
