# Đuổi hình bắt chữ

Game đoán chữ từ hình, viết bằng **WPF / C# / .NET 10** cho Windows. Người chơi
nhìn một tấm ảnh ghép rồi bấm các phím chữ cái để điền vào ô trả lời — kiểu chơi
quen thuộc của game đố chữ trên điện thoại.

Có hai chế độ: **một người** (chơi offline, lưu tiến trình) và **nhiều người**
(đấu qua mạng với một máy chủ ASP.NET Core + SignalR).

## Lối chơi (chế độ một người)

Một **ván** là một lượt đi qua bộ câu đố. Ván kết thúc khi **hết mạng** hoặc
**hết bộ câu**; thoát giữa chừng không tính — vào lại là chơi tiếp đúng ván đó.

- **5 ♥ mạng.** Đoán sai, hết giờ hoặc **bỏ qua** đều mất 1 mạng. Bỏ qua được
  cho biết đáp án (mất mạng để khỏi ngồi chờ hết 60 giây một câu chắc chắn
  không ra).
- **Mỗi câu 60 giây.** Trả lời đúng trong giờ được **điểm nền** `10 × độ khó`;
  càng nhanh càng được thưởng thêm, nhanh nhất là gấp đôi (`SoloScoring`).
- **Điểm là điểm của một ván**, không cộng dồn. Ván kết thúc thì đem so với
  **kỷ lục** (`BestScore`); cao hơn thì thay. Ván mới luôn bắt đầu từ 0 điểm.
  Bảng xếp hạng xếp theo kỷ lục.
- **Chuỗi đúng liên tiếp:** đúng **5 câu liền** được thưởng 1 💎; sai / hết giờ /
  bỏ qua là chuỗi về 0.
- **Kim cương:** tài khoản mới có vốn **2 💎**. Hai trợ giúp, mỗi lần dùng tốn
  1 💎:
  - **Mở 1 chữ** — người chơi **tự chọn ô** muốn mở.
  - **Xóa chữ thừa** — bỏ bớt các phím không nằm trong đáp án.

  Mỗi lần tiêu kim cương đều **hỏi lại** trước khi trừ. Đồng hồ vẫn chạy trong
  lúc hỏi, để hộp thoại không thành mẹo câu giờ. Đang hỏi mà hết giờ (hoặc
  bấm bỏ qua) thì hộp tự đóng và không trừ gì — kim cương chỉ mất cho câu
  đang chơi. Hết kim cương thì hai nút trợ giúp mờ đi.
- **Lối chơi của ván.** Ván mới bắt đầu là màn chơi hiện hộp *"Mời bạn chọn
  lối chơi"*, chọn một lần cho cả ván (`RunOrder`, lưu cùng `RunSeed`):
  - **Từ dễ đến khó** — sắp theo độ khó tăng dần, trong cùng một bậc vẫn xáo
    theo hạt giống, riêng câu dài (từ 12 chữ cái) xếp cuối bậc để không mở màn
    bằng một câu 27 chữ. Điểm ×1.
  - **Ngẫu nhiên** — xáo hết, câu khó có thể đến ngay từ đầu. Rủi ro hơn nên
    mỗi câu đúng được nhân **×1,5** (`SoloScoring.RandomOrderMultiplier`).

  "Chơi tiếp" không hỏi lại. Đang hỏi mà đóng cửa sổ thì ván chưa tính, lần
  sau vào hỏi lại. Ván nào cũng bốc hạt giống mới, nên chơi lại không gặp đúng
  dãy câu vừa rồi.
- Ô trả lời và phím chữ **luôn không dấu**; đáp án đầy đủ có dấu chỉ hiện khi
  trả lời đúng hoặc bấm bỏ qua. Sai hoặc hết giờ chỉ báo sai, không lộ đáp án.
- **Gõ bằng bàn phím vật lý** như màn Đấu: chữ cái điền vào ô, Backspace lấy
  chữ cuối ra (chữ mở bằng trợ giúp thì giữ), Enter trả lời khi đã kín. Không
  nhận phím khi đang tạm dừng, đang hỏi kim cương hay đang chọn ô để mở.
