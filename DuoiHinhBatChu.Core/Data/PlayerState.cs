namespace DuoiHinhBatChu.Data;

/// <summary>
/// Một dòng trong bảng <c>PlayerStates</c>: tiến trình chơi đơn của một tài khoản.
///
/// Mỗi tài khoản đúng một dòng, khóa chính chính là mã tài khoản (quan hệ 1-1).
/// Danh sách câu đã giải KHÔNG nằm ở đây mà tách sang bảng
/// <see cref="PuzzleResult"/> — đó là cách làm đúng của cơ sở dữ liệu quan hệ:
/// một ô chỉ giữ một giá trị, cái gì "nhiều" thì cho ra bảng riêng.
/// </summary>
public class PlayerState
{
    /// <summary>Khóa chính, đồng thời là khóa ngoại trỏ về <c>Accounts.Id</c>.</summary>
    public string AccountId { get; set; } = "";

    /// <summary>Điểm của VÁN đang chơi dở. Ván mới bắt đầu là về 0.</summary>
    public int Score { get; set; }

    /// <summary>
    /// Điểm ván cao nhất từ trước tới nay — con số duy nhất được lên bảng xếp hạng.
    ///
    /// Trước đây bảng xếp hạng lấy thẳng <see cref="Score"/>, mà điểm hồi đó
    /// cộng dồn vĩnh viễn qua mọi ván: ai ngồi lâu thì cao, không liên quan tới
    /// giỏi hay dở. Tách làm hai con số thì "điểm" mới có chỗ để dừng lại và so.
    /// </summary>
    public int BestScore { get; set; }

    public int Rubies { get; set; } = 2;
    public int CorrectStreak { get; set; }
    public int Lives { get; set; } = 5;
    public int MaxLives { get; set; } = 5;

    /// <summary>
    /// Hạt giống dùng để xáo thứ tự câu của ván này. 0 = chưa có ván nào.
    ///
    /// Mỗi ván mới bốc một hạt giống mới, nên thứ tự câu lần nào cũng khác.
    /// Lưu HẠT GIỐNG chứ không lưu cả danh sách đã xáo: một số nguyên là đủ
    /// dựng lại y nguyên thứ tự đó, nhờ vậy thoát ra rồi vào lại vẫn đúng ván
    /// cũ chứ không bị xáo lại giữa chừng.
    /// </summary>
    public int RunSeed { get; set; }

    /// <summary>
    /// Lối chơi của ván này (<see cref="Models.RunOrder"/>): 0 = ngẫu nhiên,
    /// 1 = từ dễ đến khó. Đi cùng <see cref="RunSeed"/>: chỉ có nghĩa khi
    /// ván đang dở, ván chốt sổ thì về 0 cùng lúc với hạt giống.
    /// </summary>
    public int RunOrder { get; set; }

    /// <summary>
    /// Mã câu đang chơi dở. Rỗng nghĩa là chưa vào ván nào.
    ///
    /// Trước đây chỗ này giữ SỐ THỨ TỰ của câu, và đó là chỗ hỏng: thứ tự câu
    /// là thứ tự tên file trong <c>Assets/CauHoi</c>, nên chỉ cần thêm một ảnh
    /// mới có tên đứng trước là mọi con số đã lưu trỏ sang câu khác — ai đang
    /// chơi dở cũng bị đá sang một câu chẳng liên quan. Mã câu thì gắn với
    /// chính đáp án (xem <see cref="PuzzleSync.MakeId"/>), thêm bớt ảnh bao
    /// nhiêu cũng không xê dịch.
    /// </summary>
    public string CurrentPuzzleId { get; set; } = "";

    public bool IsSoundEnabled { get; set; } = true;
    public bool IsBgmEnabled { get; set; } = true;
    public bool IsTimerEnabled { get; set; } = true;

    /// <summary>Lần lưu gần nhất, để sau này làm mục "chơi tiếp".</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
