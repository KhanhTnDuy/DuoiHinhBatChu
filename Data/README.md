# Dữ liệu câu đố

## Thêm câu đố mới

Bỏ file ảnh vào `Assets/CauHoi/`. **Tên file chính là đáp án** — có dấu, có
khoảng trắng giữa các tiếng:

```
Assets/CauHoi/CÁ HEO.png
Assets/CauHoi/CHUỒN CHUỒN KIM.jpg
```

Không cần sửa code hay khai báo gì thêm: game quét thư mục lúc khởi động
(`PuzzleSync.Sync()`), có bao nhiêu ảnh thì có bấy nhiêu câu. Mã câu là đáp án
đã chuẩn hóa — bỏ dấu, bỏ khoảng trắng (`"CÁ HEO"` → `"CAHEO"`), nên hai file
chỉ khác dấu sẽ bị coi là một câu.

Định dạng nhận được: `.png`, `.jpg`, `.jpeg`, `.webp`, `.bmp`.

Tên file có thể mang số ở đầu (`01 - CÁ HEO.png`); số này chỉ dùng để sắp cột
`Order` trong bảng, **không** quyết định thứ tự chơi — mỗi ván xáo lại toàn bộ
bộ câu.

## puzzles.json — chủ đề, độ khó, đáp án chấp nhận thêm

File này **không** quyết định có bao nhiêu câu, nó chỉ bổ sung thông tin cho
đáp án nào cần. Ghép theo đáp án, không phân biệt hoa thường và dấu.

```json
[
  {
    "answer": "CÁ HEO",
    "category": "Động vật",
    "difficulty": 2,
    "hint": "Con vật sống dưới biển nhưng thở bằng phổi, rất thông minh.",
    "acceptedAnswers": ["CÁ HEO XANH"]
  }
]
```

| Trường | Ý nghĩa |
| --- | --- |
| `answer` | Đáp án hiển thị khi báo kết quả. Ưu tiên giá trị này hơn tên file, vì tên file hay gõ thiếu dấu. |
| `category` | Chủ đề, luôn hiện sẵn trên màn chơi: Đồ vật, Động vật, Thực vật, Địa danh, Thể thao, Cụm từ, Ca dao - tục ngữ, Nhân vật... |
| `difficulty` | 1–5, nhân với điểm nền khi tính điểm. Chấm tay theo thang bên dưới. |
| `hint` | Gợi ý lời. Hiện tại **không dùng** trong game (trợ giúp "Gợi ý" đã bỏ vì chủ đề đã hiện sẵn), giữ lại để sau. |
| `acceptedAnswers` | Các cách viết khác cũng được tính đúng, lưu ở bảng `PuzzleAnswers`. |

### Thang độ khó

| Sao | Kiểu hình |
| --- | --- |
| 1 | Hình chỉ thẳng đáp án |
| 2 | Minh hoạ trực tiếp, nhìn là ra |
| 3 | Ghép hai tiếng từ hai hình |
| 4 | Chơi chữ, Hán-Việt, cần vốn văn hoá |
| 5 | Pun nhiều tầng |

Đáp án nào không khai `difficulty` thì game tạm suy theo số chữ cái (đến 5 chữ
là 1 sao, 6-7 là 2, 8-9 là 3, 10-12 là 4, dài hơn là 5) — chỉ là dự phòng, nên
chấm tay cho mọi câu.