- **Tạm dừng rồi thoát game:** số giây còn lại được lưu, vào lại thì câu đó
  hiện ở trạng thái tạm dừng với đúng số giây cũ, bấm Tiếp tục để chạy tiếp.
  Bộ gõ tiếng Việt của Windows (Telex/VNI) đang bật cũng không sao: cửa sổ
  chơi tắt IME (`InputMethod.IsInputMethodEnabled=False`) và đọc
  `ImeProcessedKey` khi phím bị bộ gõ chặn. Lớp phủ đóng lại thì focus được
  kéo về cửa sổ (nút vừa bấm trong lớp phủ bị ẩn nhưng vẫn giữ focus).

Tiêu đề màn chơi nói thẳng đáp án thuộc loại gì: *"Đây là một con vật"*, *"Đây
là một câu ca dao - tục ngữ"*, *"Đây là một địa danh"*… (bảng
`GameViewModel.CategoryPrompts`; chủ đề chưa có trong bảng thì hiện *"Đây là
gì?"*). Đây là gợi ý cho không duy nhất. Độ khó 1–5 do người soạn chấm tay,
dùng để tính điểm và để sắp thứ tự khi chọn "Từ dễ đến khó".

### Chế độ đấu nhiều người

- Từ màn chọn chế độ có hai lối vào: **Vào phòng** (phòng bạn bè đã mở) hoặc
  **Tạo phòng ngay**. Chủ phòng đặt **mật khẩu** và **tên phòng** (tên không
  bắt buộc, chỉ để hiển thị cạnh mã); máy chủ cấp **mã phòng 6 ký tự** (không
  có 0/O, 1/I cho khỏi đọc nhầm). Người vào gõ đúng mã + mật khẩu. Sảnh chờ
  **không có đoạn hướng dẫn**: ô nhập tự nói mình là gì bằng chữ mờ
  (`conv:Placeholder.Text`, `Converters/Placeholder.cs` — mất khi bấm vào ô,
  hiện lại khi rời ô còn trống; PasswordBox theo dõi qua `Placeholder.HasContent`).
- Mỗi phòng tối đa **5 người** kể cả chủ phòng (`Room.MaxPlayers`); đầy thì
  máy chủ từ chối, kiểm tra trong cùng khóa với lúc thêm để hai người cùng vào
  chỗ cuối không lọt cả hai.
- **Sẵn sàng:** người vào phòng bấm *Sẵn sàng* (bấm lại để hủy). Chủ phòng chỉ
  bắt đầu được khi phòng có **từ 2 người** và **mọi khách đã sẵn sàng** (chủ
  phòng không cần bấm — bấm bắt đầu tức là sẵn sàng). Nút bắt đầu mờ đi kèm lý
  do; máy chủ kiểm tra lại lần nữa (`AllGuestsReady`). Ván bắt đầu thì cờ sẵn
  sàng xóa hết, ván sau phải bấm lại.
- Không phải chọn máy chủ hay đăng nhập máy chủ: app **tự nối** lúc bấm tạo /
  vào phòng (địa chỉ nằm trong `Data/app-settings.json`, mặc định
  `localhost:5180`).
- Trong phòng, **chỉ chủ phòng** chọn **kiểu chơi** và số câu (1–20, mặc định 5);
  người vào sau ở sảnh chờ, thấy lựa chọn của chủ phòng nhưng không đổi được. Hai kiểu chơi:
  - **Thi đấu** — cả phòng cùng nhận một ảnh, ai ghép chữ nhanh hơn thắng
    (đã chạy, luật bên dưới).
  - **Tôi vẽ bạn đoán** — một người vẽ, cả phòng đoán. *Đang để dành*, chọn
    được nhưng chưa bắt đầu được.
- Màn đấu cũng hiện câu dẫn theo chủ đề (*"Đây là một con vật"*) như màn Cổ
  điển: máy chủ gửi `RoundInfo.Category`, client dựng câu bằng
  `CategoryPrompt` (Core). Máy chủ phải có `Data/puzzles.json` (dự án Server
  copy sang lúc build) thì chủ đề / độ khó mới khớp với client.
