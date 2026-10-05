using System.Windows.Input;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Gõ đáp án bằng bàn phím vật lý: chữ cái điền vào ô, Backspace lấy chữ ra,
/// Enter nộp bài. Màn Cổ điển và màn Đấu dùng chung đúng một đoạn này — trước
/// đây mỗi cửa sổ giữ một bản, sửa một bên là bên kia lệch.
/// </summary>
public static class LetterKeys
{
    /// <summary>
    /// Biến một cú gõ thành thao tác trên hàng đáp án. Trả về true khi đã xử lý
    /// (và tự đặt <c>e.Handled</c>), để phím không đi tiếp xuống các nút.
    ///
    /// Bộ gõ tiếng Việt (Telex/VNI của Windows) đang bật thì phím tới dưới dạng
    /// <see cref="Key.ImeProcessed"/>; phải lấy phím thật ra, không thì chữ rơi
    /// vào ô ghép của bộ gõ chứ không vào ô đáp án.
    ///
    /// Việc chặn lúc không được gõ (đang tạm dừng, đang hỏi kim cương, đang chờ
    /// phạt…) là của view model, không phải của chỗ này.
    /// </summary>
    public static bool Handle(KeyEventArgs e, ILetterTyping typing)
    {
        Key key = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

        if (key is >= Key.A and <= Key.Z) typing.TypeLetter((char)('A' + (key - Key.A)));
        else if (key == Key.Back) typing.EraseLast();
        else if (key == Key.Enter) typing.Submit();
        else return false;

        return e.Handled = true;
    }
}
