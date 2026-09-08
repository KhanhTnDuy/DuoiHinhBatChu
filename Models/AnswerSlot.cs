using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu.Models;

/// <summary>
/// Một ô ký tự trong hàng đáp án. Kế thừa ViewModelBase để giao diện tự cập nhật
/// mỗi khi người chơi bỏ chữ vào ô hoặc lấy chữ ra.
/// </summary>
public class AnswerSlot : ViewModelBase
{
    public int Index { get; set; }

    /// <summary>Ký tự đúng của ô này (đã viết hoa).</summary>
    public char TargetChar { get; set; }

    /// <summary>Ô này là khoảng trắng giữa hai từ (không bấm được).</summary>
    public bool IsSpace { get; set; }

    private char? _currentChar;
    public char? CurrentChar
    {
        get => _currentChar;
        set
        {
            if (!SetProperty(ref _currentChar, value)) return;
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(HasValue));
        }
    }

    private bool _isRevealedByHint;
    /// <summary>Chữ do trợ giúp mở ra — người chơi không được lấy ra.</summary>
    public bool IsRevealedByHint
    {
        get => _isRevealedByHint;
        set => SetProperty(ref _isRevealedByHint, value);
    }

    private bool _isWrong;
    /// <summary>Đang tô đỏ vì đáp án vừa sai.</summary>
    public bool IsWrong
    {
        get => _isWrong;
        set => SetProperty(ref _isWrong, value);
    }

    /// <summary>Id của phím chữ đã bỏ vào đây, để trả lại đúng phím đó khi gỡ ra.</summary>
    public int? SourceTileId { get; set; }

    public bool HasValue => CurrentChar.HasValue;
    public string DisplayText => IsSpace ? "" : (CurrentChar?.ToString() ?? "");
}