- Trong ván đấu **gõ bằng bàn phím vật lý**: chữ cái điền vào ô kế tiếp
  (tự tìm phím trên màn hình có chữ đó, hết phím thì bỏ qua), **Backspace**
  lấy chữ cuối ra, **Enter** (hoặc nút *Gửi*) nộp — chỉ nộp được khi đã điền
  kín. Phím trên màn hình vẫn bấm được cho màn cảm ứng. **Không tự nộp** khi
  điền kín nữa: gõ nhanh dễ nhầm chữ cuối, mà sai là bị phạt chờ. Đang gõ
  trong ô nhập (mã phòng, số câu) thì bàn phím không bị bắt.
- Thi đấu: cả phòng cùng nhận một câu, **20 giây** mỗi câu. Ai đúng nhanh hơn
  được nhiều điểm hơn: `100 × độ khó × hệ số tốc độ`, sát giờ còn 20%, hết giờ
  là 0 (`MatchScoring`). Điểm luôn do **máy chủ** chấm vì chỉ máy chủ giữ mốc
  thời gian phát câu.
- Đoán sai phải chờ **1, 2, 3… tối đa 5 giây** mới được gửi tiếp, để không dò
  đáp án bằng cách bấm bừa.
- Đáp án không rời máy chủ trước khi câu kết thúc — kể cả qua đường ảnh:
  client tải ảnh bằng **mã ngẫu nhiên** máy chủ cấp trong `RoundInfo.ImageName`
  (`RoomManager._imageKeys`, sinh lại mỗi lần khởi động), không phải tên file,
  vì tên file chính là đáp án.
- Không có mạng, không có kim cương; hết số câu thì tổng kết điểm. Bằng điểm
  thì báo **hòa**. Sau ván, khách bấm **Sẵn sàng** lại ngay trên hộp kết quả,
  đủ người sẵn sàng thì chủ phòng bấm **Ván mới**. *Rời phòng* (và ESC) đưa về
  màn chọn chế độ chứ không đóng cửa sổ — cửa sổ đấu là cửa sổ duy nhất, đóng
  thẳng là app tắt.
- **Đứt kết nối là đứt hẳn**, không tự nối lại (`MatchClient` không dùng
  `WithAutomaticReconnect`): máy chủ đã xóa người rớt khỏi phòng ngay lúc đứt
  dây, nối lại cũng là kết nối mới không thuộc phòng nào. Client nhận `Closed`
  → `MatchViewModel.OnDisconnected` dừng đồng hồ, xóa phòng, về sảnh chờ và
  báo "Mất kết nối tới máy chủ"; người chơi tạo / vào lại phòng.

## Giao diện

Mọi cửa sổ cùng một cỡ: `WindowState="Maximized"`, `MinWidth=1280 MinHeight=900`,
thanh tiêu đề mặc định của Windows (từng có màn nhỏ 880×720 và màn đấu không
viền — đổi 2026-09-15 cho khỏi nhảy cỡ khi chuyển màn). Không có câu hướng dẫn
hay mô tả luật trên màn hình; ô nhập dùng chữ mờ. Nhãn mục (`SectionLabel`) 13px.

## Chạy thử

