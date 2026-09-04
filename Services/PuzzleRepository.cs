using System.IO;
using System.Text.Json;
using DuoiHinhBatChu.Models;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Đọc danh sách câu đố từ file JSON.
/// </summary>
public class PuzzleRepository
{
    // Bỏ qua khác biệt hoa/thường giữa khóa JSON (camelCase) và thuộc tính C# (PascalCase).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;

    /// <summary>
    /// Mặc định đọc file Data/puzzles.json nằm cạnh file .exe.
    /// Có thể truyền đường dẫn khác (dùng khi test hoặc màn quản lý câu đố sau này).
    /// </summary>
    public PuzzleRepository(string? filePath = null)
    {
        _filePath = filePath
            ?? Path.Combine(AppContext.BaseDirectory, "Data", "puzzles.json");
    }

    /// <summary>
    /// Nạp toàn bộ câu đố, sắp xếp theo độ khó rồi theo mã câu.
    /// </summary>
    public List<Puzzle> LoadAll()
    {
        if (!File.Exists(_filePath))
            throw new FileNotFoundException(
                $"Không tìm thấy file câu đố: {_filePath}. " +
                "Kiểm tra Data/puzzles.json đã đặt Build Action = Content, Copy if newer chưa.");

        string json = File.ReadAllText(_filePath);

        List<Puzzle> puzzles;
        try
        {
            puzzles = JsonSerializer.Deserialize<List<Puzzle>>(json, JsonOptions) ?? new();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"File {_filePath} sai định dạng JSON: {ex.Message}", ex);
        }

        return puzzles
            .OrderBy(p => p.Difficulty)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .ToList();
    }
}
