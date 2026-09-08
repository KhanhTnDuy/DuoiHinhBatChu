using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu.Models;

/// <summary>
/// Một phím chữ cái trong ngân hàng ký tự phía dưới màn chơi.
/// </summary>
public class LetterTile : ViewModelBase
{
    public int Id { get; set; }
    public char Character { get; set; }

    private bool _isUsed;
    /// <summary>Đã được bấm chọn lên hàng đáp án.</summary>
    public bool IsUsed
    {
        get => _isUsed;
        set => SetProperty(ref _isUsed, value);
    }

    private bool _isEliminated;
    /// <summary>Bị trợ giúp "Xóa chữ thừa" loại bỏ.</summary>
    public bool IsEliminated
    {
        get => _isEliminated;
        set => SetProperty(ref _isEliminated, value);
    }

    /// <summary>Là chữ thuộc đáp án (dùng cho trợ giúp loại trừ).</summary>
    public bool IsCorrectLetter { get; set; }
}