Cần [.NET 10 SDK](https://dotnet.microsoft.com/download) trên Windows.

```bash
git clone <repo>
cd DuoiHinhBatChu
dotnet run --project DuoiHinhBatChu.csproj
```

Lần chạy đầu, cơ sở dữ liệu `Data/game.db` được tạo tự động (EF Core Migrations)
và toàn bộ ảnh trong `Assets/CauHoi/` được nạp vào bảng câu đố.

Luồng màn hình: **Đăng nhập → Chọn chế độ → Menu → Màn chơi**. Muốn xem nhanh thì
bấm *Chơi khách* (khách không lưu tiến trình và không lên bảng xếp hạng).

Chế độ đấu **không phải mở máy chủ tay**: bấm *Tạo phòng* / *Vào phòng* là
app tự tìm `DuoiHinhBatChu.Server.exe` (build cùng app nhờ tham chiếu trong
`DuoiHinhBatChu.csproj`) và bật lên ở `http://localhost:5180` nếu chưa có ai
nghe ở đó (`Services/LocalServer`). Lần đầu mất chừng 15–20 giây vì máy chủ
phải nạp 52 ảnh vào cơ sở dữ liệu riêng của nó
(`DuoiHinhBatChu.Server/bin/.../Data/game.db`). App thoát thì máy chủ do nó
bật cũng tắt.

Vẫn mở tay được nếu muốn xem log:

```bash
dotnet run --project DuoiHinhBatChu.Server
```

Muốn đấu qua LAN thì máy chủ phải chạy ở máy kia (`dotnet run --project
DuoiHinhBatChu.Server --launch-profile lan`), còn máy client sửa `ServerAddress`
trong `Data/app-settings.json` thành địa chỉ máy đó (ví dụ `192.168.1.10:5180`)
— địa chỉ không phải máy này thì app không tự bật gì cả.

Tài khoản trên máy chủ là **sổ riêng** (máy chủ là bên ghi điểm ván đấu) nhưng
người chơi không phải đăng ký lại: client tự đăng nhập bằng tên đăng nhập của
tài khoản trên máy, "mật khẩu" là mã tài khoản (`Account.Id`, chuỗi ngẫu nhiên
sinh lúc tạo tài khoản); lần đầu gặp máy chủ thì tự đăng ký
(`ServerClient.SignInAsync`). Đăng ký cũng bị từ chối vì tên đã có (xóa
`game.db` rồi đăng ký lại cùng tên → `Account.Id` đổi) thì thử bước ba: gọi
`reset-password` với số điện thoại đã khai — đúng số thì lấy lại được tài
khoản trên máy chủ, sai số mới báo lỗi. Chi tiết:
[`Docs/MULTIPLAYER.md`](Docs/MULTIPLAYER.md).

Đoán sai trong ván đấu bị phạt chờ 1–5 giây (`RoomManager.Judge`). Đáp án gửi
tới lúc máy chủ còn đang phạt thì **không chấm**: `AnswerResult.Judged = false`,
client giữ nguyên chữ đang ghép và chỉ nối lại quãng chờ — không nháy đỏ, không
xóa chữ như khi sai thật.

## Kiến trúc

Ba dự án trong một solution (`DuoiHinhBatChu.slnx`):

| Dự án | Vai trò |
| --- | --- |
| `DuoiHinhBatChu.csproj` (thư mục gốc) | App WPF: các cửa sổ, ViewModel, giao diện sáng/tối, âm thanh |
| `DuoiHinhBatChu.Core` | Phần lõi không phụ thuộc WPF: luật chơi, tính điểm, tài khoản, EF Core. Nhắm `net10.0` thuần để máy chủ dùng lại y nguyên |
| `DuoiHinhBatChu.Server` | ASP.NET Core + SignalR: xác thực, phòng đấu, bốc câu, chấm điểm, phục vụ ảnh câu hỏi |

```
WPF client ──SignalR──► Server
    │                     │
    └── Core ◄────────────┘      (cùng một Puzzle, AnswerChecker, MatchContracts)
```

### App WPF — MVVM

Cửa sổ XAML chỉ lo hiển thị; mọi trạng thái nằm trong ViewModel, nối qua
binding và `RelayCommand`.

```
LoginWindow → ModeWindow → MenuWindow → MainWindow (một người)
                                      └→ MatchWindow (nhiều người)
```

MenuWindow chỉ `Hide()` khi mở màn chơi, đóng màn chơi là quay về menu. Mọi
màn trừ màn đang chơi có **nút quay lại** (style `BackButton`): Chọn chế độ →
Đăng nhập, Menu → Chọn chế độ, Sảnh chờ → Chọn chế độ. Chuyển cửa sổ luôn theo
quy tắc **mở cửa sổ mới trước rồi mới `Close()`** cửa sổ cũ, không thì WPF thấy
hết cửa sổ là tắt app.

Riêng MatchWindow đóng theo hai nhịp: `Closing` hủy lần đầu, `await` rời phòng
trên máy chủ, rồi mới đóng thật. Bước đóng thật phải `Dispatcher.BeginInvoke(Close)`
— chưa nối máy chủ thì việc rời phòng xong tức thì, `await` không nhả luồng,
gọi `Close()` ngay trong `Closing` là WPF ném lỗi và app sập (bug "mở sảnh chờ
rồi bấm quay lại liền", sửa 2026-09-15).

```
ViewModels/
  GameViewModel      toàn bộ ván chơi một người: mạng, điểm, chuỗi, kim cương,
                     đồng hồ, trợ giúp, xáo câu, chốt sổ ván
  MatchViewModel     phòng đấu: nhận sự kiện từ máy chủ, hiện câu và bảng điểm
  LoginViewModel / ModeViewModel / MenuViewModel
Models/              AnswerSlot (ô đáp án), LetterTile (phím chữ)
Services/
  MatchClient        kết nối SignalR tới GameHub (mở lười lúc tạo / vào phòng)
  ServerClient       gọi REST: tự đăng nhập máy chủ, tải ảnh câu đố
  ThemeService       đổi Light/Dark (Themes/Light.xaml, Dark.xaml)
  AudioService       hiệu ứng âm thanh, nhạc nền
  AppSettings        cài đặt máy này
Converters/          BoolToVisibilityConverter
```

### Core — luật chơi và dữ liệu

```
Models/
  Puzzle             một câu đố: Id, Answer, Category, Difficulty, Hint, AcceptedAnswers
  PuzzleRound        dựng ô đáp án + bộ phím cho một câu (chữ không dấu)
  PlayerProfile      tiến trình người chơi (điểm ván, kỷ lục, mạng, kim cương, RunSeed...)
  Account            tài khoản
  MatchContracts     các record gửi qua SignalR, client và server dùng chung
Services/
  SoloScoring        điểm chế độ một người (60 giây, điểm nền + thưởng tốc độ)
  MatchScoring       điểm chế độ đấu (20 giây, hệ số tốc độ)
  AnswerChecker      chuẩn hóa (bỏ dấu, bỏ khoảng trắng) và so đáp án
  GameStateService   đọc/ghi PlayerProfile vào DB, chốt sổ ván, bảng xếp hạng
  AccountService     đăng ký, đăng nhập, băm mật khẩu
  PuzzleRepository   đọc câu đố từ DB
  PuzzleImageLocator / AppImageLocator   tìm file ảnh theo tên đã chuẩn hóa
Data/
  GameDbContext      EF Core, SQLite
  PuzzleSync         đối chiếu Assets/CauHoi + puzzles.json với bảng Puzzles
  Migrations/
```

### Server

```
Program.cs        REST (đăng ký / đăng nhập / ảnh / health) + map GameHub
GameHub.cs        SignalR hub: tạo / vào phòng, sẵn sàng, chọn kiểu chơi, bắt đầu,
                  gửi đáp án; vòng lặp ván chạy nền
RoomManager.cs    phòng (mã máy chủ sinh + mật khẩu + kiểu chơi + sức chứa 5),
                  người chơi, bốc câu, chấm điểm, phạt đoán sai
TokenService.cs   token đăng nhập
```

## Cơ chế bên trong

### Tiến trình một người được lưu thế nào

`PlayerProfile` ↔ bảng `PlayerStates`, ghi sau mỗi câu (`SaveProfile`).

- Vị trí đang chơi lưu bằng **mã câu** (`CurrentPuzzleId`), không phải số thứ
  tự — thêm/bớt ảnh cũng không lệch.
- Thứ tự câu của ván lưu bằng **hạt giống** `RunSeed` + **lối chơi** `RunOrder`,
  không lưu cả danh sách: vào lại thì `GameViewModel.Arrange` xuất phát từ thứ
  tự gốc trong bảng, xáo với cùng hạt giống (rồi sắp theo độ khó nếu là "Từ
  dễ đến khó") là ra đúng dãy cũ. Phải xáo từ thứ tự gốc — xáo đè lên danh
  sách đã xáo cho ra dãy khác. Sau đó nhảy tới
  `CurrentPuzzleId`.
- Lúc ván chốt sổ (`SaveEndOfRun`), bảng nhận **trạng thái của ván sau** — 0
  điểm, đầy mạng, `RunSeed = 0`, `CurrentPuzzleId = ""` — chứ không phải ảnh
  chụp ván vừa kết thúc; màn hình vẫn giữ điểm cũ để hiện lớp phủ tổng kết.
- "Đặt lại hồ sơ" xóa tiến trình nhưng cố ý **giữ `BestScore`**.
- Mất mạng cuối rồi đóng cửa sổ ngay (trong 1–2 giây chờ hiệu ứng, trước khi
  `EndRun` kịp chạy) thì bảng còn `Lives = 0` với `RunSeed` khác 0. Lần mở sau
  `GameViewModel` nhận ra ván đã chết (`CloseDeadRun`): so kỷ lục, chốt sổ,
  rồi hỏi lối chơi cho ván mới — không để chơi tiếp với 0 mạng.

### Câu đố đi từ file ảnh vào game

1. `Assets/CauHoi/*.png` — **tên file chính là đáp án**.
2. Khởi động: `PuzzleSync.Sync()` quét thư mục, ghép với `Data/puzzles.json`
   theo đáp án đã chuẩn hóa, ghi/cập nhật bảng `Puzzles` (kèm byte ảnh) và
   `PuzzleAnswers` (các đáp án chấp nhận thêm).
3. `PuzzleRepository.LoadAll()` trả `List<Puzzle>`; `GameViewModel` xáo theo
   `RunSeed` rồi `PuzzleRound` dựng ô + phím cho từng câu.

Mã câu = đáp án đã chuẩn hóa (`"CÁ HEO"` → `"CAHEO"`). Đáp án hiển thị ưu tiên
`answer` trong json, chỉ lấy tên file khi json không khai.

### Vòng lặp một ván đấu (server)

`StartMatch` bốc ngẫu nhiên N câu → với mỗi câu: phát `RoundInfo`, chờ tới khi
mọi người đã trả lời hoặc hết 20 giây, phát `RoundEnded` (kèm đáp án), nghỉ 3
giây → hết N câu phát `MatchEnded`. Vòng lặp chạy nền ngoài lời gọi hub nên
dùng `IHubContext<GameHub>`; danh sách người chơi khóa bằng `Room.Gate` vì
người vào/ra phòng sửa nó cùng lúc.

## Thêm câu đố mới

Bỏ file ảnh vào `Assets/CauHoi/`, **đặt tên file chính là đáp án** (có dấu, có
khoảng trắng giữa các tiếng). Lần khởi động sau, `PuzzleSync.Sync()` tự đối
chiếu thư mục với cơ sở dữ liệu.

Chủ đề, độ khó và đáp án chấp nhận thêm khai trong `Data/puzzles.json`, ghép
theo đáp án. Hướng dẫn đầy đủ: [`Data/README.md`](Data/README.md).

Hiện có 39 ảnh câu đố, cả 39 đã chấm chủ đề + độ khó; mục tiêu là 50.

## Dữ liệu

Tài khoản, tiến trình và câu đố nằm trong SQLite `Data/game.db`, đọc ghi qua
EF Core — các bảng `Accounts`, `PlayerStates`, `PuzzleResults`, `Puzzles`,
`PuzzleAnswers`. Client và máy chủ mỗi bên giữ một `game.db` riêng. Chi tiết và
cách sinh migration mới: [`Docs/DATABASE.md`](Docs/DATABASE.md).

File dữ liệu sinh lúc chạy đã được `.gitignore` bỏ qua, nên kho mã không mang
theo tài khoản hay tiến trình của ai.

## Việc còn dở

- 52 câu đố, đã chấm chủ đề + độ khó đủ 52 (phân bố 2/7/25/16/2).
- Hệ số ×1,5 của lối Ngẫu nhiên là giá trị đầu, chưa có số liệu để chỉnh.
- Kiểu chơi **Tôi vẽ bạn đoán** mới có chỗ chọn, chưa có luật và màn chơi —
  làm sau khi Thi đấu hoàn thiện.
- Chưa có test tự động.

## Tài liệu khác

- [`Docs/DATABASE.md`](Docs/DATABASE.md) — cơ sở dữ liệu, migrations
- [`Docs/MULTIPLAYER.md`](Docs/MULTIPLAYER.md) — thiết kế chế độ đấu nhiều người
- [`Assets/tongquanthietke/DESIGN.md`](Assets/tongquanthietke/DESIGN.md) — bảng màu, typography
- [`tools/nguyenlieu/HUONG DAN.md`](tools/nguyenlieu/HUONG%20DAN.md) — quy tắc đặt tên ảnh nguyên liệu

---

Dự án cá nhân, làm để học WPF và C#.
