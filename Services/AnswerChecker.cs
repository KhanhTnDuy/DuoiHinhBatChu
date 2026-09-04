using System.Globalization;
using System.Text;
using DuoiHinhBatChu.Models;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Chuẩn hóa và so khớp đáp án kiểu tiếng Việt:
/// viết thường, bỏ dấu thanh + dấu mũ/móc, đổi "đ" -> "d",
/// bỏ ký tự không phải chữ/số, gộp khoảng trắng.
/// Nhờ vậy "CÁ HEO", "cá heo", "ca heo", " Cá-Heo! " đều khớp nhau.
/// </summary>
public static class AnswerChecker
{
    /// <summary>Người chơi trả lời <paramref name="guess"/> có đúng câu <paramref name="puzzle"/> không.</summary>
    public static bool IsCorrect(string guess, Puzzle puzzle)
    {
        string g = Normalize(guess);
        if (g.Length == 0) return false;

        if (g == Normalize(puzzle.Answer)) return true;

        foreach (string accepted in puzzle.AcceptedAnswers)
            if (g == Normalize(accepted)) return true;

        return false;
    }

    /// <summary>Đưa chuỗi về dạng "chuẩn" để đem so sánh (cũng dùng cho gợi ý ở Phần 5).</summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        // 1) Viết thường + đổi "đ" -> "d".
        //    Chữ "đ" không tự tách dấu qua Unicode nên phải thay bằng tay.
        string lower = input.Trim().ToLowerInvariant().Replace("đ", "d");

        // 2) Tách chữ khỏi dấu (FormD), rồi bỏ mọi "dấu kết hợp":
        //    sắc / huyền / hỏi / ngã / nặng, mũ (â ê ô), móc (ơ ư), trăng (ă).
        string decomposed = lower.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;                       // bỏ dấu, KHÔNG chèn khoảng trắng

            if (char.IsLetterOrDigit(c))
                sb.Append(c);                   // giữ chữ và số
            else
                sb.Append(' ');                 // khoảng trắng và mọi ký tự khác (. , - ! …)
                                                // -> thành dấu cách, giữ ranh giới giữa các từ
        }

        // 3) Gộp mọi khoảng trắng liên tiếp thành đúng 1 dấu cách.
        return string.Join(
            ' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
