using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu;

public partial class MainWindow : Window
{
    private readonly List<Puzzle> _puzzles;
    private int _index;
    private bool _solved;   // câu hiện tại đã trả lời đúng chưa (chặn bấm tiếp)

    // Bộ đếm — Phần 4 sẽ dùng để tính điểm
    private int _solvedCount;
    private int _wrongCount;
    private int _skippedCount;

    public MainWindow()
    {
        InitializeComponent();

        _puzzles = new PuzzleRepository().LoadAll();
        ShowPuzzle(0);
    }

    private Puzzle Current => _puzzles[_index];

    /// <summary>Hiển thị câu đố thứ <paramref name="index"/> lên màn hình.</summary>
    private void ShowPuzzle(int index)
    {
        _index = index;
        _solved = false;

        Puzzle p = Current;
        TxtProgress.Text = $"Câu {_index + 1}/{_puzzles.Count}";
        TxtDifficulty.Text = "Độ khó: "
            + new string('★', p.Difficulty)
            + new string('☆', 5 - p.Difficulty);
        TxtFeedback.Text = "";
        TxtHint.Text = "";
        TxtAnswer.Clear();
        TxtAnswer.Focus();

        RefreshCounters();
        ShowImage(p);
    }

    private void RefreshCounters()
        => TxtScore.Text = $"Đúng {_solvedCount}  ·  Sai {_wrongCount}  ·  Bỏ qua {_skippedCount}";

    /// <summary>Có file ảnh thì hiện ảnh; chưa có thì hiện mô tả để vẫn chơi được.</summary>
    private void ShowImage(Puzzle p)
    {
        string relative = p.Image.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(AppContext.BaseDirectory, relative);

        if (File.Exists(fullPath))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;  // đọc xong nhả file, không khóa
            bmp.UriSource = new Uri(fullPath);
            bmp.EndInit();

            ImgPuzzle.Source = bmp;
            ImgPuzzle.Visibility = Visibility.Visible;
            PanelNoImage.Visibility = Visibility.Collapsed;
        }
        else
        {
            ImgPuzzle.Source = null;
            ImgPuzzle.Visibility = Visibility.Collapsed;
            TxtDraw.Text = p.Draw;
            PanelNoImage.Visibility = Visibility.Visible;
        }
    }

    private void SubmitAnswer()
    {
        if (_solved) return;

        string guess = TxtAnswer.Text.Trim();
        if (guess.Length == 0) return;

        if (AnswerChecker.IsCorrect(guess, Current))
        {
            _solved = true;
            _solvedCount++;
            RefreshCounters();
            TxtFeedback.Foreground = System.Windows.Media.Brushes.LightGreen;
            TxtFeedback.Text = $"✔ Chính xác!  Đáp án: {Current.Answer}";
            AdvanceAfterDelay();
        }
        else
        {
            _wrongCount++;
            RefreshCounters();
            TxtFeedback.Foreground = System.Windows.Media.Brushes.Salmon;
            TxtFeedback.Text = "✘ Chưa đúng, thử lại!";
        }
    }

    /// <summary>Sau 1,2 giây tự chuyển câu kế tiếp.</summary>
    private void AdvanceAfterDelay()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            GoNext();
        };
        timer.Start();
    }

    private void GoNext()
    {
        if (_index + 1 < _puzzles.Count)
        {
            ShowPuzzle(_index + 1);
        }
        else
        {
            _solved = true;
            TxtFeedback.Foreground = System.Windows.Media.Brushes.Gold;
            TxtFeedback.Text = "🎉 Bạn đã chơi hết 50 câu! (màn kết thúc làm ở Phần 6)";
        }
    }

    // ----- Sự kiện -----

    private void BtnSubmit_Click(object sender, RoutedEventArgs e) => SubmitAnswer();

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        if (!_solved)
        {
            _skippedCount++;
            RefreshCounters();
        }
        GoNext();
    }

    // Phần 5 sẽ đổi thành lộ dần chữ cái + trừ điểm.
    private void BtnHint_Click(object sender, RoutedEventArgs e)
        => TxtHint.Text = $"Gợi ý: {Current.Hint}";

    private void TxtAnswer_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SubmitAnswer();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
