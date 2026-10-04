namespace DuoiHinhBatChu.Services;

/// <summary>
/// Câu dẫn trên đầu màn chơi, nói thẳng đáp án thuộc loại gì: "Đây là một
/// con vật", "Đây là một câu ca dao - tục ngữ"…
///
/// Đặt ở Core vì cả màn Cổ điển lẫn màn Đấu đều hỏi cùng một câu; máy chủ
/// chỉ gửi tên chủ đề, còn diễn đạt thành câu là việc của phía hiển thị.
///
/// Trước đây tiêu đề là "Đây là gì?" và chủ đề chỉ là một chip nhỏ bên dưới —
/// bot chơi thử 2026-09-15 cho thấy người chơi thường chết ở câu 15/39, và
/// một phần là vì gợi ý duy nhất được cho không lại quá mờ nhạt. Đưa chủ đề
/// lên thành chính câu hỏi thì gợi ý đó được đọc thật sự.
/// </summary>
public static class CategoryPrompt
{
    /// <summary>Câu dùng khi chủ đề trống hoặc chưa có trong bảng.</summary>
    public const string Fallback = "Đây là gì?";

    private static readonly Dictionary<string, string> Prompts = new()
    {
        ["Động vật"] = "Đây là một con vật",
        ["Thực vật"] = "Đây là một loài cây",
        ["Đồ vật"] = "Đây là một đồ vật",
        ["Địa danh"] = "Đây là một địa danh",
        ["Nhân vật"] = "Đây là một nhân vật nổi tiếng",
        ["Ca dao - tục ngữ"] = "Đây là một câu ca dao - tục ngữ",
        ["Cụm từ"] = "Đây là một từ hoặc cụm từ",
        ["Hoạt động"] = "Đây là một hành động",
        ["Giáo dục"] = "Đây là một từ về giáo dục",
        ["Kiến trúc"] = "Đây là một công trình kiến trúc",
        ["Phương tiện"] = "Đây là một phương tiện đi lại",
        ["Thể thao"] = "Đây là một môn thể thao",
    };

    /// <summary>Câu dẫn cho một chủ đề; chủ đề lạ thì về <see cref="Fallback"/>.</summary>
    public static string For(string? category) =>
        category != null && Prompts.TryGetValue(category.Trim(), out string? s) ? s : Fallback;
}
