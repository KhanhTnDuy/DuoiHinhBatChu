namespace DuoiHinhBatChu.Models;

/// <summary>
/// Lối chơi của một ván Cổ điển: thứ tự các câu được đưa ra.
///
/// Chọn một lần lúc ván mới bắt đầu và giữ nguyên tới khi ván chốt sổ, nên
/// lưu cùng chỗ với <c>RunSeed</c>. Số gán cố định vì cột trong bảng
/// <c>PlayerStates</c> giữ nguyên con số này.
/// </summary>
public enum RunOrder
{
    /// <summary>
    /// Xáo hết, câu khó có thể đến ngay từ đầu. Vì rủi ro hơn nên mỗi câu
    /// đúng được nhân thêm hệ số (<see cref="Services.SoloScoring.RandomOrderMultiplier"/>).
    /// Là giá trị 0 để mọi dòng cũ trong bảng (trước khi có lựa chọn này) tự
    /// rơi về đúng lối chơi mà chúng đã được xáo.
    /// </summary>
    Random = 0,

    /// <summary>Câu dễ trước, khó dần; trong cùng một độ khó vẫn xáo theo hạt giống.</summary>
    EasyFirst = 1,
}
