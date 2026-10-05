using System.Runtime.InteropServices;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.Models;

/// <summary>Một phím chữ trong ngân hàng ký tự.</summary>
/// <param name="Character">Chữ hiện trên phím.</param>
/// <param name="IsAnswerLetter">
/// Chữ này thuộc đáp án hay chỉ là chữ nhiễu. Trợ giúp "Xóa chữ thừa" chỉ được
/// phép xóa chữ nhiễu.
/// </param>
public readonly record struct RoundTile(char Character, bool IsAnswerLetter);

/// <summary>
/// Một lượt chơi đã dựng sẵn: chuỗi ô đáp án và ngân hàng phím chữ.
///
/// Đặt ở Core vì cả hai bên đều cần đúng một luật:
///   - Cổ điển: <c>GameViewModel</c> dựng lượt ngay trên máy.
///   - Đấu nhiều người: máy chủ dựng lượt rồi phát cho mọi người chơi, để ai
///     cũng nhận đúng một bộ phím và đáp án không rời khỏi máy chủ.
/// </summary>
public sealed class PuzzleRound
{
    /// <summary>Bảng chữ cái tiếng Việt không dấu (không có F, J, W, Z).</summary>
    private const string Alphabet = "ABCDEGHIKLMNOPQRSTUVXY";

    private PuzzleRound(string slotText, IReadOnlyList<RoundTile> tiles)
    {
        SlotText = slotText;
        Tiles = tiles;
    }

    /// <summary>Đáp án dạng người chơi phải ghép: bỏ dấu, viết hoa, vd "SONG CHO".</summary>
    public string SlotText { get; }

    /// <summary>Ngân hàng phím chữ đã xáo trộn.</summary>
    public IReadOnlyList<RoundTile> Tiles { get; }

    /// <summary>Số chữ cái của từng tiếng, dùng để vẽ ô mà không lộ đáp án.</summary>
    public int[] WordLengths => SlotText
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(w => w.Length)
        .ToArray();

    /// <summary>
    /// Chữ hiện trên ô đáp án và trên phím: bỏ dấu, đổi "đ" thành "d", viết hoa.
    /// Dạng có dấu đầy đủ chỉ dùng khi báo đáp án lúc trả lời xong.
    /// </summary>
    public static string ToSlotText(string answer) =>
        string.Join(' ', AnswerChecker.Normalize(answer)
                                      .Split(' ', StringSplitOptions.RemoveEmptyEntries))
              .ToUpperInvariant();

    /// <summary>
    /// Dựng một lượt chơi cho <paramref name="answer"/>.
    /// </summary>
    /// <param name="answer">Đáp án gốc, có thể còn dấu.</param>
    /// <param name="otherAnswers">
    /// Đáp án của các câu khác, lấy chữ nhiễu từ đây cho sát chất tiếng Việt.
    /// Gom lại chưa đủ nhiều thì bù thêm từ bảng chữ cái.
    /// </param>
    /// <param name="rng">Nguồn ngẫu nhiên, truyền vào để test lặp lại được.</param>
    /// <param name="minTiles">Số phím tối thiểu, cho bàn phím đỡ trống trải.</param>
    /// <param name="maxTiles">Số phím tối đa, cho bàn phím khỏi tràn màn hình.</param>
    /// <param name="minFiller">
    /// Số chữ nhiễu ít nhất phải có. Trần <paramref name="maxTiles"/> là để bàn
    /// phím khỏi tràn màn hình, nhưng câu ca dao dài hơn cả trần thì trần ăn
    /// mất sạch phần nhiễu: mọi phím đều là chữ thật, người chơi biết chắc
    /// không có chữ nào thừa và câu khó nhất bộ hóa ra lại dễ nhất. Sàn này
    /// thắng trần, vì thà bàn phím thêm một hàng còn hơn hỏng luật chơi.
    /// </param>
    public static PuzzleRound Create(
        string answer,
        IEnumerable<string> otherAnswers,
        Random rng,
        int minTiles = 12,
        int maxTiles = 21,
        int minFiller = 4)
    {
        string slotText = ToSlotText(answer);

        var letters = slotText.Where(char.IsLetter).ToList();
        int answerLetters = letters.Count;

        char[] pool = FillerPool(otherAnswers, minTiles);
        int total = Math.Max(
            Math.Clamp(answerLetters + 6, minTiles, maxTiles),
            answerLetters + minFiller);
        while (letters.Count < total)
            letters.Add(pool[rng.Next(pool.Length)]);

        // Xáo để vị trí phím không đoán được
        rng.Shuffle(CollectionsMarshal.AsSpan(letters));

        // Đúng số lượng chữ cần cho đáp án được đánh dấu "chữ thật", phần dư là chữ nhiễu
        var remaining = slotText.Where(char.IsLetter)
                                .GroupBy(c => c)
                                .ToDictionary(g => g.Key, g => g.Count());

        var tiles = new List<RoundTile>(letters.Count);
        foreach (char c in letters)
        {
            bool isReal = remaining.TryGetValue(c, out int n) && n > 0;
            if (isReal) remaining[c] = n - 1;
            tiles.Add(new RoundTile(c, isReal));
        }

        return new PuzzleRound(slotText, tiles);
    }

    private static char[] FillerPool(IEnumerable<string> answers, int minTiles)
    {
        var pool = answers
            .SelectMany(ToSlotText)
            .Where(char.IsLetter)
            .ToHashSet();

        if (pool.Count < minTiles) pool.UnionWith(Alphabet);

        return pool.ToArray();
    }
}
