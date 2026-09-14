using DuoiHinhBatChu.Data;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Server;
using DuoiHinhBatChu.Services;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<AccountService>(_ => new AccountService());
builder.Services.AddSingleton<PuzzleRepository>(_ => new PuzzleRepository());
builder.Services.AddSingleton<RoomManager>();

var app = builder.Build();

// Mở (và tạo nếu chưa có) cơ sở dữ liệu ngay lúc khởi động: sai đường dẫn hay
// hỏng file thì báo ngay, chứ không đợi tới lúc có người bấm đăng nhập
GameDatabase.EnsureReady();
app.Logger.LogInformation("Cơ sở dữ liệu: {Path}", GameDatabase.FilePath);

// Đối chiếu thư mục ảnh với bảng câu đố. Phải chạy TRƯỚC khi lấy RoomManager,
// vì RoomManager nạp danh sách câu ngay trong hàm dựng.
PuzzleSync.Report sync = PuzzleSync.Sync();
app.Logger.LogInformation("Câu đố: {Sync}", sync);

var rooms = app.Services.GetRequiredService<RoomManager>();
app.Logger.LogInformation("Đã nạp {Count} câu đố", rooms.PuzzleCount);

// ===== Kiểm tra máy chủ sống =====

app.MapGet("/api/health", (RoomManager rooms) => Results.Ok(new HealthResponse(
    "Đuổi hình bắt chữ",
    typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0",
    rooms.PuzzleCount,
    rooms.RoomCount)));

// ===== Xác thực =====

app.MapPost("/api/auth/register",
    (RegisterRequest req, AccountService accounts, TokenService tokens) =>
    {
        AuthResult result = accounts.Register(
            req.UserName, req.DisplayName, req.Password, req.Confirm, req.Phone);

        return result.Ok ? Ok(result.Account!, tokens) : BadRequest(result.Error);
    });

// Quên mật khẩu: khai đúng số điện thoại đã đăng ký thì đặt lại mật khẩu và
// vào luôn, khỏi phải quay ra đăng nhập lại một lần nữa
app.MapPost("/api/auth/reset-password",
    (ResetPasswordRequest req, AccountService accounts, TokenService tokens) =>
    {
        AuthResult result = accounts.ResetPassword(
            req.UserName, req.Phone, req.NewPassword, req.Confirm);

        return result.Ok ? Ok(result.Account!, tokens) : BadRequest(result.Error);
    });

app.MapPost("/api/auth/login",
    (LoginRequest req, AccountService accounts, TokenService tokens) =>
    {
        AuthResult result = accounts.Login(req.UserName, req.Password);
        return result.Ok ? Ok(result.Account!, tokens) : BadRequest(result.Error);
    });

// ===== Ảnh câu đố =====
// Client tải ảnh từ đây nên máy người chơi không cần có sẵn cùng bộ ảnh.

app.MapGet("/api/puzzles/{imageKey}/image", (string imageKey, RoomManager rooms) =>
{
    // Tham số là MÃ tải ảnh ngẫu nhiên do RoomManager cấp trong RoundInfo, không
    // phải tên file — tên file chính là đáp án. Mã lạ (hay trò đi ngược thư
    // mục "../../secret.txt") đều chỉ ra 404; ảnh nằm trong cơ sở dữ liệu chứ
    // không phải trên đĩa.
    var image = rooms.Image(imageKey);

    return image == null
        ? Results.NotFound(new ErrorResponse("Không có ảnh này."))
        : Results.File(image.Value.Bytes, image.Value.ContentType);
});

app.MapHub<GameHub>("/game");

app.Run();

static Results<Ok<AuthResponse>, BadRequest<ErrorResponse>> Ok(Account account, TokenService tokens)
    => TypedResults.Ok(new AuthResponse(
        tokens.Issue(account), account.Id, account.UserName, account.DisplayName));

static Results<Ok<AuthResponse>, BadRequest<ErrorResponse>> BadRequest(string error)
    => TypedResults.BadRequest(new ErrorResponse(error));
