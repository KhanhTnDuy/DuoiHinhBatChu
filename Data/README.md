# Dữ liệu câu đố

## Thêm câu đố mới

Bỏ file ảnh vào `Assets/CauHoi/`. **Tên file chính là đáp án** — có dấu, có
khoảng trắng giữa các tiếng:

```
Assets/CauHoi/CÁ HEO.png
Assets/CauHoi/CHUỒN CHUỒN KIM.jpg
```

Không cần sửa code hay khai báo gì thêm: game quét thư mục lúc khởi động,
có bao nhiêu ảnh thì có bấy nhiêu câu.

Định dạng nhận được: `.png`, `.jpg`, `.jpeg`, `.webp`, `.bmp`.

### Sắp thứ tự câu

Thêm số ở đầu tên file, ngăn bằng `-`, `_` hoặc `.`:

```
01 - CÁ HEO.png
02 - SAO CHỔI.png
```

File không có số xếp sau cùng, theo bảng chữ cái.

## puzzles.json — gợi ý và độ khó (tùy chọn)

File này **không** quyết định có bao nhiêu câu, nó chỉ bổ sung thông tin cho
đáp án nào cần. Ghép theo đáp án, không phân biệt hoa thường và dấu.

```json
[
  {
    "answer": "CÁ HEO",
    "hint": "Sống dưới biển, thông minh, hay làm xiếc.",
    "difficulty": 1
  }
]
```

Đáp án nào không khai báo ở đây thì game tự sinh:

- **Gợi ý**: số tiếng và số chữ cái của đáp án.
- **Độ khó**: theo số chữ cái — đến 5 chữ là 1 sao, 6-7 là 2 sao, 8-9 là 3 sao,
  10-12 là 4 sao, dài hơn là 5 sao.
